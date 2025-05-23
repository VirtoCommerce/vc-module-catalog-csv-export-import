using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;

public class ExCsvProductMap : CsvProductMap<ExCsvProduct>
{
    public ExCsvProductMap(CsvProductMappingConfiguration mappingCfg)
    {
        Initialize(mappingCfg);
    }
}
