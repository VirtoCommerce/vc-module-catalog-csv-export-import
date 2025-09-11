using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;

public interface ICsvProductReader
{
    Task<IList<string>> ReadColumns(Stream stream, string delimiter);
    Task<List<CsvProduct>> ReadProducts(Stream stream, CsvProductMappingConfiguration configuration, Action<ExportImportProgressInfo> progressCallback);
}
