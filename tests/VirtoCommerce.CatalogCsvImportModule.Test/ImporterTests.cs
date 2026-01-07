using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Model.Search;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class ImporterTests
{
    private readonly IMapper _mapper;
    private readonly Catalog _catalog = CreateCatalog();
    private readonly List<Category> _categoriesInternal = [];
    private List<CatalogProduct> _productsInternal = [];
    private List<Price> _pricesInternal = [];
    private List<CatalogProduct> _savedProducts;

    public ImporterTests()
    {
        // To fix the error: 'Cyrillic' is not a supported encoding name. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method. (Parameter 'name')
        // https://github.com/dotnet/runtime/issues/17516
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<CatalogProductMappingProfile>();
        });

        _mapper = configuration.CreateMapper();
    }

    [Theory]
    [InlineData("https://example.com/path/to/file.txt", "file.txt")]
    [InlineData("https://example.com/path/to/file.txt?query=param", "file.txt")]
    [InlineData("https://example.com/path/to/file.txt#fragment", "file.txt")]
    [InlineData("https://example.com/path/to/file%20with%20spaces.txt", "file with spaces.txt")]
    [InlineData("/path/to/file.txt", "file.txt")]
    [InlineData("/path/to/file%20with%20spaces.txt", "file with spaces.txt")]
    [InlineData("https://example.com/path/to/", "")]
    [InlineData("https://example.com/", "")]
    public void ExtractFileNameFromUrl_ValidUrls_ReturnsExpectedFileName(string url, string expectedFileName)
    {
        // Act
        string result = null;
        if (!string.IsNullOrEmpty(url))
        {
            result = UrlHelper.ExtractFileNameFromUrl(url);
        }

        // Assert
        Assert.Equal(expectedFileName, result);
    }

    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    public async Task DoImport_NewProductMultilanguageProperty_ValuesCreated(string delimiter)
    {
        // Arrange
        var product = GetCsvProductBase();
        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_Multilanguage",
                Multilanguage = true,
                Values = new List<PropertyValue>
                {
                    new()
                    {
                        PropertyName = "CatalogProductProperty_Multilanguage",
                        LanguageCode = "en-US",
                        Value = "value-en",
                        ValueType = PropertyValueType.ShortText,
                    },
                    new()
                    {
                        PropertyName = "CatalogProductProperty_Multilanguage",
                        LanguageCode = "de-DE",
                        Value = "value-de",
                        ValueType = PropertyValueType.ShortText,
                    },
                },
            },
        };

        var target = GetImporter();

        var progressInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(delimiter), progressInfo, _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.PropertyName == "CatalogProductProperty_Multilanguage" && x.LanguageCode == "en-US" && (string)x.Value == "value-en"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_Multilanguage" && x.LanguageCode == "de-DE" && (string)x.Value == "value-de"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        Assert.Empty(progressInfo.Errors);
    }

    [Theory]
    [InlineData(",")]
    [InlineData(";")]
    public async Task DoImport_ColorMultivalueDictionary_ColorCodeIsTakenFromDictionary(string delimiter)
    {
        // Arrange
        var csvProduct = GetCsvProductBase();

        csvProduct.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "Catalog Product Property 3 Color Multivalue Dictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "Catalog Product Property 3 Color Multivalue Dictionary", Value = "Red" },
                    new() { PropertyName = "Catalog Product Property 3 Color Multivalue Dictionary", Value = "Blue" },
                },
            },
        };

        var importer = GetImporter();

        var progressInfo = new ExportImportProgressInfo();

        // Act
        await importer.DoImport([csvProduct], GetCsvImportInfo(delimiter), progressInfo, _ => { });

        // Assert
        Assert.Empty(progressInfo.Errors);
        csvProduct.Properties.Should().HaveCount(1);

        var property = csvProduct.Properties.First();
        property.Values.Should().HaveCount(2);

        var value1 = property.Values[0];
        Assert.Equal("CatalogProductProperty_3_ColorMultivalueDictionary_1", value1.ValueId);
        Assert.Equal(PropertyValueType.Color, value1.ValueType);
        Assert.Equal("#ff0000", value1.ColorCode);
        Assert.Equal("Red", value1.Alias);

        var value2 = property.Values[1];
        Assert.Equal("CatalogProductProperty_3_ColorMultivalueDictionary_3", value2.ValueId);
        Assert.Equal(PropertyValueType.Color, value2.ValueType);
        Assert.Equal("#0000ff", value2.ColorCode);
        Assert.Equal("Blue", value2.Alias);
    }

    [Fact]
    public async Task DoImport_NewProductMultivalueDictionaryProperties_PropertyValuesCreated()
    {
        // Arrange
        var product = GetCsvProductBase();
        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1, 3", ValueType = PropertyValueType.ShortText },
                },
            },
            new CsvProperty
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "2, 1", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var target = GetImporter();

        var progressInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), progressInfo, _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_3" && x.Alias == "3"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_2" && x.Alias == "2"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_1" && x.Alias == "1"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        Assert.Empty(progressInfo.Errors);
    }

    [Fact]
    public async Task DoImport_NewProductDictionaryMultivaluePropertyWithNotExistingValue_ErrorIsPresent()
    {
        // Arrange
        var product = GetCsvProductBase();
        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "NotExistingValue", ValueType = PropertyValueType.ShortText },
                },
            }};

        var target = GetImporter();

        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.NotEmpty(exportInfo.Errors);
    }

    [Fact]
    public async Task DoImport_NewProductDictionaryMultivaluePropertyWithNewValue_NewDictPropertyItemCreated()
    {
        // Arrange
        var product = GetCsvProductBase();
        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var mockPropDictItemService = new Mock<IPropertyDictionaryItemService>();
        var target = GetImporter(mockPropDictItemService.Object, createDictionaryValues: true);

        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        mockPropDictItemService.Verify(mock => mock.SaveChangesAsync(It.Is<List<PropertyDictionaryItem>>(dictItems => dictItems.Any(dictItem => dictItem.Alias == "NewValue"))), Times.Once());

        Assert.Empty(exportInfo.Errors);
    }

    [Fact]
    public async Task DoImport_UpdateProductDictionaryMultivalueProperties_PropertyValuesMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        existingProduct.Properties = new List<Property>
        {
            new()
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "TestCategory_ProductProperty_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "2,3", ValueType = PropertyValueType.ShortText },
                },
            },
            new CsvProperty
            {
                Name = "TestCategory_ProductProperty_MultivalueDictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var target = GetImporter();
        var progressInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), progressInfo, _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_2" && x.Alias == "2"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_3" && x.Alias == "3"),
            x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_2" && x.Alias == "2"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_2" && x.Alias == "2"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        Assert.Empty(progressInfo.Errors);
    }

    [Fact]
    public async Task DoImport_UpdateProductCategory_CategoryIsNotUpdated()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.Properties = new List<Property>();
        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();
        product.Category = null;

        var target = GetImporter();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Assert.NotNull(product.Category);
        Assert.Equal(existingProduct.Category.Id, product.Category.Id);
    }

    [Fact]
    public async Task DoImport_UpdateProductNameIsNull_NameIsNotUpdated()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.Properties = new List<Property>();
        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();
        product.Name = null;

        var target = GetImporter();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Assert.Equal(existingProduct.Name, product.Name);
    }



    [Fact]
    public async Task DoImport_UpdateProductMultivalueProperties_PropertyValuesMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        existingProduct.Properties = new List<Property>
        {
            new()
            {
                Name = "CatalogProductProperty_1_Multivalue",
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_Multivalue", Value = "TestValue1", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "CatalogProductProperty_1_Multivalue", Value = "TestValue2", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "CatalogProductProperty_2_Multivalue",
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue3", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue4", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "TestCategory_ProductProperty_Multivalue",
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue5", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue6", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_2_Multivalue",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue1, TestValue2", ValueType = PropertyValueType.ShortText },
                },
            },
            new CsvProperty
            {
                Name = "TestCategory_ProductProperty_Multivalue",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue3", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var target = GetImporter();
        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Multivalue" && (string) x.Value == "TestValue1"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Multivalue" && (string) x.Value == "TestValue2"),
            x => Assert.True(x.PropertyName == "TestCategory_ProductProperty_Multivalue" && (string) x.Value == "TestValue3"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Multivalue" && (string) x.Value == "TestValue1"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Multivalue" && (string) x.Value == "TestValue2"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
    }


    [Fact]
    public async Task DoImport_NewProductDictionaryProperties_PropertyValuesCreated()
    {
        // Arrange
        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_Dictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_Dictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                },
            },
            new CsvProperty
            {
                Name = "CatalogProductProperty_2_Dictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_Dictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var target = GetImporter();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Dictionary" && (string) x.Value == "1"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Dictionary" && (string) x.Value == "2"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
    }

    [Fact]
    public async Task DoImport_NewProductDictionaryPropertyWithNotExistingValue_ErrorIsPresent()
    {
        // Arrange
        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_Dictionary",
                Dictionary = true,
                Multivalue = false,
                Values =
                [
                    new PropertyValue{ PropertyName = "CatalogProductProperty_1_Dictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                ],
            },
        };

        var target = GetImporter();

        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.NotEmpty(exportInfo.Errors);
    }

    [Fact]
    public async Task DoImport_NewProductDictionaryPropertyWithNewValue_NewPropertyValueCreated()
    {
        // Arrange
        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1_Dictionary",
                Dictionary = true,
                Multivalue = false,
                Values =
                [
                    new PropertyValue{ PropertyName = "CatalogProductProperty_1_Dictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                ],
            },
        };

        var mockPropDictItemService = new Mock<IPropertyDictionaryItemService>();
        var target = GetImporter(mockPropDictItemService.Object, createDictionaryValues: true);


        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        mockPropDictItemService.Verify(mock => mock.SaveChangesAsync(It.Is<List<PropertyDictionaryItem>>(dictItems => dictItems.Any(dictItem => dictItem.Alias == "NewValue"))), Times.Once());
        Assert.Empty(exportInfo.Errors);
    }

    [Fact]
    public async Task DoImport_NewProductProperties_PropertyValuesCreated()
    {
        // Arrange
        var target = GetImporter();

        var product = GetCsvProductBase();

        product.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_1",
                Values =
                [
                    new PropertyValue{ PropertyName = "CatalogProductProperty_1", Value = "1", ValueType = PropertyValueType.ShortText },
                ],
            },
            new CsvProperty
            {
                Name = "CatalogProductProperty_2",
                Values =
                [
                    new PropertyValue{ PropertyName = "CatalogProductProperty_2", Value = "2", ValueType = PropertyValueType.ShortText },
                ],
            },
        };

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.PropertyName == "CatalogProductProperty_1" && (string) x.Value == "1"),
            x => Assert.True(x.PropertyName == "CatalogProductProperty_2" && (string) x.Value == "2"),
        ];
        Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
    }


    [Fact]
    public async Task DoImport_UpdateProductSeoInfoIsEmpty_SeoInfosNotClearedUp()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.Properties = new List<Property>();
        existingProduct.SeoInfos = new List<SeoInfo>
        {
            new()
            {
                Id = "SeoInfo_test",
                Name = "SeoInfo_test",
            },
        };
        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();

        var target = GetImporter();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        product.SeoInfos.Should().HaveCount(1);
        Assert.Equal(existingProduct.SeoInfos.First().Id, product.SeoInfos.First().Id);
    }

    [Fact]
    public async Task DoImport_UpdateProductReviewIsEmpty_ReviewsNotClearedUp()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.Properties = new List<Property>();
        existingProduct.Reviews = new List<EditorialReview>
        {
            new()
            {
                Id = "EditorialReview_test",
                Content = "EditorialReview_test",
            },
        };
        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product = GetCsvProductBase();

        var target = GetImporter();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        product.Reviews.Should().HaveCount(1);
        Assert.Equal(existingProduct.Reviews.First().Id, product.Reviews.First().Id);
    }

    [Fact]
    public async Task DoImport_UpdateProductTwoProductsWithSameCode_ProductsMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.Id = null;
        secondProduct.Id = null;

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        _savedProducts.Should().HaveCount(1);
    }

    [Fact]
    public async Task DoImport_TwoProductsSameCodeDifferentReviewTypes_ReviewsMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        existingProduct.Reviews.Clear();

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.EditorialReview.ReviewType = "FullReview";
        firstProduct.EditorialReview.Content = "Review Content 1";
        secondProduct.EditorialReview.ReviewType = "QuickReview";
        secondProduct.EditorialReview.Content = "Review Content 2";

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<EditorialReview>[] inspectors =
        [
            x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 1"),
            x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 2"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().Reviews, inspectors);
    }

    [Fact]
    public async Task DoImport_TwoProductsSameCodeSameReviewTypes_ReviewsMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        existingProduct.Reviews.Clear();

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.EditorialReview.Content = "Review Content 1";
        secondProduct.EditorialReview.Content = "Review Content 2";

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<EditorialReview>[] inspectors =
        [
            x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 1"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().Reviews, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductTwoProductsSameCodeDifferentReviewTypes_ProductsMerged_ReviewsAdded()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        existingProduct.Reviews = new List<EditorialReview>
        {
            new() { Content = "Review Content 3", ReviewType = "QuickReview", Id = "1", LanguageCode = "en-US"},
        };


        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.EditorialReview.ReviewType = "FullReview";
        firstProduct.EditorialReview.Content = "Review Content 1";
        secondProduct.EditorialReview.ReviewType = "QuickReview";
        secondProduct.EditorialReview.Content = "Review Content 2";
        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert

        var savedReview = _savedProducts.FirstOrDefault()?.Reviews;

        _savedProducts.Should().HaveCount(1);
        savedReview.Should().HaveCount(3);
        savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 1" && x.ReviewType == "FullReview");
        savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 2" && x.ReviewType == "QuickReview");
        savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 3" && x.ReviewType == "QuickReview");
    }

    [Fact]
    public async Task DoImport_UpdateProductTwoProductsSameCodeDifferentReviewTypes_ProductsMerged_ReviewsMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        existingProduct.Reviews = new List<EditorialReview>
        {
            new() { Content = "Review Content 1", ReviewType = "FullReview" , Id = "1", LanguageCode = "en-US" },
            new() { Content = "Review Content 2", ReviewType = "QuickReview", Id = "2", LanguageCode = "en-US" },
        };

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();

        firstProduct.EditorialReview = new EditorialReview { Content = "Review Content 1", ReviewType = "FullReview" };
        secondProduct.EditorialReview = new EditorialReview { Content = "Review Content 2", ReviewType = "QuickReview" };


        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert

        var savedReview = _savedProducts.FirstOrDefault()?.Reviews;

        _savedProducts.Should().HaveCount(1);
        savedReview.Should().HaveCount(2);
        savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 1" && x.ReviewType == "FullReview");
        savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 2" && x.ReviewType == "QuickReview");
    }



    [Fact]
    public async Task DoImport_TwoProductsSameCodeDifferentSeoInfo_SeoInfosMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.SeoInfos.Clear();

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
        secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl2";

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<SeoInfo>[] inspectors =
        [
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1"),
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl2"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().SeoInfos, inspectors);
    }

    [Fact]
    public async Task DoImport_TwoProductsSameCodeSameSeoInfo_SeoInfosMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();
        existingProduct.SeoInfos.Clear();

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
        secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<SeoInfo>[] inspectors =
        [
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().SeoInfos, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductTwoProductsSameCodeDifferentSeoInfo_SeoInfosMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        existingProduct.SeoInfos = new List<SeoInfo> { new() { Id = "1", LanguageCode = "en-US", SemanticUrl = "SemanticsUrl3" } };

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();
        firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
        secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl2";

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<SeoInfo>[] inspectors =
        [
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1"),
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl2"),
            x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl3"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().SeoInfos, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductsTwoProductsSamePropertyName_PropertyValuesMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        existingProduct.Properties = new List<Property>
        {
            new()
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    new() { PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                },
            },
            new()
            {
                Name = "TestCategory_ProductProperty_MultivalueDictionary",
                Dictionary = true,
                Multivalue = true,
                Values = new List<PropertyValue>(),
            },
        };

        _productsInternal = [existingProduct];

        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var firstProduct = GetCsvProductBase();
        var secondProduct = GetCsvProductBase();

        firstProduct.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                },
            },
            new CsvProperty
            {
                Name = "TestCategory_ProductProperty_MultivalueDictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "1,2", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        secondProduct.Properties = new List<Property>
        {
            new CsvProperty
            {
                Name = "TestCategory_ProductProperty_MultivalueDictionary",
                Values = new List<PropertyValue>
                {
                    new() { PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                },
            },
        };

        var list = new List<CsvProduct> { firstProduct, secondProduct };

        var progressInfo = new ExportImportProgressInfo();

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), progressInfo, _ => { });

        // Assert
        Action<PropertyValue>[] inspectors =
        [
            x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_3" && x.Alias == "3"),
            x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_1" && x.Alias == "1"),
            x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_2" && x.Alias == "2"),
            x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_3" && x.Alias == "3"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
            x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_2" && x.Alias == "2"),
        ];

        _savedProducts.Should().HaveCount(1);
        Assert.Collection(_savedProducts.First().Properties.SelectMany(x => x.Values), inspectors);
        Assert.Empty(progressInfo.Errors);
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceCurrency_PriceUpdated()
    {
        // Arrange
        var listPrice = 555.5m;
        var existingPriceId = "ExistingPrice_ID";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> { new CsvPrice
        {
            List = listPrice,
            Sale = listPrice,
            Currency = "EUR",
            MinQuantity = 1,
        }};

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = 333.3m,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
                MinQuantity = 2,
            },

        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<Price>[] inspectors =
        [
            x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.MinQuantity == 1),
        ];
        Assert.Collection(_pricesInternal, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceId_PriceUpdated()
    {
        // Arrange
        var listPrice = 555.5m;
        var existingPriceId = "ExistingPrice_ID";
        var existingPriceId2 = "ExistingPrice_ID_2";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> {new CsvPrice
        {
            List = listPrice,
            Sale = listPrice,
            Currency = "EUR",
            Id = existingPriceId,
        }};

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = 333.3m,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },

            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = 333.3m,
                Id = existingPriceId2,
                ProductId = firstProduct.Id,
            },

        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<Price>[] inspectors =
        [
            x => Assert.True(x.List ==  333.3m && x.Id == existingPriceId2 && x.ProductId == firstProduct.Id),
            x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id),
        ];
        Assert.Collection(_pricesInternal, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceListId_PriceUpdated()
    {
        // Arrange
        var listPrice = 555.5m;
        var existingPriceId = "ExistingPrice_ID";
        var existingPriceId2 = "ExistingPrice_ID_2";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> {new CsvPrice
        {
            List = listPrice,
            Sale = listPrice,
            Currency = "EUR",
            PricelistId = "DefaultEUR",
        }};

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = 333.3m,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },

            new Price
            {
                Currency = "USD",
                PricelistId = "DefaultUSD",
                List = 333.3m,
                Id = existingPriceId2,
                ProductId = firstProduct.Id,
            },

        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<Price>[] inspectors =
        [
            x => Assert.True(x.List ==  333.3m && x.PricelistId == "DefaultUSD" && x.Id == existingPriceId2 && x.ProductId == firstProduct.Id),
            x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.PricelistId == "DefaultEUR" && x.ProductId == firstProduct.Id),
        ];
        Assert.Collection(_pricesInternal, inspectors);
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceListIdWithoutCurrency_PriceUpdated()
    {
        // Arrange
        var newPrice = 555.5m;
        var oldPrice = 333.3m;
        var existingPriceId = "ExistingPrice_ID";
        var existingPriceId2 = "ExistingPrice_ID_2";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> {
            new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "DefaultEUR" },
            new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "DefaultUSD" },
        };

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = oldPrice,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },
            new Price
            {
                Currency = "USD",
                PricelistId = "DefaultUSD",
                List = oldPrice,
                Id = existingPriceId2,
                ProductId = firstProduct.Id,
            },
        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        _pricesInternal.Should().HaveCount(2);
        _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "DefaultEUR");
        _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "DefaultUSD");
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceListId_PriceAdded()
    {
        // Arrange
        var newPrice = 555.5m;
        var oldPrice = 333.3m;
        var existingPriceId = "ExistingPrice_ID";
        var existingPriceId2 = "ExistingPrice_ID_2";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> {
            new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "NewDefaultEUR" },
        };

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = oldPrice,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },
            new Price
            {
                Currency = "USD",
                PricelistId = "DefaultUSD",
                List = oldPrice,
                Id = existingPriceId2,
                ProductId = firstProduct.Id,
            },
        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        _pricesInternal.Should().HaveCount(3);
        _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "NewDefaultEUR");
        _pricesInternal.Should().Contain(x => x.List == oldPrice && x.PricelistId == "DefaultEUR");
        _pricesInternal.Should().Contain(x => x.List == oldPrice && x.PricelistId == "DefaultUSD");
    }

    [Fact]
    public async Task DoImport_UpdateProductHasPriceListId_PricesWithDifferentQuantitiesAdded()
    {
        // Arrange
        var newPrice1 = 555.5m;
        var newPrice2 = 333.3m;
        var existingPricelistId = "ExistingPricelist_ID";
        var minQuantity1 = 1;
        var minQuantity2 = 2;

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> {
            new CsvPrice { List = newPrice1, Sale = newPrice1, PricelistId = existingPricelistId, MinQuantity = minQuantity1 },
            new CsvPrice { List = newPrice2, Sale = newPrice2, PricelistId = existingPricelistId, MinQuantity = minQuantity2 },
        };

        _pricesInternal = [];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        _pricesInternal.Should().HaveCount(2);
        _pricesInternal.Should().Contain(x => x.List == newPrice1 && x.PricelistId == existingPricelistId && x.MinQuantity == minQuantity1);
        _pricesInternal.Should().Contain(x => x.List == newPrice2 && x.PricelistId == existingPricelistId && x.MinQuantity == minQuantity2);
    }


    [Fact]
    public async Task DoImport_UpdateProductsTwoProductDifferentPriceCurrency_PricesMerged()
    {
        // Arrange
        var listPrice = 555.5m;
        var salePrice = 666.6m;
        var existingPriceId = "ExistingPrice_ID";

        var existingProduct = GetCsvProductBase();
        _productsInternal = [existingProduct];

        var firstProduct = GetCsvProductBase();
        firstProduct.Prices = new List<Price> { new CsvPrice { List = listPrice, Sale = salePrice, Currency = "EUR" } };

        var secondProduct = GetCsvProductBase();
        secondProduct.Prices = new List<Price> { new CsvPrice { List = listPrice, Sale = salePrice, Currency = "USD" } };

        _pricesInternal =
        [
            new Price
            {
                Currency = "EUR",
                PricelistId = "DefaultEUR",
                List = 333.3m,
                Sale = 444.4m,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },
            new Price
            {
                Currency = "USD",
                PricelistId = "DefaultUSD",
                List = 444.4m,
                Sale = 555.5m,
                Id = existingPriceId,
                ProductId = firstProduct.Id,
            },
        ];

        var target = GetImporter();

        // Act
        await target.DoImport([firstProduct, secondProduct], GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<Price>[] inspectors =
        [
            x => Assert.True(x.List == listPrice && x.Sale == salePrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.Currency == "EUR"),
            x => Assert.True(x.List == listPrice && x.Sale == salePrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.Currency == "USD"),
        ];
        Assert.Collection(_pricesInternal, inspectors);
    }


    [Fact]
    public async Task DoImport_UpdateProducts_OnlyExistingProductsMerged()
    {
        // Arrange
        var existingProduct = GetCsvProductBase();

        _productsInternal = [existingProduct];
        var existingCategory = CreateCategory(existingProduct);
        _categoriesInternal.Add(existingCategory);

        var product1 = GetCsvProductBase();
        product1.Id = null;

        var product2 = GetCsvProductBase();
        product2.Id = null;

        var product3 = GetCsvProductBase();
        product3.Code = null;
        product3.Id = null;

        var product4 = GetCsvProductBase();
        product4.Code = null;
        product4.Id = null;

        var list = new List<CsvProduct> { product1, product2, product3, product4 };

        var target = GetImporter();

        // Act
        await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), _ => { });

        // Assert
        Action<CatalogProduct>[] inspectors =
        [
            x => Assert.True(x.Code == "TST1" && x.Id == "1"),
            x => Assert.NotEqual("TST1", x.Code),
            x => Assert.NotEqual("TST1", x.Code),
        ];
        Assert.Collection(_savedProducts, inspectors);
    }

    [Fact]
    public async Task DoImport_NewProductWithVariationsProductUseSku()
    {
        // Arrange
        var mainProduct = GetCsvProductBase();
        var variationProduct = GetCsvProductWithMainProduct(mainProduct.Sku);

        var target = GetImporter();

        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([mainProduct, variationProduct], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.Equal(mainProduct.Id, variationProduct.MainProductId);
    }

    [Fact]
    public async Task DoImport_NewProductWithVariationsProductUseId()
    {
        // Arrange
        var mainProduct = GetCsvProductBase();
        var variationProduct = GetCsvProductWithMainProduct(mainProduct.Id);

        var target = GetImporter();

        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([mainProduct, variationProduct], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.Equal(mainProduct.Id, variationProduct.MainProductId);
    }

    [Fact]
    public async Task DoImport_ProductNameShouldBeTrimmed()
    {
        // Arrange
        var product = GetCsvProductBase();

        const string expectedProductName = "Trimmed name";
        product.Name = "\n \r" + expectedProductName + "\t";

        var target = GetImporter();
        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.Equal(expectedProductName, product.Name);
    }

    [Fact]
    public async Task DoImport_ProductShouldBePlacedInCorrectCategory()
    {
        // Arrange
        var product = GetCsvProductBase();
        product.CategoryPath = "Category1/Category2/Category3";
        product.Category = null;

        var target = GetImporter();
        var exportInfo = new ExportImportProgressInfo();

        // Act
        await target.DoImport([product], GetCsvImportInfo(), exportInfo, _ => { });

        // Assert
        Assert.NotNull(product.Category);
        Assert.Equal("Category3", product.Category.Name);

        Assert.Equal(3, _categoriesInternal.Count);
        Assert.Equal("Category1", _categoriesInternal[0].Name);
        Assert.Equal("Category2", _categoriesInternal[1].Name);
        Assert.Equal("Category3", _categoriesInternal[2].Name);
    }


    private CsvCatalogImporter GetImporter(IPropertyDictionaryItemService propertyDictionaryItemService = null, bool? createDictionaryValues = false)
    {
        #region IStoreService

        var storeService = new Mock<IStoreService>();
        storeService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        #endregion

        #region ICatalogService

        var catalogService = new Mock<ICatalogService>();
        catalogService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(() => [_catalog]);

        #endregion

        #region ICategoryService

        var categoryService = new Mock<ICategoryService>();
        categoryService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Category>>()))
            .Returns((IList<Category> cats) =>
            {
                foreach (var category in cats.Where(x => x.Id == null))
                {
                    category.Id = Guid.NewGuid().ToString();
                    category.Catalog = _catalog;
                    _categoriesInternal.Add(category);

                }
                return Task.FromResult(cats);
            });

        categoryService
            .Setup(x => x.GetAsync(
                It.IsAny<IList<string>>(),
                It.Is<string>(c => c == nameof(CategoryResponseGroup.Full)),
                It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) =>
            {
                var result = ids.Select(id => _categoriesInternal.FirstOrDefault(x => x.Id == id));
                result = result.Where(x => x != null).Select(x => x.CloneTyped()).ToList();
                foreach (var category in result)
                {
                    category.Properties ??= new List<Property>();

                    // Emulate catalog property inheritance
                    category.Properties.AddRange(_catalog.Properties);
                }
                return result.ToArray();
            });

        #endregion

        #region ICategorySearchService

        var categorySearchService = new Mock<ICategorySearchService>();
        categorySearchService
            .Setup(x => x.SearchAsync(It.IsAny<CategorySearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((CategorySearchCriteria criteria, bool _) =>
            {
                var result = new CategorySearchResult();
                var query = _categoriesInternal.AsQueryable();

                if (!criteria.CatalogIds.IsNullOrEmpty())
                {
                    query = query.Where(x => criteria.CatalogIds.Contains(x.CatalogId));
                }

                if (!string.IsNullOrEmpty(criteria.CategoryId) && !criteria.SearchOnlyInRoot)
                {
                    query = query.Where(x => x.ParentId == criteria.CategoryId);
                }

                if (criteria.SearchOnlyInRoot)
                {
                    query = query.Where(x => x.ParentId == null);
                }

                var categories = query.ToList();
                var cloned = categories.Select(x => x.CloneTyped()).ToList();
                foreach (var category in cloned)
                {
                    // Search service doesn't return included properties
                    category.Properties = new List<Property>();
                }
                result.Results = cloned;

                return result;
            });

        #endregion

        #region IItemService

        var itemService = new Mock<IItemService>();
        itemService
            .Setup(x => x.GetAsync(
                It.IsAny<IList<string>>(),
                It.Is<string>(c => c == nameof(ItemResponseGroup.Full)),
                It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) =>
            {
                var result = _productsInternal.Where(x => ids.Contains(x.Id));
                return result.ToArray();
            });

        itemService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<CatalogProduct>>()))
            .Callback((IList<CatalogProduct> products) =>
            {
                _savedProducts = products.ToList();
            });

        #endregion

        #region ICatalogRepository

        var items = _productsInternal.Select(x => new ItemEntity { CatalogId = x.CatalogId, Id = x.Id, Code = x.Code }).ToList();
        var itemsDbSetMock = items.BuildMockDbSet();
        var catalogRepository = new Mock<ICatalogRepository>();
        catalogRepository
            .Setup(x => x.Items)
            .Returns(itemsDbSetMock.Object);

        #endregion

        #region ISkuGenerator

        var skuGeneratorService = new Mock<ISkuGenerator>();
        skuGeneratorService
            .Setup(x => x.GenerateSku(It.IsAny<CatalogProduct>()))
            .Returns((CatalogProduct _) => Guid.NewGuid().GetHashCode().ToString());

        #endregion

        #region IPriceService

        var pricingService = new Mock<IPriceService>();
        pricingService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Price>>()))
            .Callback((IList<Price> prices) =>
            {
                _pricesInternal.RemoveAll(x => prices.Any(y => y.Id == x.Id));
                foreach (var price in prices)
                {
                    price.Id ??= Guid.NewGuid().ToString();
                }

                _pricesInternal.AddRange(prices);
            });
        pricingService
            .Setup(x => x.GetAsync(
                It.IsAny<IList<string>>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IList<string> ids, string _, bool _) =>
            {
                var result = _pricesInternal.Where(x => ids.Contains(x.Id)).ToArray();
                return result;
            });

        #endregion

        #region IInventoryService

        var inventoryService = new Mock<IInventoryService>();

        inventoryService
            .Setup(x => x.SaveChangesAsync(It.IsAny<IList<InventoryInfo>>()))
            .Callback((IEnumerable<InventoryInfo> _) => { });

        #endregion

        #region IInventorySearchService

        var inventorySearchService = new Mock<IInventorySearchService>();

        inventorySearchService
            .Setup(x => x.SearchAsync(It.IsAny<InventorySearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((InventorySearchCriteria _, bool _) => new InventoryInfoSearchResult());

        #endregion

        #region IPropertyDictionaryItemService

        propertyDictionaryItemService ??= new Mock<IPropertyDictionaryItemService>().Object;

        #endregion

        #region IPriceSearchService

        var pricingSearchService = new Mock<IPriceSearchService>();
        pricingSearchService
            .Setup(x => x.SearchAsync(It.IsAny<PricesSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((PricesSearchCriteria criteria, bool _) =>
            {
                return new PriceSearchResult
                {
                    Results = _pricesInternal.Where(x => criteria.ProductIds.Contains(x.ProductId)).Select(TestUtils.Clone).ToList(),
                };
            });

        #endregion

        #region ISettingsManager

        var settingsManager = new Mock<ISettingsManager>();

        #endregion

        #region IFulfillmentCenterSearchService

        var fulfillmentCenterSearchService = new Mock<IFulfillmentCenterSearchService>();
        fulfillmentCenterSearchService
            .Setup(x => x.SearchAsync(It.IsAny<FulfillmentCenterSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(new FulfillmentCenterSearchResult());

        #endregion IFulfillmentCenterSearchService

        var csvProductConverter = new CsvProductConverter(_mapper);

        return new CsvCatalogImporter(
            new CsvProductReader(),
            catalogService.Object,
            categoryService.Object,
            itemService.Object,
            skuGeneratorService.Object,
            pricingService.Object,
            inventoryService.Object,
            inventorySearchService.Object,
            fulfillmentCenterSearchService.Object,
            () => catalogRepository.Object,
            pricingSearchService.Object,
            settingsManager.Object,
            GetPropertyDictionaryItemService(),
            propertyDictionaryItemService,
            storeService.Object,
            categorySearchService.Object,
            csvProductConverter
        )
        {
            CreatePropertyDictionaryValues = createDictionaryValues ?? false,
        };
    }

    private static List<Property> CreateProductPropertiesInCategory(Category category, Catalog catalog)
    {
        var multivalueDictionaryProperty = new Property
        {
            Name = $"{category.Name}_ProductProperty_MultivalueDictionary",
            Id = $"{category.Name}_ProductProperty_MultivalueDictionary",
            //Catalog = catalog,
            CatalogId = catalog.Id,
            //Category = category,
            CategoryId = category.Id,
            Dictionary = true,
            Multivalue = true,
            Type = PropertyType.Product,
            IsInherited = false,
            ValueType = PropertyValueType.ShortText,
        };

        var multivalueProperty = new Property
        {
            Name = $"{category.Name}_ProductProperty_Multivalue",
            Id = $"{category.Name}_ProductProperty_Multivalue",
            //Catalog = catalog,
            CatalogId = catalog.Id,
            //Category = category,
            CategoryId = category.Id,
            Dictionary = false,
            Multivalue = true,
            Type = PropertyType.Product,
            ValueType = PropertyValueType.ShortText,
        };

        var dictionaryProperty = new Property
        {
            Name = $"{category.Name}_ProductProperty_Dictionary",
            Id = $"{category.Name}_ProductProperty_Dictionary",
            //Catalog = catalog,
            CatalogId = catalog.Id,
            //Category = category,
            CategoryId = category.Id,
            Dictionary = true,
            Multivalue = false,
            Type = PropertyType.Product,
            ValueType = PropertyValueType.ShortText,
        };

        var property = new Property
        {
            Name = $"{category.Name}_ProductProperty",
            Id = $"{category.Name}_ProductProperty",
            //Catalog = catalog,
            CatalogId = catalog.Id,
            //Category = category,
            CategoryId = category.Id,
            Dictionary = false,
            Multivalue = false,
            Type = PropertyType.Product,
            ValueType = PropertyValueType.ShortText,
        };

        return [multivalueDictionaryProperty, multivalueProperty, dictionaryProperty, property];
    }

    private static Catalog CreateCatalog()
    {
        var catalogId = Guid.NewGuid().ToString();

        return new Catalog
        {
            Id = catalogId,
            Name = "EmptyCatalogTest",
            Properties = new List<Property>
            {
                new()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Id = "CatalogProductProperty_1_MultivalueDictionary",
                    CatalogId = catalogId,
                    Dictionary = true,
                    Multivalue = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Id = "CatalogProductProperty_2_MultivalueDictionary",
                    CatalogId = catalogId,
                    Dictionary = true,
                    Multivalue = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Id =   "CatalogProductProperty_3_ColorMultivalueDictionary",
                    Name = "Catalog Product Property 3 Color Multivalue Dictionary",
                    CatalogId = catalogId,
                    Dictionary = true,
                    Multivalue = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.Color,
                },
                new()
                {
                    Name = "CatalogProductProperty_1_Multivalue",
                    Id = "CatalogProductProperty_1_Multivalue",
                    CatalogId = catalogId,
                    Dictionary = false,
                    Multivalue = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_2_Multivalue",
                    Id = "CatalogProductProperty_2_Multivalue",
                    CatalogId = catalogId,
                    Dictionary = false,
                    Multivalue = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_1_Dictionary",
                    Id = "CatalogProductProperty_1_Dictionary",
                    CatalogId = catalogId,
                    Dictionary = true,
                    Multivalue = false,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_2_Dictionary",
                    Id = "CatalogProductProperty_2_Dictionary",
                    CatalogId = catalogId,
                    Dictionary = true,
                    Multivalue = false,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_1",
                    Id = "CatalogProductProperty_1",
                    CatalogId = catalogId,
                    Dictionary = false,
                    Multivalue = false,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_2",
                    Id = "CatalogProductProperty_2",
                    CatalogId = catalogId,
                    Dictionary = false,
                    Multivalue = false,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
                new()
                {
                    Name = "CatalogProductProperty_Multilanguage",
                    Id = "CatalogProductProperty_Multilanguage",
                    CatalogId = catalogId,
                    Multilanguage = true,
                    Type = PropertyType.Product,
                    ValueType = PropertyValueType.ShortText,
                },
            },
        };
    }

    private static IPropertyDictionaryItemSearchService GetPropertyDictionaryItemService()
    {
        var propDictItemSearchServiceMock = new Mock<IPropertyDictionaryItemSearchService>();

        var registeredPropDictionaryItems = new List<PropertyDictionaryItem>
        {
            new() { Id = "CatalogProductProperty_1_MultivalueDictionary_1", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "1" },
            new() { Id = "CatalogProductProperty_1_MultivalueDictionary_2", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "2" },
            new() { Id = "CatalogProductProperty_1_MultivalueDictionary_3", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "3" },

            new() { Id = "CatalogProductProperty_2_MultivalueDictionary_1", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "1" },
            new() { Id = "CatalogProductProperty_2_MultivalueDictionary_2", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "2" },
            new() { Id = "CatalogProductProperty_2_MultivalueDictionary_3", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "3" },

            new() { Id = "CatalogProductProperty_3_ColorMultivalueDictionary_1", PropertyId = "CatalogProductProperty_3_ColorMultivalueDictionary", Alias = "Red", ColorCode = "#ff0000" },
            new() { Id = "CatalogProductProperty_3_ColorMultivalueDictionary_2", PropertyId = "CatalogProductProperty_3_ColorMultivalueDictionary", Alias = "Green", ColorCode = "#00ff00" },
            new() { Id = "CatalogProductProperty_3_ColorMultivalueDictionary_3", PropertyId = "CatalogProductProperty_3_ColorMultivalueDictionary", Alias = "Blue", ColorCode = "#0000ff" },

            new() { Id = "CatalogProductProperty_1_Dictionary_1", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "1" },
            new() { Id = "CatalogProductProperty_1_Dictionary_2", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "2" },
            new() { Id = "CatalogProductProperty_1_Dictionary_3", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "3" },

            new() { Id = "CatalogProductProperty_2_Dictionary_1", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "1" },
            new() { Id = "CatalogProductProperty_2_Dictionary_2", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "2" },
            new() { Id = "CatalogProductProperty_2_Dictionary_3", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "3" },

            new() { Id = "TestCategory_ProductProperty_MultivalueDictionary_1", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "1" },
            new() { Id = "TestCategory_ProductProperty_MultivalueDictionary_2", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "2" },
            new() { Id = "TestCategory_ProductProperty_MultivalueDictionary_3", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "3" },
        };

        propDictItemSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<PropertyDictionaryItemSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync(new PropertyDictionaryItemSearchResult { Results = registeredPropDictionaryItems.ToList() });

        return propDictItemSearchServiceMock.Object;
    }

    private static CsvProduct GetCsvProductBase()
    {
        var seoInfo = new SeoInfo { ObjectType = "CatalogProduct" };
        var review = new EditorialReview();

        return new CsvProduct
        {
            CategoryPath = "TestCategory",
            Code = "TST1",
            Currency = "USD",
            EditorialReview = review,
            Reviews = new List<EditorialReview> { review },
            Id = "1",
            ListPrice = "100",
            Inventory = new InventoryInfo(),
            SeoInfo = seoInfo,
            SeoInfos = new List<SeoInfo> { seoInfo },
            Name = "TST1-TestCategory",
            Price = new Price(),
            Quantity = "0",
            Sku = "TST1",
            TrackInventory = true,
        };
    }

    private static CsvProduct GetCsvProductWithMainProduct(string mainProductIdOrSku)
    {
        var seoInfo = new SeoInfo { ObjectType = "CatalogProduct" };
        var review = new EditorialReview();

        return new CsvProduct
        {
            CategoryPath = "TestCategory",
            Code = "TST2",
            Currency = "USD",
            EditorialReview = review,
            Reviews = new List<EditorialReview> { review },
            Id = "2",
            ListPrice = "100",
            Inventory = new InventoryInfo(),
            SeoInfo = seoInfo,
            SeoInfos = new List<SeoInfo> { seoInfo },
            Name = "TST2-TestCategory",
            Price = new Price(),
            Quantity = "0",
            Sku = "TST2",
            TrackInventory = true,
            MainProductId = mainProductIdOrSku,
        };
    }

    private Category CreateCategory(CsvProduct existingProduct)
    {
        var category = new Category
        {
            Id = Guid.NewGuid().ToString(),
            Catalog = _catalog,
            CatalogId = _catalog.Id,
            Name = existingProduct.CategoryPath,
            Properties = new List<Property>(),
        };

        category.Properties.AddRange(CreateProductPropertiesInCategory(category, _catalog));

        existingProduct.Category = category;
        existingProduct.CategoryId = category.Id;
        existingProduct.Catalog = _catalog;
        existingProduct.CatalogId = _catalog.Id;

        return category;
    }

    private CsvImportInfo GetCsvImportInfo(string delimiter = ";")
    {
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = delimiter;

        return new CsvImportInfo
        {
            CatalogId = _catalog.Id,
            Configuration = configuration,
        };
    }
}
