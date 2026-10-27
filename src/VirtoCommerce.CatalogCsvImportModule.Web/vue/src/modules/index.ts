import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, useExtensionPoint } from "@vc-shell/framework";
import { markRaw } from "vue";
import type { CatalogToolbarCommand } from "./toolbar";
import "./styles.scss";

// CSV export/import on the Catalog list toolbar (legacy: catalogExportService/catalogImportService registrations)
const none = markRaw({ render: () => null });
const toolbar = useExtensionPoint("catalog-items-toolbar");
toolbar.add({
  id: "csv-export",
  priority: 10,
  component: none,
  meta: {
    title: "CSVIMPORT.EXPORT",
    icon: "lucide-download",
    permissions: "catalog:export",
    // legacy canExecute: a catalog is open (not yet loaded, or a search across catalogs: none)
    disabled: (ctx) => !ctx.catalog?.id,
    onClick: (ctx) =>
      ctx.openBlade({
        name: "CsvCatalogExport",
        options: {
          catalog: ctx.catalog,
          categoryIds: ctx.selection.filter((e) => e.type === "category").map((e) => e.id),
          productIds: ctx.selection.filter((e) => e.type === "product").map((e) => e.id),
        },
      }),
  } satisfies CatalogToolbarCommand,
});
toolbar.add({
  id: "csv-import",
  priority: 20,
  component: none,
  meta: {
    title: "CSVIMPORT.IMPORT",
    icon: "lucide-upload",
    permissions: "catalog:import",
    isVisible: (ctx) => !ctx.catalog?.isVirtual,
    disabled: (ctx) => !ctx.catalog?.id,
    onClick: (ctx) => ctx.openBlade({ name: "CsvCatalogImport", options: { catalog: ctx.catalog } }),
  } satisfies CatalogToolbarCommand,
});

export default defineAppModule({
  blades,
  locales,
  // legacy: menu/history notification templates of the export/import jobs
  notifications: {
    CatalogCsvExport: { toast: { mode: "progress" } },
    CatalogCsvImport: { toast: { mode: "progress" } },
  },
});

export * from "./pages";
