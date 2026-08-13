using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public interface ICatalogCsvImportModuleMapper
{
    void MapTo(CsvProduct source, CatalogProduct target);
}
