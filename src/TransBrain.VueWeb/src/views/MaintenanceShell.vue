<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

const auth = useAuthStore();
const { t } = useI18n();
const route = useRoute();

// Selection is derived from the path instead of relying on router-link's active matching,
// which would mark the backup tab active on /maintenance/import too (prefix match).
const tab = computed(() => {
    if (route.path.startsWith('/maintenance/import/verlauf')) {
        return 'history';
    }
    return route.path.startsWith('/maintenance/import') ? 'import' : 'backup';
});
</script>

<!--
    Shell of the Wartung (maintenance) area: one tab bar over the admin tooling - backup/restore,
    the import wizard and the import history. The tabs mirror the route guards: Datensicherung
    needs the maintenance capability, both import tabs need masterData.write. In practice only
    admins hold either, so the whole area is admin territory.
-->
<template>
    <div v-if="auth.isAuthenticated">
        <v-container class="py-0">
            <v-tabs :model-value="tab" color="primary">
                <v-tab
                    v-if="auth.can('maintenance')"
                    value="backup"
                    to="/maintenance"
                    data-testid="maintenance-tab-backup"
                    >{{ t('maintenance.tabs.backup') }}</v-tab
                >
                <v-tab
                    v-if="auth.can('masterData.write')"
                    value="import"
                    to="/maintenance/import"
                    data-testid="maintenance-tab-import"
                    >{{ t('maintenance.tabs.import') }}</v-tab
                >
                <v-tab
                    v-if="auth.can('masterData.write')"
                    value="history"
                    to="/maintenance/import/verlauf"
                    data-testid="maintenance-tab-history"
                    >{{ t('maintenance.tabs.history') }}</v-tab
                >
            </v-tabs>
            <v-divider />
        </v-container>
        <router-view />
    </div>
</template>
