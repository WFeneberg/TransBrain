import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { SYSTEM_TENANT_STORAGE_KEY } from '../api/system';

/**
 * Reactive view over the system administrator's tenant choice. The value itself lives in
 * sessionStorage (see api/system.ts) so a reload inside one session keeps the choice while a
 * fresh sign-in starts neutral; this store only makes it bindable for the maintenance shell.
 */
export const useSystemTenantStore = defineStore('systemTenant', () => {
    const selected = ref<string | null>(sessionStorage.getItem(SYSTEM_TENANT_STORAGE_KEY));

    const hasSelection = computed(() => selected.value !== null);

    function select(tenantId: string | null): void {
        if (tenantId === null) {
            sessionStorage.removeItem(SYSTEM_TENANT_STORAGE_KEY);
        } else {
            sessionStorage.setItem(SYSTEM_TENANT_STORAGE_KEY, tenantId);
        }
        selected.value = tenantId;
    }

    return { selectedTenantId: selected, hasSelection, select };
});
