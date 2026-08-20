using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;

public interface IExportFileNameBuilder
{
    Task<string> GetFileName(SettingDescriptor templateSetting);

    string GetRandomToken();
}
