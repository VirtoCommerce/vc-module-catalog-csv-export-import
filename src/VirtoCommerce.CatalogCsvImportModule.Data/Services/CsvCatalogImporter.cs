using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Core.Helpers;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
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
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services
{
    public class CsvCatalogImporter : ICsvCatalogImporter
    {
        private readonly char[] _categoryDelimiters = { '/', '|', '\\', '>' };
        private readonly ICatalogService _catalogService;
        private readonly ICategoryService _categoryService;
        private readonly IItemService _productService;
        private readonly ISkuGenerator _skuGenerator;
        private readonly IPriceService _priceService;
        private readonly IPriceSearchService _priceSearchService;
        private readonly ISettingsManager _settingsManager;
        private readonly IInventoryService _inventoryService;
        private readonly IFulfillmentCenterSearchService _fulfillmentCenterSearchService;
        private readonly ICategorySearchService _categorySearchService;
        private readonly Func<ICatalogRepository> _catalogRepositoryFactory;
        private readonly IPropertyDictionaryItemSearchService _propDictItemSearchService;
        private readonly IPropertyDictionaryItemService _propDictItemService;
        private readonly IStoreSearchService _storeSearchService;
        private readonly ICsvProductConverter _csvProductConverter;
        private readonly object _lockObject = new object();

        private readonly List<Store> _stores = new List<Store>();
        private bool? _createPropertyDictionatyValues;

        public CsvCatalogImporter(
            ICatalogService catalogService,
            ICategoryService categoryService,
            IItemService productService,
            ISkuGenerator skuGenerator,
            IPriceService priceService,
            IInventoryService inventoryService,
            IFulfillmentCenterSearchService fulfillmentCenterSearchService,
            Func<ICatalogRepository> catalogRepositoryFactory,
            IPriceSearchService priceSearchService,
            ISettingsManager settingsManager,
            IPropertyDictionaryItemSearchService propDictItemSearchService,
            IPropertyDictionaryItemService propDictItemService,
            IStoreSearchService storeSearchService,
            ICategorySearchService categorySearchService,
            ICsvProductConverter csvProductConverter)
        {
            _catalogService = catalogService;
            _categoryService = categoryService;
            _productService = productService;
            _skuGenerator = skuGenerator;
            _priceService = priceService;
            _inventoryService = inventoryService;
            _fulfillmentCenterSearchService = fulfillmentCenterSearchService;
            _catalogRepositoryFactory = catalogRepositoryFactory;
            _priceSearchService = priceSearchService;
            _settingsManager = settingsManager;
            _storeSearchService = storeSearchService;
            _propDictItemSearchService = propDictItemSearchService;
            _propDictItemService = propDictItemService;
            _categorySearchService = categorySearchService;
            _csvProductConverter = csvProductConverter;
        }

        public bool CreatePropertyDictionatyValues
        {
            get
            {
                _createPropertyDictionatyValues ??= _settingsManager.GetValue<bool>(ModuleConstants.Settings.General.CreateDictionaryValues);
                return _createPropertyDictionatyValues.Value;
            }
            set
            {
                _createPropertyDictionatyValues = value;
            }
        }

        public Task DoImportAsync(Stream inputStream, CsvImportInfo importInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var csvProducts = new List<CsvProduct>();

            var progressInfo = new ExportImportProgressInfo
            {
                Description = "Reading products from csv..."
            };
            progressCallback(progressInfo);

            var encoding = DetectEncoding(inputStream);

            var readerConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = importInfo.Configuration.Delimiter,
                TrimOptions = string.IsNullOrWhiteSpace(importInfo.Configuration.Delimiter) ? TrimOptions.None : TrimOptions.Trim,
                MissingFieldFound = args => { }
            };

            using (var reader = new CsvReader(new StreamReader(inputStream, encoding), readerConfig))
            {
                reader.Context.RegisterClassMap(CsvProductMap.Create(importInfo.Configuration));
                var csvProductType = AbstractTypeFactoryHelper.GetEffectiveType<CsvProduct>();

                while (reader.Read())
                {
                    try
                    {
                        var csvProduct = (CsvProduct)reader.GetRecord(csvProductType);

                        ReplaceEmptyStringsWithNull(csvProduct);

                        csvProducts.Add(csvProduct);
                    }
                    catch (TypeConverterException ex)
                    {
                        progressInfo.Errors.Add($"Column: {ex.MemberMapData.Member.Name}, {ex.Message}");
                        progressCallback(progressInfo);
                    }
                    catch (Exception ex)
                    {
                        var error = ex.Message;
                        if (ex.InnerException?.Message != null)
                        {
                            error = $"{error} {ex.InnerException.Message}";
                        }

                        if (ex.Data.Contains("CsvHelper"))
                        {
                            error += ex.Data["CsvHelper"];
                        }
                        progressInfo.Errors.Add(error);
                        progressCallback(progressInfo);
                    }
                }
            }

            return DoImport(csvProducts, importInfo, progressInfo, progressCallback);
        }

        private void ReplaceEmptyStringsWithNull(CsvProduct csvProduct)
        {
            csvProduct.Id = string.IsNullOrEmpty(csvProduct.Id) ? null : csvProduct.Id;
            csvProduct.OuterId = string.IsNullOrEmpty(csvProduct.OuterId) ? null : csvProduct.OuterId;
            csvProduct.CategoryId = string.IsNullOrEmpty(csvProduct.CategoryId) ? null : csvProduct.CategoryId;
            csvProduct.MainProductId = string.IsNullOrEmpty(csvProduct.MainProductId) ? null : csvProduct.MainProductId;
            csvProduct.PriceId = string.IsNullOrEmpty(csvProduct.PriceId) ? null : csvProduct.PriceId;
            csvProduct.PriceListId = string.IsNullOrEmpty(csvProduct.PriceListId) ? null : csvProduct.PriceListId;
            csvProduct.FulfillmentCenterId = string.IsNullOrEmpty(csvProduct.FulfillmentCenterId) ? null : csvProduct.FulfillmentCenterId;
            csvProduct.PackageType = string.IsNullOrEmpty(csvProduct.PackageType) ? null : csvProduct.PackageType;
            csvProduct.Reviews = csvProduct.Reviews.Where(x => !string.IsNullOrEmpty(x.Content) && !string.IsNullOrEmpty(x.ReviewType)).ToList();

            csvProduct.CreateImagesFromFlatData();
        }

        public static Encoding DetectEncoding(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream must support seeking.", nameof(stream));
            }

            // Save the current position of the stream to reset later
            var originalPosition = stream.Position;

            try
            {
                // Read the first few bytes to check for a BOM
                var bom = new byte[4];
                var bytesRead = stream.Read(bom, 0, bom.Length);

                // UTF-8 BOM (EF BB BF)
                if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                {
                    return Encoding.UTF8;
                }

                // UTF-16 LE BOM (FF FE)
                if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                {
                    return Encoding.Unicode;
                }

                // UTF-16 BE BOM (FE FF)
                if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode;
                }

                // UTF-32 LE BOM (FF FE 00 00)
                if (bytesRead >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                {
                    return Encoding.UTF32;
                }

                // UTF-32 BE BOM (00 00 FE FF)
                if (bytesRead >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                {
                    return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
                }

                // Default to UTF-8 if no BOM is found
                return Encoding.UTF8;
            }
            finally
            {
                // Reset the stream position to the original state
                stream.Position = originalPosition;
            }
        }

        public async Task DoImport(List<CsvProduct> csvProducts, CsvImportInfo importInfo, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var catalog = await _catalogService.GetByIdAsync(importInfo.CatalogId);

            if (catalog == null)
            {
                throw new InvalidOperationException($"Catalog with id \"{importInfo.CatalogId}\" does not exist.");
            }

            _stores.AddRange((await _storeSearchService.SearchAsync(new StoreSearchCriteria { Take = int.MaxValue })).Results);

            var contunie = ImportAllowed(csvProducts, progressInfo, progressCallback);

            if (!contunie)
            {
                return;
            }

            csvProducts = MergeCsvProducts(csvProducts, catalog);

            await MergeFromAlreadyExistProducts(csvProducts, catalog);

            await SaveCategoryTree(catalog, csvProducts, progressInfo, progressCallback);

            await LoadProductDependencies(csvProducts, catalog, importInfo);
            await ResolvePropertyDictionaryItems(csvProducts, progressInfo, progressCallback);

            //take parentless prodcuts and save them first
            progressInfo.TotalCount = csvProducts.Count;

            var mainProcuts = csvProducts.Where(x => x.MainProduct == null).ToList();
            await SaveProducts(mainProcuts, progressInfo, progressCallback);

            //prepare and save variations (needed to be able to save variation with SKU as MainProductId)
            var variations = csvProducts.Except(mainProcuts).ToList();

            foreach (var variation in variations.Where(x => x.MainProductId == null))
            {
                variation.MainProductId = variation.MainProduct.Id;
            }

            await SaveProducts(variations, progressInfo, progressCallback);
        }


        //Is it allowed to continue
        private bool ImportAllowed(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            progressInfo.Description = "Check product...";
            // Рere you can enter checks before import for example SeoAllowed(csvProducts) && SkuCkeck(csvProducts)
            return SeoAllowed(csvProducts, progressInfo, progressCallback);
        }

        private List<CsvProduct> MergeCsvProducts(List<CsvProduct> csvProducts, Catalog catalog)
        {
            var mergedCsvProducts = new List<CsvProduct>();

            var haveCodeProducts = csvProducts.Where(x => !string.IsNullOrEmpty(x.Code)).ToList();
            csvProducts = csvProducts.Except(haveCodeProducts).ToList();

            var groupedCsv = haveCodeProducts.GroupBy(x => new { x.Code });
            foreach (var group in groupedCsv)
            {
                mergedCsvProducts.Add(MergeCsvProductsGroup(group.ToList()));
            }

            var defaultLanguge = GetDefaultLanguage(catalog);
            MergeCsvProductComplexObjects(mergedCsvProducts, defaultLanguge);

            foreach (var seoInfo in csvProducts.SelectMany(x => x.SeoInfos).Where(y => y.LanguageCode.IsNullOrEmpty()))
            {
                seoInfo.LanguageCode = defaultLanguge;
            }

            foreach (var review in csvProducts.SelectMany(x => x.Reviews).Where(y => y.LanguageCode.IsNullOrEmpty()))
            {
                review.LanguageCode = defaultLanguge;
            }

            mergedCsvProducts.AddRange(csvProducts);
            return mergedCsvProducts;
        }

        private CsvProduct MergeCsvProductsGroup(List<CsvProduct> csvProducts)
        {
            var firstProduct = csvProducts.FirstOrDefault();
            if (firstProduct == null)
            {
                return null;
            }

            firstProduct.Reviews = csvProducts.SelectMany(x => x.Reviews).ToList();
            firstProduct.SeoInfos = csvProducts.SelectMany(x => x.SeoInfos).ToList();
            firstProduct.Properties = csvProducts.SelectMany(x => x.Properties).ToList();
            firstProduct.Prices = csvProducts.SelectMany(x => x.Prices).ToList();

            return firstProduct;
        }

        private void MergeCsvProductComplexObjects(List<CsvProduct> csvProducts, string defaultLanguge)
        {
            foreach (var csvProduct in csvProducts)
            {
                var reviews = csvProduct.Reviews.Where(x => !string.IsNullOrEmpty(x.Content)).GroupBy(x => x.ReviewType).Select(g => g.FirstOrDefault()).ToList();

                foreach (var review in reviews.Where(x => x.LanguageCode.IsNullOrEmpty()))
                {
                    review.LanguageCode = defaultLanguge;
                }

                csvProduct.Reviews = reviews;

                var seoInfos = csvProduct.SeoInfos.Where(x => x.SemanticUrl != null).GroupBy(x => x.SemanticUrl).Select(g => g.FirstOrDefault()).ToList();

                foreach (var seoInfo in seoInfos.Where(x => x.LanguageCode.IsNullOrEmpty()))
                {
                    seoInfo.LanguageCode = defaultLanguge;
                }

                csvProduct.SeoInfos = seoInfos;

                csvProduct.Properties = csvProduct.Properties
                    .Where(property => property.Values?.Any(propertyValue => !string.IsNullOrEmpty(propertyValue.Value?.ToString())) == true)
                    .GroupBy(x => x.Name)
                    .Select(propertyGroup => GetMergedProperty(propertyGroup))
                    .ToList();

                csvProduct.Prices = csvProduct.Prices.Where(x => x.EffectiveValue > 0).GroupBy(x => new { x.Currency, x.PricelistId, x.MinQuantity }).Select(g => g.FirstOrDefault()).ToList();
            }
        }

        private static Property GetMergedProperty(IGrouping<string, Property> propertyGroup)
        {
            var result = propertyGroup.First();

            foreach (var property in propertyGroup.Skip(1))
            {
                foreach (var propertyValue in property.Values)
                {
                    if (!result.Values.Any(x => x.Value == propertyValue.Value))
                    {
                        result.Values.Add(propertyValue);
                    }
                }
            }

            return result;
        }

        private string GetDefaultLanguage(Catalog catalog)
        {
            return catalog.DefaultLanguage != null ? catalog.DefaultLanguage.LanguageCode : "en-US";
        }

        private async Task ResolvePropertyDictionaryItems(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var allDictPropertyIds = csvProducts.SelectMany(x => x.Properties).Where(x => x.Dictionary)
                                                .Select(x => x.Id).Distinct()
                                                .ToArray();

            var allDictItems = (await _propDictItemSearchService.SearchAsync(new PropertyDictionaryItemSearchCriteria
            {
                PropertyIds = allDictPropertyIds,
                Take = int.MaxValue
            }, false)).Results;

            foreach (var dictProperty in csvProducts.SelectMany(x => x.Properties).Where(x => x.Dictionary && x.Values?.Any(v => v != null) == true))
            {
                foreach (var propertyValue in dictProperty.Values.Where(x => !string.IsNullOrWhiteSpace(x.Value?.ToString())))
                {
                    // VP-5516:
                    // For imported propertyValue the Alias field is empty - need to fill it from value.
                    // For existing propertyValue Alias should be already filled, we shouldn't rewrite it.
                    propertyValue.Alias = string.IsNullOrEmpty(propertyValue.Alias) ? propertyValue.Value.ToString() : propertyValue.Alias;

                    var existentDictItem = allDictItems.FirstOrDefault(x => x.PropertyId == propertyValue.PropertyId && x.Alias.EqualsIgnoreCase(propertyValue.Alias));

                    if (existentDictItem == null)
                    {
                        if (CreatePropertyDictionatyValues)
                        {
                            existentDictItem = new PropertyDictionaryItem
                            {
                                Alias = propertyValue.Alias,
                                PropertyId = propertyValue.PropertyId
                            };
                            allDictItems.Add(existentDictItem);
                            await _propDictItemService.SaveChangesAsync(new List<PropertyDictionaryItem> { existentDictItem });
                        }
                        else
                        {
                            progressInfo.Errors.Add($"The '{propertyValue.Alias}' dictionary item is not found in '{propertyValue.PropertyName}' dictionary");
                            progressCallback(progressInfo);
                        }
                    }
                    propertyValue.ValueId = existentDictItem?.Id;
                }
            }
        }

        /// <summary>
        /// Try to find (create if not) categories for products with Category.Path
        /// </summary>
        private async Task SaveCategoryTree(Catalog catalog, IEnumerable<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var cachedCategoryMap = new Dictionary<string, Category>();
            var outline = new StringBuilder();

            foreach (var csvProduct in csvProducts.Where(x => x.Category != null && !string.IsNullOrEmpty(x.Category.Path)))
            {
                outline.Clear();
                var productCategoryNames = csvProduct.Category.Path.Split(_categoryDelimiters);
                string parentCategoryId = null;
                var count = progressInfo.ProcessedCount;
                foreach (var categoryName in productCategoryNames)
                {
                    outline.Append($"\\{categoryName}");
                    if (!cachedCategoryMap.TryGetValue(outline.ToString(), out var category))
                    {
                        var searchCriteria = new CategorySearchCriteria
                        {
                            CatalogId = catalog.Id,
                            CategoryId = parentCategoryId,
                            SearchOnlyInRoot = parentCategoryId == null,
                            Keyword = categoryName
                        };

                        category = (await _categorySearchService.SearchAsync(searchCriteria)).Results.FirstOrDefault();
                    }

                    if (category == null)
                    {
                        category = new Category
                        {
                            Name = categoryName,
                            Code = GenerateSlug(categoryName),
                            CatalogId = catalog.Id,
                            ParentId = parentCategoryId
                        };

                        await _categoryService.SaveChangesAsync(new[] { category });

                        //Raise notification each notifyCategorySizeLimit category
                        progressInfo.Description = $"Creating categories: {++count} created";
                        progressCallback(progressInfo);
                    }

                    csvProduct.CategoryId = category.Id;
                    csvProduct.Category = category;
                    parentCategoryId = category.Id;
                    cachedCategoryMap[outline.ToString()] = category;
                }
            }
        }

        private string GenerateSlug(string categoryName)
        {
            var code = categoryName.GenerateSlug();
            if (string.IsNullOrEmpty(code))
            {
                code = Guid.NewGuid().ToString("N");
            }
            return code;
        }

        private async Task SaveProducts(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var defaultFulfilmentCenter = (await _fulfillmentCenterSearchService.SearchAsync(new FulfillmentCenterSearchCriteria { Take = 1 })).Results.FirstOrDefault();

            var totalProductsCount = csvProducts.Count;
            for (int i = 0; i < totalProductsCount; i += 10)
            {
                var products = csvProducts.Skip(i).Take(10).ToList();

                try
                {
                    var catalogProducts = products.Select(x => _csvProductConverter.GetCatalogProduct(x)).ToArray();
                    await _productService.SaveChangesAsync(catalogProducts);

                    await SaveProductInventories(products, defaultFulfilmentCenter);

                    await SaveProductPrices(products);
                }
                catch (FluentValidation.ValidationException validationEx)
                {
                    lock (_lockObject)
                    {
                        foreach (var validationErrorGroup in validationEx.Errors.GroupBy(x => x.PropertyName))
                        {
                            var errorMessage = string.Join("; ", validationErrorGroup.Select(x => x.ErrorMessage));
                            progressInfo.Errors.Add(errorMessage);
                            progressCallback(progressInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (_lockObject)
                    {
                        progressInfo.Errors.Add(ex.ToString());
                        progressCallback(progressInfo);
                    }
                }
                finally
                {
                    lock (_lockObject)
                    {
                        //Raise notification
                        progressInfo.ProcessedCount += products.Count;
                        progressInfo.Description =
                            $"Saving products: {progressInfo.ProcessedCount} of {progressInfo.TotalCount} created";
                        progressCallback(progressInfo);
                    }
                }
            }
        }

        private async Task SaveProductInventories(IList<CsvProduct> products, FulfillmentCenter defaultFulfilmentCenter)
        {
            //Set productId for dependent objects
            foreach (var product in products)
            {
                if (defaultFulfilmentCenter != null || product.Inventory.FulfillmentCenterId != null)
                {
                    product.Inventory.ProductId = product.Id;
                    product.Inventory.FulfillmentCenterId = product.Inventory.FulfillmentCenterId ?? defaultFulfilmentCenter?.Id;
                }
                else
                {
                    product.Inventory = null;
                }
            }
            var productIds = products.Select(x => x.Id).ToArray();
            var existInventories = await _inventoryService.GetProductsInventoryInfosAsync(productIds);
            var inventories = products.Where(x => x.Inventory != null).Select(x => x.Inventory).Where(x => !string.IsNullOrEmpty(x.ProductId)).ToArray();
            foreach (var inventory in inventories)
            {
                var exitsInventory = existInventories.FirstOrDefault(x => x.ProductId == inventory.ProductId && x.FulfillmentCenterId == inventory.FulfillmentCenterId);
                if (exitsInventory != null)
                {
                    inventory.ProductId = exitsInventory.ProductId;
                    inventory.FulfillmentCenterId = exitsInventory.FulfillmentCenterId;
                    inventory.AllowBackorder = exitsInventory.AllowBackorder;
                    inventory.AllowPreorder = exitsInventory.AllowPreorder;
                    inventory.BackorderAvailabilityDate = exitsInventory.BackorderAvailabilityDate;
                    inventory.BackorderQuantity = exitsInventory.BackorderQuantity;
                    inventory.InTransit = exitsInventory.InTransit;
                }
            }
            await _inventoryService.SaveChangesAsync(inventories);
        }

        private async Task SaveProductPrices(IList<CsvProduct> products)
        {
            // updating prices productid
            foreach (var product in products)
            {
                foreach (var price in product.Prices)
                {
                    price.ProductId = product.Id;
                }
            }

            var prices = products.SelectMany(x => x.Prices).OfType<CsvPrice>().ToArray();

            //min quantity 0 is not allowed
            foreach (var price in prices.Where(x => x.MinQuantity == 0))
            {
                price.MinQuantity = 1;
            }

            //try update update prices by id
            var pricesWithIds = prices.Where(x => !string.IsNullOrEmpty(x.Id)).ToArray();
            var mergedPrices = await GetMergedPriceById(pricesWithIds);

            //then update for products with PriceListId set
            var pricesWithPriceListIds = prices.Except(pricesWithIds).Where(x => !string.IsNullOrEmpty(x.PricelistId)).ToArray();
            mergedPrices.AddRange(await GetMergedPriceByPriceList(pricesWithPriceListIds));

            //We do not have information about concrete price list id or price id and therefore select first product price then
            var restPrices = prices.Except(pricesWithIds).Except(pricesWithPriceListIds).ToArray();
            mergedPrices.AddRange(await GetMergedPriceDefault(restPrices));

            await _priceService.SaveChangesAsync(mergedPrices);
        }

        private async Task<IList<Price>> GetMergedPriceById(IList<CsvPrice> pricesWithIds)
        {
            if (!pricesWithIds.Any())
            {
                return new List<Price>();
            }

            var result = new List<Price>();

            var pricesIds = pricesWithIds.Select(x => x.Id).ToArray();
            var existingPricesByIds = await _priceService.GetAsync(pricesIds);
            foreach (var price in pricesWithIds)
            {
                var existPrice = existingPricesByIds.FirstOrDefault(x => x.Id == price.Id);
                if (existPrice != null)
                {
                    price.MergeFrom(existPrice);
                }
                result.Add(price);
            }

            return result;
        }

        private async Task<IList<Price>> GetMergedPriceByPriceList(IList<CsvPrice> pricesWithPriceListIds)
        {
            if (!pricesWithPriceListIds.Any())
            {
                return new List<Price>();
            }

            var existentPrices = new List<Price>();

            var dictionary = pricesWithPriceListIds.GroupBy(x => x.PricelistId).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var priceListId in dictionary.Keys)
            {
                var criteria = new PricesSearchCriteria
                {
                    PriceListId = priceListId,
                    ProductIds = dictionary[priceListId].Select(x => x.ProductId).ToArray(),
                    Take = int.MaxValue,
                };

                var searchResult = await _priceSearchService.SearchAsync(criteria);
                existentPrices.AddRange(searchResult.Results);
            }

            var result = new List<Price>();
            foreach (var price in pricesWithPriceListIds)
            {
                var existPrice = existentPrices.FirstOrDefault(x => x.ProductId.EqualsIgnoreCase(price.ProductId) && x.PricelistId.EqualsIgnoreCase(price.PricelistId));

                if (existPrice != null)
                {
                    price.MergeFrom(existPrice);
                }

                result.Add(price);
            }

            return result;
        }

        private async Task<IList<Price>> GetMergedPriceDefault(IList<CsvPrice> restPrices)
        {
            if (!restPrices.Any())
            {
                return new List<Price>();
            }

            var criteria = new PricesSearchCriteria
            {
                ProductIds = restPrices.Select(x => x.ProductId).ToArray(),
                Take = int.MaxValue,
            };

            var result = new List<Price>();
            var existPrices = (await _priceSearchService.SearchAsync(criteria)).Results;
            foreach (var price in restPrices)
            {
                var existPrice = existPrices.FirstOrDefault(x => x.Currency.EqualsIgnoreCase(price.Currency)
                    && x.ProductId.EqualsIgnoreCase(price.ProductId));

                if (existPrice != null)
                {
                    price.MergeFrom(existPrice);
                }

                result.Add(price);
            }

            return result;
        }


        private async Task LoadProductDependencies(IEnumerable<CsvProduct> csvProducts, Catalog catalog, CsvImportInfo importInfo)
        {
            var allCategoriesIds = csvProducts.Select(x => x.CategoryId).Distinct().ToArray();
            var categoriesMap = (await _categoryService.GetAsync(allCategoriesIds, CategoryResponseGroup.Full.ToString())).ToDictionary(x => x.Id);

            foreach (var csvProduct in csvProducts)
            {
                csvProduct.Catalog = catalog;
                csvProduct.CatalogId = catalog.Id;
                if (csvProduct.CategoryId != null)
                {
                    csvProduct.Category = categoriesMap[csvProduct.CategoryId];
                }

                //Try to set parent relations
                //By id or code reference
                var parentProduct = csvProducts.FirstOrDefault(x => !string.IsNullOrEmpty(csvProduct.MainProductId) && (x.Id.EqualsIgnoreCase(csvProduct.MainProductId) || x.Code.EqualsIgnoreCase(csvProduct.MainProductId)));
                csvProduct.MainProduct = parentProduct;
                csvProduct.MainProductId = parentProduct != null ? parentProduct.Id : null;

                if (string.IsNullOrEmpty(csvProduct.Code))
                {
                    csvProduct.Code = _skuGenerator.GenerateSku(csvProduct);
                }
                //Properties inheritance
                var inheritedProperties = GetInheritedProperties(csvProduct);

                foreach (var property in csvProduct.Properties.ToArray())
                {
                    //Try to find property for product
                    var inheritedProperty = inheritedProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(property.Name));
                    if (inheritedProperty != null)
                    {
                        property.ValueType = inheritedProperty.ValueType;
                        property.Id = inheritedProperty.Id;
                        property.Dictionary = inheritedProperty.Dictionary;
                        property.Multivalue = inheritedProperty.Multivalue;

                        foreach (var propertyValue in property.Values)
                        {
                            propertyValue.ValueType = inheritedProperty.ValueType;
                            propertyValue.PropertyId = inheritedProperty.Id;
                        }

                        //Try to split the one value to multiple values for Multivalue/Multilanguage properties
                        if (inheritedProperty.Multivalue || inheritedProperty.Multilanguage)
                        {
                            var parsedValues = new List<PropertyValue>();

                            foreach (var propertyValue in property.Values)
                            {
                                parsedValues.AddRange(ParseValuesFromMultivalueString(propertyValue, importInfo.Configuration.Delimiter));
                            }

                            property.Values = parsedValues;
                        }
                        // Combining multiple values ​​into one for non-multivalued properties
                        else if (property.Values.Count > 1)
                        {
                            var propertyValue = property.Values.First();
                            propertyValue.Value = string.Join(CsvReaderExtension.Delimiter, property.Values.Select(x => x.Value));
                            property.Values = new List<PropertyValue> { propertyValue };
                        }
                    }
                }
            }
        }

        private static List<Property> GetInheritedProperties(CsvProduct csvProduct)
        {
            if (csvProduct.Category != null && csvProduct.Category.Properties != null)
            {
                return csvProduct.Category.Properties.OrderBy(x => x.Name).ToList();
            }
            return csvProduct.Catalog.Properties.OrderBy(x => x.Name).ToList();
        }

        private static List<PropertyValue> ParseValuesFromMultivalueString(PropertyValue firstPropertyValue, string additionalDelimiter)
        {
            var result = new List<PropertyValue>();
            var multivalue = firstPropertyValue.Value?.ToString();
            var chars = new[] { ",", additionalDelimiter };
            var valueArray = multivalue?.Split(chars, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(x => x.Trim())
                                   .Where(x => !string.IsNullOrEmpty(x))
                                   .Distinct()
                                   .ToArray();

            foreach (var singleValue in valueArray ?? Array.Empty<string>())
            {
                var valueClone = firstPropertyValue.Clone() as PropertyValue;
                valueClone.Value = singleValue;
                result.Add(valueClone);
            }

            return result;
        }

        //Merge importing products with already exist to prevent erasing already exist data, import should only update or create data
        private async Task MergeFromAlreadyExistProducts(IEnumerable<CsvProduct> csvProducts, Catalog catalog)
        {
            var transientProducts = csvProducts.Where(x => x.IsTransient()).ToArray();
            var nonTransientProducts = csvProducts.Where(x => !x.IsTransient()).ToArray();

            var alreadyExistProducts = new List<CatalogProduct>();
            //Load exist products
            for (int i = 0; i < nonTransientProducts.Count(); i += 50)
            {
                alreadyExistProducts.AddRange(await _productService.GetAsync(nonTransientProducts.Skip(i).Take(50).Select(x => x.Id).ToArray(), ItemResponseGroup.ItemLarge.ToString()));
            }
            //Detect already exist product by Code
            var transientProductsCodes = transientProducts.Select(x => x.Code).Where(x => x != null).Distinct().ToArray();
            using (var repository = _catalogRepositoryFactory())
            {
                var products = repository.Items.Where(x => x.CatalogId == catalog.Id && transientProductsCodes.Contains(x.Code));
                var foundProducts = products.Select(x => new { x.Id, x.Code }).ToArray();
                for (int i = 0; i < foundProducts.Count(); i += 50)
                {
                    alreadyExistProducts.AddRange(await _productService.GetAsync(foundProducts.Skip(i).Take(50).Select(x => x.Id).ToArray(), ItemResponseGroup.ItemLarge.ToString()));
                }
            }
            foreach (var csvProduct in csvProducts)
            {
                var existProduct = csvProduct.IsTransient() ? alreadyExistProducts.FirstOrDefault(x => x.Code.EqualsIgnoreCase(csvProduct.Code)) : alreadyExistProducts.FirstOrDefault(x => x.Id == csvProduct.Id);
                if (existProduct != null)
                {
                    csvProduct.MergeFrom(existProduct);
                }
            }
        }

        #region Import allowed

        private bool SeoAllowed(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            bool isCompleted = true;

            foreach (var product in csvProducts)
            {
                if (!CorrectProduct(product))
                {
                    isCompleted = false;
                }
            }

            progressCallback(progressInfo);

            return isCompleted;

            bool CorrectProduct(CsvProduct product)
            {
                //check seoinfo storeif if specified
                if (!string.IsNullOrEmpty(product.SeoStore))
                {
                    var result = _stores.Any(x => x.Id == product.SeoStore);
                    if (!result)
                    {
                        progressInfo.Errors.Add($"No store with Id = {product.SeoStore}. Line number: {product.LineNumber}");
                    }

                    return result;
                }

                return true;
            }
        }

        #endregion
    }
}
