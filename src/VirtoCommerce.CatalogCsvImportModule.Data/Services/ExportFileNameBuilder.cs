using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class ExportFileNameBuilder(ISettingsManager settingsManager, TimeProvider timeProvider) : IExportFileNameBuilder
{
    public const string RandomTokenCharacters = "0123456789";
    public const int RandomTokenLength = 6;

    public virtual async Task<string> GetFileName(SettingDescriptor templateSetting)
    {
        var template = await settingsManager.GetValueAsync<string>(templateSetting);

        if (string.IsNullOrWhiteSpace(template))
        {
            template = (string)templateSetting.DefaultValue;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var randomToken = GetRandomToken();

        return string.Format(CultureInfo.InvariantCulture, template, utcNow, randomToken);
    }

    public virtual string GetRandomToken()
    {
        return RandomNumberGenerator.GetString(RandomTokenCharacters, RandomTokenLength);
    }
}
