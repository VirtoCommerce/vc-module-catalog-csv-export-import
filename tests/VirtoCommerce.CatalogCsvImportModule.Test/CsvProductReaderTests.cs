using System.Collections.Generic;
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
    public async Task DetectEncoding(string encodingName, byte[] testContent)
    {
        // Arrange
        var reader = new CsvProductReader();
        using var stream = new MemoryStream(testContent);

        // Act
        var columns = await reader.ReadColumns(stream, ",");

        // Assert
        Assert.NotEmpty(encodingName);
        columns.Should().HaveCount(1);
        Assert.Equal("Test", columns[0]);
    }

    public static IEnumerable<object[]> TestData()
    {
        yield return ["UTF-32 LE", new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x54, 0x00, 0x00, 0x00, 0x65, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00, 0x74, 0x00, 0x00, 0x00 }];
        yield return ["UTF-32 BE", new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x54, 0x00, 0x00, 0x00, 0x65, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x00, 0x74 }];
        yield return ["UTF-8", new byte[] { 0xEF, 0xBB, 0xBF, 0x54, 0x65, 0x73, 0x74 }];
        yield return ["UTF-16 LE", new byte[] { 0xFF, 0xFE, 0x54, 0x00, 0x65, 0x00, 0x73, 0x00, 0x74, 0x00 }];
        yield return ["UTF-16 BE", new byte[] { 0xFE, 0xFF, 0x00, 0x54, 0x00, 0x65, 0x00, 0x73, 0x00, 0x74 }];
    }
}
