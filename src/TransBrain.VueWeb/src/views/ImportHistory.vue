<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import {
    deleteImportSession,
    listImportSessions,
    listImportTargets,
    type ImportSession,
    type ImportSessionStatus,
} from '../api/imports';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import { statusColor } from '../composables/statusColor';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

const auth = useAuthStore();
const { t, locale } = useI18n();

const sessions = ref<ImportSession[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed delete must not hide the table the way a failed list
// load does.
const actionError = ref<string | null>(null);

// Entity key → German label, decoration from /targets; without it the raw key is shown.
const targetLabels = ref(new Map<string, string>());

const deleteDialogOpen = ref(false);
const deleteTarget = ref<ImportSession | null>(null);

const headers = computed(() => [
    { title: t('import.history.source'), key: 'sourceName' },
    { title: t('common.status'), key: 'status' },
    { title: t('import.history.target'), key: 'targetEntity', value: (session: ImportSession) => targetLabel(session) },
    { title: t('import.history.importedFailed'), key: 'counts', sortable: false, align: 'end' as const },
    { title: t('import.history.createdBy'), key: 'createdBy' },
    { title: t('import.history.createdAt'), key: 'createdAtUtc' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await Promise.all([refresh(), loadTargetLabels()]);
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        sessions.value = await listImportSessions();
    } catch (error) {
        errorMessage.value = describe(error, t('import.history.loadFailed'));
    } finally {
        loading.value = false;
    }
}

async function loadTargetLabels(): Promise<void> {
    try {
        const targets = await listImportTargets();
        targetLabels.value = new Map(targets.map((target) => [target.entity, target.label]));
    } catch {
        targetLabels.value = new Map();
    }
}

function targetLabel(session: ImportSession): string {
    return session.targetEntity ? (targetLabels.value.get(session.targetEntity) ?? session.targetEntity) : '';
}

function statusLabel(status: ImportSessionStatus): string {
    return t(`import.status.${status}`);
}

function formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString(locale.value === 'de' ? 'de-DE' : 'en-US', {
        dateStyle: 'short',
        timeStyle: 'short',
    });
}

function askDelete(session: ImportSession): void {
    deleteTarget.value = session;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    if (!deleteTarget.value) {
        return;
    }
    actionError.value = null;
    try {
        await deleteImportSession(deleteTarget.value.id);
        await refresh();
    } catch (error) {
        actionError.value = describe(error, t('import.history.deleteFailed'));
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as { title?: string; detail?: string } | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container class="page-container">
        <template v-if="auth.isAuthenticated">
            <div class="page-header">
                <div>
                    <h1 class="text-h4">{{ t('import.history.title') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('import.history.subtitle') }}</p>
                </div>
            </div>

            <p v-if="actionError" class="text-error error-line" data-testid="import-history-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="import-history-error">{{ errorMessage }}</p>
            <v-card v-else border flat>
                <v-data-table
                    :headers="headers"
                    :items="sessions"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('import.history.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="import-history-table"
                >
                    <template #item.sourceName="{ item }">
                        <div class="font-weight-medium" data-testid="import-history-source">{{ item.sourceName }}</div>
                        <div class="text-caption text-medium-emphasis">{{ item.sourceType }}</div>
                    </template>
                    <template #item.status="{ item }">
                        <v-chip :color="statusColor(item.status)" size="small" variant="tonal" data-testid="import-history-status">{{
                            statusLabel(item.status)
                        }}</v-chip>
                    </template>
                    <template #item.targetEntity="{ value }">{{ value || '–' }}</template>
                    <template #item.counts="{ item }">
                        <span class="text-success">{{ item.importedCount }}</span>
                        /
                        <span :class="item.failedCount > 0 ? 'text-error' : 'text-medium-emphasis'">{{ item.failedCount }}</span>
                    </template>
                    <template #item.createdAtUtc="{ item }">{{ formatDateTime(item.createdAtUtc) }}</template>
                    <template #item.actions="{ item }">
                        <v-tooltip :text="t('common.delete')" location="top">
                            <template #activator="{ props: tooltip }">
                                <v-btn
                                    v-bind="tooltip"
                                    icon="mdi-delete"
                                    variant="text"
                                    size="small"
                                    color="error"
                                    :aria-label="t('common.delete')"
                                    data-testid="import-history-delete"
                                    @click="askDelete(item)"
                                />
                            </template>
                        </v-tooltip>
                    </template>
                </v-data-table>
            </v-card>

            <ConfirmDialog
                v-model="deleteDialogOpen"
                :title="t('import.history.deleteTitle')"
                :message="t('import.history.deleteConfirm', { name: deleteTarget?.sourceName ?? '' })"
                test-id-prefix="import-history-delete"
                @confirm="confirmDelete"
            />
        </template>
        <p v-else>{{ t('import.signInHint') }}</p>
    </v-container>
</template>
