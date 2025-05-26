using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core.Extensions;
using VirtoCommerce.CatalogCsvImportModule.Core.Helpers;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Caching;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Services;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services
{
    public class CsvCatalogExporter : ICsvCatalogExporter
    {
        private readonly IProductSearchService _productSearchService;
        private readonly IItemService _productService;
        private readonly IPricingEvaluatorService _pricingEvaluatorService;
        private readonly IBlobUrlResolver _blobUrlResolver;
        private readonly IInventorySearchService _inventorySearchService;
        private readonly Func<CsvProductMappingConfiguration, ClassMap> _getClassMap;

        private const int _batchSize = 50;

        public CsvCatalogExporter(
            IProductSearchService productSearchService,
            IItemService productService,
            IPricingEvaluatorService pricingEvaluatorService,
            IInventorySearchService inventorySearchService,
            IBlobUrlResolver blobUrlResolver,
            Func<CsvProductMappingConfiguration, ClassMap> getClassMap)
        {
            _productSearchService = productSearchService;
            _productService = productService;
            _pricingEvaluatorService = pricingEvaluatorService;
            _inventorySearchService = inventorySearchService;
            _blobUrlResolver = blobUrlResolver;
            _getClassMap = getClassMap;
        }

        public async Task DoExportAsync(Stream outStream, CsvExportInfo exportInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var progressInfo = new ExportImportProgressInfo
            {
                Description = "Counting products...",
                TotalCount = await GetProductsCount(exportInfo),
            };
            progressCallback(progressInfo);

            // It seems we need to read all the products twice.

            // First time: gather all dynamic properties to have full header
            progressInfo.Description = "Collecting product properties...";
            progressInfo.ProcessedCount = 0;
            progressCallback(progressInfo);

            await ProcessProductsByPage(exportInfo, progressInfo, progressCallback,
                "Collecting properties for {0} of {1} products...",
                products => CollectCsvColumns(exportInfo, products));

            // Second time: fetch and save products to CSV file
            progressInfo.Description = "Exporting...";
            progressInfo.ProcessedCount = 0;
            progressCallback(progressInfo);

            var writerConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = exportInfo.Configuration.Delimiter,
            };

            var streamWriter = new StreamWriter(outStream, Encoding.UTF8, 1024, true) { AutoFlush = true };
            await using var csvWriter = new CsvWriter(streamWriter, writerConfig);

            var classMap = _getClassMap(exportInfo.Configuration);
            csvWriter.Context.RegisterClassMap(classMap);

            var csvProductType = AbstractTypeFactoryHelper.GetEffectiveType<CsvProduct>();
            csvWriter.WriteHeader(csvProductType);
            await csvWriter.NextRecordAsync();

            await ProcessProductsByPage(exportInfo, progressInfo, progressCallback,
                "Exporting {0} of {1} products...",
                products => ExportProducts(exportInfo, progressInfo, progressCallback, csvWriter, products));

            progressInfo.Description = "Done.";
            progressCallback(progressInfo);
        }

        private async Task<int> GetProductsCount(CsvExportInfo exportInfo)
        {
            var result = GetDistinctProductIds(exportInfo).Count;

            if (TryGetProductSearchCriteria(exportInfo, take: 0, out var criteria))
            {
                result += (await _productSearchService.SearchNoCloneAsync(criteria)).TotalCount;
            }

            return result;
        }

        private static Task CollectCsvColumns(CsvExportInfo exportInfo, IList<CatalogProduct> products)
        {
            exportInfo.Configuration.PropertyCsvColumns = exportInfo.Configuration.PropertyCsvColumns
                .Union(products.SelectMany(x => x.Properties).Select(x => x.Name))
                .OrderBy(x => x)
                .ToArray();

            return Task.CompletedTask;
        }

        private async Task ExportProducts(
            CsvExportInfo exportInfo,
            ExportImportProgressInfo progressInfo,
            Action<ExportImportProgressInfo> progressCallback,
            CsvWriter csvWriter,
            IList<CatalogProduct> products)
        {
            var productIds = products.Select(x => x.Id).ToArray();

            // Load prices
            var priceEvaluationContext = AbstractTypeFactory<PriceEvaluationContext>.TryCreateInstance();
            priceEvaluationContext.ProductIds = productIds;
            priceEvaluationContext.PricelistIds = exportInfo.PriceListId == null ? null : [exportInfo.PriceListId];
            priceEvaluationContext.Currency = exportInfo.Currency;

            var allPrices = await _pricingEvaluatorService.EvaluateProductPricesAsync(priceEvaluationContext);

            // Load inventories
            var inventorySearchCriteria = AbstractTypeFactory<InventorySearchCriteria>.TryCreateInstance();
            inventorySearchCriteria.ProductIds = productIds;
            inventorySearchCriteria.FulfillmentCenterIds = string.IsNullOrWhiteSpace(exportInfo.FulfilmentCenterId) ? null : [exportInfo.FulfilmentCenterId];
            inventorySearchCriteria.Take = _batchSize;

            var allInventories = await _inventorySearchService.SearchAllNoCloneAsync(inventorySearchCriteria);

            // Convert to dictionary for faster search
            var pricesByProductIds = allPrices.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());
            var inventoriesByProductIds = allInventories.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());

            foreach (var product in products)
            {
                try
                {
                    var price = pricesByProductIds.GetValueSafe(product.Id);
                    var inventory = inventoriesByProductIds.GetValueSafe(product.Id);
                    var seoInfos = product.SeoInfos.Count > 0 ? product.SeoInfos : [null];

                    foreach (var seoInfo in seoInfos)
                    {
                        var csvProduct = AbstractTypeFactory<CsvProduct>.TryCreateInstance();
                        csvProduct.Initialize(product, price, inventory, seoInfo);
                        SetImages(csvProduct);

                        // IEnumerable must be used for records to prevent stack overflow in CsvHelper 
                        IEnumerable records = new[] { csvProduct };
                        await csvWriter.WriteRecordsAsync(records);
                    }
                }
                catch (Exception ex)
                {
                    progressInfo.Errors.Add(ex.ToString());
                    progressCallback(progressInfo);
                }
            }
        }

        private async Task ProcessProductsByPage(
            CsvExportInfo exportInfo,
            ExportImportProgressInfo progressInfo,
            Action<ExportImportProgressInfo> progressCallback,
            string progressMessageTemplate,
            Func<IList<CatalogProduct>, Task> action)
        {
            await ProcessProducts(GetDistinctProductIds(exportInfo));

            if (TryGetProductSearchCriteria(exportInfo, take: _batchSize, out var criteria))
            {
                await foreach (var searchResult in _productSearchService.SearchBatchesNoCloneAsync(criteria))
                {
                    var productIds = searchResult.Results.Select(x => x.Id).ToArray();
                    await ProcessProducts(productIds);
                }
            }

            async Task ProcessProducts(IList<string> productIds)
            {
                if (productIds.Count == 0)
                {
                    return;
                }

                progressInfo.ProcessedCount += productIds.Count;
                progressInfo.Description = string.Format(progressMessageTemplate, progressInfo.ProcessedCount, progressInfo.TotalCount);
                progressCallback(progressInfo);

                // Pass no more than _batchSize products to the action
                await foreach (var products in GetProductsWithVariations(productIds, _batchSize).Paginate(_batchSize))
                {
                    await action(products);

                    // Need to rewrite with caching disabled
                    ItemCacheRegion.ExpireRegion();
                    GC.Collect();
                }
            }
        }

        private static IList<string> GetDistinctProductIds(CsvExportInfo exportInfo)
        {
            return exportInfo.ProductIds != null
                ? exportInfo.ProductIds.Distinct().ToArray()
                : [];
        }

        private static bool TryGetProductSearchCriteria(CsvExportInfo exportInfo, int take, out ProductSearchCriteria result)
        {
            result = null;

            if (!exportInfo.CategoryIds.IsNullOrEmpty())
            {
                result = AbstractTypeFactory<ProductSearchCriteria>.TryCreateInstance();
                result.CatalogId = exportInfo.CatalogId;
                result.CategoryIds = exportInfo.CategoryIds;
                result.SearchInChildren = true;
                result.SearchInVariations = false;
                result.Take = take;
            }
            else if (exportInfo.ProductIds.IsNullOrEmpty())
            {
                result = AbstractTypeFactory<ProductSearchCriteria>.TryCreateInstance();
                result.CatalogId = exportInfo.CatalogId;
                result.SearchInChildren = true;
                result.SearchInVariations = false;
                result.Take = take;
            }

            return result != null;
        }

        private async IAsyncEnumerable<CatalogProduct> GetProductsWithVariations(IList<string> productIds, int batchSize)
        {
            var products = await GetProducts(productIds, batchSize);

            // Variations in products come without properties, only VariationProperties are included. Have to load variations again to receive all properties.
            var variationIds = products.SelectMany(product => product.Variations.Select(variation => variation.Id)).ToArray();
            var variations = await GetProducts(variationIds, batchSize);

            foreach (var product in products)
            {
                yield return product;

                foreach (var variation in variations.Where(x => x.MainProductId == product.Id))
                {
                    yield return variation;
                }
            }
        }

        private async Task<IList<CatalogProduct>> GetProducts(IList<string> productIds, int batchSize)
        {
            var allProducts = new List<CatalogProduct>();

            foreach (var ids in productIds.Paginate(batchSize))
            {
                var products = await _productService.GetAsync(ids, ItemResponseGroup.ItemLarge.ToString());
                allProducts.AddRange(products);
            }

            return allProducts;
        }

        private void SetImages(CsvProduct csvProduct)
        {
            if (!string.IsNullOrEmpty(csvProduct.PrimaryImage))
            {
                csvProduct.PrimaryImage = _blobUrlResolver.GetAbsoluteUrl(csvProduct.PrimaryImage);
            }

            if (!string.IsNullOrEmpty(csvProduct.AltImage))
            {
                csvProduct.AltImage = _blobUrlResolver.GetAbsoluteUrl(csvProduct.AltImage);
            }
        }
    }
}
