using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CsvHelper;
using CsvHelper.Configuration;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Omu.ValueInjecter;
using VirtoCommerce.AssetsModule.Core.Assets;
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
    ICsvCatalogExporter csvExporter,
    ICsvCatalogImporter csvImporter,
    IUserNameResolver userNameResolver,
    ISettingsManager settingsManager,
    IItemService itemService,
    ICategoryService categoryService)
    : Controller
{
    [HttpGet]
    [Route("export/mappingconfiguration")]
    [Authorize(CatalogModuleConstants.Security.Permissions.Export)]
    public ActionResult<CsvProductMappingConfiguration> GetExportMappingConfiguration([FromQuery] string delimiter = ";")
    {
        var result = CsvProductMappingConfiguration.GetDefaultConfiguration();
        var decodedDelimiter = HttpUtility.UrlDecode(delimiter);
        result.Delimiter = decodedDelimiter;

        return Ok(result);
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


        BackgroundJob.Enqueue(() => BackgroundExport(exportInfo, notification));

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
        var result = CsvProductMappingConfiguration.GetDefaultConfiguration();
        var decodedDelimiter = HttpUtility.UrlDecode(delimiter);
        result.Delimiter = decodedDelimiter;

        //Read csv headers and try to auto map fields by name
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = decodedDelimiter,
        };

        using (var reader = new CsvReader(new StreamReader(await blobStorageProvider.OpenReadAsync(fileUrl)), config))
        {
            if (await reader.ReadAsync() && reader.ReadHeader())
            {
                result.AutoMap(reader.HeaderRecord);
            }
        }

        return Ok(result);
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

        BackgroundJob.Enqueue(() => BackgroundImport(importInfo, notification));

        return Ok(notification);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    // Only public methods can be invoked in the background. (Hangfire)
    public async Task BackgroundImport(CsvImportInfo importInfo, ImportNotification notifyEvent)
    {
        await using var stream = await blobStorageProvider.OpenReadAsync(importInfo.FileUrl);
        try
        {
            await csvImporter.DoImportAsync(stream, importInfo, ProgressCallback);
        }
        catch (Exception ex)
        {
            notifyEvent.Description = "Export error";
            notifyEvent.Errors.Add(ex.ToString());
        }
        finally
        {
            notifyEvent.Finished = DateTime.UtcNow;
            notifyEvent.Description = "Import finished" + (notifyEvent.Errors.Any() ? " with errors" : " successfully");
            await pushNotificationManager.SendAsync(notifyEvent);
        }

        return;

        void ProgressCallback(ExportImportProgressInfo x)
        {
            notifyEvent.InjectFrom(x);
            pushNotificationManager.SendAsync(notifyEvent);
        }
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    // Only public methods can be invoked in the background. (Hangfire)
    public async Task BackgroundExport(CsvExportInfo exportInfo, ExportNotification notifyEvent)
    {
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
            var fileName = string.Format(fileNameTemplate, DateTime.UtcNow);
            fileName = Path.ChangeExtension(fileName, ".csv");

            var blobRelativeUrl = Path.Combine("temp", fileName);

            //Upload result csv to blob storage
            await using (var blobStream = await blobStorageProvider.OpenWriteAsync(blobRelativeUrl))
            {
                await csvExporter.DoExportAsync(blobStream, exportInfo, ProgressCallback);
            }

            //Get a download url
            notifyEvent.DownloadUrl = blobUrlResolver.GetAbsoluteUrl(blobRelativeUrl);
            notifyEvent.Description = "Export finished";

            void ProgressCallback(ExportImportProgressInfo x)
            {
                notifyEvent.InjectFrom(x);
                pushNotificationManager.SendAsync(notifyEvent);
            }
        }
        catch (Exception ex)
        {
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
