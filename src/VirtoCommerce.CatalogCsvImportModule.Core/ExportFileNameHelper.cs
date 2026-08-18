using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace VirtoCommerce.CatalogCsvImportModule.Core;

public static class ExportFileNameHelper
{
    /// <summary>
    /// Builds the exported CSV file name from the <see cref="ModuleConstants.Settings.General.ExportFileNameTemplate"/> template.
    /// Supported parameters: {0} - the UTC timestamp of the export, {1} - a random 6-digit number.
    /// The .csv extension is always enforced.
    /// </summary>
    public static string GetFileName(string template, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            template = (string)ModuleConstants.Settings.General.ExportFileNameTemplate.DefaultValue;
        }

        // Invariant culture keeps the file name stable regardless of the server locale.
        var fileName = string.Format(CultureInfo.InvariantCulture, template, timestampUtc, GenerateRandomNumber());

        return Path.ChangeExtension(fileName, ".csv");
    }

    /// <summary>
    /// Generates a cryptographically strong random number (000000-999999) that makes the download URL of an export hard to guess.
    /// Returned zero-padded as a string, so the {1} template parameter always renders exactly 6 digits
    /// and any format specifier the user adds cannot break the file name.
    /// </summary>
    public static string GenerateRandomNumber()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
