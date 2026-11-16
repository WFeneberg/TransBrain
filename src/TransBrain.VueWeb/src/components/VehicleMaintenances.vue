<script setup lang="ts">
import axios from 'axios';
import { computed, reactive, ref, watch } from 'vue';
import {
    MAINTENANCE_KINDS,
    createVehicleMaintenance,
    deleteVehicleMaintenance,
    listVehicleMaintenances,
    updateVehicleMaintenance,
    type VehicleMaintenance,
    type VehicleMaintenanceKind,
} from '../api/vehicleMaintenances';
import { useVehicleLabels } from '../composables/useVehicleLabels';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

type MaintenanceField =
    | 'kind'
    | 'performedOn'
    | 'description'
    | 'workshop'
    | 'odometerKm'
    | 'cost'
    | 'nextDueOn';
type DueState = 'Overdue' | 'DueSoon' | 'Scheduled';

const DESCRIPTION_MAX_LENGTH = 1000;
const WORKSHOP_MAX_LENGTH = 200;
const MAX_ODOMETER_KM = 9999999;
const MAX_COST = 10000000;
const DUE_SOON_DAYS = 30;
const MIN_YEAR = 1900;

const STATE_COLORS: Record<DueState, string> = {
    Overdue: 'error',
    DueSoon: 'warning',
    Scheduled: 'success',
};

const props = defineProps<{
    /** Undefined for a vehicle that has not been saved yet. */
    vehicleId: string | undefined;
}>();

const auth = useAuthStore();
const { t, locale } = useI18n();
const { formatDate, formatDateTime } = useVehicleLabels();

const canWrite = computed(() => auth.can('masterData.write'));

const maintenances = ref<VehicleMaintenance[]>([]);
const loaded = ref(false);
const listError = ref<string | null>(null);

const dialogOpen = ref(false);
const editTarget = ref<VehicleMaintenance | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive<Record<MaintenanceField, string>>({
    kind: 'Service',
    performedOn: '',
    description: '',
    workshop: '',
    odometerKm: '',
    cost: '',
    nextDueOn: '',
});
const fieldErrors = reactive<Record<MaintenanceField, string | null>>({
    kind: null,
    performedOn: null,
    description: null,
    workshop: null,
    odometerKm: null,
    cost: null,
    nextDueOn: null,
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<VehicleMaintenance | null>(null);

const kindOptions = computed(() =>
    MAINTENANCE_KINDS.map((kind) => ({ value: kind, title: kindLabel(kind) })),
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
        maintenances.value = await listVehicleMaintenances(props.vehicleId!);
        listError.value = null;
    } catch (error) {
        listError.value = describe(error, t('vehicles.maintenance.loadFailed'));
    } finally {
        loaded.value = true;
    }
}

function kindLabel(kind: VehicleMaintenanceKind): string {
    return t(`vehicles.maintenance.kind.${kind}` as MessageKey);
}

function dateText(value: string | null): string {
    return value ? formatDate(value) : '–';
}

function numberTag(): string {
    return locale.value === 'de' ? 'de-DE' : 'en-US';
}

function odometerText(value: number | null): string {
    return value === null ? '' : `${new Intl.NumberFormat(numberTag()).format(value)} km`;
}

function costText(value: number | null): string {
    return value === null
        ? ''
        : new Intl.NumberFormat(numberTag(), { style: 'currency', currency: 'EUR' }).format(value);
}

function localIsoDate(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
}

// "YYYY-MM-DD" strings compare correctly as plain strings.
function dueState(nextDueOn: string): DueState {
    const now = new Date();
    const today = localIsoDate(now);
    const soon = localIsoDate(new Date(now.getFullYear(), now.getMonth(), now.getDate() + DUE_SOON_DAYS));
    if (nextDueOn < today) {
        return 'Overdue';
    }
    return nextDueOn <= soon ? 'DueSoon' : 'Scheduled';
}

function modifiedText(maintenance: VehicleMaintenance): string {
    return t('vehicles.maintenance.modifiedLine', {
        date: formatDateTime(maintenance.modifiedAt),
        user: maintenance.modifiedBy ?? '–',
    });
}

function openCreate(): void {
    editTarget.value = null;
    form.kind = 'Service';
    form.performedOn = '';
    form.description = '';
    form.workshop = '';
    form.odometerKm = '';
    form.cost = '';
    form.nextDueOn = '';
    openDialog();
}

function openEdit(maintenance: VehicleMaintenance): void {
    editTarget.value = maintenance;
    form.kind = maintenance.kind;
    form.performedOn = maintenance.performedOn;
    form.description = maintenance.description;
    form.workshop = maintenance.workshop ?? '';
    form.odometerKm = maintenance.odometerKm === null ? '' : String(maintenance.odometerKm);
    form.cost = maintenance.cost === null ? '' : String(maintenance.cost);
    form.nextDueOn = maintenance.nextDueOn ?? '';
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    clearFieldErrors();
    dialogOpen.value = true;
}

function clearFieldErrors(): void {
    for (const field of Object.keys(fieldErrors) as MaintenanceField[]) {
        fieldErrors[field] = null;
    }
}

function errorsFor(field: MaintenanceField): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function dateError(value: string): string | null {
    return value && Number(value.slice(0, 4)) < MIN_YEAR ? t('common.yearTooEarly') : null;
}

function wholeNumberError(raw: string, max: number): string | null {
    if (!raw) {
        return null;
    }
    const value = Number(raw);
    if (Number.isNaN(value)) {
        return t('common.invalid');
    }
    if (!Number.isInteger(value)) {
        return t('common.mustBeWholeNumber');
    }
    if (value < 0) {
        return t('common.mustNotBeNegative');
    }
    return value > max ? t('common.mustNotExceed', { max }) : null;
}

function amountError(raw: string, max: number): string | null {
    if (!raw) {
        return null;
    }
    const value = Number(raw);
    if (Number.isNaN(value)) {
        return t('common.invalid');
    }
    if (value < 0) {
        return t('common.mustNotBeNegative');
    }
    if (value > max) {
        return t('common.mustNotExceed', { max });
    }
    return Math.abs(value * 100 - Math.round(value * 100)) > 1e-6 ? t('common.atMostTwoDecimals') : null;
}

function validate(): boolean {
    clearFieldErrors();
    fieldErrors.kind = form.kind ? null : t('common.required');
    fieldErrors.performedOn = form.performedOn ? dateError(form.performedOn) : t('common.required');
    fieldErrors.description = form.description.trim()
        ? form.description.trim().length > DESCRIPTION_MAX_LENGTH
            ? t('common.maxLength', { max: DESCRIPTION_MAX_LENGTH })
            : null
        : t('common.required');
    if (form.workshop.trim().length > WORKSHOP_MAX_LENGTH) {
        fieldErrors.workshop = t('common.maxLength', { max: WORKSHOP_MAX_LENGTH });
    }
    fieldErrors.odometerKm = wholeNumberError(form.odometerKm, MAX_ODOMETER_KM);
    fieldErrors.cost = amountError(form.cost, MAX_COST);
    fieldErrors.nextDueOn = dateError(form.nextDueOn);
    if (!fieldErrors.nextDueOn && form.performedOn && form.nextDueOn && form.nextDueOn < form.performedOn) {
        fieldErrors.nextDueOn = t('vehicles.maintenance.nextDueBeforePerformed');
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
        kind: form.kind as VehicleMaintenanceKind,
        performedOn: form.performedOn,
        description: form.description.trim(),
        workshop: form.workshop.trim() || null,
        odometerKm: form.odometerKm === '' ? null : Number(form.odometerKm),
        cost: form.cost === '' ? null : Number(form.cost),
        nextDueOn: form.nextDueOn || null,
    };

    saving.value = true;
    try {
        if (editTarget.value) {
            await updateVehicleMaintenance(props.vehicleId!, editTarget.value.id, request);
        } else {
            await createVehicleMaintenance(props.vehicleId!, request);
        }
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        applyServerErrors(error);
    } finally {
        saving.value = false;
    }
}

// The `errors` dictionary is keyed by the .NET property name ("NextDueOn"); a key this dialog
// has no field for still has to reach the user, so it goes to the dialog-level line.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [key, messages] of Object.entries(problem.errors)) {
                const field = key.charAt(0).toLowerCase() + key.slice(1);
                const message = messages.join(' ');
                if (field in fieldErrors) {
                    fieldErrors[field as MaintenanceField] = message;
                } else {
                    dialogError.value = dialogError.value ? `${dialogError.value} ${message}` : message;
                }
            }
            return;
        }
    }
    dialogError.value = describe(error, t('vehicles.maintenance.saveFailed'));
}

function askDelete(maintenance: VehicleMaintenance): void {
    deleteTarget.value = maintenance;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    const maintenance = deleteTarget.value;
    if (!maintenance) {
        return;
    }
    listError.value = null;
    try {
        await deleteVehicleMaintenance(props.vehicleId!, maintenance.id);
        await refresh();
    } catch (error) {
        listError.value = describe(error, t('vehicles.maintenance.deleteFailed'));
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
    <v-card border flat class="section-card" data-testid="vehicle-maintenances">
        <v-card-item prepend-icon="mdi-wrench-outline" :title="t('vehicles.section.maintenance')">
            <template v-if="vehicleId && canWrite" #append>
                <v-btn
                    type="button"
                    color="primary"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    data-testid="vehicle-maintenance-add"
                    @click="openCreate"
                    >{{ t('vehicles.maintenance.add') }}</v-btn
                >
            </template>
        </v-card-item>
        <v-card-text>
            <p v-if="!vehicleId" class="text-medium-emphasis hint" data-testid="vehicle-maintenances-hint">
                <v-icon icon="mdi-information-outline" size="small" />
                {{ t('vehicles.maintenance.hint') }}
            </p>
            <template v-else>
                <p v-if="listError" class="text-error list-error" data-testid="vehicle-maintenances-error">
                    {{ listError }}
                </p>
                <p
                    v-if="loaded && !listError && maintenances.length === 0"
                    class="text-medium-emphasis hint"
                    data-testid="vehicle-maintenances-empty"
                >
                    {{ t('vehicles.maintenance.empty') }}
                </p>
                <v-table v-else-if="maintenances.length > 0" density="comfortable" class="maintenances-table">
                    <thead>
                        <tr>
                            <th>{{ t('vehicles.maintenance.performedOn') }}</th>
                            <th>{{ t('vehicles.maintenance.kindLabel') }}</th>
                            <th>{{ t('vehicles.maintenance.description') }}</th>
                            <th>{{ t('vehicles.maintenance.workshop') }}</th>
                            <th>{{ t('vehicles.maintenance.odometerKm') }}</th>
                            <th>{{ t('vehicles.maintenance.cost') }}</th>
                            <th>{{ t('vehicles.maintenance.nextDueOn') }}</th>
                            <th>{{ t('vehicles.maintenance.modified') }}</th>
                            <th v-if="canWrite" class="text-end">{{ t('common.actions') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="maintenance in maintenances" :key="maintenance.id" data-testid="vehicle-maintenance-row">
                            <td data-testid="vehicle-maintenance-performedOn">{{ dateText(maintenance.performedOn) }}</td>
                            <td class="font-weight-medium" data-testid="vehicle-maintenance-kind">
                                {{ kindLabel(maintenance.kind) }}
                            </td>
                            <td class="description" data-testid="vehicle-maintenance-description">
                                {{ maintenance.description }}
                            </td>
                            <td data-testid="vehicle-maintenance-workshop">{{ maintenance.workshop ?? '' }}</td>
                            <td data-testid="vehicle-maintenance-odometerKm">{{ odometerText(maintenance.odometerKm) }}</td>
                            <td data-testid="vehicle-maintenance-cost">{{ costText(maintenance.cost) }}</td>
                            <td>
                                <template v-if="maintenance.nextDueOn">
                                    <span data-testid="vehicle-maintenance-nextDueOn">{{
                                        dateText(maintenance.nextDueOn)
                                    }}</span>
                                    <v-chip
                                        :color="STATE_COLORS[dueState(maintenance.nextDueOn)]"
                                        size="small"
                                        variant="tonal"
                                        class="due-chip"
                                        data-testid="vehicle-maintenance-status"
                                        >{{ t(`vehicles.maintenance.state.${dueState(maintenance.nextDueOn)}`) }}</v-chip
                                    >
                                </template>
                            </td>
                            <td class="text-medium-emphasis" data-testid="vehicle-maintenance-modified">
                                {{ modifiedText(maintenance) }}
                            </td>
                            <td v-if="canWrite" class="text-end">
                                <div class="row-actions">
                                    <v-btn
                                        type="button"
                                        icon="mdi-pencil"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.edit')"
                                        data-testid="vehicle-maintenance-edit"
                                        @click="openEdit(maintenance)"
                                    />
                                    <v-btn
                                        type="button"
                                        icon="mdi-delete"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :aria-label="t('common.delete')"
                                        data-testid="vehicle-maintenance-delete"
                                        @click="askDelete(maintenance)"
                                    />
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </v-table>
            </template>
        </v-card-text>

        <v-dialog v-model="dialogOpen" max-width="600" :persistent="saving">
            <v-card data-testid="maintenance-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'"
                    :title="editTarget ? t('vehicles.maintenance.edit') : t('vehicles.maintenance.new')"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="maintenance-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-row dense>
                            <v-col cols="12" sm="6">
                                <v-select
                                    v-model="form.kind"
                                    :label="`${t('vehicles.maintenance.kindLabel')} *`"
                                    :items="kindOptions"
                                    :error-messages="errorsFor('kind')"
                                    variant="outlined"
                                    density="comfortable"
                                    data-testid="maintenance-kind"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="maintenance-kind-error">{{ message }}</span>
                                    </template>
                                </v-select>
                            </v-col>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.performedOn"
                                    v-native-testid="'maintenance-performedOn'"
                                    :label="`${t('vehicles.maintenance.performedOn')} *`"
                                    type="date"
                                    :error-messages="errorsFor('performedOn')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="maintenance-performedOn-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                        </v-row>
                        <v-textarea
                            v-model="form.description"
                            v-native-testid="'maintenance-description'"
                            :label="`${t('vehicles.maintenance.description')} *`"
                            :counter="DESCRIPTION_MAX_LENGTH"
                            :error-messages="errorsFor('description')"
                            rows="3"
                            variant="outlined"
                            class="dialog-field"
                        >
                            <template #message="{ message }">
                                <span data-testid="maintenance-description-error">{{ message }}</span>
                            </template>
                        </v-textarea>
                        <v-text-field
                            v-model="form.workshop"
                            v-native-testid="'maintenance-workshop'"
                            :label="t('vehicles.maintenance.workshop')"
                            :maxlength="WORKSHOP_MAX_LENGTH"
                            :error-messages="errorsFor('workshop')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                        >
                            <template #message="{ message }">
                                <span data-testid="maintenance-workshop-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                        <v-row dense>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.odometerKm"
                                    v-native-testid="'maintenance-odometerKm'"
                                    :label="t('vehicles.maintenance.odometerKm')"
                                    type="number"
                                    min="0"
                                    suffix="km"
                                    :error-messages="errorsFor('odometerKm')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="maintenance-odometerKm-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.cost"
                                    v-native-testid="'maintenance-cost'"
                                    :label="t('vehicles.maintenance.cost')"
                                    type="number"
                                    min="0"
                                    step="0.01"
                                    suffix="€"
                                    :error-messages="errorsFor('cost')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="maintenance-cost-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                        </v-row>
                        <v-text-field
                            v-model="form.nextDueOn"
                            v-native-testid="'maintenance-nextDueOn'"
                            :label="t('vehicles.maintenance.nextDueOn')"
                            type="date"
                            :error-messages="errorsFor('nextDueOn')"
                            variant="outlined"
                            density="comfortable"
                        >
                            <template #message="{ message }">
                                <span data-testid="maintenance-nextDueOn-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn
                            type="button"
                            variant="text"
                            data-testid="maintenance-cancel"
                            :disabled="saving"
                            @click="dialogOpen = false"
                            >{{ t('common.cancel') }}</v-btn
                        >
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="maintenance-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <v-dialog v-model="deleteDialogOpen" max-width="440">
            <v-card>
                <v-card-title>{{ t('vehicles.maintenance.deleteTitle') }}</v-card-title>
                <v-card-text>{{
                    t('vehicles.maintenance.deleteConfirm', {
                        date: deleteTarget ? formatDate(deleteTarget.performedOn) : '',
                    })
                }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="vehicle-maintenance-delete-cancel" @click="deleteDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="vehicle-maintenance-delete-confirm" @click="confirmDelete">{{
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

.maintenances-table {
    overflow-x: auto;
}

.description {
    white-space: pre-line;
}

.due-chip {
    margin-left: 8px;
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
