using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Omu.ValueInjecter;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.PushNotifications;
using CsvModuleConstants = VirtoCommerce.CatalogCsvImportModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogCsvImportModule.Web.BackgroundJobs;

public class CsvImportJobPayload
{
    public CsvImportInfo ImportInfo { get; set; }

    public ImportNotification Notification { get; set; }
}

/// <summary>
/// Imports a catalog from a CSV file and reports progress through the push notification returned to the admin UI.
/// </summary>
public class CsvImportJobHandler(
    IBlobStorageProvider blobStorageProvider,
    ICsvCatalogImporter csvImporter,
    IPushNotificationManager pushNotificationManager,
    IDistributedLock distributedLock,
    ILogger<CsvImportJobHandler> logger)
    : IBackgroundJobHandler<CsvImportJobPayload>
{
    /// <remarks>
    /// Replaces Hangfire's [DisableConcurrentExecution(ImportLockKey, ImportLockTimeoutSeconds)] with the same lock
    /// resource and wait: imports run one at a time across the worker fleet, so two imports touching the same SKU cannot
    /// overwrite each other. A queued import waits up to the timeout and then fails, and the engine retries it.
    /// </remarks>
    public virtual Task Execute(CsvImportJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return distributedLock.ExecuteAsync(
            CsvModuleConstants.BackgroundJobs.ImportLockKey,
            lockToken => ImportAsync(payload.ImportInfo, payload.Notification, lockToken),
            TimeSpan.FromSeconds(CsvModuleConstants.BackgroundJobs.ImportLockTimeoutSeconds),
            cancellationToken);
    }

    protected virtual async Task ImportAsync(CsvImportInfo importInfo, ImportNotification notifyEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting background import process for file {FileUrl}", importInfo.FileUrl);

        var canceled = false;

        await using var stream = await blobStorageProvider.OpenReadAsync(importInfo.FileUrl);
        try
        {
            await csvImporter.DoImportAsync(stream, importInfo, ProgressCallback, cancellationToken);

            logger.LogInformation("Import process completed for file {FileUrl}", importInfo.FileUrl);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            logger.LogWarning("Import process canceled for file {FileUrl}", importInfo.FileUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import process failed for file {FileUrl}", importInfo.FileUrl);

            notifyEvent.Errors.Add(ex.ToString());
        }
        finally
        {
            notifyEvent.Finished = DateTime.UtcNow;
            notifyEvent.Description = canceled
                ? "Import canceled"
                : "Import finished" + (notifyEvent.Errors.Count > 0 ? " with errors" : " successfully");
            await pushNotificationManager.SendAsync(notifyEvent);
        }

        return;

        void ProgressCallback(ExportImportProgressInfo x)
        {
            notifyEvent.InjectFrom(x);
            pushNotificationManager.SendAsync(notifyEvent);
        }
    }
}
