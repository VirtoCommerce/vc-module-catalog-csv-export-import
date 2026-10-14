<template>
  <VcBlade
    :title="$t('CSVIMPORT.IMPORT_TITLE')"
    :subtitle="options?.catalog?.name"
    :toolbar-items="toolbar"
    :loading="loading"
    width="60%"
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
          <VcSelect
            v-model="delimiter"
            :label="$t('CSVIMPORT.DELIMITER')"
            :options="delimiters"
            :clearable="false"
            @update:model-value="loadMapping"
          />
          <VcFileUpload
            variant="file-upload"
            accept=".csv"
            :custom-text="{ dragHere: $t('CSVIMPORT.DROP'), browse: $t('CSVIMPORT.BROWSE') }"
            @upload="upload"
          />
          <div
            v-if="file"
            class="csv-hint"
          >
            {{ $t("CSVIMPORT.FILE", { name: file.name }) }}
          </div>

          <VcCard
            v-if="config"
            :header="$t('CSVIMPORT.MAPPING')"
            is-collapsable
          >
            <div class="csv-card">
              <VcSelect
                v-model="config.propertyCsvColumns"
                :label="$t('CSVIMPORT.PROPERTY_COLUMNS')"
                :options="columns"
                multiple
              />
              <VcRow
                v-for="map in config.propertyMaps"
                :key="map.entityColumnName"
                class="tw-gap-4"
              >
                <VcCol>
                  <VcField
                    :label="$t('CSVIMPORT.FIELD')"
                    :model-value="map.entityColumnName + (map.isRequired ? ' *' : '')"
                  />
                </VcCol>
                <VcCol>
                  <VcSelect
                    v-model="map.csvColumnName"
                    :label="$t('CSVIMPORT.COLUMN')"
                    :options="columns"
                  />
                </VcCol>
                <VcCol>
                  <VcInput
                    v-model="map.customValue"
                    :label="$t('CSVIMPORT.CUSTOM_VALUE')"
                  />
                </VcCol>
              </VcRow>
            </div>
          </VcCard>
          <JobProgress
            v-if="job"
            :job="job"
            :finished="finished"
            :percent="percent"
            prefix="CSVIMPORT"
          />
        </template>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useAsync, useBlade, type IBladeToolbar } from "@vc-shell/framework";
import {
  VcBanner,
  VcBlade,
  VcCard,
  VcCol,
  VcContainer,
  VcField,
  VcFileUpload,
  VcForm,
  VcInput,
  VcRow,
  VcSelect,
} from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { delimiters, http, options as toOptions } from "../api";
import JobProgress from "../components/JobProgress.vue";
import { useJob, type JobNotification } from "../components/useJob";
import type { Catalog } from "../toolbar";

// VirtoCommerce.CatalogCsvImportModule import wizard + column mapping (legacy catalogCSVimportWizardController)
// not routable: the catalog arrives only through options, which a page reload doesn't restore
defineBlade({ url: "/csv-import", name: "CsvCatalogImport", permissions: ["catalog:import"], routable: false });

interface PropertyMap {
  entityColumnName: string;
  csvColumnName?: string;
  customValue?: string;
  isRequired?: boolean;
}
interface MappingConfiguration {
  eTag?: string;
  delimiter?: string;
  csvColumns?: string[];
  propertyMaps?: PropertyMap[];
  propertyCsvColumns?: string[];
}

const STORAGE_KEY = "catalog.csvImport.lastMapping";
const { t } = useI18n({ useScope: "global" });
const { options, callParent } = useBlade<{ catalog: Catalog }>();
const { job, finished, percent, follow } = useJob();

const delimiter = ref(",");
const file = ref<{ name?: string; relativeUrl?: string; url?: string }>();
const config = ref<MappingConfiguration>();
const columns = computed(() => toOptions(config.value?.csvColumns ?? []));
const mapped = computed(() =>
  (config.value?.propertyMaps ?? []).every((m) => !m.isRequired || m.csvColumnName || m.customValue),
);

// notify: false: a failure reaches the caller as before, with no extra toast
const { loading, action: busy } = useAsync(async (action?: () => Promise<void>) => action?.(), { notify: false });

async function upload(files: FileList) {
  await busy(async () => {
    const body = new FormData();
    body.append("file", files[0]);
    const res = await fetch("/api/assets?folderUrl=temp", { method: "POST", body });
    if (!res.ok) throw new Error(`upload: ${res.status}`);
    file.value = (await res.json())[0];
  });
  await loadMapping();
}

async function loadMapping() {
  if (!file.value) return;
  const fileUrl = file.value.relativeUrl ?? file.value.url ?? "";
  await busy(async () => {
    config.value = await http<MappingConfiguration>(
      "GET",
      `/api/catalogcsvimport/import/mappingconfiguration?fileUrl=${encodeURIComponent(fileUrl)}&delimiter=${encodeURIComponent(delimiter.value)}`,
    );
  });
  // restore the user's last mapping of the same columns (legacy $localStorage.lastKnownImportData)
  try {
    const last = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "null") as MappingConfiguration | null;
    if (last && config.value && last.eTag === config.value.eTag) {
      config.value.propertyMaps = last.propertyMaps;
      config.value.propertyCsvColumns = last.propertyCsvColumns;
    }
  } catch {
    // per-viewer convenience only
  }
}

const toolbar = computed<IBladeToolbar[]>(() => [
  {
    id: "start",
    title: t("CSVIMPORT.START_IMPORT"),
    icon: "lucide-play",
    permissions: "catalog:import",
    disabled: !options.value?.catalog?.id || !config.value || !mapped.value || (!!job.value && !finished.value),
    async clickHandler() {
      try {
        const c = config.value!;
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ eTag: c.eTag, propertyMaps: c.propertyMaps, propertyCsvColumns: c.propertyCsvColumns }));
      } catch {
        // ignore
      }
      await busy(async () =>
        follow(
          await http<JobNotification>("POST", "/api/catalogcsvimport/import", {
            catalogId: options.value?.catalog?.id,
            fileUrl: file.value?.relativeUrl ?? file.value?.url,
            configuration: config.value,
          }),
        ),
      );
      callParent("reload");
    },
  },
]);
</script>
