using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;

public class ExCsvProductMap(CsvProductMappingConfiguration mappingCfg)
    : CsvProductMap<ExCsvProduct>(mappingCfg);
