<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useI18n } from '../i18n';
import {
    downloadExport,
    getExportTargets,
    saveExport,
    type ExportFormat,
    type ExportTarget,
} from '../api/exports';

const { t } = useI18n();

const targets = ref<ExportTarget[]>([]);
const target = ref<string | null>(null);
const format = ref<ExportFormat>('Csv');
const downloading = ref(false);
const error = ref<string | null>(null);

const targetItems = computed(() =>
    targets.value.map((item) => ({
        value: item.entity,
        title: `${item.label} (${t('export.rows', { rows: item.rowCount })})`,
    })),
);

const formatItems = [
    { value: 'Csv', title: 'CSV' },
    { value: 'Xlsx', title: 'Excel (XLSX)' },
    { value: 'Json', title: 'JSON' },
];

onMounted(async () => {
    try {
        targets.value = await getExportTargets();
        target.value = targets.value[0]?.entity ?? null;
    } catch {
        error.value = t('export.error');
    }
});

async function download(): Promise<void> {
    if (!target.value) {
        return;
    }
    downloading.value = true;
    error.value = null;
    try {
        const { blob, fileName } = await downloadExport(target.value, format.value);
        saveExport(blob, fileName);
    } catch {
        error.value = t('export.error');
    } finally {
        downloading.value = false;
    }
}
</script>

<!--
    The Export tab of the Wartung area: pick a master-data set (Kunden, Fahrer, Fahrzeuge) and a
    file format, then download it. The files are the mirror image of the import - CSV and Excel
    carry the import wizard's German column labels, JSON its field names - so every export can be
    played back through the Import tab without manual column mapping.
-->
<template>
    <v-container class="page-narrow">
        <header class="mb-4">
            <h1 class="text-h4">{{ t('export.title') }}</h1>
            <p class="text-medium-emphasis">{{ t('export.subtitle') }}</p>
        </header>

        <v-card variant="outlined">
            <v-card-text>
                <p class="mb-4">{{ t('export.description') }}</p>

                <v-row dense>
                    <v-col cols="12" md="6">
                        <v-select
                            v-model="target"
                            :items="targetItems"
                            :label="t('export.target')"
                            variant="outlined"
                            density="comfortable"
                            hide-details
                            data-testid="export-target-select"
                        />
                    </v-col>
                    <v-col cols="12" md="6">
                        <v-select
                            v-model="format"
                            :items="formatItems"
                            :label="t('export.format')"
                            variant="outlined"
                            density="comfortable"
                            hide-details
                            data-testid="export-format-select"
                        />
                    </v-col>
                </v-row>

                <p class="text-medium-emphasis text-body-2 mt-4 mb-0">{{ t('export.hint') }}</p>

                <v-alert v-if="error" type="error" variant="tonal" class="mt-3" data-testid="export-error">
                    {{ error }}
                </v-alert>
            </v-card-text>
            <v-card-actions>
                <v-btn
                    color="primary"
                    variant="flat"
                    prepend-icon="mdi-download"
                    :loading="downloading"
                    :disabled="!target"
                    data-testid="export-download"
                    @click="download"
                >
                    {{ t('export.download') }}
                </v-btn>
            </v-card-actions>
        </v-card>
    </v-container>
</template>
