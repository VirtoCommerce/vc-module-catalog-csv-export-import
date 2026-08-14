using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CsvProductConverter(ICatalogCsvImportModuleMapper mapper) : ICsvProductConverter
{
    public virtual CatalogProduct GetCatalogProduct(CsvProduct csvProduct)
    {
        var catalogProduct = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();

        mapper.MapTo(csvProduct, catalogProduct);

        return catalogProduct;
    }
}
