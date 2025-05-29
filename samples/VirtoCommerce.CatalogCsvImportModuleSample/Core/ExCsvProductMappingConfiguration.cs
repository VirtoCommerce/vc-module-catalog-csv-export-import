using System.Collections.Generic;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Core;

public class ExCsvProductMappingConfiguration : CsvProductMappingConfiguration
{
    public override IList<string> GetOptionalFields()
    {
        var result = base.GetOptionalFields();

        result.Add(nameof(ExCsvProduct.ExProperty));

        return result;
    }
}
