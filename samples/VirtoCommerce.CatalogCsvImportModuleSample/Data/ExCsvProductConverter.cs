using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;
public class ExCsvProductConverter : CsvProductConverter
{
    public ExCsvProductConverter(IMapper mapper) : base(mapper)
    {
    }

    public override CatalogProduct GetCatalogProduct(CsvProduct csvProduct)
    {
        var result =  base.GetCatalogProduct(csvProduct);

        if (csvProduct is ExCsvProduct exCsvProduct && result is ExCatalogProduct catalogProductExtension)
        {
            catalogProductExtension.ItemLineNumber = exCsvProduct.ItemLineNumber;
        }

        return result;
    }
}
