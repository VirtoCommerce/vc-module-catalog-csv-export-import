using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class DerivedCsvProductStandIn : CsvProduct
{
    public string ExtraProperty { get; set; }
}

public class DerivedCatalogProductStandIn : CatalogProduct
{
    public string ExtraProperty { get; set; }
}
