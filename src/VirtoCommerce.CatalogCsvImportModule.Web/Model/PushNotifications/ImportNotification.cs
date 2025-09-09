using VirtoCommerce.Platform.Core.Swagger;

namespace VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;

/// <summary>
///  Notification for catalog data import job.
/// </summary>
[SwaggerSchemaId("CatalogCsvImportNotification")]
public class ImportNotification : JobNotificationBase
{
    public ImportNotification(string creator)
        : base(creator)
    {
        NotifyType = "CatalogCsvImport";
    }
}
