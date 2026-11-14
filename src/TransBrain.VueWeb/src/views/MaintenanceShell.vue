<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useSystemTenantStore } from '../stores/systemTenant';
import { getTenants, type TenantSummary } from '../api/system';
import { useI18n } from '../i18n';
import { vNativeTestid } from '../directives/nativeTestId';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const auth = useAuthStore();
const tenant = useSystemTenantStore();
const { t } = useI18n();
const route = useRoute();

const tenants = ref<TenantSummary[]>([]);
const loadError = ref(false);
const manualId = ref('');
const manualInvalid = ref(false);

const tenantOptions = computed(() =>
    tenants.value.map((entry) => ({
        title: `${entry.tenantId} · ${t('maintenance.tenant.rows', { rows: entry.totalRows })}`,
        value: entry.tenantId,
    })),
);

onMounted(async () => {
    try {
        tenants.value = await getTenants();
    } catch {
        loadError.value = true;
    }
});

function choose(tenantId: string | null): void {
    if (tenantId !== null) {
        manualId.value = '';
        manualInvalid.value = false;
        tenant.select(tenantId);
    }
}

function applyManual(): void {
    const id = manualId.value.trim();
    if (!GUID_PATTERN.test(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) {
        manualInvalid.value = true;
        return;
    }
    manualInvalid.value = false;
    tenant.select(id.toLowerCase());
}

// Selection is derived from the path instead of relying on router-link's active matching,
// which would mark the backup tab active on /maintenance/import too (prefix match).
const tab = computed(() => {
    if (route.path.startsWith('/maintenance/import/verlauf')) {
        return 'history';
    }
    if (route.path.startsWith('/maintenance/export')) {
        return 'export';
    }
    return route.path.startsWith('/maintenance/import') ? 'import' : 'backup';
});
</script>

<!--
    Shell of the global Wartung (maintenance) area, reserved for system administrators. A system
    admin belongs to no tenant, so the shell's first job is choosing the tenant to work ON: picked
    from the list of tenants owning data, or entered as a GUID for a tenant that has none yet.
    Only with a choice made do the tool tabs (backup/restore, import, history, export) and their
    pages appear - every one of them acts on exactly the chosen tenant.
-->
<template>
    <div v-if="auth.isAuthenticated">
        <v-container>
            <v-card variant="outlined" class="mb-6">
                <v-card-title>{{ t('maintenance.tenant.title') }}</v-card-title>
                <v-card-subtitle class="text-wrap">{{ t('maintenance.tenant.description') }}</v-card-subtitle>
                <v-card-text>
                    <p v-if="loadError" class="text-error" data-testid="system-tenant-error">
                        {{ t('maintenance.tenant.loadError') }}
                    </p>
                    <div class="d-flex flex-wrap align-baseline ga-4">
                        <v-select
                            :model-value="tenant.selectedTenantId"
                            :items="tenantOptions"
                            :label="t('maintenance.tenant.select')"
                            :no-data-text="t('maintenance.tenant.none')"
                            variant="outlined"
                            density="compact"
                            class="tenant-select"
                            hide-details
                            data-testid="system-tenant-select"
                            @update:model-value="choose"
                        />
                        <span class="text-medium-emphasis">{{ t('maintenance.tenant.or') }}</span>
                        <v-text-field
                            v-model="manualId"
                            v-native-testid="'system-tenant-manual'"
                            :label="t('maintenance.tenant.manual')"
                            :error-messages="manualInvalid ? [t('maintenance.tenant.invalid')] : []"
                            placeholder="00000000-0000-0000-0000-000000000000"
                            variant="outlined"
                            density="compact"
                            class="tenant-manual"
                        />
                        <v-btn
                            variant="outlined"
                            data-testid="system-tenant-apply"
                            @click="applyManual"
                        >
                            {{ t('maintenance.tenant.apply') }}
                        </v-btn>
                    </div>
                    <p
                        v-if="tenant.hasSelection"
                        class="font-weight-medium mb-0 mt-2"
                        data-testid="system-tenant-active"
                    >
                        {{ t('maintenance.tenant.current', { id: tenant.selectedTenantId! }) }}
                    </p>
                </v-card-text>
            </v-card>
        </v-container>
        <template v-if="tenant.hasSelection">
            <v-container class="py-0">
                <v-tabs :model-value="tab" color="primary">
                    <v-tab value="backup" to="/maintenance" data-testid="maintenance-tab-backup">{{
                        t('maintenance.tabs.backup')
                    }}</v-tab>
                    <v-tab value="import" to="/maintenance/import" data-testid="maintenance-tab-import">{{
                        t('maintenance.tabs.import')
                    }}</v-tab>
                    <v-tab
                        value="history"
                        to="/maintenance/import/verlauf"
                        data-testid="maintenance-tab-history"
                        >{{ t('maintenance.tabs.history') }}</v-tab
                    >
                    <v-tab value="export" to="/maintenance/export" data-testid="maintenance-tab-export">{{
                        t('maintenance.tabs.export')
                    }}</v-tab>
                </v-tabs>
                <v-divider />
            </v-container>
            <router-view />
        </template>
        <p
            v-else
            class="text-center text-medium-emphasis mt-12"
            data-testid="system-tenant-required"
        >
            {{ t('maintenance.tenant.required') }}
        </p>
    </div>
</template>

<style scoped>
.tenant-select,
.tenant-manual {
    min-width: 340px;
    flex: 0 1 auto;
}
</style>
