using System;

namespace VirtoCommerce.CatalogCsvImportModule.Core;
public static class UrlHelper
{
    public static string ExtractFileNameFromUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            throw new UriFormatException($"Invalid URL format {url}.");
        }

        if (!uri.IsAbsoluteUri)
        {
            uri = new Uri(new Uri("https://dummy-base/"), url);
        }

        var localPath = uri.LocalPath;

        // Get the file name from the path
        return localPath.Substring(localPath.LastIndexOf('/') + 1);
    }
}
