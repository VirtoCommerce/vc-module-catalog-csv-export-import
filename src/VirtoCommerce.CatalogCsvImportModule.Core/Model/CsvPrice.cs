using VirtoCommerce.PricingModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Model;

public class CsvPrice : Price
{
    public virtual void MergeFrom(Price source)
    {
        Id = source.Id;
        Sale ??= source.Sale;
        List = List == 0M ? source.List : List;
#pragma warning disable S3358 // Ternary operators should not be nested
        MinQuantity = MinQuantity == 0 ? (source.MinQuantity == 0 ? 1 : source.MinQuantity) : MinQuantity;
#pragma warning restore S3358 // Ternary operators should not be nested
        PricelistId = source.PricelistId;
    }
}
