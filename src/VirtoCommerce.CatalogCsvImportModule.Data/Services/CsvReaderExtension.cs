using System.Collections.Generic;
using System.Linq;
using CsvHelper;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public static class CsvReaderExtension
{
    public static string ValueDelimiter { get; set; } = ";";
    public static string LanguageDelimiter { get; set; } = "__";
    public static string ColorDelimiter { get; set; } = "|";

    public static IEnumerable<PropertyValue> GetValues(this IReaderRow reader, string columnName)
    {
        var columnValue = reader.GetField<string>(columnName);

        if (columnValue.IsNullOrEmpty())
        {
            yield return new PropertyValue
            {
                PropertyName = columnName,
            };

            yield break;
        }

        foreach (var value in columnValue.Trim().Split(ValueDelimiter))
        {
            var propertyValue = new PropertyValue
            {
                PropertyName = columnName,
            };

            ParseString(propertyValue, value);

            yield return propertyValue;
        }
    }

    public static string JoinValues(this Property property)
    {
        if (property?.Values is null)
        {
            return string.Empty;
        }

        var values = property.Dictionary
            ? property.Values.Select(x => x.Alias)
            : property.Values.Select(GetString);

        return JoinCsvValues(values);
    }

    public static string Join(this IList<PropertyValue> propertyValues)
    {
        return JoinCsvValues(propertyValues.Select(x => x.Value?.ToString()));
    }


    private static string JoinCsvValues(IEnumerable<string> values)
    {
        values = values
            .Where(x => !x.IsNullOrEmpty())
            .Distinct();

        return string.Join(ValueDelimiter, values);
    }

    private static string GetString(PropertyValue propertyValue)
    {
        var value = propertyValue.Value?.ToString().EmptyToNull();

        if (value is null)
        {
            return null;
        }

        if (!propertyValue.LanguageCode.IsNullOrEmpty())
        {
            value = $"{propertyValue.LanguageCode}{LanguageDelimiter}{value}";
        }

        if (!propertyValue.ColorCode.IsNullOrEmpty())
        {
            value = $"{value}{ColorDelimiter}{propertyValue.ColorCode}";
        }

        return value;
    }

    private static void ParseString(PropertyValue propertyValue, string input)
    {
        var value = input;

        const int partsCount = 2;

        if (value.Contains(LanguageDelimiter))
        {
            var parts = value.Split(LanguageDelimiter, partsCount);
            if (parts.Length == partsCount)
            {
                propertyValue.LanguageCode = parts[0].EmptyToNull();
                value = parts[1].EmptyToNull();
            }
        }

        if (value != null && value.Contains(ColorDelimiter))
        {
            var parts = value.Split(ColorDelimiter, partsCount);
            if (parts.Length == partsCount)
            {
                value = parts[0].EmptyToNull();
                propertyValue.ColorCode = parts[1].EmptyToNull();
            }
        }

        propertyValue.Value = value;
    }
}
