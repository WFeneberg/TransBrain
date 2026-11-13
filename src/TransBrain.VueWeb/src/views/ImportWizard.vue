<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
    createDatabaseImportSession,
    createFileImportSession,
    executeImport,
    IMPORT_FILE_EXTENSIONS,
    IMPORT_MAX_FILE_MB,
    listImportTargets,
    previewImport,
    type ImportDatabaseProvider,
    type ImportExecution,
    type ImportFieldKind,
    type ImportFieldMapping,
    type ImportPreview,
    type ImportSession,
    type ImportSessionStatus,
    type ImportTable,
    type ImportTarget,
    type ImportTargetField,
} from '../api/imports';
import { statusColor } from '../composables/statusColor';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

/** One editable mapping line of step 3; `constantValue` stays a string because it is a text field. */
interface MappingRow {
    field: ImportTargetField;
    sourceColumn: string | null;
    constantValue: string;
}

const PREVIEW_MAX_ROWS = 20;

const auth = useAuthStore();
const router = useRouter();
const { t } = useI18n();

const step = ref(1);

// Step 1 — source.
const sourceMode = ref<'file' | 'database'>('file');
const file = ref<File | File[] | null>(null);
const provider = ref<ImportDatabaseProvider>('PostgreSql');
const connectionString = ref('');
const analyzing = ref(false);
const sourceError = ref<string | null>(null);

// The providers are product names, identical in both languages, so they carry no i18n keys.
const providerOptions = [
    { title: 'PostgreSQL', value: 'PostgreSql' },
    { title: 'SQL Server', value: 'SqlServer' },
    { title: 'MySQL', value: 'MySql' },
    { title: 'ODBC', value: 'Odbc' },
] as const;

const fileAccept = IMPORT_FILE_EXTENSIONS.join(',');
const fileHint = computed(() =>
    t('import.source.fileHint', { extensions: IMPORT_FILE_EXTENSIONS.join(', '), maxMb: IMPORT_MAX_FILE_MB }),
);

// Step 2 — table & target.
const session = ref<ImportSession | null>(null);
const targets = ref<ImportTarget[]>([]);
const targetsError = ref<string | null>(null);
const tableName = ref<string | null>(null);
const targetEntity = ref<string | null>(null);

// Step 3 — mapping. `mappingsBuiltFor` remembers which table/target pair the rows were built
// for: bouncing between steps keeps the user's edits, changing table or target re-maps.
const mappingRows = ref<MappingRow[]>([]);
const mappingsBuiltFor = ref<string | null>(null);

// Step 4 — preview.
const preview = ref<ImportPreview | null>(null);
const previewLoading = ref(false);
const previewError = ref<string | null>(null);

// Step 5 — result.
const execution = ref<ImportExecution | null>(null);
const executing = ref(false);
const executeError = ref<string | null>(null);

const tables = computed<ImportTable[]>(() => session.value?.tables ?? []);
const selectedTable = computed(() => tables.value.find((table) => table.name === tableName.value) ?? null);
const selectedTarget = computed(() => targets.value.find((target) => target.entity === targetEntity.value) ?? null);

const tableOptions = computed(() =>
    tables.value.map((table) => ({
        title: `${table.name} · ${t('import.table.rowCount', { count: table.rowCount })}`,
        value: table.name,
    })),
);

const targetOptions = computed(() => targets.value.map((target) => ({ title: target.label, value: target.entity })));

const columnOptions = computed(() => selectedTable.value?.columns ?? []);

const missingRequired = computed(() =>
    mappingRows.value
        .filter((row) => row.field.required && row.sourceColumn === null && row.constantValue.trim() === '')
        .map((row) => row.field.label),
);

/** The fields the preview table shows: exactly the ones the mapping will send. */
const previewFields = computed(() =>
    mappingRows.value
        .filter((row) => row.sourceColumn !== null || row.constantValue.trim() !== '')
        .map((row) => row.field),
);

const canExecute = computed(() => !previewLoading.value && preview.value !== null && preview.value.validRows > 0);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await loadTargets();
    }
});

async function loadTargets(): Promise<void> {
    try {
        targets.value = await listImportTargets();
    } catch (error) {
        targetsError.value = describeFailure(error, t('import.error.targets'));
    }
}

function selectedFile(): File | null {
    const value = file.value;
    return Array.isArray(value) ? (value[0] ?? null) : value;
}

async function analyze(): Promise<void> {
    sourceError.value = null;

    if (sourceMode.value === 'file') {
        const chosen = selectedFile();
        if (!chosen) {
            sourceError.value = t('import.source.fileRequired');
            return;
        }
        if (chosen.size > IMPORT_MAX_FILE_MB * 1024 * 1024) {
            sourceError.value = t('import.source.fileTooLarge', { maxMb: IMPORT_MAX_FILE_MB });
            return;
        }
        analyzing.value = true;
        try {
            adoptSession(await createFileImportSession(chosen));
        } catch (error) {
            sourceError.value = describeFailure(error, t('import.error.analyze'));
        } finally {
            analyzing.value = false;
        }
        return;
    }

    const trimmed = connectionString.value.trim();
    if (trimmed === '') {
        sourceError.value = t('import.source.connectionStringRequired');
        return;
    }
    analyzing.value = true;
    try {
        adoptSession(await createDatabaseImportSession(provider.value, trimmed));
    } catch (error) {
        // 503 is the API's "could not connect" answer; its detail is English, the UI is not.
        sourceError.value =
            axios.isAxiosError(error) && error.response?.status === 503
                ? t('import.error.databaseUnreachable')
                : describeFailure(error, t('import.error.analyze'));
    } finally {
        analyzing.value = false;
    }
}

function adoptSession(created: ImportSession): void {
    session.value = created;
    tableName.value = created.tables?.[0]?.name ?? null;
    targetEntity.value = null;
    mappingRows.value = [];
    mappingsBuiltFor.value = null;
    preview.value = null;
    execution.value = null;
    step.value = 2;
}

function goToMapping(): void {
    const table = selectedTable.value;
    const target = selectedTarget.value;
    if (!table || !target) {
        return;
    }
    const key = `${table.name}|${target.entity}`;
    if (mappingsBuiltFor.value !== key) {
        mappingRows.value = target.fields.map((field) => ({
            field,
            sourceColumn: autoMatch(field, table.columns),
            constantValue: '',
        }));
        mappingsBuiltFor.value = key;
    }
    step.value = 3;
}

/** Case-insensitive match of the field name or its German label against the source columns. */
function autoMatch(field: ImportTargetField, columns: string[]): string | null {
    const find = (candidate: string): string | null =>
        columns.find((column) => column.trim().toLowerCase() === candidate.trim().toLowerCase()) ?? null;
    return find(field.name) ?? find(field.label);
}

function mappingsPayload(): ImportFieldMapping[] {
    return mappingRows.value
        .filter((row) => row.sourceColumn !== null || row.constantValue.trim() !== '')
        .map((row) => ({
            targetField: row.field.name,
            sourceColumn: row.sourceColumn,
            constantValue: row.constantValue.trim() === '' ? null : row.constantValue.trim(),
        }));
}

async function goToPreview(): Promise<void> {
    step.value = 4;
    await loadPreview();
}

async function loadPreview(): Promise<void> {
    if (!session.value || !tableName.value || !targetEntity.value) {
        return;
    }
    preview.value = null;
    previewError.value = null;
    previewLoading.value = true;
    try {
        preview.value = await previewImport(
            session.value.id,
            tableName.value,
            targetEntity.value,
            mappingsPayload(),
            PREVIEW_MAX_ROWS,
        );
    } catch (error) {
        previewError.value = describeFailure(error, t('import.error.preview'));
    } finally {
        previewLoading.value = false;
    }
}

async function runImport(): Promise<void> {
    if (!session.value || !tableName.value || !targetEntity.value) {
        return;
    }
    step.value = 5;
    execution.value = null;
    executeError.value = null;
    executing.value = true;
    try {
        execution.value = await executeImport(session.value.id, tableName.value, targetEntity.value, mappingsPayload());
    } catch (error) {
        executeError.value = describeFailure(error, t('import.error.execute'));
    } finally {
        executing.value = false;
    }
}

function resetWizard(): void {
    step.value = 1;
    file.value = null;
    connectionString.value = '';
    sourceError.value = null;
    session.value = null;
    tableName.value = null;
    targetEntity.value = null;
    mappingRows.value = [];
    mappingsBuiltFor.value = null;
    preview.value = null;
    previewError.value = null;
    execution.value = null;
    executeError.value = null;
}

function statusLabel(status: ImportSessionStatus): string {
    return t(`import.status.${status}`);
}

function kindLabel(kind: ImportFieldKind): string {
    return t(`import.kind.${kind}`);
}

function describeFailure(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            const messages = Object.values(problem.errors).flat();
            if (messages.length > 0) {
                return messages.join(' ');
            }
        }
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
                    <h1 class="text-h4">{{ t('import.title') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('import.subtitle') }}</p>
                </div>
                <div class="header-actions">
                    <v-btn
                        variant="tonal"
                        prepend-icon="mdi-history"
                        data-testid="import-history-link"
                        @click="router.push('/import/verlauf')"
                        >{{ t('import.historyLink') }}</v-btn
                    >
                </div>
            </div>

            <v-stepper v-model="step" border flat>
                <v-stepper-header>
                    <v-stepper-item :value="1" :title="t('import.step.source')" :complete="step > 1" />
                    <v-divider />
                    <v-stepper-item :value="2" :title="t('import.step.table')" :complete="step > 2" />
                    <v-divider />
                    <v-stepper-item :value="3" :title="t('import.step.mapping')" :complete="step > 3" />
                    <v-divider />
                    <v-stepper-item :value="4" :title="t('import.step.preview')" :complete="step > 4" />
                    <v-divider />
                    <v-stepper-item :value="5" :title="t('import.step.result')" />
                </v-stepper-header>

                <v-stepper-window>
                    <!-- Step 1: source -->
                    <v-stepper-window-item :value="1">
                        <v-btn-toggle v-model="sourceMode" mandatory color="primary" variant="outlined" divided class="mode-toggle">
                            <v-btn value="file" prepend-icon="mdi-file-upload-outline" data-testid="import-mode-file">{{
                                t('import.source.file')
                            }}</v-btn>
                            <v-btn value="database" prepend-icon="mdi-database-outline" data-testid="import-mode-database">{{
                                t('import.source.database')
                            }}</v-btn>
                        </v-btn-toggle>

                        <v-row v-if="sourceMode === 'file'">
                            <v-col cols="12" md="8">
                                <v-file-input
                                    v-model="file"
                                    v-native-testid="'import-file-input'"
                                    :label="t('import.source.fileLabel')"
                                    :accept="fileAccept"
                                    :hint="fileHint"
                                    persistent-hint
                                    variant="outlined"
                                    density="compact"
                                />
                            </v-col>
                        </v-row>
                        <v-row v-else>
                            <v-col cols="12" sm="4" md="3">
                                <v-select
                                    v-model="provider"
                                    :items="providerOptions"
                                    :label="t('import.source.provider')"
                                    variant="outlined"
                                    density="compact"
                                    data-testid="import-provider"
                                />
                            </v-col>
                            <v-col cols="12" sm="8" md="9">
                                <v-text-field
                                    v-model="connectionString"
                                    v-native-testid="'import-connection-string'"
                                    :label="t('import.source.connectionString')"
                                    :hint="t('import.source.connectionStringHint')"
                                    persistent-hint
                                    variant="outlined"
                                    density="compact"
                                />
                            </v-col>
                        </v-row>

                        <p v-if="sourceError" class="text-error error-line" data-testid="import-source-error">{{ sourceError }}</p>
                        <div class="step-actions">
                            <v-spacer />
                            <v-btn color="primary" :loading="analyzing" data-testid="import-analyze" @click="analyze">{{
                                t('import.source.analyze')
                            }}</v-btn>
                        </div>
                    </v-stepper-window-item>

                    <!-- Step 2: table & target -->
                    <v-stepper-window-item :value="2">
                        <v-row>
                            <v-col cols="12" md="6">
                                <v-select
                                    v-model="tableName"
                                    :items="tableOptions"
                                    :label="t('import.table.title')"
                                    :no-data-text="t('import.table.empty')"
                                    variant="outlined"
                                    density="compact"
                                    data-testid="import-table-select"
                                />
                            </v-col>
                            <v-col cols="12" md="6">
                                <v-select
                                    v-model="targetEntity"
                                    :items="targetOptions"
                                    :label="t('import.target.title')"
                                    variant="outlined"
                                    density="compact"
                                    data-testid="import-target-select"
                                />
                            </v-col>
                        </v-row>
                        <p v-if="targetsError" class="text-error error-line" data-testid="import-targets-error">{{ targetsError }}</p>

                        <template v-if="selectedTable">
                            <p class="section-group-title">
                                {{ t('import.table.sampleTitle', { count: selectedTable.sampleRows.length }) }}
                            </p>
                            <v-card border flat class="table-scroll">
                                <v-table density="compact" data-testid="import-sample-table">
                                    <thead>
                                        <tr>
                                            <th v-for="column in selectedTable.columns" :key="column">{{ column }}</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="(row, rowIndex) in selectedTable.sampleRows" :key="rowIndex">
                                            <td v-for="(cell, cellIndex) in row" :key="cellIndex">{{ cell ?? '–' }}</td>
                                        </tr>
                                    </tbody>
                                </v-table>
                            </v-card>
                        </template>

                        <div class="step-actions">
                            <v-btn variant="text" data-testid="import-step2-back" @click="step = 1">{{ t('import.back') }}</v-btn>
                            <v-spacer />
                            <v-btn
                                color="primary"
                                :disabled="!tableName || !targetEntity"
                                data-testid="import-step2-continue"
                                @click="goToMapping"
                                >{{ t('import.continue') }}</v-btn
                            >
                        </div>
                    </v-stepper-window-item>

                    <!-- Step 3: mapping -->
                    <v-stepper-window-item :value="3">
                        <v-row
                            v-for="row in mappingRows"
                            :key="row.field.name"
                            dense
                            class="mapping-row"
                            :data-testid="`import-mapping-${row.field.name}`"
                        >
                            <v-col cols="12" md="4">
                                <div class="font-weight-medium">
                                    {{ row.field.label }}<span v-if="row.field.required" class="text-error"> *</span>
                                </div>
                                <div class="text-caption text-medium-emphasis">
                                    {{ kindLabel(row.field.kind)
                                    }}<template v-if="row.field.description"> · {{ row.field.description }}</template
                                    ><template v-if="row.field.allowedValues?.length">
                                        · {{ t('import.mapping.allowedValues', { values: row.field.allowedValues.join(', ') }) }}</template
                                    >
                                </div>
                            </v-col>
                            <v-col cols="12" sm="6" md="4">
                                <v-select
                                    v-model="row.sourceColumn"
                                    :items="columnOptions"
                                    :label="t('import.mapping.sourceColumn')"
                                    :placeholder="t('import.mapping.notMapped')"
                                    clearable
                                    variant="outlined"
                                    density="compact"
                                    hide-details
                                    :data-testid="`import-map-${row.field.name}`"
                                />
                            </v-col>
                            <v-col cols="12" sm="6" md="4">
                                <v-text-field
                                    v-model="row.constantValue"
                                    v-native-testid="`import-const-${row.field.name}`"
                                    :label="t('import.mapping.constant')"
                                    variant="outlined"
                                    density="compact"
                                    hide-details
                                />
                            </v-col>
                        </v-row>

                        <p v-if="missingRequired.length" class="text-error error-line" data-testid="import-mapping-missing">
                            {{ t('import.mapping.requiredMissing', { fields: missingRequired.join(', ') }) }}
                        </p>
                        <div class="step-actions">
                            <v-btn variant="text" data-testid="import-step3-back" @click="step = 2">{{ t('import.back') }}</v-btn>
                            <v-spacer />
                            <v-btn
                                color="primary"
                                :disabled="missingRequired.length > 0"
                                data-testid="import-step3-continue"
                                @click="goToPreview"
                                >{{ t('import.continue') }}</v-btn
                            >
                        </div>
                    </v-stepper-window-item>

                    <!-- Step 4: preview -->
                    <v-stepper-window-item :value="4">
                        <div v-if="previewLoading" class="busy-block">
                            <v-progress-circular indeterminate color="primary" />
                        </div>
                        <template v-else-if="preview">
                            <div class="chip-row">
                                <v-chip color="info" variant="tonal" data-testid="import-preview-total">{{
                                    t('import.preview.total', { count: preview.totalRows })
                                }}</v-chip>
                                <v-chip color="success" variant="tonal" data-testid="import-preview-valid">{{
                                    t('import.preview.valid', { count: preview.validRows })
                                }}</v-chip>
                                <v-chip
                                    :color="preview.invalidRows > 0 ? 'error' : 'grey'"
                                    variant="tonal"
                                    data-testid="import-preview-invalid"
                                    >{{ t('import.preview.invalid', { count: preview.invalidRows }) }}</v-chip
                                >
                            </div>
                            <v-alert
                                v-if="preview.validRows === 0"
                                type="error"
                                variant="tonal"
                                class="error-line"
                                data-testid="import-preview-all-invalid"
                                >{{ t('import.preview.allInvalid') }}</v-alert
                            >
                            <p class="text-caption text-medium-emphasis">
                                {{ t('import.preview.showing', { count: preview.rows.length }) }}
                            </p>
                            <v-card border flat class="table-scroll">
                                <v-table density="compact" data-testid="import-preview-table">
                                    <thead>
                                        <tr>
                                            <th>{{ t('import.preview.row') }}</th>
                                            <th v-for="field in previewFields" :key="field.name">{{ field.label }}</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <template v-for="row in preview.rows" :key="row.rowNumber">
                                            <tr :class="{ 'row-invalid': row.errors.length > 0 }">
                                                <td>{{ row.rowNumber }}</td>
                                                <td v-for="field in previewFields" :key="field.name">
                                                    {{ row.values[field.name] ?? '–' }}
                                                </td>
                                            </tr>
                                            <tr v-if="row.errors.length" class="row-invalid">
                                                <td />
                                                <td :colspan="previewFields.length" class="text-error text-caption">
                                                    {{ row.errors.join(' · ') }}
                                                </td>
                                            </tr>
                                        </template>
                                    </tbody>
                                </v-table>
                            </v-card>
                        </template>
                        <p v-if="previewError" class="text-error error-line" data-testid="import-preview-error">{{ previewError }}</p>

                        <div class="step-actions">
                            <v-btn variant="text" data-testid="import-step4-back" @click="step = 3">{{ t('import.back') }}</v-btn>
                            <v-spacer />
                            <v-btn color="primary" :disabled="!canExecute" data-testid="import-execute" @click="runImport">{{
                                t('import.execute')
                            }}</v-btn>
                        </div>
                    </v-stepper-window-item>

                    <!-- Step 5: result -->
                    <v-stepper-window-item :value="5">
                        <div v-if="executing" class="busy-block">
                            <v-progress-circular indeterminate color="primary" size="48" />
                            <p class="busy-text">{{ t('import.executing') }}</p>
                        </div>
                        <template v-else-if="execution">
                            <div class="chip-row">
                                <v-chip :color="statusColor(execution.status)" variant="tonal" data-testid="import-result-status">{{
                                    statusLabel(execution.status)
                                }}</v-chip>
                                <v-chip color="success" variant="tonal" data-testid="import-result-imported">{{
                                    t('import.result.imported', { count: execution.importedCount })
                                }}</v-chip>
                                <v-chip
                                    :color="execution.failedCount > 0 ? 'error' : 'grey'"
                                    variant="tonal"
                                    data-testid="import-result-failed"
                                    >{{ t('import.result.failed', { count: execution.failedCount }) }}</v-chip
                                >
                            </div>
                            <template v-if="execution.errors.length">
                                <p class="section-group-title">{{ t('import.result.errorsTitle') }}</p>
                                <v-card border flat class="table-scroll">
                                    <v-table density="compact" data-testid="import-result-errors">
                                        <thead>
                                            <tr>
                                                <th class="row-number-col">{{ t('import.preview.row') }}</th>
                                                <th>{{ t('import.result.message') }}</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr v-for="(rowError, errorIndex) in execution.errors" :key="errorIndex">
                                                <td>{{ rowError.rowNumber }}</td>
                                                <td>{{ rowError.message }}</td>
                                            </tr>
                                        </tbody>
                                    </v-table>
                                </v-card>
                            </template>
                        </template>
                        <p v-if="executeError" class="text-error error-line" data-testid="import-execute-error">{{ executeError }}</p>

                        <div class="step-actions">
                            <v-btn v-if="executeError" variant="text" data-testid="import-step5-back" @click="step = 4">{{
                                t('import.back')
                            }}</v-btn>
                            <v-spacer />
                            <v-btn
                                v-if="!executing"
                                color="primary"
                                prepend-icon="mdi-restart"
                                data-testid="import-new"
                                @click="resetWizard"
                                >{{ t('import.result.newImport') }}</v-btn
                            >
                        </div>
                    </v-stepper-window-item>
                </v-stepper-window>
            </v-stepper>
        </template>
        <p v-else>{{ t('import.signInHint') }}</p>
    </v-container>
</template>

<style scoped>
.mode-toggle {
    margin-bottom: 24px;
}

.step-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 16px;
}

.chip-row {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-bottom: 16px;
}

.mapping-row {
    align-items: center;
    padding: 4px 0;
    border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.table-scroll {
    overflow-x: auto;
}

.row-invalid {
    background-color: rgba(var(--v-theme-error), 0.08);
}

.row-number-col {
    width: 90px;
}

.busy-block {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 16px;
    margin: 48px 0;
}

.busy-text {
    margin: 0;
}
</style>
