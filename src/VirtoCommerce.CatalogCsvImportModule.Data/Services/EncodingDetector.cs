using System;
using System.Text;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public static class EncodingDetector
{
    private static readonly byte[] _utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] _utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
    private static readonly byte[] _utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] _utf16LeBom = [0xFF, 0xFE];
    private static readonly byte[] _utf16BeBom = [0xFE, 0xFF];

    public static Encoding DetectEncoding(this byte[] array, int size)
    {
        var span = new ReadOnlySpan<byte>(array, 0, size);

        if (span.StartsWith(_utf32LeBom))
        {
            return Encoding.UTF32;
        }

        if (span.StartsWith(_utf32BeBom))
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        }

        if (span.StartsWith(_utf8Bom))
        {
            return Encoding.UTF8;
        }

        if (span.StartsWith(_utf16LeBom))
        {
            return Encoding.Unicode;
        }

        if (span.StartsWith(_utf16BeBom))
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
