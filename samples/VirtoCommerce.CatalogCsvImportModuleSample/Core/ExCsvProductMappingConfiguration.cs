using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Core;
public class ExCsvProductMappingConfiguration : CsvProductMappingConfiguration
{
    public override IList<string> GetOptionalFields()
    {
        var result = base.GetOptionalFields();

        var extendedOptionlaFields = ReflectionUtility.GetPropertyNames<ExCsvProduct>(x => x.ItemLineNumber);
        result.AddRange(extendedOptionlaFields);

        return result;
    }
}
