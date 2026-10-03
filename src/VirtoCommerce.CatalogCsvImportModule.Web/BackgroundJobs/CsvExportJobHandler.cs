using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Omu.ValueInjecter;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Web.Model.PushNotifications;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.Platform.Core.PushNotifications;
using CsvModuleConstants = VirtoCommerce.CatalogCsvImportModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogCsvImportModule.Web.BackgroundJobs;

public class CsvExportJobPayload
{
    public CsvExportInfo ExportInfo { get; set; }

    public ExportNotification Notification { get; set; }
}

/// <summary>
/// Exports a catalog to CSV via <see cref="CsvExportRunner"/> and reports progress through the push notification
/// returned to the admin UI.
/// </summary>
public class CsvExportJobHandler(
    CsvExportRunner exportRunner,
    IPushNotificationManager pushNotificationManager,
    IDistributedLock distributedLock,
    ILogger<CsvExportJobHandler> logger)
    : IBackgroundJobHandler<CsvExportJobPayload>
{
    /// <remarks>
    /// Replaces Hangfire's [DisableConcurrentExecution(ExportLockKey, ExportLockTimeoutSeconds)] with the same lock
    /// resource and wait: exports run one at a time across the worker fleet. A queued export waits up to the timeout and
    /// then fails, and the engine retries it.
    /// </remarks>
    public virtual Task Execute(CsvExportJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return distributedLock.ExecuteAsync(
            CsvModuleConstants.BackgroundJobs.ExportLockKey,
            lockToken => ExportAsync(payload.ExportInfo, payload.Notification, lockToken),
            TimeSpan.FromSeconds(CsvModuleConstants.BackgroundJobs.ExportLockTimeoutSeconds),
            cancellationToken);
    }

    protected virtual async Task ExportAsync(CsvExportInfo exportInfo, ExportNotification notifyEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting background export process for catalog {CatalogId}", exportInfo.CatalogId);

        try
        {
            notifyEvent.DownloadUrl = await exportRunner.ExportAsync(exportInfo, ProgressCallback, cancellationToken);
            notifyEvent.Description = "Export finished";

            logger.LogInformation("Export process completed for catalog {CatalogId}", exportInfo.CatalogId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Export process canceled for catalog {CatalogId}", exportInfo.CatalogId);

            notifyEvent.Description = "Export canceled";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export process failed for catalog {CatalogId}", exportInfo.CatalogId);

            notifyEvent.Description = "Export failed";
            notifyEvent.Errors.Add(ex.ExpandExceptionMessage());
        }
        finally
        {
            notifyEvent.Finished = DateTime.UtcNow;
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
