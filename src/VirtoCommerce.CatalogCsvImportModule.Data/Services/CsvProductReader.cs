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
using VirtoCommerce.CatalogCsvImportModule.Core.Helpers;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CsvProductReader : ICsvProductReader
{
    public virtual async Task<IList<string>> ReadColumns(Stream stream, string delimiter)
    {
        using var csvReader = await GetCsvReader(stream, delimiter);

        if (await csvReader.ReadAsync() && csvReader.ReadHeader())
        {
            return csvReader.HeaderRecord;
        }

        return [];
    }

    public virtual async Task<List<CsvProduct>> ReadProducts(Stream stream, CsvProductMappingConfiguration configuration, Action<ExportImportProgressInfo> progressCallback)
    {
        var csvProducts = new List<CsvProduct>();

        var progressInfo = new ExportImportProgressInfo
        {
            Description = "Reading products from CSV file...",
        };
        progressCallback(progressInfo);

        using var csvReader = await GetCsvReader(stream, configuration.Delimiter);
        csvReader.Context.RegisterClassMap(CsvProductMap.Create(configuration));
        //csvReader.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty);

        var csvProductType = AbstractTypeFactoryHelper.GetEffectiveType<CsvProduct>();

        while (await csvReader.ReadAsync())
        {
            try
            {
                var csvProduct = (CsvProduct)csvReader.GetRecord(csvProductType);

                ReplaceEmptyStringsWithNull(csvProduct);
                csvProduct.CreateImagesFromFlatData();

                csvProducts.Add(csvProduct);
            }
            catch (TypeConverterException ex)
            {
                progressInfo.Errors.Add($"Column: {ex.MemberMapData.Member?.Name}, {ex.Message}");
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

        return csvProducts;
    }

    private static async Task<CsvReader> GetCsvReader(Stream stream, string delimiter)
    {
        var readerConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            TrimOptions = delimiter.IsNullOrWhiteSpace()
                ? TrimOptions.None
                : TrimOptions.Trim,
            MissingFieldFound = _ => { },
        };

        var encoding = await DetectEncoding(stream);
        var csvReader = new CsvReader(new StreamReader(stream, encoding), readerConfig);

        return csvReader;
    }

    private static async Task<Encoding> DetectEncoding(Stream stream)
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
            var bytesRead = await stream.ReadAsync(bom, 0, bom.Length);

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

    private static void ReplaceEmptyStringsWithNull(CsvProduct csvProduct)
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
    }
}
