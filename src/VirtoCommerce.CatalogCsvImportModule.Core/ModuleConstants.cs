using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.CatalogCsvImportModule.Core;

[ExcludeFromCodeCoverage]
public static class ModuleConstants
{
    public static class BackgroundJobs
    {
        /// <summary>
        /// Distributed lock resource that serializes catalog CSV import jobs so only one runs at a time.
        /// Prevents concurrent imports touching the same SKU from overwriting each other (lost-write race).
        /// </summary>
        public const string ImportLockKey = "CatalogCsvImport";

        /// <summary>
        /// Maximum time (seconds) an import job waits to acquire <see cref="ImportLockKey"/> before failing.
        /// Sized for the worst-case queue duration so queued imports block-then-run instead of timing out.
        /// </summary>
        public const int ImportLockTimeoutSeconds = 60 * 60 * 24; // 1 day

        /// <summary>
        /// Distributed lock resource that serializes catalog CSV export jobs so only one runs at a time.
        /// Prevents concurrent exports touching the same SKU from overwriting each other (lost-write race).
        /// </summary>
        public const string ExportLockKey = "CatalogCsvExport";

        /// <summary>
        /// Maximum time (seconds) an export job waits to acquire <see cref="ExportLockKey"/> before failing.
        /// Sized for the worst-case queue duration so queued exports block-then-run instead of timing out.
        /// </summary>
        public const int ExportLockTimeoutSeconds = 60 * 60 * 24; // 1 day

    }

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
