// The Catalog list's toolbar extension point "catalog-items-toolbar" (contract documented in the Catalog
// plugin, vc-module-catalog .../vue/src/modules/pages/browse.vue). Types repeated here: the contract is by shape.

export interface Catalog {
  id?: string;
  name?: string;
  isVirtual?: boolean;
}

export interface ListEntry {
  id?: string;
  type?: string;
  name?: string;
  code?: string;
  catalogId?: string;
}

export interface CatalogToolbarContext {
  catalog?: Catalog;
  category?: { id: string; name?: string };
  selection: ListEntry[];
  allSelected: boolean;
  searchCriteria: Record<string, unknown>;
  openBlade: (event: { name: string; param?: string; options?: Record<string, unknown> }) => Promise<void>;
  reload: () => Promise<void>;
}

export interface CatalogToolbarCommand {
  title: string;
  icon?: string;
  permissions?: string | string[];
  isVisible?: (ctx: CatalogToolbarContext) => boolean;
  disabled?: (ctx: CatalogToolbarContext) => boolean;
  onClick: (ctx: CatalogToolbarContext) => unknown;
}
