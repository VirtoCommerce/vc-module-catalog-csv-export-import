using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class CsvProductReaderTests
{
    [Theory]
    [MemberData(nameof(TestData))]
    public async Task DetectEncoding(byte[] testContent)
    {
        // Arrange
        var reader = new CsvProductReader();
        using var stream = new MemoryStream(testContent);

        // Act
        var columns = await reader.ReadColumns(stream, ",");

        // Assert
        columns.Should().HaveCount(1);
        Assert.Equal("Test", columns[0]);
    }

    public static TheoryData<byte[]> TestData()
    {
        var data = new TheoryData<byte[]>
        {
            // UTF-32 LE
            { [0xFF, 0xFE, 0x00, 0x00, 0x54, 0x00, 0x00, 0x00, 0x65, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00, 0x74, 0x00, 0x00, 0x00] },

            // UTF-32 BE
            { [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x54, 0x00, 0x00, 0x00, 0x65, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00, 0x74] },

            // UTF-8
            { [0xEF, 0xBB, 0xBF, 0x54, 0x65, 0x73, 0x74] },

            // UTF-16 LE
            { [0xFF, 0xFE, 0x54, 0x00, 0x65, 0x00, 0x73, 0x00, 0x74, 0x00] },

            // UTF-16 BE
            { [0xFE, 0xFF, 0x00, 0x54, 0x00, 0x65, 0x00, 0x73, 0x00, 0x74] },

            // UTF-8
            { [0x54, 0x65, 0x73, 0x74] },
        };

        return data;
    }
}
