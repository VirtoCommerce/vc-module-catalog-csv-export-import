using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Omu.ValueInjecter;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Model;

public class CsvProduct : CatalogProduct
{
    private readonly string[] _csvCellDelimiter = ["--", "|"];
    private IBlobUrlResolver _blobUrlResolver;

    public CsvProduct()
    {
        SeoInfos = new List<SeoInfo>();
        Reviews = new List<EditorialReview>();
        Properties = new List<Property>();
        Images = new List<Image>();
        Assets = new List<Asset>();
        Price = new CsvPrice { Currency = "USD" };
        Prices = new List<Price> { Price };
        Inventory = new InventoryInfo();
        EditorialReview = new EditorialReview();
        Reviews = new List<EditorialReview> { EditorialReview };
        SeoInfo = new CsvSeoInfo { ObjectType = nameof(CatalogProduct) };
        SeoInfos = new List<SeoInfo> { SeoInfo };
    }

    public static CsvProduct Create(CatalogProduct product, Price price, InventoryInfo inventory, SeoInfo seoInfo, IBlobUrlResolver blobUrlResolver)
    {
        var csvProduct = AbstractTypeFactory<CsvProduct>.TryCreateInstance();
        csvProduct.Initialize(product, price, inventory, seoInfo, blobUrlResolver);

        return csvProduct;
    }

    public virtual void Initialize(CatalogProduct product, Price price, InventoryInfo inventory, SeoInfo seoInfo, IBlobUrlResolver blobUrlResolver)
    {
        _blobUrlResolver = blobUrlResolver;

        this.InjectFrom(product);

        Properties = product.Properties;
        Images = product.Images;
        Assets = product.Assets;
        Links = product.Links;
        Variations = product.Variations;
        SeoInfos = product.SeoInfos;
        Reviews = product.Reviews;
        Associations = product.Associations;

        if (price != null)
        {
            Price = price;
        }

        if (inventory != null)
        {
            Inventory = inventory;
        }

        if (seoInfo != null)
        {
            SeoInfo = seoInfo;
        }
    }

    public Price Price { get; set; }
    public InventoryInfo Inventory { get; set; }
    public EditorialReview EditorialReview { get; set; }
    public SeoInfo SeoInfo { get; set; }
    public IList<Price> Prices { get; set; }

    public string PriceId
    {
        get
        {
            return Price.Id;
        }
        set
        {
            Price.Id = value;
        }
    }

    public string SalePrice
    {
        get
        {
            return Price.Sale?.ToString(CultureInfo.InvariantCulture);
        }
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                Price.Sale = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
        }
    }

    public string ListPrice
    {
        get
        {
            return Price.List.ToString(CultureInfo.InvariantCulture);
        }
        set
        {
            Price.List = string.IsNullOrEmpty(value) ? 0 : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
    }

    public string PriceMinQuantity
    {
        get
        {
            return Price.MinQuantity.ToString(CultureInfo.InvariantCulture);
        }
        set
        {
            Price.MinQuantity = value.IsNullOrEmpty() ? 1 : Convert.ToInt32(value);
        }
    }

    public string Currency
    {
        get
        {
            return Price.Currency;
        }
        set
        {
            Price.Currency = value;
        }
    }

    public string PriceListId
    {
        get
        {
            return Price.PricelistId;
        }
        set
        {
            Price.PricelistId = value;
        }
    }

    public string FulfillmentCenterId
    {
        get
        {
            return Inventory?.FulfillmentCenterId;
        }
        set
        {
            Inventory.FulfillmentCenterId = value;
        }
    }

    public string Quantity
    {
        get
        {
            return Inventory?.InStockQuantity.ToString();
        }
        set
        {
            Inventory.InStockQuantity = value.IsNullOrEmpty() ? 0 : Convert.ToInt64(value);
        }
    }

    private string _primaryImage;
    public string PrimaryImage
    {
        get
        {
            if (Images != null && _primaryImage == null)
            {
                var primaryImage = Images.OrderBy(x => x.SortOrder).FirstOrDefault();
                if (primaryImage != null)
                {
                    _primaryImage = _blobUrlResolver != null
                        ? _blobUrlResolver.GetAbsoluteUrl(primaryImage.Url)
                        : primaryImage.Url;
                }
            }
            return _primaryImage;
        }

        set
        {
            _primaryImage = value;
        }
    }

    private string _primaryImageGroup;
    public string PrimaryImageGroup
    {
        get
        {
            if (Images != null && _primaryImageGroup == null)
            {
                var primaryImage = Images.OrderBy(x => x.SortOrder).FirstOrDefault();
                if (primaryImage != null)
                {
                    _primaryImageGroup = primaryImage.Group;
                }
            }
            return _primaryImageGroup;
        }
        set
        {
            _primaryImageGroup = value;
        }
    }

    private string _altImage;
    public string AltImage
    {
        get
        {
            if (Images != null && _altImage == null)
            {
                var altImageUrls = Images
                    .OrderBy(x => x.SortOrder)
                    .Select(x => _blobUrlResolver != null ? _blobUrlResolver.GetAbsoluteUrl(x.Url) : x.Url)
                    .Skip(1)
                    .ToArray();

                _altImage = string.Join(_csvCellDelimiter[1], altImageUrls);

            }
            return _altImage;
        }

        set
        {
            _altImage = value;
        }
    }

    private string _altImageGroup;
    public string AltImageGroup
    {
        get
        {
            if (Images != null && _altImageGroup == null)
            {
                var altImageGroups = Images
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.Group)
                    .Skip(1)
                    .ToArray();

                _altImageGroup = string.Join(_csvCellDelimiter[1], altImageGroups);

            }
            return _altImageGroup;
        }

        set
        {
            _altImageGroup = value;
        }
    }

    public string Sku
    {
        get
        {
            return Code;
        }
        set
        {
            Code = value?.Trim();
        }
    }

    public string ParentSku { get; set; }

    public string CategoryPath
    {
        get
        {
            if (Category == null)
            {
                return null;
            }

            return Category.Path;
        }
        set
        {
            Category = new Category { Path = value };
        }
    }

    public string ReviewType
    {
        get { return EditorialReview.ReviewType; }
        set { EditorialReview.ReviewType = value; }
    }

    public string Review
    {
        get { return EditorialReview.Content; }
        set { EditorialReview.Content = value; }
    }

    public string SeoTitle
    {
        get { return SeoInfo.PageTitle; }
        set { SeoInfo.PageTitle = value; }
    }

    public string SeoUrl
    {
        get { return SeoInfo.SemanticUrl; }
        set
        {
            var slug = value;
            SeoInfo.SemanticUrl = slug.Substring(0, Math.Min(slug.Length, 240));
        }
    }

    public string SeoDescription
    {
        get { return SeoInfo.MetaDescription; }
        set { SeoInfo.MetaDescription = value; }
    }

    public string SeoLanguage
    {
        get { return SeoInfo.LanguageCode; }
        set { SeoInfo.LanguageCode = value; }
    }

    public string SeoStore
    {
        get { return SeoInfo.StoreId; }
        set { SeoInfo.StoreId = value; }
    }

    public string SeoMetaKeywords
    {
        get { return SeoInfo.MetaKeywords; }
        set { SeoInfo.MetaKeywords = value; }
    }

    public string SeoImageAlternativeText
    {
        get { return SeoInfo.ImageAltDescription; }
        set { SeoInfo.ImageAltDescription = value; }
    }

    public int LineNumber { get; set; }

    /// <summary>
    /// Merge from other product, without any deletion, only update and create allowed
    ///
    /// </summary>
    /// <param name="product"></param>
    public virtual void MergeFrom(CatalogProduct product)
    {
        Id = product.Id;

        if (string.IsNullOrEmpty(Code))
        {
            Code = product.Code;
        }

        if (string.IsNullOrEmpty(Name))
        {
            Name = product.Name;
        }

        if (string.IsNullOrEmpty(CategoryId))
        {
            CategoryId = product.CategoryId;
        }

        if (Category == null || (Category != null && string.IsNullOrEmpty(Category.Path)))
        {
            Category = product.Category;
        }

        if (string.IsNullOrEmpty(ProductType))
        {
            ProductType = product.ProductType;
        }

        if (string.IsNullOrEmpty(Vendor))
        {
            Vendor = product.Vendor;
        }

        if (string.IsNullOrEmpty(Gtin))
        {
            Gtin = product.Gtin;
        }

        if (string.IsNullOrEmpty(OuterId))
        {
            OuterId = product.OuterId;
        }

        if (string.IsNullOrEmpty(PackageType))
        {
            PackageType = product.PackageType;
        }

        if (string.IsNullOrEmpty(ManufacturerPartNumber))
        {
            ManufacturerPartNumber = product.ManufacturerPartNumber;
        }

        if (string.IsNullOrEmpty(WeightUnit))
        {
            WeightUnit = product.WeightUnit;
        }

        if (string.IsNullOrEmpty(MeasureUnit))
        {
            MeasureUnit = product.MeasureUnit;
        }

        if (string.IsNullOrEmpty(DownloadType))
        {
            DownloadType = product.DownloadType;
        }

        if (string.IsNullOrEmpty(ShippingType))
        {
            ShippingType = product.ShippingType;
        }

        if (string.IsNullOrEmpty(TaxType))
        {
            TaxType = product.TaxType;
        }

        Weight ??= product.Weight;
        Height ??= product.Height;
        Length ??= product.Length;
        Width ??= product.Width;

        MaxQuantity ??= product.MaxQuantity;
        MinQuantity ??= product.MinQuantity;

        if (Priority == 0)
        {
            Priority = product.Priority;
        }

        EndDate ??= product.EndDate;

        foreach (var image in product.Images)
        {
            var existingImage = Images.FirstOrDefault(x => x.Url.Equals(image.Url, StringComparison.InvariantCultureIgnoreCase));
            if (existingImage != null)
            {
                existingImage.Id = image.Id;
            }
            else
            {
                Images.Add(image);
            }
        }

        var assetComparer = AnonymousComparer.Create((Asset x) => x.Url);
        Assets = Assets.Concat(product.Assets).Distinct(assetComparer).ToList();

        var reviewsComparer = AnonymousComparer.Create((EditorialReview x) => string.Join(":", x.ReviewType, x.LanguageCode, x.Content));
        Reviews = Reviews.Concat(product.Reviews).Distinct(reviewsComparer).ToList();

        // Merge Properties - leave properties that are not presented in CSV and add all from the CSV (with merging metadata and replacing existing ones)
        var propertyComparer = AnonymousComparer.Create((Property x) => x.Name);
        var skippedExistingProperties = new List<Property>();

        foreach (var property in Properties.OfType<CsvProperty>())
        {
            var existingProperty = product.Properties.FirstOrDefault(x => propertyComparer.Equals(x, property));
            if (existingProperty != null)
            {
                property.MergeFrom(existingProperty);
                skippedExistingProperties.Add(existingProperty);
            }
        }

        Properties = Properties.Where(x => !x.Name.IsNullOrEmpty())
            .Concat(product.Properties
                .Where(x => !skippedExistingProperties.Any(existingProperty => propertyComparer.Equals(x, existingProperty))))
            .ToList();

        // Merge SEO
        var seoComparer = AnonymousComparer.Create((SeoInfo x) => string.Join(":", x.SemanticUrl, x.LanguageCode?.ToLower(), x.StoreId));

        foreach (var seoInfo in SeoInfos.OfType<CsvSeoInfo>())
        {
            var existingSeoInfo = product.SeoInfos.FirstOrDefault(x => seoComparer.Equals(x, seoInfo));
            if (existingSeoInfo != null)
            {
                seoInfo.MergeFrom(existingSeoInfo);
                product.SeoInfos.Remove(existingSeoInfo);
            }
        }
        SeoInfos = SeoInfos.Where(x => !x.SemanticUrl.IsNullOrEmpty()).Concat(product.SeoInfos).ToList();
    }

    public void CreateImagesFromFlatData()
    {
        var imageUrls = new List<string>();
        var imageGroups = new List<string>();

        if (!string.IsNullOrEmpty(PrimaryImage))
        {
            imageUrls.Add(PrimaryImage);
            imageGroups.Add(PrimaryImageGroup);
        }

        if (!string.IsNullOrEmpty(AltImage))
        {
            imageUrls.AddRange(AltImage.Split(_csvCellDelimiter, StringSplitOptions.RemoveEmptyEntries));
            imageGroups.AddRange(AltImageGroup.Split(_csvCellDelimiter, StringSplitOptions.RemoveEmptyEntries));
        }

        // Fill imageGroups with empty strings if its length is less than imageUrls
        while (imageGroups.Count < imageUrls.Count)
        {
            imageGroups.Add(string.Empty);
        }

        var index = 0;
        var images = imageUrls.Zip(imageGroups, (url, group) => new Image
        {
            Url = url,
            Group = string.IsNullOrEmpty(group) ? "images" : group,
            SortOrder = index++,
            Name = UrlHelper.ExtractFileNameFromUrl(url),
        });

        Images.AddRange(images);
    }
}
