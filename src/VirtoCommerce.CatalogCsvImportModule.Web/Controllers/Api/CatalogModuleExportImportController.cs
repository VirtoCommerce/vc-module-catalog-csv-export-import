using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Omu.ValueInjecter;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using CatalogModuleConstants = VirtoCommerce.CatalogModule.Core.ModuleConstants;
using CsvModuleConstants = VirtoCommerce.CatalogCsvImportModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogCsvImportModule.Web.Controllers.Api;

[Route("api/catalogcsvimport")]
public class ExportImportController(
    ICatalogService catalogService,
    IPushNotificationManager pushNotificationManager,
    IAuthorizationService authorizationService,
    ICurrencyService currencyService,
    IBlobStorageProvider blobStorageProvider,
    IBlobUrlResolver blobUrlResolver,
    ICsvProductReader csvProductReader,
    ICsvCatalogExporter csvExporter,
    ICsvCatalogImporter csvImporter,
    IUserNameResolver userNameResolver,
    ISettingsManager settingsManager,
    IItemService itemService,
    ICategoryService categoryService,
    ILogger<ExportImportController> logger)
    : Controller
{
    [HttpGet]
    [Route("export/mappingconfiguration")]
    [Authorize(CatalogModuleConstants.Security.Permissions.Export)]
    public ActionResult<CsvProductMappingConfiguration> GetExportMappingConfiguration([FromQuery] string delimiter = ";")
    {
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = HttpUtility.UrlDecode(delimiter);

        return Ok(configuration);
    }

    /// <summary>
    /// Start catalog data export process.
    /// </summary>
    /// <remarks>Data export is an async process. An ExportNotification is returned for progress reporting.</remarks>
    /// <param name="exportInfo">The export configuration.</param>
    [HttpPost]
    [Route("export")]
    [Authorize(CatalogModuleConstants.Security.Permissions.Export)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExportNotification), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExportNotification>> DoExport([FromBody] CsvExportInfo exportInfo)
    {
        var hasPermissions = true;

        if (!exportInfo.ProductIds.IsNullOrEmpty())
        {
            var items = await itemService.GetAsync(exportInfo.ProductIds, nameof(ItemResponseGroup.ItemInfo));
            hasPermissions = await CheckCatalogPermission(items, CatalogModuleConstants.Security.Permissions.Read);
        }

        if (hasPermissions && !exportInfo.CategoryIds.IsNullOrEmpty())
        {
            var categories = await categoryService.GetAsync(exportInfo.CategoryIds, nameof(CategoryResponseGroup.Info));
            hasPermissions = await CheckCatalogPermission(categories, CatalogModuleConstants.Security.Permissions.Read);
        }

        if (hasPermissions && !exportInfo.CatalogId.IsNullOrEmpty())
        {
            var catalog = await catalogService.GetByIdAsync(exportInfo.CatalogId, nameof(CategoryResponseGroup.Info));

            if (catalog != null)
            {
                hasPermissions = await CheckCatalogPermission(catalog, CatalogModuleConstants.Security.Permissions.Read);
            }
        }

        if (!hasPermissions)
        {
            return Unauthorized();
        }

        var notification = new ExportNotification(userNameResolver.GetCurrentUserName())
        {
            Title = "Catalog export task",
            Description = "starting export....",
        };

        await pushNotificationManager.SendAsync(notification);

        BackgroundJob.Enqueue(() => BackgroundExport(exportInfo, notification, CancellationToken.None));

        return Ok(notification);
    }

    /// <summary>
    /// Gets the CSV mapping configuration.
    /// </summary>
    /// <remarks>Analyses the supplied file's structure and returns automatic column mapping.</remarks>
    /// <param name="fileUrl">The file URL.</param>
    /// <param name="delimiter">The CSV delimiter.</param>
    /// <returns></returns>
    [HttpGet]
    [Route("import/mappingconfiguration")]
    [Authorize(CatalogModuleConstants.Security.Permissions.Import)]
    public async Task<ActionResult<CsvProductMappingConfiguration>> GetImportMappingConfiguration([FromQuery] string fileUrl, [FromQuery] string delimiter = ";")
    {
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = HttpUtility.UrlDecode(delimiter);

        // Read CSV headers and try to map fields by name
        var columns = await csvProductReader.ReadColumns(await blobStorageProvider.OpenReadAsync(fileUrl), configuration.Delimiter);
        if (columns.Count > 0)
        {
            configuration.AutoMap(columns);
        }

        return Ok(configuration);
    }

    /// <summary>
    /// Start catalog data import process.
    /// </summary>
    /// <remarks>Data import is an async process. An ImportNotification is returned for progress reporting.</remarks>
    /// <param name="importInfo">The import data configuration.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("import")]
    [Authorize(CatalogModuleConstants.Security.Permissions.Import)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ImportNotification), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportNotification>> DoImport([FromBody] CsvImportInfo importInfo)
    {
        var hasPermissions = true;

        if (!importInfo.CatalogId.IsNullOrEmpty())
        {
            var catalog = await catalogService.GetByIdAsync(importInfo.CatalogId, nameof(CategoryResponseGroup.Info));

            if (catalog != null)
            {
                hasPermissions = await CheckCatalogPermission(catalog, CatalogModuleConstants.Security.Permissions.Update);
            }
        }

        if (!hasPermissions)
        {
            return Unauthorized();
        }

        var criteria = AbstractTypeFactory<CatalogSearchCriteria>.TryCreateInstance();
        criteria.CatalogIds = [importInfo.CatalogId];

        var authorizationResult = await authorizationService.AuthorizeAsync(User, criteria, new CatalogAuthorizationRequirement(CatalogModuleConstants.Security.Permissions.Update));
        if (!authorizationResult.Succeeded)
        {
            return Unauthorized();
        }

        var notification = new ImportNotification(userNameResolver.GetCurrentUserName())
        {
            Title = "Import catalog from CSV",
            Description = "starting import....",
        };

        await pushNotificationManager.SendAsync(notification);

        BackgroundJob.Enqueue(() => BackgroundImport(importInfo, notification, CancellationToken.None));

        return Ok(notification);
    }

    [DisableConcurrentExecution(CsvModuleConstants.BackgroundJobs.ImportLockKey, CsvModuleConstants.BackgroundJobs.ImportLockTimeoutSeconds)]
    [ApiExplorerSettings(IgnoreApi = true)]
    // Only public methods can be invoked in the background. (Hangfire)
    public async Task BackgroundImport(CsvImportInfo importInfo, ImportNotification notifyEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting background import process for file {FileUrl}", importInfo.FileUrl);

        var canceled = false;

        await using var stream = await blobStorageProvider.OpenReadAsync(importInfo.FileUrl);
        try
        {
            await csvImporter.DoImportAsync(stream, importInfo, ProgressCallback, cancellationToken);

            logger.LogInformation("Import process completed for file {FileUrl}", importInfo.FileUrl);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            logger.LogWarning("Import process canceled for file {FileUrl}", importInfo.FileUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import process failed for file {FileUrl}", importInfo.FileUrl);

            notifyEvent.Errors.Add(ex.ToString());
        }
        finally
        {
            notifyEvent.Finished = DateTime.UtcNow;
            notifyEvent.Description = canceled
                ? "Import canceled"
                : "Import finished" + (notifyEvent.Errors.Count > 0 ? " with errors" : " successfully");
            await pushNotificationManager.SendAsync(notifyEvent);
        }

        return;

        void ProgressCallback(ExportImportProgressInfo x)
        {
            notifyEvent.InjectFrom(x);
            pushNotificationManager.SendAsync(notifyEvent);
        }
    }

    [DisableConcurrentExecution(CsvModuleConstants.BackgroundJobs.ExportLockKey, CsvModuleConstants.BackgroundJobs.ExportLockTimeoutSeconds)]
    [ApiExplorerSettings(IgnoreApi = true)]
    // Only public methods can be invoked in the background. (Hangfire)
    public async Task BackgroundExport(CsvExportInfo exportInfo, ExportNotification notifyEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting background export process for catalog {CatalogId}", exportInfo.CatalogId);

        try
        {
            var currencies = await currencyService.GetAllCurrenciesAsync();
            var defaultCurrency = currencies.FirstOrDefault(x => x.IsPrimary);

            if (defaultCurrency == null)
            {
                throw new InvalidOperationException("Primary currency not found");
            }

            exportInfo.Currency ??= defaultCurrency.Code;

            var catalog = await catalogService.GetNoCloneAsync([exportInfo.CatalogId]);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Cannot get catalog with id '{exportInfo.CatalogId}'");
            }

            exportInfo.Configuration ??= CsvProductMappingConfiguration.GetDefaultConfiguration();

            var fileNameTemplate = await settingsManager.GetValueAsync<string>(CsvModuleConstants.Settings.General.ExportFileNameTemplate);
            var fileName = ExportFileNameHelper.GetFileName(fileNameTemplate, DateTime.UtcNow);

            var blobRelativeUrl = Path.Combine("temp", fileName);

            // Upload result csv to blob storage
            await using (var blobStream = await blobStorageProvider.OpenWriteAsync(blobRelativeUrl))
            {
                await csvExporter.DoExportAsync(blobStream, exportInfo, ProgressCallback, cancellationToken);
            }

            // Get a download url
            notifyEvent.DownloadUrl = blobUrlResolver.GetAbsoluteUrl(blobRelativeUrl);
            notifyEvent.Description = "Export finished";

            void ProgressCallback(ExportImportProgressInfo x)
            {
                notifyEvent.InjectFrom(x);
                pushNotificationManager.SendAsync(notifyEvent);
            }

            logger.LogInformation("Export process completed for catalog {CatalogId}", exportInfo.CatalogId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Export process canceled for catalog {CatalogId}", exportInfo.CatalogId);

            notifyEvent.Description = "Export canceled";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export process failed for catalog {CatalogId}", exportInfo.CatalogId);

            notifyEvent.Description = "Export failed";
            notifyEvent.Errors.Add(ex.ExpandExceptionMessage());
        }
        finally
        {
            notifyEvent.Finished = DateTime.UtcNow;
            await pushNotificationManager.SendAsync(notifyEvent);
        }
    }

    private async Task<bool> CheckCatalogPermission(object checkedEntities, string permission)
    {
        var result = true;
        var authorizationResult = await authorizationService.AuthorizeAsync(User, checkedEntities, new CatalogAuthorizationRequirement(permission));

        if (!authorizationResult.Succeeded)
        {
            result = false;
        }

        return result;
    }
}
