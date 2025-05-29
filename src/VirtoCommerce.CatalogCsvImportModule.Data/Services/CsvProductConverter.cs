using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CsvProductConverter(IMapper mapper) : ICsvProductConverter
{
    public virtual CatalogProduct GetCatalogProduct(CsvProduct csvProduct)
    {
        var catalogProduct = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();

        mapper.Map(csvProduct, catalogProduct);

        return catalogProduct;
    }
}
