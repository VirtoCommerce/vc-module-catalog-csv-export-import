using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CatalogCsvImportModule.Core;
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
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CsvCatalogImporter(
    ICsvProductReader csvProductReader,
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
    IStoreService storeService,
    ICategorySearchService categorySearchService,
    ICsvProductConverter csvProductConverter)
    : ICsvCatalogImporter
{
    private const int _searchAllBatchSize = 100;
    private const int _loadProductsBatchSize = 50;
    private const int _saveProductsBatchSize = 10;
    private readonly char[] _categoryDelimiters = ['/', '|', '\\', '>'];
    private readonly object _lockObject = new();

    private bool? _createPropertyDictionaryValues;

    public bool CreatePropertyDictionaryValues
    {
        get
        {
            _createPropertyDictionaryValues ??= settingsManager.GetValue<bool>(ModuleConstants.Settings.General.CreateDictionaryValues);
            return _createPropertyDictionaryValues.Value;
        }
        set
        {
            _createPropertyDictionaryValues = value;
        }
    }

    public async Task DoImportAsync(Stream inputStream, CsvImportInfo importInfo, Action<ExportImportProgressInfo> progressCallback)
    {
        var csvProducts = await csvProductReader.ReadProducts(inputStream, importInfo.Configuration, progressCallback);
        var progressInfo = new ExportImportProgressInfo();

        await DoImport(csvProducts, importInfo, progressInfo, progressCallback);
    }

    public async Task DoImport(List<CsvProduct> csvProducts, CsvImportInfo importInfo, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
    {
        var catalog = await catalogService.GetByIdAsync(importInfo.CatalogId);
        if (catalog == null)
        {
            throw new InvalidOperationException($"Catalog with id '{importInfo.CatalogId}' does not exist.");
        }

        foreach (var csvProduct in csvProducts.Where(csvProduct => !string.IsNullOrEmpty(csvProduct.Name)))
        {
            csvProduct.Name = csvProduct.Name.Trim();
        }

        var valid = await ValidateCsvProducts(csvProducts, progressInfo, progressCallback);
        if (!valid)
        {
            return;
        }

        csvProducts = MergeCsvProducts(csvProducts, catalog);

        await MergeFromExistingProducts(csvProducts, catalog);

        await SaveCategoryTree(catalog, csvProducts, progressInfo, progressCallback);

        await LoadProductDependencies(csvProducts, catalog, importInfo);
        await ResolvePropertyDictionaryItems(csvProducts, progressInfo, progressCallback);

        // Save main products first
        progressInfo.TotalCount = csvProducts.Count;

        var mainProducts = csvProducts.Where(x => x.MainProduct == null).ToList();
        await SaveProducts(mainProducts, progressInfo, progressCallback);

        // Save variations (needed to be able to save variation with SKU as MainProductId)
        var variations = csvProducts.Except(mainProducts).ToList();

        foreach (var variation in variations.Where(x => x.MainProductId == null))
        {
            variation.MainProductId = variation.MainProduct.Id;
        }

        await SaveProducts(variations, progressInfo, progressCallback);
    }


    private static List<CsvProduct> MergeCsvProducts(List<CsvProduct> csvProducts, Catalog catalog)
    {
        var productsWithCode = csvProducts
            .Where(x => !string.IsNullOrEmpty(x.Code))
            .ToList();

        csvProducts = csvProducts.Except(productsWithCode).ToList();

        var mergedCsvProducts = productsWithCode
            .GroupBy(x => new { x.Code })
            .Select(group => MergeCsvProductsGroup(group.ToList()))
            .ToList();

        var defaultLanguage = GetDefaultLanguage(catalog);
        MergeCsvProductComplexObjects(mergedCsvProducts, defaultLanguage);

        foreach (var seoInfo in csvProducts.SelectMany(x => x.SeoInfos).Where(y => y.LanguageCode.IsNullOrEmpty()))
        {
            seoInfo.LanguageCode = defaultLanguage;
        }

        foreach (var review in csvProducts.SelectMany(x => x.Reviews).Where(y => y.LanguageCode.IsNullOrEmpty()))
        {
            review.LanguageCode = defaultLanguage;
        }

        mergedCsvProducts.AddRange(csvProducts);

        return mergedCsvProducts;
    }

    private static CsvProduct MergeCsvProductsGroup(List<CsvProduct> csvProducts)
    {
        var firstProduct = csvProducts.First();

        firstProduct.Reviews = csvProducts.SelectMany(x => x.Reviews).ToList();
        firstProduct.SeoInfos = csvProducts.SelectMany(x => x.SeoInfos).ToList();
        firstProduct.Properties = csvProducts.SelectMany(x => x.Properties).ToList();
        firstProduct.Prices = csvProducts.SelectMany(x => x.Prices).ToList();

        return firstProduct;
    }

    private static void MergeCsvProductComplexObjects(List<CsvProduct> csvProducts, string defaultLanguage)
    {
        foreach (var csvProduct in csvProducts)
        {
            var reviews = csvProduct.Reviews.Where(x => !string.IsNullOrEmpty(x.Content)).GroupBy(x => x.ReviewType).Select(g => g.FirstOrDefault()).ToList();

            foreach (var review in reviews.Where(x => x.LanguageCode.IsNullOrEmpty()))
            {
                review.LanguageCode = defaultLanguage;
            }

            csvProduct.Reviews = reviews;

            var seoInfos = csvProduct.SeoInfos.Where(x => x.SemanticUrl != null).GroupBy(x => x.SemanticUrl).Select(g => g.FirstOrDefault()).ToList();

            foreach (var seoInfo in seoInfos.Where(x => x.LanguageCode.IsNullOrEmpty()))
            {
                seoInfo.LanguageCode = defaultLanguage;
            }

            csvProduct.SeoInfos = seoInfos;

            csvProduct.Properties = csvProduct.Properties
                .Where(property => property.Values?.Any(propertyValue => !string.IsNullOrEmpty(propertyValue.Value?.ToString())) == true)
                .GroupBy(x => x.Name)
                .Select(GetMergedProperty)
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
                if (result.Values.All(x => x.Value != propertyValue.Value))
                {
                    result.Values.Add(propertyValue);
                }
            }
        }

        return result;
    }

    private static string GetDefaultLanguage(Catalog catalog)
    {
        return catalog.DefaultLanguage != null ? catalog.DefaultLanguage.LanguageCode : "en-US";
    }

    private async Task ResolvePropertyDictionaryItems(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
    {
        var dictionaryItems = await GetExistingDictionaryItems(csvProducts);

        var dictionaryPropertyValues = csvProducts
            .SelectMany(product => product.Properties)
            .Where(property => property.Dictionary && property.Values != null)
            .SelectMany(property => property.Values)
            .Where(value => !string.IsNullOrWhiteSpace(value?.Value?.ToString()));

        foreach (var propertyValue in dictionaryPropertyValues)
        {
            // VP-5516:
            // For imported propertyValue the Alias field is empty - need to fill it from value.
            // For existing propertyValue Alias should be already filled, we shouldn't rewrite it.
            propertyValue.Alias = string.IsNullOrEmpty(propertyValue.Alias) ? propertyValue.Value.ToString() : propertyValue.Alias;

            var dictionaryItem = dictionaryItems.FirstOrDefault(x => x.PropertyId == propertyValue.PropertyId && x.Alias.EqualsIgnoreCase(propertyValue.Alias));
            if (dictionaryItem == null)
            {
                if (CreatePropertyDictionaryValues)
                {
                    dictionaryItem = AbstractTypeFactory<PropertyDictionaryItem>.TryCreateInstance();
                    dictionaryItem.Alias = propertyValue.Alias;
                    dictionaryItem.PropertyId = propertyValue.PropertyId;

                    await propDictItemService.SaveChangesAsync((IList<PropertyDictionaryItem>)[dictionaryItem]);

                    dictionaryItems.Add(dictionaryItem);
                }
                else
                {
                    progressInfo.Errors.Add($"The '{propertyValue.Alias}' dictionary item is not found in '{propertyValue.PropertyName}' dictionary");
                    progressCallback(progressInfo);
                }
            }

            if (dictionaryItem != null)
            {
                propertyValue.ValueId = dictionaryItem.Id;
                propertyValue.ColorCode = dictionaryItem.ColorCode;
            }
        }
    }

    private async Task<IList<PropertyDictionaryItem>> GetExistingDictionaryItems(List<CsvProduct> csvProducts)
    {
        var dictionaryItems = new List<PropertyDictionaryItem>();

        var propertyIds = csvProducts
            .SelectMany(x => x.Properties)
            .Where(x => x.Dictionary)
            .Select(x => x.Id)
            .Distinct();

        foreach (var propertyIdsBatch in propertyIds.Paginate(_searchAllBatchSize))
        {
            var searchCriteria = AbstractTypeFactory<PropertyDictionaryItemSearchCriteria>.TryCreateInstance();
            searchCriteria.PropertyIds = propertyIdsBatch;
            searchCriteria.Take = _searchAllBatchSize;

            await foreach (var searchResult in propDictItemSearchService.SearchBatchesNoCloneAsync(searchCriteria))
            {
                dictionaryItems.AddRange(searchResult.Results);
            }
        }

        return dictionaryItems;
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
            string parentCategoryId = null;
            var count = progressInfo.ProcessedCount;
            var productCategoryNames = csvProduct.Category.Path.Split(_categoryDelimiters);

            foreach (var categoryName in productCategoryNames)
            {
                outline.Append($"\\{categoryName}");
                if (!cachedCategoryMap.TryGetValue(outline.ToString(), out var category))
                {
                    var searchCriteria = AbstractTypeFactory<CategorySearchCriteria>.TryCreateInstance();
                    searchCriteria.CatalogId = catalog.Id;
                    searchCriteria.CategoryId = parentCategoryId;
                    searchCriteria.SearchOnlyInRoot = parentCategoryId == null;
                    searchCriteria.Keyword = categoryName;
                    searchCriteria.Take = 1;

                    category = (await categorySearchService.SearchAsync(searchCriteria)).Results.FirstOrDefault();
                }

                if (category == null)
                {
                    category = new Category
                    {
                        Name = categoryName,
                        Code = GenerateSlug(categoryName),
                        CatalogId = catalog.Id,
                        ParentId = parentCategoryId,
                    };

                    await categoryService.SaveChangesAsync([category]);

                    // Raise notification each notifyCategorySizeLimit category
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

    private static string GenerateSlug(string categoryName)
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
        var defaultFulfilmentCenter = await GetDefaultFulfilmentCenter();

        foreach (var csvProductsBatch in csvProducts.Paginate(_saveProductsBatchSize))
        {
            try
            {
                var catalogProducts = csvProductsBatch.Select(csvProductConverter.GetCatalogProduct).ToArray();
                await productService.SaveChangesAsync(catalogProducts);

                await SaveProductInventories(csvProductsBatch, defaultFulfilmentCenter);

                await SaveProductPrices(csvProductsBatch);
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
                    // Send notification
                    progressInfo.ProcessedCount += csvProductsBatch.Count;
                    progressInfo.Description = $"Saving products: {progressInfo.ProcessedCount} of {progressInfo.TotalCount} created";
                    progressCallback(progressInfo);
                }
            }
        }
    }

    private async Task<FulfillmentCenter> GetDefaultFulfilmentCenter()
    {
        var searchCriteria = AbstractTypeFactory<FulfillmentCenterSearchCriteria>.TryCreateInstance();
        searchCriteria.Take = 1;

        var searchResult = await fulfillmentCenterSearchService.SearchAsync(searchCriteria);

        return searchResult.Results.FirstOrDefault();
    }

    private async Task SaveProductInventories(IList<CsvProduct> csvProducts, FulfillmentCenter defaultFulfilmentCenter)
    {
        // Set productId for dependent objects
        foreach (var product in csvProducts)
        {
            if (defaultFulfilmentCenter != null || product.Inventory.FulfillmentCenterId != null)
            {
                product.Inventory.ProductId = product.Id;
                product.Inventory.FulfillmentCenterId ??= defaultFulfilmentCenter.Id;
            }
            else
            {
                product.Inventory = null;
            }
        }

        var productIds = csvProducts.Select(x => x.Id).ToArray();
        var existingInventories = await inventoryService.GetProductsInventoryInfosAsync(productIds);

        var inventories = csvProducts
            .Where(x => !string.IsNullOrEmpty(x.Inventory?.ProductId))
            .Select(x => x.Inventory)
            .ToArray();

        foreach (var inventory in inventories)
        {
            var exitingInventory = existingInventories.FirstOrDefault(x => x.ProductId == inventory.ProductId && x.FulfillmentCenterId == inventory.FulfillmentCenterId);
            if (exitingInventory != null)
            {
                inventory.ProductId = exitingInventory.ProductId;
                inventory.FulfillmentCenterId = exitingInventory.FulfillmentCenterId;
                inventory.AllowBackorder = exitingInventory.AllowBackorder;
                inventory.AllowPreorder = exitingInventory.AllowPreorder;
                inventory.BackorderAvailabilityDate = exitingInventory.BackorderAvailabilityDate;
                inventory.BackorderQuantity = exitingInventory.BackorderQuantity;
                inventory.InTransit = exitingInventory.InTransit;
            }
        }

        await inventoryService.SaveChangesAsync(inventories);
    }

    private async Task SaveProductPrices(IList<CsvProduct> csvProducts)
    {
        // Update ProductId
        foreach (var product in csvProducts)
        {
            foreach (var price in product.Prices)
            {
                price.ProductId = product.Id;
            }
        }

        var prices = csvProducts.SelectMany(x => x.Prices).OfType<CsvPrice>().ToArray();

        // MinQuantity 0 is not allowed
        foreach (var price in prices.Where(x => x.MinQuantity == 0))
        {
            price.MinQuantity = 1;
        }

        // Try to update prices by id
        var pricesWithId = prices.Where(x => !string.IsNullOrEmpty(x.Id)).ToArray();
        var mergedPrices = await GetMergedPriceById(pricesWithId);

        // Then update prices with PricelistId
        var pricesWithPricelistId = prices.Except(pricesWithId).Where(x => !string.IsNullOrEmpty(x.PricelistId)).ToArray();
        mergedPrices.AddRange(await GetMergedPricesByPricelistId(pricesWithPricelistId));

        // We do not have pricelist id or price id, therefore select first product price
        var otherPrices = prices.Except(pricesWithId).Except(pricesWithPricelistId).ToArray();
        mergedPrices.AddRange(await GetMergedPricesByCurrency(otherPrices));

        await priceService.SaveChangesAsync(mergedPrices);
    }

    private async Task<IList<Price>> GetMergedPriceById(IList<CsvPrice> pricesWithId)
    {
        var result = new List<Price>();

        if (pricesWithId.Count == 0)
        {
            return result;
        }

        var priceIds = pricesWithId.Select(x => x.Id).ToArray();
        var existingPricesByIds = await priceService.GetAsync(priceIds);

        foreach (var price in pricesWithId)
        {
            var existingPrice = existingPricesByIds.FirstOrDefault(x => x.Id == price.Id);
            if (existingPrice != null)
            {
                price.MergeFrom(existingPrice);
            }

            result.Add(price);
        }

        return result;
    }

    private async Task<IList<Price>> GetMergedPricesByPricelistId(IList<CsvPrice> pricesWithPricelistId)
    {
        var result = new List<Price>();

        if (pricesWithPricelistId.Count == 0)
        {
            return result;
        }

        var existingPrices = new List<Price>();

        foreach (var group in pricesWithPricelistId.GroupBy(x => x.PricelistId))
        {
            var pricelistId = group.Key;
            var productIds = group.Select(x => x.ProductId).Distinct().ToArray();
            var prices = await GetExistingPrices(productIds, pricelistId);
            existingPrices.AddRange(prices);
        }

        foreach (var price in pricesWithPricelistId)
        {
            var existingPrice = existingPrices.FirstOrDefault(x => x.ProductId.EqualsIgnoreCase(price.ProductId) && x.PricelistId.EqualsIgnoreCase(price.PricelistId));
            if (existingPrice != null)
            {
                price.MergeFrom(existingPrice);
            }

            result.Add(price);
        }

        return result;
    }

    private async Task<IList<Price>> GetMergedPricesByCurrency(IList<CsvPrice> otherPrices)
    {
        var result = new List<Price>();

        if (otherPrices.Count == 0)
        {
            return result;
        }

        var productIds = otherPrices.Select(x => x.ProductId).Distinct().ToArray();
        var existingPrices = await GetExistingPrices(productIds);

        foreach (var price in otherPrices)
        {
            var existingPrice = existingPrices.FirstOrDefault(x =>
                x.Currency.EqualsIgnoreCase(price.Currency) &&
                x.ProductId.EqualsIgnoreCase(price.ProductId));

            if (existingPrice != null)
            {
                price.MergeFrom(existingPrice);
            }

            result.Add(price);
        }

        return result;
    }

    private async Task<IList<Price>> GetExistingPrices(IList<string> productIds, string pricelistId = null)
    {
        var result = new List<Price>();

        foreach (var productIdsBatch in productIds.Paginate(_searchAllBatchSize))
        {
            var searchCriteria = AbstractTypeFactory<PricesSearchCriteria>.TryCreateInstance();
            searchCriteria.PriceListId = pricelistId;
            searchCriteria.ProductIds = productIdsBatch;
            searchCriteria.Take = _searchAllBatchSize;

            await foreach (var searchResult in priceSearchService.SearchBatchesNoCloneAsync(searchCriteria))
            {
                result.AddRange(searchResult.Results);
            }
        }

        return result;
    }

    private async Task LoadProductDependencies(List<CsvProduct> csvProducts, Catalog catalog, CsvImportInfo importInfo)
    {
        var categoryIds = csvProducts.Select(x => x.CategoryId).Distinct().ToArray();
        var categoryById = (await categoryService.GetAsync(categoryIds, nameof(CategoryResponseGroup.Full))).ToDictionary(x => x.Id);

        foreach (var csvProduct in csvProducts)
        {
            csvProduct.Catalog = catalog;
            csvProduct.CatalogId = catalog.Id;

            if (csvProduct.CategoryId != null)
            {
                csvProduct.Category = categoryById[csvProduct.CategoryId];
            }

            if (!csvProduct.MainProductId.IsNullOrEmpty())
            {
                var mainProduct = csvProducts.FirstOrDefault(x =>
                    x.Id.EqualsIgnoreCase(csvProduct.MainProductId) ||
                    x.Code.EqualsIgnoreCase(csvProduct.MainProductId));

                csvProduct.MainProduct = mainProduct;
                csvProduct.MainProductId = mainProduct?.Id;
            }

            if (string.IsNullOrEmpty(csvProduct.Code))
            {
                csvProduct.Code = skuGenerator.GenerateSku(csvProduct);
            }

            UpdateCsvProductProperties(csvProduct, importInfo);
        }
    }

    private static void UpdateCsvProductProperties(CsvProduct csvProduct, CsvImportInfo importInfo)
    {
        var inheritedProperties = GetInheritedProperties(csvProduct);

        foreach (var property in csvProduct.Properties.ToArray())
        {
            // Try to find a property
            var inheritedProperty = inheritedProperties.FirstOrDefault(x => x.Name.EqualsIgnoreCase(property.Name));
            if (inheritedProperty == null)
            {
                continue;
            }

            property.ValueType = inheritedProperty.ValueType;
            property.Id = inheritedProperty.Id;
            property.Dictionary = inheritedProperty.Dictionary;
            property.Multivalue = inheritedProperty.Multivalue;

            foreach (var propertyValue in property.Values)
            {
                propertyValue.ValueType = inheritedProperty.ValueType;
                propertyValue.PropertyId = inheritedProperty.Id;
            }

            // Try to split a single value to multiple values for Multivalue/Multilanguage properties
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
                propertyValue.Value = property.Values.Join();
                property.Values = new List<PropertyValue> { propertyValue };
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

        foreach (var singleValue in valueArray ?? [])
        {
            var valueClone = firstPropertyValue.CloneTyped();
            valueClone.Value = singleValue;
            result.Add(valueClone);
        }

        return result;
    }

    // Merge importing products with existing ones to prevent erasing existing data, import should only update or create data
    private async Task MergeFromExistingProducts(List<CsvProduct> csvProducts, Catalog catalog)
    {
        var existingProducts = await GetExistingProducts(csvProducts, catalog);

        foreach (var csvProduct in csvProducts)
        {
            var existingProduct = csvProduct.Id != null
                ? existingProducts.FirstOrDefault(x => x.Id == csvProduct.Id)
                : existingProducts.FirstOrDefault(x => x.Code.EqualsIgnoreCase(csvProduct.Code));

            if (existingProduct != null)
            {
                csvProduct.MergeFrom(existingProduct);
            }
        }
    }

    private async Task<List<CatalogProduct>> GetExistingProducts(List<CsvProduct> csvProducts, Catalog catalog)
    {
        var existingProducts = new List<CatalogProduct>();

        // Load existing products by ID
        var productIds = csvProducts
            .Where(x => x.Id != null)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        foreach (var productIdsBatch in productIds.Paginate(_loadProductsBatchSize))
        {
            existingProducts.AddRange(await productService.GetAsync(productIdsBatch, nameof(ItemResponseGroup.Full)));
        }

        // Load existing products by Code
        var productCodes = csvProducts
            .Where(x => x.Id == null && x.Code != null)
            .Select(x => x.Code)
            .Distinct()
            .ToArray();

        using var repository = catalogRepositoryFactory();

        foreach (var productCodesBatch in productCodes.Paginate(_loadProductsBatchSize))
        {
            var productIdsBatch = await repository.Items
                .Where(x => x.CatalogId == catalog.Id && productCodesBatch.Contains(x.Code))
                .Select(x => x.Id)
                .ToArrayAsync();

            existingProducts.AddRange(await productService.GetAsync(productIdsBatch, nameof(ItemResponseGroup.Full)));
        }

        return existingProducts;
    }

    #region Validate CSV products

    private Task<bool> ValidateCsvProducts(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
    {
        progressInfo.Description = "Validating products...";

        // Here you can add checks before import, for example ValidateSeo(csvProducts) && ValidateSku(csvProducts)
        return ValidateSeo(csvProducts, progressInfo, progressCallback);
    }

    private async Task<bool> ValidateSeo(List<CsvProduct> csvProducts, ExportImportProgressInfo progressInfo, Action<ExportImportProgressInfo> progressCallback)
    {
        var valid = true;

        var productsWithSeoStore = csvProducts
            .Where(x => !x.SeoStore.IsNullOrEmpty())
            .ToList();

        var existingStoreIds = await GetExistingStoreIds(productsWithSeoStore);

        foreach (var product in productsWithSeoStore.Where(x => !existingStoreIds.ContainsIgnoreCase(x.SeoStore)))
        {
            progressInfo.Errors.Add($"Cannot find store with Id '{product.SeoStore}'. Line number: {product.LineNumber}");
            valid = false;
        }

        progressCallback(progressInfo);

        return valid;
    }

    private async Task<IList<string>> GetExistingStoreIds(List<CsvProduct> csvProducts)
    {
        var existingStoreIds = new List<string>();

        var storeIds = csvProducts
            .Select(x => x.SeoStore)
            .Distinct();

        foreach (var storeIdsBatch in storeIds.Paginate(_searchAllBatchSize))
        {
            var stores = await storeService.GetNoCloneAsync(storeIdsBatch);
            existingStoreIds.AddRange(stores.Select(x => x.Id));
        }

        return existingStoreIds;
    }

    #endregion
}
