<template>
  <VcBlade
    :title="$t('CSVIMPORT.EXPORT_TITLE')"
    :subtitle="options?.catalog?.name"
    :toolbar-items="toolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <VcForm class="csv-form">
        <VcBanner
          v-if="options?.catalog?.isVirtual"
          variant="warning"
        >
          {{ $t("CSVIMPORT.VIRTUAL") }}
        </VcBanner>
        <template v-else>
          <div class="csv-hint">
            {{ scope }}
          </div>
          <VcSelect
            v-model="delimiter"
            :label="$t('CSVIMPORT.DELIMITER')"
            :options="delimiters"
            :clearable="false"
          />
          <VcSelect
            v-model="fulfillmentCenterId"
            :label="$t('CSVIMPORT.FULFILLMENT_CENTER')"
            :options="searchCenters"
            option-value="id"
            option-label="name"
            searchable
          />
          <VcSelect
            v-model="pricelistId"
            :label="$t('CSVIMPORT.PRICELIST')"
            :options="searchPricelists"
            option-value="id"
            option-label="name"
            searchable
          />
          <JobProgress
            v-if="job"
            :job="job"
            :finished="finished"
            :percent="percent"
            prefix="CSVIMPORT"
          >
            <a
              v-if="job.downloadUrl"
              :href="job.downloadUrl"
              target="_blank"
              rel="noopener"
            >{{ $t("CSVIMPORT.JOB.DOWNLOAD") }}</a>
          </JobProgress>
        </template>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcContainer, VcForm, VcSelect } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { delimiters, http } from "../api";
import JobProgress from "../components/JobProgress.vue";
import { useJob, type JobNotification } from "../components/useJob";
import type { Catalog } from "../toolbar";

// VirtoCommerce.CatalogCsvImportModule export (legacy catalogCSVexportController), called through its API
// not routable: the catalog arrives only through options, which a page reload doesn't restore
defineBlade({ url: "/csv-export", name: "CsvCatalogExport", permissions: ["catalog:export"], routable: false });

type Page = { results?: { id: string; name: string }[]; totalCount?: number };

const { t } = useI18n({ useScope: "global" });
const { options } = useBlade<{ catalog: Catalog; categoryIds?: string[]; productIds?: string[] }>();
const { job, finished, percent, follow } = useJob();

const delimiter = ref(",");
const fulfillmentCenterId = ref<string>();
const pricelistId = ref<string>();
const loading = ref(false);

const scope = computed(() => {
  const c = options.value?.categoryIds?.length ?? 0;
  const p = options.value?.productIds?.length ?? 0;
  return c || p ? t("CSVIMPORT.SCOPE_SELECTED", { categories: c, products: p }) : t("CSVIMPORT.SCOPE_ALL");
});

const searchCenters = (keyword?: string, skip = 0, ids?: string[]) =>
  http<Page>("POST", "/api/inventory/fulfillmentcenters/search", { keyword, objectIds: ids, skip, take: 20 }).catch(
    () => ({ results: [], totalCount: 0 }),
  );
const searchPricelists = (keyword?: string, skip = 0) =>
  http<Page>("GET", `/api/pricing/pricelists?keyword=${encodeURIComponent(keyword ?? "")}&skip=${skip}&take=20`).catch(
    () => ({ results: [], totalCount: 0 }),
  );

const toolbar = computed<IBladeToolbar[]>(() => [
  {
    id: "start",
    title: t("CSVIMPORT.START_EXPORT"),
    icon: "lucide-play",
    permissions: "catalog:export",
    disabled: !options.value?.catalog?.id || !!options.value.catalog.isVirtual || (!!job.value && !finished.value),
    async clickHandler() {
      loading.value = true;
      try {
        const configuration = await http("GET", `/api/catalogcsvimport/export/mappingconfiguration?delimiter=${encodeURIComponent(delimiter.value)}`);
        follow(
          await http<JobNotification>("POST", "/api/catalogcsvimport/export", {
            catalogId: options.value?.catalog?.id,
            categoryIds: options.value?.categoryIds ?? [],
            productIds: options.value?.productIds ?? [],
            fulfilmentCenterId: fulfillmentCenterId.value,
            priceListId: pricelistId.value,
            configuration,
          }),
        );
      } finally {
        loading.value = false;
      }
    },
  },
]);
</script>
