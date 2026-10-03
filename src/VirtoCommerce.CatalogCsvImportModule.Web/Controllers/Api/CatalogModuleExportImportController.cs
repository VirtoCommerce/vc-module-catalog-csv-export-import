using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Web.BackgroundJobs;
using VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;
using CatalogModuleConstants = VirtoCommerce.CatalogModule.Core.ModuleConstants;
using CsvModuleConstants = VirtoCommerce.CatalogCsvImportModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogCsvImportModule.Web.Controllers.Api;

[Route("api/catalogcsvimport")]
public class ExportImportController(
    ICatalogService catalogService,
    IPushNotificationManager pushNotificationManager,
    IAuthorizationService authorizationService,
    IBlobStorageProvider blobStorageProvider,
    ICsvProductReader csvProductReader,
    IUserNameResolver userNameResolver,
    IItemService itemService,
    ICategoryService categoryService)
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

        await EnqueueExport(exportInfo, notification);

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

        await EnqueueImport(importInfo, notification);

        return Ok(notification);
    }

    /// <summary>
    /// Kept for background jobs enqueued by an earlier version, which reference this method by name.
    /// Hands the work to <see cref="CsvImportJobHandler"/>; remove this once no such job can still be pending.
    /// </summary>
    // Signature is byte-identical on purpose: Hangfire persists a queued job as type name + method name +
    // parameter types + serialized args, so changing any of them would strand already-queued entries as Failed.
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Enqueued indirectly by legacy Hangfire jobs only; new work uses CsvImportJobHandler.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public Task BackgroundImport(CsvImportInfo importInfo, ImportNotification notifyEvent, CancellationToken cancellationToken)
    {
        return EnqueueImport(importInfo, notifyEvent);
    }

    /// <summary>
    /// Kept for background jobs enqueued by an earlier version, which reference this method by name.
    /// Hands the work to <see cref="CsvExportJobHandler"/>; remove this once no such job can still be pending.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Enqueued indirectly by legacy Hangfire jobs only; new work uses CsvExportJobHandler.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public Task BackgroundExport(CsvExportInfo exportInfo, ExportNotification notifyEvent, CancellationToken cancellationToken)
    {
        return EnqueueExport(exportInfo, notifyEvent);
    }

    // The static facade rather than an injected IBackgroundJob avoids another constructor parameter, and it
    // also works when Hangfire activates this controller outside a request to run a legacy job.
    private static Task EnqueueImport(CsvImportInfo importInfo, ImportNotification notification)
    {
        var payload = AbstractTypeFactory<CsvImportJobPayload>.TryCreateInstance();
        payload.ImportInfo = importInfo;
        payload.Notification = notification;

        return BackgroundJob.Enqueue<CsvImportJobHandler>(payload);
    }

    private static Task EnqueueExport(CsvExportInfo exportInfo, ExportNotification notification)
    {
        var payload = AbstractTypeFactory<CsvExportJobPayload>.TryCreateInstance();
        payload.ExportInfo = exportInfo;
        payload.Notification = notification;

        return BackgroundJob.Enqueue<CsvExportJobHandler>(payload);
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
