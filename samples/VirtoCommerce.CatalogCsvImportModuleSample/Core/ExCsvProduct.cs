using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Core;

public class ExCsvProduct : CsvProduct
{
    public string ExProperty { get; set; }

    public override void Initialize(CatalogProduct product, Price price, InventoryInfo inventory, SeoInfo seoInfo, IBlobUrlResolver blobUrlResolver)
    {
        base.Initialize(product, price, inventory, seoInfo, blobUrlResolver);

        if (product is ExCatalogProduct catalogProductExtension)
        {
            ExProperty = catalogProductExtension.ExProperty;
        }
    }

    public override void MergeFrom(CatalogProduct product)
    {
        base.MergeFrom(product);

        if (product is ExCatalogProduct catalogProductExtension &&
            string.IsNullOrEmpty(ExProperty))
        {
            ExProperty = catalogProductExtension.ExProperty;
        }
    }
}
