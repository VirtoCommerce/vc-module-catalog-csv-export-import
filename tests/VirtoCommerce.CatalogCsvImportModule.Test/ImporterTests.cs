using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Model.Search;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests
{
    public class ImporterTests
    {
        private readonly IMapper _mapper;

        private readonly Catalog _catalog = CreateCatalog();

        private readonly List<Category> _categoriesInternal = new List<Category>();
        private List<CatalogProduct> _productsInternal = new List<CatalogProduct>();

        private List<Price> _pricesInternal = new List<Price>();
        private readonly List<FulfillmentCenter> _fulfillmentCentersInternal = new List<FulfillmentCenter>();
        private readonly List<InventoryInfo> _inventoryInfosInternal = new List<InventoryInfo>();

        private List<CatalogProduct> _savedProducts;

        public ImporterTests()
        {
            // To fix the error:  'Cyrillic' is not a supported encoding name. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method. (Parameter 'name')
            // https://github.com/dotnet/runtime/issues/17516
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var configuration = new MapperConfiguration(cfg => {
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

        [Fact]
        public async Task DoImport_NewProductMultivalueDictionaryProperties_PropertyValuesCreated()
        {
            //Arrange
            var product = GetCsvProductBase();
            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1, 3", ValueType = PropertyValueType.ShortText },
                    }
                },
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "2, 1", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var target = GetImporter();

            var progressInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), progressInfo, info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_3" && x.Alias == "3"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_2" && x.Alias == "2"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_1" && x.Alias == "1")
            };
            Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
            Assert.Empty(progressInfo.Errors);
        }

        [Fact]
        public async Task DoImport_NewProductDictionaryMultivaluePropertyWithNotExistingValue_ErrorIsPresent()
        {
            //Arrange
            var product = GetCsvProductBase();
            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "NotExistingValue", ValueType = PropertyValueType.ShortText },
                    }
            }};

            var target = GetImporter();

            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            Assert.NotEmpty(exportInfo.Errors);
        }

        [Fact]
        public async Task DoImport_NewProductDictionaryMultivaluePropertyWithNewValue_NewDictPropertyItemCreated()
        {
            //Arrange
            var product = GetCsvProductBase();
            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var mockPropDictItemService = new Mock<IPropertyDictionaryItemService>();
            var target = GetImporter(propDictItemService: mockPropDictItemService.Object, createDictionayValues: true);

            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            mockPropDictItemService.Verify(mock => mock.SaveChangesAsync(It.Is<List<PropertyDictionaryItem>>(dictItems => dictItems.Any(dictItem => dictItem.Alias == "NewValue"))), Times.Once());

            Assert.Empty(exportInfo.Errors);
        }

        [Fact]
        public async Task DoImport_UpdateProductDictionaryMultivalueProperties_PropertyValuesMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            existingProduct.Properties = new List<Property>
            {
                new Property()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                    }
                },
                new Property()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                    }
                },
                 new Property()
                {
                    Name = "TestCategory_ProductProperty_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "2,3", ValueType = PropertyValueType.ShortText },
                    }
                },
                new CsvProperty()
                {
                    Name = "TestCategory_ProductProperty_MultivalueDictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var target = GetImporter();
            var progressInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), progressInfo, info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_2" && x.Alias == "2"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_3" && x.Alias == "3"),
                x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_2" && x.Alias == "2"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_2" && x.Alias == "2"),
            };
            Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
            Assert.Empty(progressInfo.Errors);
        }

        [Fact]
        public async Task DoImport_UpdateProductCategory_CategoryIsNotUpdated()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.Properties = new List<Property>();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();
            product.Category = null;

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Assert.True(product.Category.Id == existingProduct.Category.Id);
        }

        [Fact]
        public async Task DoImport_UpdateProductNameIsNull_NameIsNotUpdated()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.Properties = new List<Property>();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();
            product.Name = null;

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Assert.True(product.Name == existingProduct.Name);
        }



        [Fact]
        public async Task DoImport_UpdateProductMultivalueProperties_PropertyValuesMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            existingProduct.Properties = new List<Property>
            {
                new Property()
                {
                    Name = "CatalogProductProperty_1_Multivalue",
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_Multivalue", Value = "TestValue1", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_Multivalue", Value = "TestValue2", ValueType = PropertyValueType.ShortText },
                    }
                },
                new Property()
                {
                    Name = "CatalogProductProperty_2_Multivalue",
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue3", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue4", ValueType = PropertyValueType.ShortText },
                    }
                },
                 new Property()
                {
                    Name = "TestCategory_ProductProperty_Multivalue",
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue5", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue6", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2_Multivalue",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_Multivalue", Value = "TestValue1, TestValue2", ValueType = PropertyValueType.ShortText },
                    }
                },
                new CsvProperty()
                {
                    Name = "TestCategory_ProductProperty_Multivalue",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_Multivalue", Value = "TestValue3", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var target = GetImporter();
            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Multivalue" && (string) x.Value == "TestValue1"),
                x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Multivalue" && (string) x.Value == "TestValue2"),
                x => Assert.True(x.PropertyName == "TestCategory_ProductProperty_Multivalue" && (string) x.Value == "TestValue3"),
                x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Multivalue" && (string) x.Value == "TestValue1"),
                x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Multivalue" && (string) x.Value == "TestValue2"),
            };
            Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        }


        [Fact]
        public async Task DoImport_NewProductDictionaryProperties_PropertyValuesCreated()
        {
            //Arrange
            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_Dictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_Dictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    }
                },
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2_Dictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_Dictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.PropertyName == "CatalogProductProperty_1_Dictionary" && (string) x.Value == "1"),
                x => Assert.True(x.PropertyName == "CatalogProductProperty_2_Dictionary" && (string) x.Value == "2")
            };
            Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        }

        [Fact]
        public async Task DoImport_NewProductDictionaryPropertyWithNotExistingValue_ErrorIsPresent()
        {
            //Arrange
            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_Dictionary",
                    Dictionary = true,
                    Multivalue = false,
                    Values = new []
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_Dictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var target = GetImporter();

            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            Assert.NotEmpty(exportInfo.Errors);
        }

        [Fact]
        public async Task DoImport_NewProductDictionaryPropertyWithNewValue_NewPropertyValueCreated()
        {
            //Arrange
            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1_Dictionary",
                    Dictionary = true,
                    Multivalue = false,
                    Values = new []
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_Dictionary", Value = "NewValue", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var mockPropDictItemService = new Mock<IPropertyDictionaryItemService>();
            var target = GetImporter(propDictItemService: mockPropDictItemService.Object, createDictionayValues: true);


            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            mockPropDictItemService.Verify(mock => mock.SaveChangesAsync(It.Is<List<PropertyDictionaryItem>>(dictItems => dictItems.Any(dictItem => dictItem.Alias == "NewValue"))), Times.Once());
            Assert.Empty(exportInfo.Errors);
        }

        [Fact]
        public async Task DoImport_NewProductProperties_PropertyValuesCreated()
        {
            //Arrange
            var target = GetImporter();

            var product = GetCsvProductBase();

            product.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_1",
                    Values = new []
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1", Value = "1", ValueType = PropertyValueType.ShortText },
                    },
                },
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2",
                    Values = new []
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2", Value = "2", ValueType = PropertyValueType.ShortText },
                    },
                },
            };

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.PropertyName == "CatalogProductProperty_1" && (string) x.Value == "1"),
                x => Assert.True(x.PropertyName == "CatalogProductProperty_2" && (string) x.Value == "2")
            };
            Assert.Collection(product.Properties.SelectMany(x => x.Values), inspectors);
        }


        [Fact]
        public async Task DoImport_UpdateProductSeoInfoIsEmpty_SeoInfosNotClearedUp()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.Properties = new List<Property>();
            existingProduct.SeoInfos = new List<SeoInfo>
            {
                new SeoInfo()
                {
                    Id = "SeoInfo_test",
                    Name = "SeoInfo_test"
                }
            };
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Assert.True(product.SeoInfos.Count == 1);
            Assert.True(product.SeoInfos.First().Id == existingProduct.SeoInfos.First().Id);
        }

        [Fact]
        public async Task DoImport_UpdateProductReviewIsEmpty_ReviewsNotClearedUp()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.Properties = new List<Property>();
            existingProduct.Reviews = new List<EditorialReview>
            {
                new EditorialReview()
                {
                    Id = "EditorialReview_test",
                    Content = "EditorialReview_test"
                }
            };
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var product = GetCsvProductBase();

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { product }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Assert.True(product.Reviews.Count == 1);
            Assert.True(product.Reviews.First().Id == existingProduct.Reviews.First().Id);
        }

        [Fact]
        public async Task DoImport_UpdateProductTwoProductsWithSameCode_ProductsMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.Id = null;
            secondProduct.Id = null;

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Assert.True(_savedProducts.Count == 1);
        }

        [Fact]
        public async Task DoImport_TwoProductsSameCodeDifferentReviewTypes_ReviewsMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            existingProduct.Reviews.Clear();

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.EditorialReview.ReviewType = "FullReview";
            firstProduct.EditorialReview.Content = "Review Content 1";
            secondProduct.EditorialReview.ReviewType = "QuickReview";
            secondProduct.EditorialReview.Content = "Review Content 2";

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<EditorialReview>[] inspectors = {
                x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 1"),
                x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 2")
            };
            Assert.Collection(_savedProducts.FirstOrDefault().Reviews, inspectors);
        }

        [Fact]
        public async Task DoImport_TwoProductsSameCodeSameReviewTypes_ReviewsMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            existingProduct.Reviews.Clear();

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.EditorialReview.Content = "Review Content 1";
            secondProduct.EditorialReview.Content = "Review Content 2";

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<EditorialReview>[] inspectors = {
                x => Assert.True(x.LanguageCode == "en-US" && x.Content == "Review Content 1")
            };
            Assert.Collection(_savedProducts.FirstOrDefault().Reviews, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductTwoProductsSameCodeDifferentReviewTypes_ProductsMerged_ReviewsAdded()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            existingProduct.Reviews = new List<EditorialReview>
            {
                new EditorialReview { Content = "Review Content 3", ReviewType = "QuickReview", Id = "1", LanguageCode = "en-US"}
            };


            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.EditorialReview.ReviewType = "FullReview";
            firstProduct.EditorialReview.Content = "Review Content 1";
            secondProduct.EditorialReview.ReviewType = "QuickReview";
            secondProduct.EditorialReview.Content = "Review Content 2";
            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert

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
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            existingProduct.Reviews = new List<EditorialReview>
            {
                new EditorialReview { Content = "Review Content 1", ReviewType = "FullReview" , Id = "1", LanguageCode = "en-US" },
                new EditorialReview { Content = "Review Content 2", ReviewType = "QuickReview", Id = "2", LanguageCode = "en-US" }
            };

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();

            firstProduct.EditorialReview = new EditorialReview { Content = "Review Content 1", ReviewType = "FullReview" };
            secondProduct.EditorialReview = new EditorialReview { Content = "Review Content 2", ReviewType = "QuickReview" };


            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert

            var savedReview = _savedProducts.FirstOrDefault()?.Reviews;

            _savedProducts.Should().HaveCount(1);
            savedReview.Should().HaveCount(2);
            savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 1" && x.ReviewType == "FullReview");
            savedReview.Should().Contain(x => x.LanguageCode == "en-US" && x.Content == "Review Content 2" && x.ReviewType == "QuickReview");
        }



        [Fact]
        public async Task DoImport_TwoProductsSameCodeDifferentSeoInfo_SeoInfosMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.SeoInfos.Clear();

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
            secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl2";

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<SeoInfo>[] inspectors = {
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1"),
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl2")
            };
            Assert.Collection(_savedProducts.FirstOrDefault().SeoInfos, inspectors);
        }

        [Fact]
        public async Task DoImport_TwoProductsSameCodeSameSeoInfo_SeoInfosMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();
            existingProduct.SeoInfos.Clear();

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
            secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<SeoInfo>[] inspectors = {
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1")
            };
            Assert.Collection(_savedProducts.FirstOrDefault().SeoInfos, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductTwoProductsSameCodeDifferentSeoInfo_SeoInfosMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            existingProduct.SeoInfos = new List<SeoInfo>() { new SeoInfo() { Id = "1", LanguageCode = "en-US", SemanticUrl = "SemanticsUrl3" } };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();
            firstProduct.SeoInfo.SemanticUrl = "SemanticsUrl1";
            secondProduct.SeoInfo.SemanticUrl = "SemanticsUrl2";

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<SeoInfo>[] inspectors = {
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl1"),
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl2"),
                x => Assert.True(x.LanguageCode == "en-US" && x.SemanticUrl == "SemanticsUrl3")
            };
            Assert.Collection(_savedProducts.FirstOrDefault().SeoInfos, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductsTwoProductsSamePropertyName_PropertyValuesMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            existingProduct.Properties = new List<Property>
            {
                new Property()
                {
                    Name = "CatalogProductProperty_1_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                        new PropertyValue{ PropertyName = "CatalogProductProperty_1_MultivalueDictionary", Value = "2", ValueType = PropertyValueType.ShortText },
                    }
                },
                new Property()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "1", ValueType = PropertyValueType.ShortText },
                    }
                },
                new Property()
                {
                    Name = "TestCategory_ProductProperty_MultivalueDictionary",
                    Dictionary = true,
                    Multivalue = true,
                    Values = new List<PropertyValue>(),
                },
            };

            _productsInternal = new List<CatalogProduct> { existingProduct };

            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

            var firstProduct = GetCsvProductBase();
            var secondProduct = GetCsvProductBase();

            firstProduct.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "CatalogProductProperty_2_MultivalueDictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "CatalogProductProperty_2_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                    }
                },
                new CsvProperty()
                {
                    Name = "TestCategory_ProductProperty_MultivalueDictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "1,2", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            secondProduct.Properties = new List<Property>
            {
                new CsvProperty()
                {
                    Name = "TestCategory_ProductProperty_MultivalueDictionary",
                    Values = new List<PropertyValue>
                    {
                        new PropertyValue{ PropertyName = "TestCategory_ProductProperty_MultivalueDictionary", Value = "3", ValueType = PropertyValueType.ShortText },
                    }
                },
            };

            var list = new List<CsvProduct> { firstProduct, secondProduct };

            var progressInfo = new ExportImportProgressInfo();

            var target = GetImporter();

            //Act
            await target.DoImport(list, GetCsvImportInfo(), progressInfo, info => { });

            //Assert
            Action<PropertyValue>[] inspectors = {
                x => Assert.True(x.ValueId == "CatalogProductProperty_2_MultivalueDictionary_3" && x.Alias == "3"),
                x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_1" && x.Alias == "1"),
                x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_2" && x.Alias == "2"),
                x => Assert.True(x.ValueId == "TestCategory_ProductProperty_MultivalueDictionary_3" && x.Alias == "3"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_1" && x.Alias == "1"),
                x => Assert.True(x.ValueId == "CatalogProductProperty_1_MultivalueDictionary_2" && x.Alias == "2"),
            };
            Assert.Collection(_savedProducts.FirstOrDefault().Properties.SelectMany(x => x.Values), inspectors);
            Assert.Empty(progressInfo.Errors);
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceCurrency_PriceUpdated()
        {
            //Arrange
            var listPrice = 555.5m;
            var existingPriceId = "ExistingPrice_ID";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> { new CsvPrice()
            {
                List = listPrice,
                Sale = listPrice,
                Currency = "EUR",
                MinQuantity = 1
            }};

            _pricesInternal = new List<Price>()
            {
                new Price
                {
                    Currency = "EUR",
                    PricelistId = "DefaultEUR",
                    List = 333.3m,
                    Id = existingPriceId,
                    ProductId = firstProduct.Id,
                    MinQuantity = 2
                }
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<Price>[] inspectors =
            {
                x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.MinQuantity == 1)
            };
            Assert.Collection(_pricesInternal, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceId_PriceUpdated()
        {
            //Arrange
            var listPrice = 555.5m;
            var existingPriceId = "ExistingPrice_ID";
            var existingPriceId2 = "ExistingPrice_ID_2";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> {new CsvPrice()
            {
                List = listPrice,
                Sale = listPrice,
                Currency = "EUR",
                Id = existingPriceId
            }};

            _pricesInternal = new List<Price>()
            {
                new Price
                {
                    Currency = "EUR",
                    PricelistId = "DefaultEUR",
                    List = 333.3m,
                    Id = existingPriceId,
                    ProductId = firstProduct.Id
                },
                new Price
                {
                    Currency = "EUR",
                    PricelistId = "DefaultEUR",
                    List = 333.3m,
                    Id = existingPriceId2,
                    ProductId = firstProduct.Id
                }
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<Price>[] inspectors =
            {
                x => Assert.True(x.List ==  333.3m && x.Id == existingPriceId2 && x.ProductId == firstProduct.Id),
                x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id)
            };
            Assert.Collection(_pricesInternal, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceListId_PriceUpdated()
        {
            //Arrange
            var listPrice = 555.5m;
            var existingPriceId = "ExistingPrice_ID";
            var existingPriceId2 = "ExistingPrice_ID_2";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> {new CsvPrice()
            {
                List = listPrice,
                Sale = listPrice,
                Currency = "EUR",
                PricelistId = "DefaultEUR",
            }};

            _pricesInternal = new List<Price>()
            {
                new Price
                {
                    Currency = "EUR",
                    PricelistId = "DefaultEUR",
                    List = 333.3m,
                    Id = existingPriceId,
                    ProductId = firstProduct.Id
                },
                new Price
                {
                    Currency = "USD",
                    PricelistId = "DefaultUSD",
                    List = 333.3m,
                    Id = existingPriceId2,
                    ProductId = firstProduct.Id
                }
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<Price>[] inspectors =
            {
                x => Assert.True(x.List ==  333.3m && x.PricelistId == "DefaultUSD" && x.Id == existingPriceId2 && x.ProductId == firstProduct.Id),
                x => Assert.True(x.List == listPrice && x.Id == existingPriceId && x.PricelistId == "DefaultEUR" && x.ProductId == firstProduct.Id)
            };
            Assert.Collection(_pricesInternal, inspectors);
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceListIdWithoutCurrency_PriceUpdated()
        {
            //Arrange
            var newPrice = 555.5m;
            var oldPrice = 333.3m;
            var existingPriceId = "ExistingPrice_ID";
            var existingPriceId2 = "ExistingPrice_ID_2";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> {
                new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "DefaultEUR" },
                new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "DefaultUSD" }
            };

            _pricesInternal = new List<Price>
            {
                new Price { Currency = "EUR", PricelistId = "DefaultEUR", List = oldPrice, Id = existingPriceId, ProductId = firstProduct.Id },
                new Price { Currency = "USD", PricelistId = "DefaultUSD", List = oldPrice, Id = existingPriceId2, ProductId = firstProduct.Id }
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            _pricesInternal.Should().HaveCount(2);
            _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "DefaultEUR");
            _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "DefaultUSD");
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceListId_PriceAdded()
        {
            //Arrange
            var newPrice = 555.5m;
            var oldPrice = 333.3m;
            var existingPriceId = "ExistingPrice_ID";
            var existingPriceId2 = "ExistingPrice_ID_2";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> {
                new CsvPrice { List = newPrice, Sale = newPrice, PricelistId = "NewDefaultEUR" },
            };

            _pricesInternal = new List<Price>
            {
                new Price { Currency = "EUR", PricelistId = "DefaultEUR", List = oldPrice, Id = existingPriceId, ProductId = firstProduct.Id },
                new Price { Currency = "USD", PricelistId = "DefaultUSD", List = oldPrice, Id = existingPriceId2, ProductId = firstProduct.Id }
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            _pricesInternal.Should().HaveCount(3);
            _pricesInternal.Should().Contain(x => x.List == newPrice && x.PricelistId == "NewDefaultEUR");
            _pricesInternal.Should().Contain(x => x.List == oldPrice && x.PricelistId == "DefaultEUR");
            _pricesInternal.Should().Contain(x => x.List == oldPrice && x.PricelistId == "DefaultUSD");
        }

        [Fact]
        public async Task DoImport_UpdateProductHasPriceListId_PricesWithDifferentQuanitiesAdded()
        {
            //Arrange
            var newPrice1 = 555.5m;
            var newPrice2 = 333.3m;
            var existingPricelistId = "ExistingPricelist_ID";
            var minQuantity1 = 1;
            var minQuantity2 = 2;

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> {
                new CsvPrice { List = newPrice1, Sale = newPrice1, PricelistId = existingPricelistId, MinQuantity = minQuantity1 },
                new CsvPrice { List = newPrice2, Sale = newPrice2, PricelistId = existingPricelistId, MinQuantity = minQuantity2 },
            };

            _pricesInternal = new List<Price>
            {
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            _pricesInternal.Should().HaveCount(2);
            _pricesInternal.Should().Contain(x => x.List == newPrice1 && x.PricelistId == existingPricelistId && x.MinQuantity == minQuantity1);
            _pricesInternal.Should().Contain(x => x.List == newPrice2 && x.PricelistId == existingPricelistId && x.MinQuantity == minQuantity2);
        }


        [Fact]
        public async Task DoImport_UpdateProductsTwoProductDifferentPriceCurrency_PricesMerged()
        {
            //Arrange
            var listPrice = 555.5m;
            var salePrice = 666.6m;
            var existingPriceId = "ExistingPrice_ID";

            var existingProduct = GetCsvProductBase();
            _productsInternal = new List<CatalogProduct> { existingProduct };

            var firstProduct = GetCsvProductBase();
            firstProduct.Prices = new List<Price> { new CsvPrice { List = listPrice, Sale = salePrice, Currency = "EUR" } };

            var secondProduct = GetCsvProductBase();
            secondProduct.Prices = new List<Price> { new CsvPrice { List = listPrice, Sale = salePrice, Currency = "USD" } };

            _pricesInternal = new List<Price>
            {
                new Price {Currency = "EUR",PricelistId = "DefaultEUR",List = 333.3m,Sale = 444.4m,Id = existingPriceId,ProductId = firstProduct.Id},
                new Price {Currency = "USD",PricelistId = "DefaultUSD",List = 444.4m,Sale = 555.5m,Id = existingPriceId,ProductId = firstProduct.Id}
            };

            var target = GetImporter();

            //Act
            await target.DoImport(new List<CsvProduct> { firstProduct, secondProduct }, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<Price>[] inspectors =
            {
                x => Assert.True(x.List == listPrice && x.Sale == salePrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.Currency == "EUR"),
                x => Assert.True(x.List == listPrice && x.Sale == salePrice && x.Id == existingPriceId && x.ProductId == firstProduct.Id && x.Currency == "USD")
            };
            Assert.Collection(_pricesInternal, inspectors);
        }


        [Fact]
        public async Task DoImport_UpdateProducts_OnlyExistringProductsMerged()
        {
            //Arrange
            var existingProduct = GetCsvProductBase();

            _productsInternal = new List<CatalogProduct> { existingProduct };
            var existringCategory = CreateCategory(existingProduct);
            _categoriesInternal.Add(existringCategory);

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

            //Act
            await target.DoImport(list, GetCsvImportInfo(), new ExportImportProgressInfo(), info => { });

            //Assert
            Action<CatalogProduct>[] inspectors = {
                x => Assert.True(x.Code == "TST1" && x.Id == "1"),
                x => Assert.True(x.Code != "TST1"),
                x => Assert.True(x.Code != "TST1")
            };
            Assert.Collection(_savedProducts, inspectors);
        }

        [Fact]
        public async Task DoImport_NewProductWithVariationsProductUseSku()
        {
            //Arrange
            var mainProduct = GetCsvProductBase();
            var variationProduct = GetCsvProductWithMainProduct(mainProduct.Sku);

            var target = GetImporter();

            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { mainProduct, variationProduct }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            Assert.True(variationProduct.MainProductId == mainProduct.Id);
        }

        [Fact]
        public async Task DoImport_NewProductWithVariationsProductUseId()
        {
            //Arrange
            var mainProduct = GetCsvProductBase();
            var variationProduct = GetCsvProductWithMainProduct(mainProduct.Id);

            var target = GetImporter();

            var exportInfo = new ExportImportProgressInfo();

            //Act
            await target.DoImport(new List<CsvProduct> { mainProduct, variationProduct }, GetCsvImportInfo(), exportInfo, info => { });

            //Assert
            Assert.True(variationProduct.MainProductId == mainProduct.Id);
        }


        private CsvCatalogImporter GetImporter(IPropertyDictionaryItemService propDictItemService = null, IPropertyDictionaryItemSearchService propDictItemSearchService = null, bool? createDictionayValues = false)
        {

            #region StoreServise

            var storeSearchService = new Mock<IStoreSearchService>();
            storeSearchService
                .Setup(x => x.SearchAsync(It.IsAny<StoreSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(new StoreSearchResult());

            #endregion

            #region CatalogService

            var catalogService = new Mock<ICatalogService>();
            catalogService
                .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(() => new[] { _catalog });

            #endregion

            #region CategoryService

            var categoryService = new Mock<ICategoryService>();
            categoryService.Setup(x => x.SaveChangesAsync(It.IsAny<Category[]>()))
                .Returns((Category[] cats) =>
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
                    It.Is<string>(c => c == CategoryResponseGroup.Full.ToString()),
                    It.IsAny<bool>()))
                .ReturnsAsync((IList<string> ids, string group, bool clone) =>
                {
                    var result = ids.Select(id => _categoriesInternal.FirstOrDefault(x => x.Id == id));
                    result = result.Where(x => x != null).Select(x => x.Clone() as Category).ToList();
                    foreach (var category in result)
                    {
                        if (category.Properties == null)
                            category.Properties = new List<Property>();

                        //emulate catalog property inheritance
                        category.Properties.AddRange(_catalog.Properties);
                    }
                    return result.ToArray();
                });

            #endregion

            #region ICategorySearchService

            var categorySearchService = new Mock<ICategorySearchService>();
            categorySearchService
                .Setup(x => x.SearchAsync(It.IsAny<CategorySearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync((CategorySearchCriteria criteria, bool clone) =>
            {
                var result = new CategorySearchResult();
                var categories = _categoriesInternal.Where(x => criteria.CatalogIds.Contains(x.CatalogId) || criteria.ObjectIds.Contains(x.Id)).ToList();
                var cloned = categories.Select(x => x.Clone() as Category).ToList();
                foreach (var category in cloned)
                {
                    //search service doesn't return included properties
                    category.Properties = new List<Property>();
                }
                result.Results = cloned;

                return result;
            });

            #endregion ICategorySearchService

            #region ItemService

            var itemService = new Mock<IItemService>();
            itemService
                .Setup(x => x.GetAsync(
                    It.IsAny<IList<string>>(),
                    It.Is<string>(c => c == ItemResponseGroup.ItemLarge.ToString()),
                    It.IsAny<bool>()))
                .ReturnsAsync((IList<string> ids, string group, bool clone) =>
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

            #region repository mock

            var items = _productsInternal.Select(x => new ItemEntity { CatalogId = x.CatalogId, Id = x.Id, Code = x.Code }).ToList();
            var itemsQuerableMock = TestUtils.CreateQuerableMock(items);
            var catalogRepository = new Mock<ICatalogRepository>();
            catalogRepository.Setup(x => x.Items).Returns(itemsQuerableMock.Object);
            Func<ICatalogRepository> repositoryFactory = () => catalogRepository.Object;

            #endregion

            #region SkuGeneratorService

            var skuGeneratorService = new Mock<ISkuGenerator>();
            skuGeneratorService.Setup(x => x.GenerateSku(It.IsAny<CatalogProduct>())).Returns((CatalogProduct product) => Guid.NewGuid().GetHashCode().ToString());

            #endregion

            #region PricingService

            var pricingService = new Mock<IPriceService>();
            pricingService
                .Setup(x => x.SaveChangesAsync(It.IsAny<IList<Price>>()))
                .Callback((IList<Price> prices) =>
                {
                    _pricesInternal.RemoveAll(x => prices.Any(y => y.Id == x.Id));
                    foreach (var price in prices)
                    {
                        if (price.Id == null)
                            price.Id = Guid.NewGuid().ToString();
                    }

                    _pricesInternal.AddRange(prices);
                });
            pricingService
                .Setup(x => x.GetAsync(
                    It.IsAny<IList<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((IList<string> ids, string group, bool clone) =>
                {
                    var result = _pricesInternal.Where(x => ids.Contains(x.Id)).ToArray();
                    return result;
                });

            #endregion

            #region InventoryService

            var inventoryService = new Mock<IInventoryService>();
            inventoryService.Setup(x => x.GetProductsInventoryInfosAsync(It.IsAny<IEnumerable<string>>(), null)).ReturnsAsync(
                (IEnumerable<string> ids, string responseGroup) =>
                {
                    var result = _inventoryInfosInternal.Where(x => ids.Contains(x.ProductId));
                    return result.ToList();
                });

            inventoryService.Setup(x => x.SaveChangesAsync(It.IsAny<IEnumerable<InventoryInfo>>())).Callback((IEnumerable<InventoryInfo> inventory) => { });

            #endregion

            #region CommerceService

            var commerceService = new Mock<IFulfillmentCenterSearchService>();
            commerceService
                .Setup(x => x.SearchAsync(It.IsAny<FulfillmentCenterSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(() => new FulfillmentCenterSearchResult { Results = _fulfillmentCentersInternal });

            #endregion

            #region PropertyDictionaryItemService
            if (propDictItemSearchService == null)
            {
                var propDictItemSearchServiceMock = new Mock<IPropertyDictionaryItemSearchService>();
                var registeredPropDictionaryItems = new[]
                {
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_MultivalueDictionary_1", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "1" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_MultivalueDictionary_2", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "2" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_MultivalueDictionary_3", PropertyId = "CatalogProductProperty_1_MultivalueDictionary", Alias = "3" },

                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_MultivalueDictionary_1", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "1" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_MultivalueDictionary_2", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "2" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_MultivalueDictionary_3", PropertyId = "CatalogProductProperty_2_MultivalueDictionary", Alias = "3" },

                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_Dictionary_1", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "1" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_Dictionary_2", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "2" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_1_Dictionary_3", PropertyId = "CatalogProductProperty_1_Dictionary", Alias = "3" },

                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_Dictionary_1", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "1" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_Dictionary_2", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "2" },
                    new PropertyDictionaryItem { Id = "CatalogProductProperty_2_Dictionary_3", PropertyId = "CatalogProductProperty_2_Dictionary", Alias = "3" },

                    new PropertyDictionaryItem { Id = "TestCategory_ProductProperty_MultivalueDictionary_1", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "1" },
                    new PropertyDictionaryItem { Id = "TestCategory_ProductProperty_MultivalueDictionary_2", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "2" },
                    new PropertyDictionaryItem { Id = "TestCategory_ProductProperty_MultivalueDictionary_3", PropertyId = "TestCategory_ProductProperty_MultivalueDictionary", Alias = "3" },
                };

                propDictItemSearchServiceMock.Setup(x => x.SearchAsync(It.IsAny<PropertyDictionaryItemSearchCriteria>(), false)).ReturnsAsync(new PropertyDictionaryItemSearchResult { Results = registeredPropDictionaryItems.ToList() });
                propDictItemSearchService = propDictItemSearchServiceMock.Object;
            }
            if (propDictItemService == null)
            {
                propDictItemService = new Mock<IPropertyDictionaryItemService>().Object;
            }
            #endregion

            #region PricingSearchService

            var pricingSearchService = new Mock<IPriceSearchService>();
            pricingSearchService
                .Setup(x => x.SearchAsync(It.IsAny<PricesSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync((PricesSearchCriteria crietera, bool clone) =>
                {
                    return new PriceSearchResult
                    {
                        Results = _pricesInternal.Where(x => crietera.ProductIds.Contains(x.ProductId)).Select(TestUtils.Clone).ToList()
                    };
                });

            #endregion

            #region settingsManager

            var settingsManager = new Mock<ISettingsManager>();

            #endregion

            #region IFulfillmentCenterSearchService

            var fulfillmentCenterSearchService = new Mock<IFulfillmentCenterSearchService>();
            fulfillmentCenterSearchService
                .Setup(x => x.SearchAsync(It.IsAny<FulfillmentCenterSearchCriteria>(), It.IsAny<bool>()))
                .ReturnsAsync(new FulfillmentCenterSearchResult());

            #endregion IFulfillmentCenterSearchService

            var csvProductConverter = new CsvProductConverter(_mapper);

            var target = new CsvCatalogImporter(catalogService.Object,
                categoryService.Object,
                itemService.Object,
                skuGeneratorService.Object,
                pricingService.Object,
                inventoryService.Object,
                fulfillmentCenterSearchService.Object,
                repositoryFactory,
                pricingSearchService.Object,
                settingsManager.Object,
                propDictItemSearchService,
                propDictItemService,
                storeSearchService.Object,
                categorySearchService.Object,
                csvProductConverter,
                null
            );

            target.CreatePropertyDictionatyValues = createDictionayValues ?? false;

            return target;
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
                ValueType = PropertyValueType.ShortText
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
                ValueType = PropertyValueType.ShortText
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
                ValueType = PropertyValueType.ShortText
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
                ValueType = PropertyValueType.ShortText
            };

            return new List<Property> { multivalueDictionaryProperty, multivalueProperty, dictionaryProperty, property };
        }

        private static Catalog CreateCatalog()
        {
            var catalog = new Catalog { Name = "EmptyCatalogTest", Id = Guid.NewGuid().ToString(), Properties = new List<Property>() };

            var catalogProductProperty = new Property
            {
                Name = "CatalogProductProperty_1_MultivalueDictionary",
                Id = "CatalogProductProperty_1_MultivalueDictionary",
                CatalogId = catalog.Id,
                Dictionary = true,
                Multivalue = true,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty2 = new Property
            {
                Name = "CatalogProductProperty_2_MultivalueDictionary",
                Id = "CatalogProductProperty_2_MultivalueDictionary",
                CatalogId = catalog.Id,
                Dictionary = true,
                Multivalue = true,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty3 = new Property
            {
                Name = "CatalogProductProperty_1_Multivalue",
                Id = "CatalogProductProperty_1_Multivalue",
                CatalogId = catalog.Id,
                Dictionary = false,
                Multivalue = true,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty4 = new Property
            {
                Name = "CatalogProductProperty_2_Multivalue",
                Id = "CatalogProductProperty_2_Multivalue",
                CatalogId = catalog.Id,
                Dictionary = false,
                Multivalue = true,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty5 = new Property
            {
                Name = "CatalogProductProperty_1_Dictionary",
                Id = "CatalogProductProperty_1_Dictionary",
                CatalogId = catalog.Id,
                Dictionary = true,
                Multivalue = false,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty6 = new Property
            {
                Name = "CatalogProductProperty_2_Dictionary",
                Id = "CatalogProductProperty_2_Dictionary",
                CatalogId = catalog.Id,
                Dictionary = true,
                Multivalue = false,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty7 = new Property
            {
                Name = "CatalogProductProperty_1",
                Id = "CatalogProductProperty_1",
                CatalogId = catalog.Id,
                Dictionary = false,
                Multivalue = false,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            var catalogProductProperty8 = new Property
            {
                Name = "CatalogProductProperty_2",
                Id = "CatalogProductProperty_2",
                CatalogId = catalog.Id,
                Dictionary = false,
                Multivalue = false,
                Type = PropertyType.Product,
                ValueType = PropertyValueType.ShortText
            };

            catalog.Properties.Add(catalogProductProperty);
            catalog.Properties.Add(catalogProductProperty2);

            catalog.Properties.Add(catalogProductProperty3);
            catalog.Properties.Add(catalogProductProperty4);

            catalog.Properties.Add(catalogProductProperty5);
            catalog.Properties.Add(catalogProductProperty6);

            catalog.Properties.Add(catalogProductProperty7);
            catalog.Properties.Add(catalogProductProperty8);

            return catalog;
        }

        private static CsvProduct GetCsvProductBase()
        {
            var seoInfo = new SeoInfo { ObjectType = "CatalogProduct" };
            var review = new EditorialReview();
            return new CsvProduct
            {
                Category = new Category
                {
                    Parents = new Category[] { },
                    Path = "TestCategory",
                },
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
                Category = new Category
                {
                    Parents = new Category[] { },
                    Path = "TestCategory"
                },
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
                MainProductId = mainProductIdOrSku
            };
        }

        private Category CreateCategory(CsvProduct existingProduct)
        {
            var category = new Category
            {
                Id = Guid.NewGuid().ToString(),
                Catalog = _catalog,
                CatalogId = _catalog.Id,
                Name = existingProduct.Category.Path,
                Path = existingProduct.CategoryPath,
                Properties = new List<Property>()
            };
            category.Properties.AddRange(CreateProductPropertiesInCategory(category, _catalog));

            existingProduct.Category = category;
            existingProduct.CategoryId = category.Id;
            existingProduct.Catalog = _catalog;
            existingProduct.CatalogId = _catalog.Id;

            return category;
        }

        private CsvImportInfo GetCsvImportInfo()
        {
            var configuration = new CsvProductMappingConfiguration();

            return new CsvImportInfo
            {
                CatalogId = _catalog.Id,
                Configuration = configuration.GetDefaultConfiguration(),
            };
        }
    }
}
