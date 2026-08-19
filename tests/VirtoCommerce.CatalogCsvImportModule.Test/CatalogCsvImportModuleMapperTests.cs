using System;
using AutoMapper;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class CatalogCsvImportModuleMapperTests
{
    private static readonly IMapper _legacyMapper = new MapperConfiguration(cfg =>
        cfg.AddProfile<LegacyCatalogProductMappingProfile>()).CreateMapper();

    private readonly CatalogCsvImportModuleMapper _mapper = new();

    [Fact]
    public void LegacyAutoMapper_WithDerivedProfileRegistered_MapsDerivedPropertyViaPolymorphism()
    {
        var legacyMapperWithBothProfiles = new MapperConfiguration(cfg =>
            cfg.AddProfile<LegacyDerivedCatalogProductMappingProfile>()).CreateMapper();

        CsvProduct source = new DerivedCsvProductStandIn { Id = "id-1", ExtraProperty = "extra-value" };
        CatalogProduct target = new DerivedCatalogProductStandIn();

        legacyMapperWithBothProfiles.Map(source, target);

        target.Id.Should().Be("id-1");
        ((DerivedCatalogProductStandIn)target).ExtraProperty.Should().Be("extra-value");
    }

    [Fact]
    public void LegacyAutoMapper_WithoutDerivedProfile_FallsBackToBaseMapWithoutThrowing()
    {
        CsvProduct source = new DerivedCsvProductStandIn { Id = "id-1", ExtraProperty = "extra-value" };
        CatalogProduct target = new DerivedCatalogProductStandIn();

        _legacyMapper.Map(source, target);

        target.Id.Should().Be("id-1");
        ((DerivedCatalogProductStandIn)target).ExtraProperty.Should().BeNull();
    }

    [Fact]
    public void MapTo_NullSource_DoesNotThrowAndLeavesTargetUntouched()
    {
        CsvProduct source = null;
        var target = new CatalogProduct { Name = "unchanged" };

        _mapper.MapTo(source, target);

        target.Name.Should().Be("unchanged");
        target.Id.Should().BeNull();
    }

    [Fact]
    public void MapTo_NullTarget_Throws()
    {
        var source = new CsvProduct { Id = "id-1" };

        FluentActions.Invoking(() => _mapper.MapTo(source, null)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapTo_CopiesScalarAndReferenceFields()
    {
        var source = new CsvProduct
        {
            Id = "id-1",
            Code = "sku-1",
            Name = "Product 1",
            CategoryId = "cat-1",
            Vendor = "vendor-1",
            IsActive = true,
            Priority = 5,
        };

        var target = new CatalogProduct();

        _mapper.MapTo(source, target);

        target.Id.Should().Be("id-1");
        target.Code.Should().Be("sku-1");
        target.Name.Should().Be("Product 1");
        target.CategoryId.Should().Be("cat-1");
        target.Vendor.Should().Be("vendor-1");
        target.IsActive.Should().BeTrue();
        target.Priority.Should().Be(5);

        target.Should().NotBeOfType<CsvProduct>();
    }

    [Fact]
    public void MapTo_ProducesSameResultAsLegacyAutoMapperConventionMap()
    {
        var source = CreateFullyPopulatedCsvProduct();

        var expected = new CatalogProduct();
        _legacyMapper.Map(source, expected);

        var actual = new CatalogProduct();
        _mapper.MapTo(source, actual);

        actual.Should().BeEquivalentTo(expected);
    }

    private static CsvProduct CreateFullyPopulatedCsvProduct()
    {
        return new CsvProduct
        {
            Id = "id-1",
            CreatedDate = new DateTime(2020, 1, 1),
            ModifiedDate = new DateTime(2020, 1, 2),
            CreatedBy = "creator",
            ModifiedBy = "modifier",

            ProductType = "Physical",
            Code = "sku-1",
            ManufacturerPartNumber = "mpn-1",
            Gtin = "gtin-1",
            Name = "Product 1",
            CatalogId = "catalog-1",
            Catalog = new Catalog { Id = "catalog-1" },
            CategoryId = "category-1",
            Category = new Category { Id = "category-1" },
            MainProductId = "main-1",
            MainProduct = new CatalogProduct { Id = "main-1" },
            IsActive = true,
            IsBuyable = true,
            TrackInventory = true,
            IndexingDate = new DateTime(2020, 1, 3),
            MaxQuantity = 100,
            MinQuantity = 1,
            PackSize = 2,
            StartDate = new DateTime(2020, 1, 4),
            EndDate = new DateTime(2020, 1, 5),
            PackageType = "box",
            WeightUnit = "kg",
            Weight = 1.5m,
            MeasureUnit = "cm",
            Height = 10m,
            Length = 20m,
            Width = 30m,
            EnableReview = true,
            MaxNumberOfDownload = 3,
            DownloadExpiration = new DateTime(2020, 1, 6),
            DownloadType = "Software",
            HasUserAgreement = true,
            ShippingType = "standard",
            TaxType = "tax-1",
            Vendor = "vendor-1",
            Priority = 5,
            OuterId = "outer-1",
            Properties = [new Property { Name = "prop-1" }],
            ExcludedProperties = [new ExcludedProperty { Name = "excluded-1" }],
            Images = [new Image { Url = "https://example.com/image.png" }],
            Assets = [new Asset { Url = "https://example.com/asset.zip" }],
            Links = [new CategoryLink { CategoryId = "category-1" }],
            Variations = [new Variation { Id = "variation-1" }],
            SeoInfos = [new SeoInfo { Id = "seo-1" }],
            Reviews = [new EditorialReview { Id = "review-1" }],
            Associations = [new ProductAssociation { AssociatedObjectId = "assoc-1" }],
            ReferencedAssociations = [new ProductAssociation { AssociatedObjectId = "ref-assoc-1" }],
            Outlines = [],
            RelevanceScore = 0.75,
        };
    }
}
