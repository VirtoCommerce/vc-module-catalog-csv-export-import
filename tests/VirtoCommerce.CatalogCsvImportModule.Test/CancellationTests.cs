using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

// Verifies the cancellation token wired from the Hangfire import/export jobs is honored
// in the row-processing loop, so a job deleted from the dashboard (or server shutdown)
// actually stops instead of running to completion.
public class CancellationTests
{
    [Fact]
    public async Task ReadProducts_WhenTokenAlreadyCanceled_ThrowsOperationCanceled()
    {
        //Arrange
        var configuration = CsvProductMappingConfiguration.GetDefaultConfiguration();
        configuration.Delimiter = ",";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Sku,Name\r\nABC,Test product\r\n"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var reader = new CsvProductReader();

        //Act
        var act = () => reader.ReadProducts(stream, configuration, _ => { }, cts.Token);

        //Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
