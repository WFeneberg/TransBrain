<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteVehicle, listVehicles, VEHICLE_STATUSES, VEHICLE_TYPES, type Vehicle } from '../api/vehicles';
import { listDrivers } from '../api/drivers';
import { useVehicleLabels } from '../composables/useVehicleLabels';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;
const INSPECTION_WARNING_DAYS = 30;
const DAY_MS = 86_400_000;

const auth = useAuthStore();
const { t } = useI18n();
const { typeLabel, classLabel, statusLabel, statusColor, formatDate } = useVehicleLabels();
const router = useRouter();
const vehicles = ref<Vehicle[]>([]);
const driverNames = ref(new Map<string, string>());
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed delete must not hide the table the way a failed list
// load does.
const actionError = ref<string | null>(null);

const search = ref<string | null>('');
const statusFilter = ref('');
const typeFilter = ref('');

const deleteDialogOpen = ref(false);
const deleteTarget = ref<Vehicle | null>(null);

const statusOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...VEHICLE_STATUSES.map((status) => ({ title: statusLabel(status), value: status })),
]);

const typeOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...VEHICLE_TYPES.map((type) => ({ title: typeLabel(type), value: type })),
]);

const headers = computed(() => [
    { title: t('vehicles.licensePlate'), key: 'licensePlate' },
    { title: t('vehicles.vehicleType'), key: 'type', value: (vehicle: Vehicle) => typeLabel(vehicle.type) },
    { title: t('vehicles.makeModel'), key: 'makeModel', value: (vehicle: Vehicle) => makeModel(vehicle) },
    {
        title: t('vehicles.vehicleClass'),
        key: 'vehicleClass',
        value: (vehicle: Vehicle) => (vehicle.vehicleClass ? classLabel(vehicle.vehicleClass) : ''),
    },
    { title: t('vehicles.payload'), key: 'payloadKg', align: 'end' as const },
    { title: t('vehicles.defaultDriver'), key: 'defaultDriver', value: (vehicle: Vehicle) => driverName(vehicle) },
    { title: t('vehicles.nextInspection'), key: 'nextInspectionDue' },
    { title: t('common.status'), key: 'status', value: (vehicle: Vehicle) => statusLabel(vehicle.status) },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredVehicles = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    return vehicles.value.filter((vehicle) => {
        if (statusFilter.value && vehicle.status !== statusFilter.value) {
            return false;
        }
        if (typeFilter.value && vehicle.type !== typeFilter.value) {
            return false;
        }
        if (!term) {
            return true;
        }
        return [vehicle.licensePlate, vehicle.make, vehicle.model, vehicle.chassisNumber, vehicle.carrierGroup].some(
            (value) => value?.toLowerCase().includes(term),
        );
    });
});

onMounted(async () => {
    if (auth.isAuthenticated) {
        await Promise.all([refresh(), loadDriverNames()]);
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        vehicles.value = (await listVehicles(LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('vehicles.listLoadFailed'));
    } finally {
        loading.value = false;
    }
}

// The driver names are decoration on this page: without them the column shows "–", but the
// vehicles themselves must still be listed.
async function loadDriverNames(): Promise<void> {
    try {
        const drivers = (await listDrivers(LIST_PAGE_SIZE)).items;
        driverNames.value = new Map(drivers.map((driver) => [driver.id, `${driver.lastName}, ${driver.firstName}`]));
    } catch {
        driverNames.value = new Map();
    }
}

function makeModel(vehicle: Vehicle): string {
    return [vehicle.make, vehicle.model].filter((part) => !!part).join(' ');
}

function driverName(vehicle: Vehicle): string {
    return vehicle.defaultDriverId ? (driverNames.value.get(vehicle.defaultDriverId) ?? '') : '';
}

function inspectionUrgency(due: string): 'overdue' | 'soon' | null {
    const [year, month, day] = due.split('-').map(Number);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const daysLeft = Math.round((new Date(year, month - 1, day).getTime() - today.getTime()) / DAY_MS);
    if (daysLeft < 0) {
        return 'overdue';
    }
    return daysLeft <= INSPECTION_WARNING_DAYS ? 'soon' : null;
}

function askDelete(vehicle: Vehicle): void {
    deleteTarget.value = vehicle;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    if (deleteTarget.value) {
        await remove(deleteTarget.value);
    }
}

async function remove(vehicle: Vehicle): Promise<void> {
    actionError.value = null;
    try {
        await deleteVehicle(vehicle.id);
        await refresh();
    } catch (error) {
        // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
        // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
        // the fallback text is therefore action-specific, not the list-load one.
        actionError.value = describe(error, t('vehicles.deleteFailed'));
    }
}

// ProblemDetails' `errors` dictionary is now keyed by field name (Task 1 of the
// 2026-08-29 master-data-completion phase fixed the previous error-code keying), but this
// component has no form fields to bind those keys onto - it only ever needs the free-text
// summary, which `title`/`detail` already provide.
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
    <v-container class="vehicle-page">
        <template v-if="auth.isAuthenticated">
            <div class="page-header">
                <div>
                    <h1 class="text-h4">{{ t('nav.vehicles') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('vehicles.count', { count: vehicles.length }) }}</p>
                </div>
                <v-btn
                    v-if="auth.can('masterData.write')"
                    color="primary"
                    prepend-icon="mdi-plus"
                    data-testid="vehicle-add"
                    @click="router.push('/vehicles/new')"
                    >{{ t('vehicles.add') }}</v-btn
                >
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'vehicle-search'"
                                :label="t('vehicles.search')"
                                prepend-inner-icon="mdi-magnify"
                                variant="outlined"
                                density="compact"
                                clearable
                                hide-details
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="3">
                            <v-select
                                v-model="statusFilter"
                                :label="t('common.status')"
                                :items="statusOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="vehicle-status-filter"
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="3">
                            <v-select
                                v-model="typeFilter"
                                :label="t('vehicles.vehicleType')"
                                :items="typeOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="vehicle-type-filter"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="actionError" class="text-error error-line" data-testid="vehicle-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="vehicle-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredVehicles"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('vehicles.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="vehicle-table"
                >
                    <template #item.licensePlate="{ item }">
                        <span class="plate">
                            <span class="font-weight-bold" data-testid="vehicle-plate">{{ item.licensePlate }}</span>
                            <v-tooltip v-if="item.isLicensePlateConfidential" :text="t('vehicles.licensePlateConfidentialHint')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-icon
                                        v-bind="tooltip"
                                        icon="mdi-lock"
                                        size="small"
                                        color="medium-emphasis"
                                        :aria-label="t('vehicles.licensePlateConfidentialHint')"
                                    />
                                </template>
                            </v-tooltip>
                        </span>
                    </template>
                    <template #item.makeModel="{ value }">{{ value || '–' }}</template>
                    <template #item.vehicleClass="{ value }">{{ value || '–' }}</template>
                    <template #item.payloadKg="{ item }">{{ item.payloadKg }}</template>
                    <template #item.defaultDriver="{ value }">{{ value || '–' }}</template>
                    <template #item.nextInspectionDue="{ item }">
                        <v-chip
                            v-if="inspectionUrgency(item.nextInspectionDue)"
                            :color="inspectionUrgency(item.nextInspectionDue) === 'overdue' ? 'error' : 'warning'"
                            :prepend-icon="inspectionUrgency(item.nextInspectionDue) === 'overdue' ? 'mdi-alert-circle' : 'mdi-clock-alert-outline'"
                            size="small"
                            variant="tonal"
                            >{{ formatDate(item.nextInspectionDue) }}</v-chip
                        >
                        <span v-else>{{ formatDate(item.nextInspectionDue) }}</span>
                    </template>
                    <template #item.status="{ item }">
                        <v-chip :color="statusColor(item.status)" size="small" variant="tonal" data-testid="vehicle-status-chip">{{
                            statusLabel(item.status)
                        }}</v-chip>
                    </template>
                    <template #item.actions="{ item }">
                        <div v-if="auth.can('masterData.write')" class="row-actions">
                            <v-tooltip :text="t('common.edit')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-pencil"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.edit')"
                                        data-testid="vehicle-edit"
                                        @click="router.push(`/vehicles/${item.id}`)"
                                    />
                                </template>
                            </v-tooltip>
                            <v-tooltip :text="t('common.delete')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-delete"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :aria-label="t('common.delete')"
                                        data-testid="vehicle-delete"
                                        @click="askDelete(item)"
                                    />
                                </template>
                            </v-tooltip>
                        </div>
                    </template>
                </v-data-table>
            </v-card>

            <v-dialog v-model="deleteDialogOpen" max-width="440">
                <v-card>
                    <v-card-title>{{ t('vehicles.deleteTitle') }}</v-card-title>
                    <v-card-text>{{ t('vehicles.deleteConfirm', { plate: deleteTarget?.licensePlate ?? '' }) }}</v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn variant="text" data-testid="vehicle-delete-cancel" @click="deleteDialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn color="error" variant="flat" data-testid="vehicle-delete-confirm" @click="confirmDelete">{{
                            t('common.delete')
                        }}</v-btn>
                    </v-card-actions>
                </v-card>
            </v-dialog>
        </template>
        <p v-else>{{ t('vehicles.signInHint') }}</p>
    </v-container>
</template>

<style scoped>
.vehicle-page {
    max-width: 1200px;
}

.page-header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 24px;
}

.page-subtitle {
    margin: 4px 0 0;
}

.filter-bar {
    margin-bottom: 16px;
}

.error-line {
    margin-bottom: 16px;
}

.plate {
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.row-actions {
    display: inline-flex;
    gap: 4px;
}
</style>
