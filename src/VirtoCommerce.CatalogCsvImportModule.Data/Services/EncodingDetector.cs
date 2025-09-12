using System;
using System.Text;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public static class EncodingDetector
{
    public static byte[] Utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    public static byte[] Utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
    public static byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    public static byte[] Utf16LeBom = [0xFF, 0xFE];
    public static byte[] Utf16BeBom = [0xFE, 0xFF];

    public static Encoding DetectEncoding(this ReadOnlySpan<byte> span)
    {
        if (span.StartsWith(Utf32LeBom))
        {
            return Encoding.UTF32;
        }

        if (span.StartsWith(Utf32BeBom))
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        }

        if (span.StartsWith(Utf8Bom))
        {
            return Encoding.UTF8;
        }

        if (span.StartsWith(Utf16LeBom))
        {
            return Encoding.Unicode;
        }

        if (span.StartsWith(Utf16BeBom))
        {
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }

    public static bool StartsWith(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> other)
    {
        if (span.Length < other.Length)
        {
            return false;
        }

        for (var i = 0; i < other.Length; i++)
        {
            if (span[i] != other[i])
            {
                return false;
            }
        }

        return true;
    }
}
