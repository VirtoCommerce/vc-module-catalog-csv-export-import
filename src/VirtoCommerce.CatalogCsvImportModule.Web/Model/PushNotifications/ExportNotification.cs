using Newtonsoft.Json;

namespace VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;

/// <summary>
///  Notification for catalog data export job.
/// </summary>
public class ExportNotification : JobNotificationBase
{
    public ExportNotification(string creator)
        : base(creator)
    {
        NotifyType = "CatalogCsvExport";
    }

    /// <summary>
    /// Gets or sets the URL for downloading exported data.
    /// </summary>
    /// <value>
    /// The download URL.
    /// </value>
    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; }
}
