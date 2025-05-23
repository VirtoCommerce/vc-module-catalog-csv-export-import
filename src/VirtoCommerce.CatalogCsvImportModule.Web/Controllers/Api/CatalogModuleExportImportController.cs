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

namespace VirtoCommerce.CatalogCsvImportModule.Web.Controllers.Api
{
    [Route("api/catalogcsvimport")]
    public class ExportImportController : Controller
    {
        private readonly ICsvCatalogExporter _csvExporter;
        private readonly ICsvCatalogImporter _csvImporter;

        private readonly ICatalogService _catalogService;
        private readonly IPushNotificationManager _notifier;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrencyService _currencyService;
        private readonly IBlobStorageProvider _blobStorageProvider;
        private readonly IUserNameResolver _userNameResolver;
        private readonly ISettingsManager _settingsManager;
        private readonly IBlobUrlResolver _blobUrlResolver;
        private readonly IItemService _itemService;
        private readonly ICategoryService _categoryService;

        public ExportImportController(ICatalogService catalogService,
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
        {
            _catalogService = catalogService;
            _notifier = pushNotificationManager;
            _authorizationService = authorizationService;
            _currencyService = currencyService;
            _blobStorageProvider = blobStorageProvider;
            _userNameResolver = userNameResolver;
            _settingsManager = settingsManager;
            _blobUrlResolver = blobUrlResolver;
            _itemService = itemService;
            _categoryService = categoryService;

            _csvExporter = csvExporter;
            _csvImporter = csvImporter;
        }

        [HttpGet]
        [Route("export/mappingconfiguration")]
        public ActionResult<CsvProductMappingConfiguration> GetExportMappingConfiguration([FromQuery] string delimiter = ";")
        {
            var configuration = AbstractTypeFactory<CsvProductMappingConfiguration>.TryCreateInstance();
            var result = configuration.GetDefaultConfiguration();
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
                var items = await _itemService.GetAsync(exportInfo.ProductIds, ItemResponseGroup.ItemInfo.ToString());
                hasPermissions = await CheckCatalogPermission(items, CatalogModuleConstants.Security.Permissions.Read);
            }

            if (hasPermissions && !exportInfo.CategoryIds.IsNullOrEmpty())
            {
                var categories = await _categoryService.GetAsync(exportInfo.CategoryIds, CategoryResponseGroup.Info.ToString());
                hasPermissions = await CheckCatalogPermission(categories, CatalogModuleConstants.Security.Permissions.Read);
            }

            if (hasPermissions && !exportInfo.CatalogId.IsNullOrEmpty())
            {
                var catalog = await _catalogService.GetByIdAsync(exportInfo.CatalogId, CategoryResponseGroup.Info.ToString());

                if (catalog != null)
                {
                    hasPermissions = await CheckCatalogPermission(catalog, CatalogModuleConstants.Security.Permissions.Read);
                }
            }

            if (!hasPermissions)
            {
                return Unauthorized();
            }

            var notification = new ExportNotification(_userNameResolver.GetCurrentUserName())
            {
                Title = "Catalog export task",
                Description = "starting export...."
            };
            await _notifier.SendAsync(notification);


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
        public async Task<ActionResult<CsvProductMappingConfiguration>> GetImportMappingConfiguration([FromQuery] string fileUrl, [FromQuery] string delimiter = ";")
        {
            var configuration = AbstractTypeFactory<CsvProductMappingConfiguration>.TryCreateInstance();
            var result = configuration.GetDefaultConfiguration();
            var decodedDelimiter = HttpUtility.UrlDecode(delimiter);
            result.Delimiter = decodedDelimiter;

            //Read csv headers and try to auto map fields by name
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = decodedDelimiter
            };
            using (var reader = new CsvReader(new StreamReader(_blobStorageProvider.OpenRead(fileUrl)), config))
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
                var catalog = await _catalogService.GetByIdAsync(importInfo.CatalogId, CategoryResponseGroup.Info.ToString());

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
            criteria.CatalogIds = new[] { importInfo.CatalogId };

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, criteria, new CatalogAuthorizationRequirement(CatalogModuleConstants.Security.Permissions.Update));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }


            var notification = new ImportNotification(_userNameResolver.GetCurrentUserName())
            {
                Title = "Import catalog from CSV",
                Description = "starting import...."
            };
            await _notifier.SendAsync(notification);

            BackgroundJob.Enqueue(() => BackgroundImport(importInfo, notification));

            return Ok(notification);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        // Only public methods can be invoked in the background. (Hangfire)
        public async Task BackgroundImport(CsvImportInfo importInfo, ImportNotification notifyEvent)
        {
            Action<ExportImportProgressInfo> progressCallback = x =>
            {
                notifyEvent.InjectFrom(x);
                _notifier.SendAsync(notifyEvent);
            };

            using (var stream = _blobStorageProvider.OpenRead(importInfo.FileUrl))
            {
                try
                {
                    await _csvImporter.DoImportAsync(stream, importInfo, progressCallback);
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
                    await _notifier.SendAsync(notifyEvent);
                }
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        // Only public methods can be invoked in the background. (Hangfire)
        public async Task BackgroundExport(CsvExportInfo exportInfo, ExportNotification notifyEvent)
        {
            try
            {
                var currencies = await _currencyService.GetAllCurrenciesAsync();
                var defaultCurrency = currencies.FirstOrDefault(x => x.IsPrimary);

                if (defaultCurrency == null)
                {
                    throw new InvalidOperationException("Primary currency not found");
                }

                exportInfo.Currency ??= defaultCurrency.Code;
                var catalog = await _catalogService.GetNoCloneAsync(new[] { exportInfo.CatalogId });
                if (catalog == null)
                {
                    throw new InvalidOperationException($"Cannot get catalog with id '{exportInfo.CatalogId}'");
                }

                void progressCallback(ExportImportProgressInfo x)
                {
                    notifyEvent.InjectFrom(x);
                    _notifier.SendAsync(notifyEvent);
                }

                if (exportInfo.Configuration == null)
                {
                    var configuration = AbstractTypeFactory<CsvProductMappingConfiguration>.TryCreateInstance();
                    exportInfo.Configuration = configuration.GetDefaultConfiguration();
                }

                var fileNameTemplate = await _settingsManager.GetValueAsync<string>(CsvModuleConstants.Settings.General.ExportFileNameTemplate);
                var fileName = string.Format(fileNameTemplate, DateTime.UtcNow);
                fileName = Path.ChangeExtension(fileName, ".csv");

                var blobRelativeUrl = Path.Combine("temp", fileName);

                //Upload result csv to blob storage
                using (var blobStream = _blobStorageProvider.OpenWrite(blobRelativeUrl))
                {
                    await _csvExporter.DoExportAsync(blobStream, exportInfo, progressCallback);
                }

                //Get a download url
                notifyEvent.DownloadUrl = _blobUrlResolver.GetAbsoluteUrl(blobRelativeUrl);
                notifyEvent.Description = "Export finished";
            }
            catch (Exception ex)
            {
                notifyEvent.Description = "Export failed";
                notifyEvent.Errors.Add(ex.ExpandExceptionMessage());
            }
            finally
            {
                notifyEvent.Finished = DateTime.UtcNow;
                await _notifier.SendAsync(notifyEvent);
            }
        }

        private async Task<bool> CheckCatalogPermission(object checkedEntities, string permission)
        {
            var result = true;
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, checkedEntities, new CatalogAuthorizationRequirement(permission));

            if (!authorizationResult.Succeeded)
            {
                result = false;
            }

            return result;
        }
    }
}
