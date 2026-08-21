using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class ExportFileNameBuilderTests
{
    private static readonly DateTimeOffset _timestamp = new(2026, 8, 13, 9, 13, 19, TimeSpan.Zero);
    private string _template;
    private readonly ExportFileNameBuilder _builder;

    public ExportFileNameBuilderTests()
    {
        var settingsManager = new Mock<ISettingsManager>();

        settingsManager
            .Setup(x => x.GetObjectSettingAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(() => new ObjectSettingEntry { Value = _template });

        _builder = new ExportFileNameBuilder(settingsManager.Object, new FakeTimeProvider(_timestamp));
    }

    [Fact]
    public async Task GetFileName_DefaultTemplate_AppendsSixDigits()
    {
        // Arrange
        var templateSetting = ModuleConstants.Settings.General.ExportFileNameTemplate;

        // Act
        var fileName = await _builder.GetFileName(templateSetting);

        // Assert
        fileName.Should().MatchRegex(@"^products_2026-08-13_09-13-19_\d{6}$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFileName_BlankTemplate_FallsBackToDefaultTemplate(string template)
    {
        // Arrange
        var templateSetting = new SettingDescriptor { DefaultValue = "default_template_{1}" };
        _template = template;

        // Act
        var fileName = await _builder.GetFileName(templateSetting);

        // Assert
        fileName.Should().MatchRegex(@"^default_template_\d{6}$");
    }

    [Fact]
    public async Task GetFileName_TemplateWithoutSecondParameter_DoesNotAppendSixDigits()
    {
        // Arrange
        var templateSetting = new SettingDescriptor();
        _template = "products_{0:yyyy-MM-dd_HH-mm-ss}";

        // Act
        var fileName = await _builder.GetFileName(templateSetting);

        // Assert
        fileName.Should().Be("products_2026-08-13_09-13-19");
    }

    [Fact]
    public async Task GetFileName_SecondParameterWithFormatSpecifier_StillAppendsSixDigits()
    {
        // Arrange
        var templateSetting = new SettingDescriptor();
        _template = "products_{1:D3}";

        // Act
        var fileName = await _builder.GetFileName(templateSetting);

        // Assert
        fileName.Should().MatchRegex(@"^products_\d{6}$");
    }

    [Fact]
    public async Task GetFileName_TemplateWithDots_DoesNotTruncateAfterDot()
    {
        // Arrange
        var templateSetting = new SettingDescriptor();
        _template = "products_{0:yyyy.MM.dd}_{1}";

        // Act
        var fileName = await _builder.GetFileName(templateSetting);

        // Assert
        fileName.Should().MatchRegex(@"^products_2026\.08\.13_\d{6}$");
    }

    [Fact]
    public async Task GetFileName_CultureSensitiveTemplate_UsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // Arrange
            var templateSetting = new SettingDescriptor();
            _template = "products_{0:dddd}";
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var fileName = await _builder.GetFileName(templateSetting);

            // Assert
            fileName.Should().Be("products_Thursday");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task GetFileName_SameTemplateAndTimestamp_ProducesDifferentNames()
    {
        // Arrange
        var templateSetting = new SettingDescriptor();
        _template = "products_{0:yyyy-MM-dd_HH-mm-ss}_{1}";

        // Act
        var fileNames = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => _builder.GetFileName(templateSetting)));

        // Assert
        fileNames.Distinct().Should().HaveCountGreaterThan(40);
    }

    [Fact]
    public void GetRandomToken_AlwaysReturnsSixDigits()
    {
        // Act & Assert
        for (var i = 0; i < 1000; i++)
        {
            _builder.GetRandomToken().Should().MatchRegex(@"^\d{6}$");
        }
    }
}
