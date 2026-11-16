<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

const auth = useAuthStore();
const { t } = useI18n();
const route = useRoute();

// Selection is derived from the path instead of relying on router-link's active matching -
// the same approach as the Wartung shell.
const tab = computed(() =>
    route.path.startsWith('/settings/geodata') ? 'geodata' : 'invoiceDesign',
);
</script>

<!--
    Shell of the Einstellungen (settings) area: the tenant-level configuration pages that used to
    sit as separate toolbar entries. One tab per page - the invoice design editor stays admin-only
    like its route guard, so its tab only shows with masterData.write; the geodata maintenance is
    readable by every signed-in role, so its tab always shows.
-->
<template>
    <div v-if="auth.isAuthenticated">
        <v-container class="py-0">
            <v-tabs :model-value="tab" color="primary">
                <v-tab
                    v-if="auth.can('masterData.write')"
                    value="invoiceDesign"
                    to="/settings/invoice-design"
                    data-testid="settings-tab-invoice-design"
                    >{{ t('settings.tabs.invoiceDesign') }}</v-tab
                >
                <v-tab value="geodata" to="/settings/geodata" data-testid="settings-tab-geodata">{{
                    t('settings.tabs.geodata')
                }}</v-tab>
            </v-tabs>
            <v-divider />
        </v-container>
        <router-view />
    </div>
</template>
