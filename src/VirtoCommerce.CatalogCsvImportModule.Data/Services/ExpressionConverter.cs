using System;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class ExpressionConverter<T>(
    Func<string, T> inExpression,
    Func<T, string> outExpression)
    : ITypeConverter
{
    public Func<string, T> InExpression { get; set; } = inExpression;
    public Func<T, string> OutExpression { get; set; } = outExpression;

    public bool CanConvertFrom(Type type)
    {
        return InExpression != null;
    }

    public bool CanConvertTo(Type type)
    {
        return OutExpression != null;
    }

    public string ConvertToString(TypeConverterOptions options, object value)
    {
        return OutExpression((T)value);
    }

    public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (OutExpression != null)
        {
            return OutExpression((T)value);
        }

        return value.ToString();
    }

    public object ConvertFromString(TypeConverterOptions options, string text)
    {
        return InExpression(text);
    }

    public object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text.IsNullOrEmpty())
        {
            return null;
        }

        if (InExpression != null)
        {
            return InExpression(text);
        }

        return null;
    }
}

public static class CsvHelperExtensions
{
    public static MemberMap UsingExpression<T>(this MemberMap map, Func<string, T> readExpression, Func<T, string> writeExpression)
    {
        return map.TypeConverter(new ExpressionConverter<T>(readExpression, writeExpression));
    }
}
