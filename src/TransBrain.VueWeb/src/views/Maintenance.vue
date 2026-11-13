<script setup lang="ts">
import { computed, ref } from 'vue';
import axios from 'axios';
import { useI18n } from '../i18n';
import {
    BACKUP_FILE_FORMAT,
    downloadBackup,
    restoreBackup,
    saveBackup,
    type RestoreResult,
    type TenantBackup,
} from '../api/maintenance';

const { t, locale } = useI18n();

const downloading = ref(false);
const backupError = ref<string | null>(null);

const chosenFile = ref<File | null>(null);
const pendingBackup = ref<TenantBackup | null>(null);
const confirmed = ref(false);
const restoring = ref(false);
const restoreError = ref<string | null>(null);
const result = ref<RestoreResult | null>(null);

const fileInfo = computed(() => {
    if (!pendingBackup.value) {
        return null;
    }
    return {
        createdAt: new Date(pendingBackup.value.createdAt).toLocaleString(locale.value),
        tables: Object.keys(pendingBackup.value.tables).length,
    };
});

const restoredTables = computed(() =>
    Object.entries(result.value?.tables ?? {})
        .filter(([, rows]) => rows > 0)
        .map(([table, rows]) => ({ table, rows }))
        .sort((a, b) => a.table.localeCompare(b.table)),
);

async function download(): Promise<void> {
    downloading.value = true;
    backupError.value = null;
    try {
        const { blob, fileName } = await downloadBackup();
        saveBackup(blob, fileName);
    } catch (error) {
        backupError.value = messageFrom(error, t('maintenance.backup.error'));
    } finally {
        downloading.value = false;
    }
}

async function onFileChosen(file: File | File[] | null): Promise<void> {
    restoreError.value = null;
    result.value = null;
    pendingBackup.value = null;
    confirmed.value = false;

    const single = Array.isArray(file) ? file[0] : file;
    if (!single) {
        return;
    }

    try {
        const backup = JSON.parse(await single.text()) as TenantBackup;
        if (backup.fileFormat !== BACKUP_FILE_FORMAT || typeof backup.tables !== 'object') {
            restoreError.value = t('maintenance.restore.invalidFile');
            return;
        }
        pendingBackup.value = backup;
    } catch {
        restoreError.value = t('maintenance.restore.invalidFile');
    }
}

async function restore(): Promise<void> {
    if (!pendingBackup.value) {
        return;
    }
    restoring.value = true;
    restoreError.value = null;
    result.value = null;
    try {
        result.value = await restoreBackup(pendingBackup.value);
        pendingBackup.value = null;
        chosenFile.value = null;
        confirmed.value = false;
    } catch (error) {
        restoreError.value = messageFrom(error, t('maintenance.restore.error'));
    } finally {
        restoring.value = false;
    }
}

// Prefers the API's problem details - its messages name the exact reason - over a generic text.
function messageFrom(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as
            | { title?: string; detail?: string; errors?: Record<string, string[]> }
            | undefined;
        const validationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
        return validationMessage ?? problem?.detail ?? fallback;
    }
    return fallback;
}
</script>

<template>
    <v-container class="page-narrow">
        <header class="mb-4">
            <h1 class="text-h4">{{ t('maintenance.title') }}</h1>
            <p class="text-medium-emphasis">{{ t('maintenance.subtitle') }}</p>
        </header>

        <v-card variant="outlined" class="mb-4">
            <v-card-item prepend-icon="mdi-cloud-download-outline" :title="t('maintenance.backup.title')" />
            <v-card-text>
                <p>{{ t('maintenance.backup.description') }}</p>
                <v-alert
                    v-if="backupError"
                    type="error"
                    variant="tonal"
                    class="mt-3"
                    data-testid="maintenance-backup-error"
                >
                    {{ backupError }}
                </v-alert>
            </v-card-text>
            <v-card-actions>
                <v-btn
                    color="primary"
                    variant="flat"
                    prepend-icon="mdi-download"
                    data-testid="maintenance-download"
                    :loading="downloading"
                    @click="download"
                >
                    {{ t('maintenance.backup.download') }}
                </v-btn>
            </v-card-actions>
        </v-card>

        <v-card variant="outlined">
            <v-card-item prepend-icon="mdi-backup-restore" :title="t('maintenance.restore.title')" />
            <v-card-text>
                <p class="mb-3">{{ t('maintenance.restore.description') }}</p>

                <v-file-input
                    v-model="chosenFile"
                    accept=".json,application/json"
                    :label="t('maintenance.restore.selectFile')"
                    prepend-icon="mdi-folder-open-outline"
                    variant="outlined"
                    density="comfortable"
                    hide-details="auto"
                    data-testid="maintenance-file-input"
                    @update:model-value="onFileChosen"
                />

                <template v-if="pendingBackup && fileInfo">
                    <p class="mt-3" data-testid="maintenance-file-info">
                        {{
                            t('maintenance.restore.fileInfo', {
                                createdAt: fileInfo.createdAt,
                                tables: fileInfo.tables,
                            })
                        }}
                    </p>

                    <v-alert type="warning" variant="tonal" class="mt-3" data-testid="maintenance-warning">
                        {{ t('maintenance.restore.warning') }}
                    </v-alert>

                    <v-checkbox
                        v-model="confirmed"
                        :label="t('maintenance.restore.confirm')"
                        hide-details
                        data-testid="maintenance-confirm"
                    />
                </template>

                <v-alert
                    v-if="restoreError"
                    type="error"
                    variant="tonal"
                    class="mt-3"
                    data-testid="maintenance-restore-error"
                >
                    {{ restoreError }}
                </v-alert>

                <div v-if="result" class="mt-4" data-testid="maintenance-result">
                    <v-alert type="success" variant="tonal" class="mb-3">
                        {{ t('maintenance.restore.success', { rows: result.totalRows }) }}
                    </v-alert>
                    <v-table density="compact">
                        <thead>
                            <tr>
                                <th>{{ t('maintenance.restore.table') }}</th>
                                <th class="text-right">{{ t('maintenance.restore.rows') }}</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="entry in restoredTables" :key="entry.table">
                                <td>{{ entry.table }}</td>
                                <td class="text-right">{{ entry.rows }}</td>
                            </tr>
                        </tbody>
                    </v-table>
                </div>
            </v-card-text>
            <v-card-actions>
                <v-btn
                    color="error"
                    variant="flat"
                    prepend-icon="mdi-restore-alert"
                    data-testid="maintenance-restore"
                    :disabled="!pendingBackup || !confirmed"
                    :loading="restoring"
                    @click="restore"
                >
                    {{ t('maintenance.restore.start') }}
                </v-btn>
            </v-card-actions>
        </v-card>
    </v-container>
</template>

<style scoped>
.page-narrow {
    max-width: 760px;
}
</style>
