using System;
using System.IO;
using System.Threading.Tasks;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;

public interface ICsvCatalogImporter
{
    Task DoImportAsync(Stream inputStream, CsvImportInfo importInfo, Action<ExportImportProgressInfo> progressCallback);
}
