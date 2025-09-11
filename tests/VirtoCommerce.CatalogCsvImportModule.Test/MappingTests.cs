using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoFixture;
using CsvHelper;
using CsvHelper.Configuration;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.ExportImport;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class MappingTests
{
    private static readonly Action<ExportImportProgressInfo> _progressCallback = _ => { };

    [Fact]
    public async Task CsvProductMapTest_CsvHasPropertyValues_PropertyValuesMapped()
    {
        var csvProducts = await ReadCsvFile("product-propertyvalues.csv", configuration =>
        {
            configuration.CsvColumns = ["Sku"];
            configuration.PropertyCsvColumns = ["ProductProperty", "ProductProperty_Multivalue"];
        });

        Action<PropertyValue>[] inspectorsFirstProduct =
        [
            x => Assert.True((string) x.Value == "Product-1-propertyvalue-test" && x.PropertyName =="ProductProperty"),
            x => Assert.True((string) x.Value == "Product-1-multivalue-1, Product-1-multivalue-2" && x.PropertyName =="ProductProperty_Multivalue"),
        ];
        Action<PropertyValue>[] inspectorsSecond =
        [
            x => Assert.True((string) x.Value == "Product-2-propertyvalue-test" && x.PropertyName =="ProductProperty"),
            x => Assert.True((string) x.Value == "Product-2-multivalue-1, Product-2-multivalue-1, Product-2-multivalue-3" && x.PropertyName =="ProductProperty_Multivalue"),
        ];

        Assert.NotEmpty(csvProducts);
        Assert.Collection(csvProducts.First().Properties.SelectMany(x => x.Values), inspectorsFirstProduct);
        Assert.Collection(csvProducts.Last().Properties.SelectMany(x => x.Values), inspectorsSecond);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasProductProperties_PropertiesMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties.csv");

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal("429408", product.Id);
        Assert.Equal("CBLK21113", product.Sku);
        Assert.Equal("cblk21113-product-1", product.Name);
        Assert.Equal("catId_1", product.CategoryId);
        Assert.Equal("GTIN_Value", product.Gtin);
        Assert.Equal("mainprod_123", product.MainProductId);
        Assert.Equal("Vendor_value", product.Vendor);
        Assert.Equal("ProductType_value", product.ProductType);
        Assert.Equal("ShippingType_value", product.ShippingType);
        Assert.Equal("DownloadType_value", product.DownloadType);
        Assert.Equal("OuterId", product.OuterId);
        Assert.Equal(1, product.Priority);
        Assert.Equal(10, product.MaxQuantity);
        Assert.Equal(5, product.MinQuantity);
        Assert.Equal("PackageType", product.PackageType);
        Assert.Equal("FulfillmentCenterId", product.FulfillmentCenterId);
        Assert.Equal(1, product.MaxNumberOfDownload);

        Assert.True(product.HasUserAgreement);
        Assert.True(product.IsBuyable);
        Assert.True(product.TrackInventory);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasPriceAndQuantity_PriceAndQuantityMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-priceQuantity.csv");

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal("123.4", product.ListPrice);
        Assert.Equal(123.4m, product.Price.List);
        Assert.Equal("456.7", product.SalePrice);
        Assert.Equal(456.7m, product.Price.Sale);
        Assert.Equal("EUR", product.Currency);
        Assert.Equal("EUR", product.Price.Currency);
        Assert.Equal("5", product.PriceMinQuantity);
        Assert.Equal(5, product.Price.MinQuantity);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasSeoInfo_SeoInfoMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-seoInfo.csv");

        csvProducts.Should().HaveCount(2);

        var product = csvProducts.First(x => x.Code == "cblk21113-product-1");

        Assert.Equal("seo-slug-url", product.SeoUrl);
        Assert.Equal("seo-slug-url", product.SeoInfo.SemanticUrl);
        Assert.Equal("Seo_Title_Value", product.SeoTitle);
        Assert.Equal("Seo_Title_Value", product.SeoInfo.PageTitle);
        Assert.Equal("Seo_Descr_Value", product.SeoDescription);
        Assert.Equal("Seo_Descr_Value", product.SeoInfo.MetaDescription);
        Assert.Equal("Seo_Language_Value", product.SeoInfo.LanguageCode);
        Assert.Equal("Seo_Meta", product.SeoMetaKeywords);
        Assert.Equal("Seo_Meta", product.SeoInfo.MetaKeywords);
        Assert.Equal("Seo_Alt_text", product.SeoImageAlternativeText);
        Assert.Equal("Seo_Alt_text", product.SeoInfo.ImageAltDescription);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasReview_ReviewMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-review.csv");

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal("Review_Content", product.Review);
        Assert.Equal("Review_Content", product.EditorialReview.Content);
        Assert.Equal("ReviewType_Value", product.ReviewType);
        Assert.Equal("ReviewType_Value", product.EditorialReview.ReviewType);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasCategoryPath_CategoryPathMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-categoryPath.csv");

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal("TestCategory1", product.CategoryPath);
        Assert.Equal("TestCategory1", product.Category.Path);
    }

    [Fact]
    public async Task CsvProductMapTest_MappingHasDefaultCategoryPath_DefaultCategoryPathMapped()
    {
        const string defaultCategoryPath = "Custom_category_path_value";

        var csvProducts = await ReadCsvFile("product-productproperties-noCategoryPath.csv", configuration =>
        {
            var categoryPathMapping = configuration.PropertyMaps.FirstOrDefault(x => x.EntityColumnName == "CategoryPath");

            Assert.NotNull(categoryPathMapping);

            categoryPathMapping.CsvColumnName = null;
            categoryPathMapping.CustomValue = defaultCategoryPath;
        });

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal(defaultCategoryPath, product.CategoryPath);
        Assert.Equal(defaultCategoryPath, product.Category.Path);
    }

    [Fact]
    public async Task CsvProductMapTest_MappingHasDefaultBoolValue_DefaultBoolValuesMapped()
    {
        const bool defaultIsBuyableValue = true;

        var csvProducts = await ReadCsvFile("product-productproperties.csv", configuration =>
        {
            var categoryPathMapping = configuration.PropertyMaps.FirstOrDefault(x => x.EntityColumnName == "IsBuyable");

            Assert.NotNull(categoryPathMapping);

            categoryPathMapping.CsvColumnName = null;
            categoryPathMapping.CustomValue = defaultIsBuyableValue.ToString();
        });

        Assert.NotEmpty(csvProducts);

        var product = csvProducts.First();

        Assert.Equal(defaultIsBuyableValue, product.IsBuyable);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasBooleanValues_BooleanFieldsMapped()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-boolean.csv");

        Assert.False(csvProducts[0].HasUserAgreement);
        Assert.False(csvProducts[0].IsBuyable);
        Assert.False(csvProducts[0].TrackInventory);

        Assert.True(csvProducts[1].HasUserAgreement);
        Assert.True(csvProducts[1].IsBuyable);
        Assert.True(csvProducts[1].TrackInventory);
    }

    [Fact]
    public async Task CsvProductMapTest_CsvHasMultipleLines_LineNumberMapTest()
    {
        var csvProducts = await ReadCsvFile("product-productproperties-twoproducts.csv");

        Assert.Equal(2, csvProducts[0].LineNumber);
        Assert.Equal(3, csvProducts[1].LineNumber);
    }

    //Export mapping test

    [Fact]
    public void CsvHeadersExportTest_DefaultConfiguration_HeadersAreSame()
    {
        using var sw = new StringWriter();
        var exportInfo = new CsvExportInfo { Configuration = CsvProductMappingConfiguration.GetDefaultConfiguration() };
        var writerConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = exportInfo.Configuration.Delimiter,
        };

        using var csv = new CsvWriter(sw, writerConfig);
        csv.Context.RegisterClassMap(CsvProductMap.Create(exportInfo.Configuration));

        csv.WriteHeader<CsvProduct>();
        csv.Flush();

        var expected = string.Join(exportInfo.Configuration.Delimiter, exportInfo.Configuration.PropertyMaps.Select(x => x.CsvColumnName));

        Assert.Equal(expected, sw.ToString());
    }

    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    public async Task CsvProductMapTest_DictionaryMultilanguage_OnlyOneAliasExported(string delimiter)
    {
        //Arrange
        var product = GetProduct();
        product.Properties = new List<Property>
        {
            new()
            {
                Id = "property1",
                Name = "Dictionary_Multilanguage",
                Dictionary = true,
                Multilanguage = true,
                Values = new List<PropertyValue>
                {
                    new() { Alias = "A", Value = "EN_A", ValueType = PropertyValueType.ShortText },
                    new() { Alias = "A", Value = "DE_A", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        //Act
        var importedCsvProduct = await ExportAndImportProduct(product, delimiter);

        //Assert
        importedCsvProduct.Properties.Should().HaveCount(1);
        importedCsvProduct.Properties.First().Values.Should().HaveCount(1);
        importedCsvProduct.Properties.First().Values.First().Value.ToString().Should().BeEquivalentTo("A");
    }

    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    public async Task CsvProductMapTest_DictionaryMultivalue_OnlyUniqAliasesExported(string delimiter)
    {
        //Arrange
        var product = GetProduct();
        product.Properties = new List<Property>
        {
            new()
            {
                Id = "property1",
                Name = "Dictionary_Multivalue",
                Dictionary = true,
                Multilanguage = false,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { Alias = "A", Value = "EN_A", LanguageCode = "en-US", ValueType = PropertyValueType.ShortText },
                    new() { Alias = "A", Value = "DE_A", LanguageCode = "de-DE", ValueType = PropertyValueType.ShortText },
                    new() { Alias = "B", Value = "EN_B", LanguageCode = "en-US", ValueType = PropertyValueType.ShortText },
                    new() { Alias = "B", Value = "DE_B", LanguageCode = "de-DE", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        //Act
        var importedCsvProduct = await ExportAndImportProduct(product, delimiter);

        //Assert
        importedCsvProduct.Properties.Should().HaveCount(1);
        importedCsvProduct.Properties.First().Values.Should().HaveCount(2);
        importedCsvProduct.Properties.First().Values.First().Value.ToString().Should().BeEquivalentTo("A");
        importedCsvProduct.Properties.First().Values.Last().Value.ToString().Should().BeEquivalentTo("B");
    }


    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    public async Task CsvProductMapTest_Multilanguage_AllValueExported(string delimiter)
    {
        //Arrange
        var product = GetProduct();
        product.Properties = new List<Property>
        {
            new()
            {
                Id = "property1",
                Name = "Multilanguage",
                Dictionary = false,
                Multilanguage = true,
                Values = new List<PropertyValue>
                {
                    new() { Value = "EN_A", LanguageCode = "en-US", ValueType = PropertyValueType.ShortText },
                    new() { Value = "DE_A", LanguageCode = "de-DE", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        //Act
        var importedCsvProduct = await ExportAndImportProduct(product, delimiter);

        //Assert
        importedCsvProduct.Properties.Should().HaveCount(1);
        importedCsvProduct.Properties.First().Values.Should().HaveCount(2);
        importedCsvProduct.Properties.First().Values.First().Value.ToString().Should().BeEquivalentTo("EN_A");
        importedCsvProduct.Properties.First().Values.Last().Value.ToString().Should().BeEquivalentTo("DE_A");
    }


    // Support methods
    private static async Task<List<CsvProduct>> ReadCsvFile(string fileName, Action<CsvProductMappingConfiguration> configure = null)
    {
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = ",";
        configure?.Invoke(configuration);

        var filePath = $"../../../data/{fileName}";
        var stream = File.Open(filePath, FileMode.Open);

        var csvProducts = await new CsvProductReader().ReadProducts(stream, configuration, _progressCallback);

        return csvProducts;
    }

    private static CatalogProduct GetProduct()
    {
        var fixture = new Fixture();
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture.Build<CatalogProduct>()
            .With(x => x.Variations, new List<Variation>())
            .With(x => x.Associations, new List<ProductAssociation>())
            .With(x => x.ReferencedAssociations, new List<ProductAssociation>())
            .Create();
    }

    private static async Task<CsvProduct> ExportAndImportProduct(CatalogProduct product, string delimiter)
    {
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = delimiter;
        configuration.PropertyCsvColumns = product.Properties.Select(x => x.Name).Distinct().ToArray();

        var csvProductMap = CsvProductMap.Create(configuration);

        using var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

        var writerConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = configuration.Delimiter,
        };

        await using (var csvWriter = new CsvWriter(streamWriter, writerConfig))
        {
            csvWriter.Context.RegisterClassMap(csvProductMap);
            csvWriter.WriteHeader<CsvProduct>();
            await csvWriter.NextRecordAsync();
            var csvProduct = CsvProduct.Create(product, null, null, null, null);
            csvWriter.WriteRecord(csvProduct);
            await csvWriter.FlushAsync();
        }

        stream.Position = 0;

        var csvProducts = await new CsvProductReader().ReadProducts(stream, configuration, _progressCallback);

        return csvProducts.FirstOrDefault();
    }
}
