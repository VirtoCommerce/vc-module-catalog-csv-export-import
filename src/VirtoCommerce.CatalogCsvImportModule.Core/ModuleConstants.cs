using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.CatalogCsvImportModule.Core;

[ExcludeFromCodeCoverage]
public static class ModuleConstants
{
    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor CreateDictionaryValues { get; } = new()
            {
                Name = "CatalogCsvImport.CreateDictionaryValues",
                GroupName = "CatalogCsvImport|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static SettingDescriptor ExportFileNameTemplate { get; } = new()
            {
                Name = "CatalogCsvImport.ExportFileNameTemplate",
                ValueType = SettingValueType.ShortText,
                GroupName = "CatalogCsvImport|General",
                DefaultValue = "products_{0:yyyy-MM-dd_HH-mm-ss}",
            };
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                yield return General.CreateDictionaryValues;
                yield return General.ExportFileNameTemplate;
            }
        }
    }
}
