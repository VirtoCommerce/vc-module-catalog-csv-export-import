using System.Collections.Generic;
using System.Linq;
using CsvHelper;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public static class CsvReaderExtension
{
    public static string Delimiter { get; set; } = ";";
    public static string InnerDelimiter { get; set; } = "__";

    public static IEnumerable<PropertyValue> GetPropertiesByColumn(this IReaderRow reader, string columnName)
    {
        var columnValue = reader.GetField<string>(columnName);

        foreach (var value in columnValue.Trim().Split(Delimiter))
        {
            const int multilanguagePartsCount = 2;
            var valueParts = value.Split(InnerDelimiter, multilanguagePartsCount);
            var multilanguage = valueParts.Length == multilanguagePartsCount;

            yield return new PropertyValue
            {
                PropertyName = columnName,
                Value = multilanguage ? valueParts.Last() : value,
                LanguageCode = multilanguage ? valueParts.First() : string.Empty,
            };
        }
    }

    public static string JoinValues(this Property property)
    {
        if (property?.Values is null)
        {
            return string.Empty;
        }

        IEnumerable<string> values;

        if (property.Dictionary)
        {
            values = property.Values
                .Where(x => !string.IsNullOrEmpty(x.Alias))
                .Select(x => x.Alias)
                .Distinct();
        }
        else if (property.Multilanguage)
        {
            values = property.Values
                .Select(x => $"{x.LanguageCode}{InnerDelimiter}{x.Value}");
        }
        else
        {
            values = property.Values
                .Where(x => x.Value != null || x.Alias != null)
                .Select(x => x.Alias ?? x.Value?.ToString());
        }

        return JoinCsvValues(values);
    }

    public static string Join(this IList<PropertyValue> propertyValues)
    {
        return JoinCsvValues(propertyValues.Select(x => x.Value?.ToString()));
    }

    public static string JoinCsvValues(IEnumerable<string> values)
    {
        return string.Join(Delimiter, values);
    }
}
