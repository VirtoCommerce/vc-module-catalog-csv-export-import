using VirtoCommerce.CatalogCsvImportModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Extensions;

public static class CsvProductExtensions
{
    public static CsvProduct ClearName(this CsvProduct csvProduct)
    {
        if (!string.IsNullOrEmpty(csvProduct.Name))
        {
            csvProduct.Name = csvProduct.Name.Trim();
        }

        return csvProduct;
    }
}
