using System;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CatalogCsvImportModuleMapper : ICatalogCsvImportModuleMapper
{
    public virtual void MapTo(CsvProduct source, CatalogProduct target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (source == null)
        {
            return;
        }

        target.Id = source.Id;
        target.CreatedDate = source.CreatedDate;
        target.ModifiedDate = source.ModifiedDate;
        target.CreatedBy = source.CreatedBy;
        target.ModifiedBy = source.ModifiedBy;

        target.ProductType = source.ProductType;
        target.Code = source.Code;
        target.ManufacturerPartNumber = source.ManufacturerPartNumber;
        target.Gtin = source.Gtin;
        target.Name = source.Name;
        target.LocalizedName = source.LocalizedName;
        target.CatalogId = source.CatalogId;
        target.Catalog = source.Catalog;
        target.CategoryId = source.CategoryId;
        target.Category = source.Category;
        target.MainProductId = source.MainProductId;
        target.MainProduct = source.MainProduct;
        target.IsActive = source.IsActive;
        target.IsBuyable = source.IsBuyable;
        target.TrackInventory = source.TrackInventory;
        target.IndexingDate = source.IndexingDate;
        target.MaxQuantity = source.MaxQuantity;
        target.MinQuantity = source.MinQuantity;
        target.PackSize = source.PackSize;
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.PackageType = source.PackageType;
        target.WeightUnit = source.WeightUnit;
        target.Weight = source.Weight;
        target.MeasureUnit = source.MeasureUnit;
        target.Height = source.Height;
        target.Length = source.Length;
        target.Width = source.Width;
        target.EnableReview = source.EnableReview;
        target.MaxNumberOfDownload = source.MaxNumberOfDownload;
        target.DownloadExpiration = source.DownloadExpiration;
        target.DownloadType = source.DownloadType;
        target.HasUserAgreement = source.HasUserAgreement;
        target.ShippingType = source.ShippingType;
        target.TaxType = source.TaxType;
        target.Vendor = source.Vendor;
        target.Priority = source.Priority;
        target.OuterId = source.OuterId;
        target.Properties = source.Properties;
        target.ExcludedProperties = source.ExcludedProperties;
        target.Images = source.Images;
        target.Assets = source.Assets;
        target.Links = source.Links;
        target.Variations = source.Variations;
        target.SeoInfos = source.SeoInfos;
        target.Reviews = source.Reviews;
        target.Associations = source.Associations;
        target.ReferencedAssociations = source.ReferencedAssociations;
        target.Outlines = source.Outlines;
        target.RelevanceScore = source.RelevanceScore;
    }
}
