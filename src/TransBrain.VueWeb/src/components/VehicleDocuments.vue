<script setup lang="ts">
import axios from 'axios';
import { computed, reactive, ref, watch } from 'vue';
import {
    createVehicleDocument,
    deleteVehicleDocument,
    listVehicleDocuments,
    updateVehicleDocument,
    type VehicleDocument,
} from '../api/vehicleDocuments';
import { catalogName, catalogSelectOptions, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import { useVehicleLabels } from '../composables/useVehicleLabels';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

type DocumentField = 'documentType' | 'lastCheckedOn' | 'validUntil' | 'remark';
type DocumentState = 'Expired' | 'ExpiresSoon' | 'Valid';

const REMARK_MAX_LENGTH = 500;
const EXPIRES_SOON_DAYS = 30;
const MIN_YEAR = 1900;

const STATE_COLORS: Record<DocumentState, string> = {
    Expired: 'error',
    ExpiresSoon: 'warning',
    Valid: 'success',
};

const props = defineProps<{
    /** Undefined for a vehicle that has not been saved yet. */
    vehicleId: string | undefined;
    documentTypes: readonly VehicleCatalogEntry[];
}>();

const auth = useAuthStore();
const { t } = useI18n();
const { formatDate, formatDateTime } = useVehicleLabels();

const canWrite = computed(() => auth.can('masterData.write'));

const documents = ref<VehicleDocument[]>([]);
const loaded = ref(false);
const listError = ref<string | null>(null);

const dialogOpen = ref(false);
const editTarget = ref<VehicleDocument | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive<Record<DocumentField, string>>({ documentType: '', lastCheckedOn: '', validUntil: '', remark: '' });
const fieldErrors = reactive<Record<DocumentField, string | null>>({
    documentType: null,
    lastCheckedOn: null,
    validUntil: null,
    remark: null,
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<VehicleDocument | null>(null);

const typeOptions = computed(() =>
    catalogSelectOptions(props.documentTypes, editTarget.value?.documentType, (name) =>
        t('vehicles.inactiveOption', { name }),
    ),
);

watch(
    () => props.vehicleId,
    async (id) => {
        if (id) {
            await refresh();
        }
    },
    { immediate: true },
);

async function refresh(): Promise<void> {
    try {
        documents.value = await listVehicleDocuments(props.vehicleId!);
        listError.value = null;
    } catch (error) {
        listError.value = describe(error, t('vehicles.documents.loadFailed'));
    } finally {
        loaded.value = true;
    }
}

function typeName(code: string): string {
    return catalogName(props.documentTypes, code);
}

function dateText(value: string | null): string {
    return value ? formatDate(value) : '–';
}

function localIsoDate(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
}

// "YYYY-MM-DD" strings compare correctly as plain strings.
function documentState(validUntil: string): DocumentState {
    const now = new Date();
    const today = localIsoDate(now);
    const soon = localIsoDate(new Date(now.getFullYear(), now.getMonth(), now.getDate() + EXPIRES_SOON_DAYS));
    if (validUntil < today) {
        return 'Expired';
    }
    return validUntil <= soon ? 'ExpiresSoon' : 'Valid';
}

function modifiedText(document: VehicleDocument): string {
    return t('vehicles.documents.modifiedLine', {
        date: formatDateTime(document.modifiedAt),
        user: document.modifiedBy ?? '–',
    });
}

function openCreate(): void {
    editTarget.value = null;
    form.documentType = '';
    form.lastCheckedOn = '';
    form.validUntil = '';
    form.remark = '';
    openDialog();
}

function openEdit(document: VehicleDocument): void {
    editTarget.value = document;
    form.documentType = document.documentType;
    form.lastCheckedOn = document.lastCheckedOn ?? '';
    form.validUntil = document.validUntil ?? '';
    form.remark = document.remark ?? '';
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    clearFieldErrors();
    dialogOpen.value = true;
}

function clearFieldErrors(): void {
    for (const field of Object.keys(fieldErrors) as DocumentField[]) {
        fieldErrors[field] = null;
    }
}

function errorsFor(field: DocumentField): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function dateError(value: string): string | null {
    return value && Number(value.slice(0, 4)) < MIN_YEAR ? t('common.yearTooEarly') : null;
}

function validate(): boolean {
    clearFieldErrors();
    fieldErrors.documentType = form.documentType ? null : t('common.required');
    fieldErrors.lastCheckedOn = dateError(form.lastCheckedOn);
    fieldErrors.validUntil = dateError(form.validUntil);
    if (!fieldErrors.validUntil && form.lastCheckedOn && form.validUntil && form.validUntil < form.lastCheckedOn) {
        fieldErrors.validUntil = t('vehicles.documents.validUntilBeforeLastChecked');
    }
    if (form.remark.trim().length > REMARK_MAX_LENGTH) {
        fieldErrors.remark = t('common.maxLength', { max: REMARK_MAX_LENGTH });
    }
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    dialogError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request = {
        documentType: form.documentType,
        lastCheckedOn: form.lastCheckedOn || null,
        validUntil: form.validUntil || null,
        remark: form.remark.trim() || null,
    };

    saving.value = true;
    try {
        if (editTarget.value) {
            await updateVehicleDocument(props.vehicleId!, editTarget.value.id, request);
        } else {
            await createVehicleDocument(props.vehicleId!, request);
        }
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        applyServerErrors(error);
    } finally {
        saving.value = false;
    }
}

// The `errors` dictionary is keyed by the .NET property name ("ValidUntil"); a key this dialog
// has no field for still has to reach the user, so it goes to the dialog-level line.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [key, messages] of Object.entries(problem.errors)) {
                const field = key.charAt(0).toLowerCase() + key.slice(1);
                const message = messages.join(' ');
                if (field in fieldErrors) {
                    fieldErrors[field as DocumentField] = message;
                } else {
                    dialogError.value = dialogError.value ? `${dialogError.value} ${message}` : message;
                }
            }
            return;
        }
    }
    dialogError.value = describe(error, t('vehicles.documents.saveFailed'));
}

function askDelete(document: VehicleDocument): void {
    deleteTarget.value = document;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    const document = deleteTarget.value;
    if (!document) {
        return;
    }
    listError.value = null;
    try {
        await deleteVehicleDocument(props.vehicleId!, document.id);
        await refresh();
    } catch (error) {
        listError.value = describe(error, t('vehicles.documents.deleteFailed'));
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-card border flat class="section-card" data-testid="vehicle-documents">
        <v-card-item prepend-icon="mdi-file-certificate-outline" :title="t('vehicles.section.documents')">
            <template v-if="vehicleId && canWrite" #append>
                <v-btn
                    type="button"
                    color="primary"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    data-testid="vehicle-document-add"
                    @click="openCreate"
                    >{{ t('vehicles.documents.add') }}</v-btn
                >
            </template>
        </v-card-item>
        <v-card-text>
            <p v-if="!vehicleId" class="text-medium-emphasis hint" data-testid="vehicle-documents-hint">
                <v-icon icon="mdi-information-outline" size="small" />
                {{ t('vehicles.documents.hint') }}
            </p>
            <template v-else>
                <p v-if="listError" class="text-error list-error" data-testid="vehicle-documents-error">{{ listError }}</p>
                <p
                    v-if="loaded && !listError && documents.length === 0"
                    class="text-medium-emphasis hint"
                    data-testid="vehicle-documents-empty"
                >
                    {{ t('vehicles.documents.empty') }}
                </p>
                <v-table v-else-if="documents.length > 0" density="comfortable" class="documents-table">
                    <thead>
                        <tr>
                            <th>{{ t('vehicles.documents.type') }}</th>
                            <th>{{ t('vehicles.documents.lastCheckedOn') }}</th>
                            <th>{{ t('vehicles.documents.validUntil') }}</th>
                            <th>{{ t('common.status') }}</th>
                            <th>{{ t('vehicles.documents.remark') }}</th>
                            <th>{{ t('vehicles.documents.modified') }}</th>
                            <th v-if="canWrite" class="text-end">{{ t('common.actions') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="document in documents" :key="document.id" data-testid="vehicle-document-row">
                            <td class="font-weight-medium" data-testid="vehicle-document-type">
                                {{ typeName(document.documentType) }}
                            </td>
                            <td data-testid="vehicle-document-lastCheckedOn">{{ dateText(document.lastCheckedOn) }}</td>
                            <td data-testid="vehicle-document-validUntil">{{ dateText(document.validUntil) }}</td>
                            <td>
                                <v-chip
                                    v-if="document.validUntil"
                                    :color="STATE_COLORS[documentState(document.validUntil)]"
                                    size="small"
                                    variant="tonal"
                                    data-testid="vehicle-document-status"
                                    >{{ t(`vehicles.documents.state.${documentState(document.validUntil)}`) }}</v-chip
                                >
                            </td>
                            <td class="remark" data-testid="vehicle-document-remark">{{ document.remark ?? '' }}</td>
                            <td class="text-medium-emphasis" data-testid="vehicle-document-modified">
                                {{ modifiedText(document) }}
                            </td>
                            <td v-if="canWrite" class="text-end">
                                <div class="row-actions">
                                    <v-btn
                                        type="button"
                                        icon="mdi-pencil"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.edit')"
                                        data-testid="vehicle-document-edit"
                                        @click="openEdit(document)"
                                    />
                                    <v-btn
                                        type="button"
                                        icon="mdi-delete"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :aria-label="t('common.delete')"
                                        data-testid="vehicle-document-delete"
                                        @click="askDelete(document)"
                                    />
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </v-table>
            </template>
        </v-card-text>

        <v-dialog v-model="dialogOpen" max-width="560" :persistent="saving">
            <v-card data-testid="document-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'"
                    :title="editTarget ? t('vehicles.documents.edit') : t('vehicles.documents.new')"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="document-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-select
                            v-model="form.documentType"
                            :label="`${t('vehicles.documents.type')} *`"
                            :items="typeOptions"
                            :error-messages="errorsFor('documentType')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                            data-testid="document-type"
                        >
                            <template #message="{ message }">
                                <span data-testid="document-type-error">{{ message }}</span>
                            </template>
                        </v-select>
                        <v-row dense>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.lastCheckedOn"
                                    v-native-testid="'document-lastCheckedOn'"
                                    :label="t('vehicles.documents.lastCheckedOn')"
                                    type="date"
                                    :error-messages="errorsFor('lastCheckedOn')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="document-lastCheckedOn-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.validUntil"
                                    v-native-testid="'document-validUntil'"
                                    :label="t('vehicles.documents.validUntil')"
                                    type="date"
                                    :error-messages="errorsFor('validUntil')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="document-validUntil-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                        </v-row>
                        <v-textarea
                            v-model="form.remark"
                            v-native-testid="'document-remark'"
                            :label="t('vehicles.documents.remark')"
                            :counter="REMARK_MAX_LENGTH"
                            :error-messages="errorsFor('remark')"
                            rows="3"
                            variant="outlined"
                        >
                            <template #message="{ message }">
                                <span data-testid="document-remark-error">{{ message }}</span>
                            </template>
                        </v-textarea>
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn type="button" variant="text" data-testid="document-cancel" :disabled="saving" @click="dialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="document-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <v-dialog v-model="deleteDialogOpen" max-width="440">
            <v-card>
                <v-card-title>{{ t('vehicles.documents.deleteTitle') }}</v-card-title>
                <v-card-text>{{
                    t('vehicles.documents.deleteConfirm', { type: deleteTarget ? typeName(deleteTarget.documentType) : '' })
                }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="vehicle-document-delete-cancel" @click="deleteDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="vehicle-document-delete-confirm" @click="confirmDelete">{{
                        t('common.delete')
                    }}</v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>
    </v-card>
</template>

<style scoped>
.hint {
    display: flex;
    align-items: center;
    gap: 6px;
    margin: 0;
}

.list-error {
    margin-bottom: 8px;
}

.documents-table {
    overflow-x: auto;
}

.remark {
    white-space: pre-line;
}

.row-actions {
    display: inline-flex;
    gap: 4px;
}

.dialog-error {
    margin-bottom: 16px;
}

.dialog-field {
    margin-bottom: 8px;
}
</style>
