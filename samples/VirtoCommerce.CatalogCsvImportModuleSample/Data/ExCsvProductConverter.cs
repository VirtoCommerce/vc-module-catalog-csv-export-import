using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;

public class ExCsvProductConverter(IMapper mapper)
    : CsvProductConverter(mapper)
{
    public override CatalogProduct GetCatalogProduct(CsvProduct csvProduct)
    {
        var catalogProduct = base.GetCatalogProduct(csvProduct);

        if (csvProduct is ExCsvProduct exCsvProduct && catalogProduct is ExCatalogProduct exCatalogProduct)
        {
            exCatalogProduct.ExProperty = exCsvProduct.ExProperty;
        }

        return catalogProduct;
    }
}
