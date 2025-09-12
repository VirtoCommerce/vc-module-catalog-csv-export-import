using System;
using System.IO;
using System.Threading.Tasks;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;

public interface ICsvCatalogExporter
{
    Task DoExportAsync(Stream outStream, CsvExportInfo exportInfo, Action<ExportImportProgressInfo> progressCallback);
}
