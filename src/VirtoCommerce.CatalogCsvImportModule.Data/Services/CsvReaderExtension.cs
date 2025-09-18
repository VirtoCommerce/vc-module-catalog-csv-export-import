using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public static class CsvReaderExtension
{
    public static string ValueDelimiter { get; set; } = ";";
    public static string LanguageDelimiter { get; set; } = "__";
    public static string ColorDelimiter { get; set; } = "|";
    public static string EscapeString { get; set; } = "`";

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

        foreach (var value in Split(columnValue.Trim(), ValueDelimiter))
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

        value = Escape(value);

        if (!propertyValue.LanguageCode.IsNullOrEmpty())
        {
            value = $"{Escape(propertyValue.LanguageCode)}{LanguageDelimiter}{value}";
        }

        if (!propertyValue.ColorCode.IsNullOrEmpty())
        {
            value = $"{value}{ColorDelimiter}{Escape(propertyValue.ColorCode)}";
        }

        return value;
    }

    private static void ParseString(PropertyValue propertyValue, string input)
    {
        var value = input;

        const int partsCount = 2;

        if (value.Contains(LanguageDelimiter))
        {
            var parts = Split(value, LanguageDelimiter, partsCount);
            if (parts.Count == partsCount)
            {
                propertyValue.LanguageCode = Unescape(parts[0]).EmptyToNull();
                value = parts[1].EmptyToNull();
            }
        }

        if (value != null && value.Contains(ColorDelimiter))
        {
            var parts = Split(value, ColorDelimiter, partsCount);
            if (parts.Count == partsCount)
            {
                propertyValue.ColorCode = Unescape(parts[1]).EmptyToNull();
                value = parts[0].EmptyToNull();
            }
        }

        propertyValue.Value = Unescape(value);
    }

    private static List<string> Split(string input, string delimiter, int count = int.MaxValue)
    {
        if (input.IsNullOrEmpty())
        {
            return [input];
        }

        var result = new List<string>();
        var builder = new StringBuilder();
        var doubleEscape = EscapeString + EscapeString;
        var inEscape = false;
        var i = 0;

        while (i < input.Length)
        {
            var remainingInput = input.AsSpan(i);

            if (result.Count == count - 1)
            {
                builder.Append(remainingInput);
                break;
            }

            if (!inEscape)
            {
                // Check for escape start
                if (remainingInput.StartsWith(EscapeString))
                {
                    builder.Append(EscapeString);
                    i += EscapeString.Length;
                    inEscape = true;
                    continue;
                }

                // Check for delimiter
                if (remainingInput.StartsWith(delimiter))
                {
                    result.Add(builder.ToString());
                    builder.Clear();
                    i += delimiter.Length;
                    continue;
                }
            }
            // Check for escape end
            else if (remainingInput.StartsWith(EscapeString))
            {
                if (remainingInput.StartsWith(doubleEscape))
                {
                    builder.Append(doubleEscape);
                    i += doubleEscape.Length;
                }
                else
                {
                    builder.Append(EscapeString);
                    i += EscapeString.Length;
                    inEscape = false;
                }

                continue;
            }

            // Regular character
            builder.Append(input[i]);
            i++;
        }

        result.Add(builder.ToString());

        return result;
    }

    private static string Escape(string input)
    {
        if (!input.Contains(ValueDelimiter) &&
            !input.Contains(LanguageDelimiter) &&
            !input.Contains(ColorDelimiter) &&
            !input.Contains(EscapeString))
        {
            return input;
        }

        var doubleEscape = EscapeString + EscapeString;

        return $"{EscapeString}{input.Replace(EscapeString, doubleEscape)}{EscapeString}";
    }

    private static string Unescape(string input)
    {
        if (!input.StartsWith(EscapeString) || !input.EndsWith(EscapeString))
        {
            return input;
        }

        var doubleEscape = EscapeString + EscapeString;

        return input
            .Substring(EscapeString.Length, input.Length - doubleEscape.Length)
            .Replace(doubleEscape, EscapeString);
    }
}
