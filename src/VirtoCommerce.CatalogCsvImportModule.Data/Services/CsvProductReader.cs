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
        csvReader.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add(string.Empty);

        var csvProductType = AbstractTypeFactoryHelper.GetEffectiveType<CsvProduct>();

        while (await csvReader.ReadAsync())
        {
            try
            {
                var csvProduct = (CsvProduct)csvReader.GetRecord(csvProductType);

                RemoveEmptyReviews(csvProduct);
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

    private static Task<Encoding> DetectEncoding(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must support seeking.", nameof(stream));
        }

        return DetectEncodingAsync();

        async Task<Encoding> DetectEncodingAsync()
        {
            // Save the current position of the stream to reset later
            var originalPosition = stream.Position;

            try
            {
                // Read the first few bytes to check for a BOM
                var bytes = new byte[4];
                var bytesRead = await stream.ReadAsync(bytes, 0, bytes.Length);
                return bytes.DetectEncoding(bytesRead);
            }
            finally
            {
                // Reset the stream position to the original state
                stream.Position = originalPosition;
            }
        }
    }

    private static void RemoveEmptyReviews(CsvProduct csvProduct)
    {
        csvProduct.Reviews = csvProduct.Reviews
            .Where(x => !string.IsNullOrEmpty(x.Content) && !string.IsNullOrEmpty(x.ReviewType))
            .ToList();
    }
}
