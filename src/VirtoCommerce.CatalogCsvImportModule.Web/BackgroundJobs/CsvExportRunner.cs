using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using CsvModuleConstants = VirtoCommerce.CatalogCsvImportModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogCsvImportModule.Web.BackgroundJobs;

/// <summary>
/// Exports a catalog to a CSV file in blob storage. Kept apart from <see cref="CsvExportJobHandler"/>, which owns the
/// push notification, logging and the export lock, so each class stays small.
/// </summary>
public class CsvExportRunner(
    ICurrencyService currencyService,
    ICatalogService catalogService,
    IExportFileNameBuilder exportFileNameBuilder,
    IBlobStorageProvider blobStorageProvider,
    IBlobUrlResolver blobUrlResolver,
    ICsvCatalogExporter csvExporter)
{
    /// <summary>
    /// Writes the export to <c>temp/</c> in blob storage.
    /// </summary>
    /// <returns>The absolute download URL of the exported file.</returns>
    public virtual async Task<string> ExportAsync(CsvExportInfo exportInfo, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken)
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

        var fileName = await exportFileNameBuilder.GetFileName(CsvModuleConstants.Settings.General.ExportFileNameTemplate) + ".csv";
        var blobRelativeUrl = Path.Combine("temp", fileName);

        // Upload result csv to blob storage
        await using (var blobStream = await blobStorageProvider.OpenWriteAsync(blobRelativeUrl))
        {
            await csvExporter.DoExportAsync(blobStream, exportInfo, progressCallback, cancellationToken);
        }

        // Get a download url
        return blobUrlResolver.GetAbsoluteUrl(blobRelativeUrl);
    }
}
