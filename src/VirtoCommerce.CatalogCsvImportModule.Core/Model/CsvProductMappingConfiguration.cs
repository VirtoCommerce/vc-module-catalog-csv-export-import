using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Model;

public class CsvProductMappingConfiguration
{
    public string ETag { get; set; }
    public string Delimiter { get; set; }
    public string[] CsvColumns { get; set; }
    public ICollection<CsvProductPropertyMap> PropertyMaps { get; set; } = new List<CsvProductPropertyMap>();
    public string[] PropertyCsvColumns { get; set; } = [];

    public static CsvProductMappingConfiguration GetDefaultConfiguration()
    {
        var configuration = AbstractTypeFactory<CsvProductMappingConfiguration>.TryCreateInstance();

        configuration.Delimiter = ";";
        configuration.PropertyMaps = configuration.GetOptionalFields().Select(x => new CsvProductPropertyMap { EntityColumnName = x, CsvColumnName = x, IsRequired = false }).ToList();

        return configuration;
    }

    public virtual IList<string> GetOptionalFields()
    {
        var optionalFields = ReflectionUtility.GetPropertyNames<CsvProduct>(x => x.Name, x => x.Id, x => x.Sku, x => x.CategoryPath, x => x.CategoryId, x => x.MainProductId,
            x => x.PrimaryImage, x => x.PrimaryImageGroup, x => x.AltImage, x => x.AltImageGroup,
            x => x.SeoUrl, x => x.SeoTitle, x => x.SeoDescription, x => x.SeoLanguage, x => x.SeoStore, x => x.SeoMetaKeywords, x => x.SeoImageAlternativeText,
            x => x.Review, x => x.ReviewType, x => x.IsActive, x => x.IsBuyable, x => x.TrackInventory,
            x => x.PriceId, x => x.SalePrice, x => x.ListPrice, x => x.PriceMinQuantity, x => x.Currency, x => x.PriceListId, x => x.Quantity,
            x => x.FulfillmentCenterId, x => x.PackageType, x => x.OuterId, x => x.Priority, x => x.MaxQuantity, x => x.MinQuantity,
            x => x.ManufacturerPartNumber, x => x.Gtin, x => x.MeasureUnit, x => x.WeightUnit, x => x.Weight,
            x => x.Height, x => x.Length, x => x.Width, x => x.TaxType, x => x.ProductType, x => x.ShippingType,
            x => x.Vendor, x => x.DownloadType, x => x.DownloadExpiration, x => x.HasUserAgreement, x => x.MaxNumberOfDownload, x => x.StartDate, x => x.EndDate);

        return optionalFields.ToList();
    }

    public void AutoMap(IEnumerable<string> csvColumns)
    {
        CsvColumns = csvColumns.ToArray();

        foreach (var propertyMap in PropertyMaps)
        {
            var entityColumnName = propertyMap.EntityColumnName;
            var betterMatchCsvColumn = csvColumns.Select(x => new { csvColumn = x, distance = x.ComputeLevenshteinDistance(entityColumnName) })
                .Where(x => x.distance < 2)
                .OrderBy(x => x.distance)
                .Select(x => x.csvColumn)
                .FirstOrDefault();
            if (betterMatchCsvColumn != null)
            {
                propertyMap.CsvColumnName = betterMatchCsvColumn;
                propertyMap.CustomValue = null;
            }
            else
            {
                propertyMap.CsvColumnName = null;
            }

        }

        //All not mapped properties may be a product property
        PropertyCsvColumns = csvColumns.Except(PropertyMaps.Where(x => x.CsvColumnName != null).Select(x => x.CsvColumnName)).ToArray();
        //Generate ETag for identifying csv format
        ETag = string.Join(";", CsvColumns).GetMD5Hash();
    }
}
