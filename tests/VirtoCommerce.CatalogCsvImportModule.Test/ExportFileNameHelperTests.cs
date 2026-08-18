using System;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Core;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class ExportFileNameHelperTests
{
    private static readonly DateTime _timestamp = new(2026, 8, 13, 9, 13, 19, DateTimeKind.Utc);

    [Fact]
    public void GetFileName_DefaultTemplate_AppendsSixDigitRandomNumber()
    {
        var template = (string)ModuleConstants.Settings.General.ExportFileNameTemplate.DefaultValue;

        var fileName = ExportFileNameHelper.GetFileName(template, _timestamp);

        fileName.Should().MatchRegex(@"^products_2026-08-13_09-13-19_\d{6}\.csv$");
    }

    [Fact]
    public void GetFileName_TemplateWithoutRandomNumber_KeepsNameUnchanged()
    {
        var fileName = ExportFileNameHelper.GetFileName("products_{0:yyyy-MM-dd_HH-mm-ss}", _timestamp);

        fileName.Should().Be("products_2026-08-13_09-13-19.csv");
    }

    [Fact]
    public void GetFileName_RandomNumberWithFormatSpecifier_StillRendersSixDigits()
    {
        var fileName = ExportFileNameHelper.GetFileName("products_{1:D6}", _timestamp);

        fileName.Should().MatchRegex(@"^products_\d{6}\.csv$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetFileName_BlankTemplate_FallsBackToDefaultTemplate(string template)
    {
        var fileName = ExportFileNameHelper.GetFileName(template, _timestamp);

        fileName.Should().MatchRegex(@"^products_2026-08-13_09-13-19_\d{6}\.csv$");
    }

    [Fact]
    public void GetFileName_TemplateWithCsvExtension_DoesNotDoubleExtension()
    {
        var fileName = ExportFileNameHelper.GetFileName("products_{1}.csv", _timestamp);

        fileName.Should().MatchRegex(@"^products_\d{6}\.csv$");
    }

    [Fact]
    public void GetFileName_CultureSensitiveTemplate_UsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var fileName = ExportFileNameHelper.GetFileName("products_{0:dddd}", _timestamp);

            fileName.Should().Be("products_Thursday.csv");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void GetFileName_SameTemplateAndTimestamp_ProducesDifferentNames()
    {
        var fileNames = Enumerable.Range(0, 50)
            .Select(_ => ExportFileNameHelper.GetFileName("products_{0:yyyy-MM-dd_HH-mm-ss}_{1}", _timestamp))
            .ToList();

        fileNames.Distinct().Should().HaveCountGreaterThan(40);
    }

    [Fact]
    public void GenerateRandomNumber_AlwaysReturnsSixDigits()
    {
        for (var i = 0; i < 1000; i++)
        {
            ExportFileNameHelper.GenerateRandomNumber().Should().MatchRegex(@"^\d{6}$");
        }
    }
}
