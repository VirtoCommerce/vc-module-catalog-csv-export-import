import { computed, getCurrentScope, onScopeDispose, ref } from "vue";
import { JobsClient, PushNotificationClient, useApiClient, useNotificationStore, type PushNotification } from "@vc-shell/framework";

/** Push notification of a background job (CSV export/import). */
export type JobNotification = PushNotification & {
  jobId?: string;
  finished?: Date | string | null;
  totalCount?: number;
  processedCount?: number;
  errorCount?: number;
  errors?: string[];
  downloadUrl?: string;
};

/**
 * The Catalog Publishing module's job follower (separate repo, no shared package).
 * Follows a background job through its push notification, the way the AngularJS blades did with
 * "new-notification-event": live updates come from the host's SignalR notification store; a 2s poll of
 * the notification API covers messages sent before we knew the id and types the store doesn't ingest.
 * The platform job API is polled too: a job that dies before it reports (state Failed/Deleted) would
 * otherwise leave the notification "running" forever.
 */
export function useJob<T extends JobNotification = JobNotification>() {
  const store = useNotificationStore();
  const { getApiClient } = useApiClient(PushNotificationClient);
  const { getApiClient: getJobsClient } = useApiClient(JobsClient);
  const started = ref<T>();
  const polled = ref<T>();
  const deadState = ref<string>();
  let timer: ReturnType<typeof setInterval> | undefined;

  const live = computed(() => store.history.value.find((x) => x.id && x.id === started.value?.id) as T | undefined);

  // the most advanced snapshot wins: finished first, then the most processed
  const job = computed<T | undefined>(() => {
    const candidates = [live.value, polled.value, started.value].filter(Boolean) as T[];
    const best = candidates.find((x) => x.finished) ?? candidates.sort((a, b) => (b.processedCount ?? 0) - (a.processedCount ?? 0))[0];
    if (!best || best.finished || !deadState.value) return best;
    const error = `Background job #${best.jobId}: ${deadState.value}`;
    return { ...best, finished: new Date(), description: error, errors: [...(best.errors ?? []), error], errorCount: (best.errorCount ?? 0) + 1 };
  });
  const finished = computed(() => !!job.value?.finished);
  const percent = computed(() =>
    job.value?.totalCount ? Math.min(100, Math.round(((job.value.processedCount ?? 0) / job.value.totalCount) * 100)) : 0,
  );

  function stop() {
    clearInterval(timer);
    timer = undefined;
  }

  async function poll() {
    if (!started.value?.id || finished.value) return stop();
    try {
      const client = await getApiClient();
      const result = await client.searchPushNotification({ ids: [started.value.id], take: 1 });
      if (result.notifyEvents?.[0]) polled.value = result.notifyEvents[0] as T;
      const jobId = job.value?.jobId;
      if (jobId && !job.value?.finished) {
        const state = (await (await getJobsClient()).getStatus(jobId)).state;
        if (state === "Failed" || state === "Deleted") deadState.value = state;
      }
    } catch {
      // transient: the next tick retries
    }
    if (finished.value) stop();
  }

  function follow(notification: T) {
    stop();
    polled.value = undefined;
    deadState.value = undefined;
    started.value = notification;
    timer = setInterval(poll, 2000);
  }

  if (getCurrentScope()) onScopeDispose(stop);

  return { job, finished, percent, follow };
}
