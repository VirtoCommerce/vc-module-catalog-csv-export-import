using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;

public interface ICsvProductConverter
{
    CatalogProduct GetCatalogProduct(CsvProduct csvProduct);
}
