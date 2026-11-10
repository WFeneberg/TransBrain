<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { listDrivers, type Driver } from '../api/drivers';
import { listTours, type Tour } from '../api/tours';
import { listVehicles, type Vehicle } from '../api/vehicles';
import { formatCalendarDate, statusColor, TOUR_STATUSES } from '../composables/statusColor';
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

const auth = useAuthStore();
const { locale, t } = useI18n();
/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

const router = useRouter();
const tours = ref<Tour[]>([]);
const vehicles = ref<Vehicle[]>([]);
const drivers = ref<Driver[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
const search = ref<string | null>('');
const statusFilter = ref('');
// Query parameters of the tour list: unlike search and status, these reload from the server.
const serverFilters = reactive({ tourDate: '', vehicleId: '', driverId: '' });

const statusOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...TOUR_STATUSES.map((status) => ({ title: status, value: status })),
]);

const vehicleOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...vehicles.value.map((vehicle) => ({ title: vehicle.licensePlate, value: vehicle.id })),
]);

const driverOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...drivers.value.map((driver) => ({ title: `${driver.lastName}, ${driver.firstName}`, value: driver.id })),
]);

const headers = computed(() => [
    { title: t('common.date'), key: 'tourDate' },
    { title: t('common.vehicle'), key: 'vehicleLicensePlate' },
    { title: t('common.driver'), key: 'driverName' },
    { title: t('tours.stops'), key: 'stops', value: (tour: Tour) => tour.stops.length, align: 'end' as const },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredTours = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    return tours.value.filter((tour) => {
        if (statusFilter.value && tour.status !== statusFilter.value) {
            return false;
        }
        if (!term) {
            return true;
        }
        return [tour.vehicleLicensePlate, tour.driverName, ...tour.stops.map((stop) => stop.orderNumber)].some((value) =>
            value.toLowerCase().includes(term),
        );
    });
});

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
        await loadFilterOptions();
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        tours.value = (
            await listTours({
                tourDate: serverFilters.tourDate,
                vehicleId: serverFilters.vehicleId,
                driverId: serverFilters.driverId,
                pageSize: LIST_PAGE_SIZE,
            })
        ).items;
    } catch (error) {
        errorMessage.value = describe(error, t('tours.listLoadFailed'));
    } finally {
        loading.value = false;
    }
}

async function applyServerFilter(filter: keyof typeof serverFilters, value: string | null): Promise<void> {
    serverFilters[filter] = value ?? '';
    await refresh();
}

// A failed option load leaves the filters empty but must not blank the table - the tours
// themselves loaded fine, and an unusable filter beats no list.
async function loadFilterOptions(): Promise<void> {
    try {
        vehicles.value = (await listVehicles(PICKER_PAGE_SIZE)).items;
        drivers.value = (await listDrivers(PICKER_PAGE_SIZE)).items;
    } catch {
        vehicles.value = [];
        drivers.value = [];
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
                    <h1 class="text-h4">{{ t('nav.tours') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('tours.count', { count: tours.length }) }}</p>
                </div>
                <div class="header-actions">
                    <v-btn
                        v-if="auth.can('dispatch.write')"
                        color="primary"
                        prepend-icon="mdi-plus"
                        data-testid="tour-add"
                        @click="router.push('/tours/new')"
                        >{{ t('tours.add') }}</v-btn
                    >
                </div>
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="4">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'tour-search'"
                                :label="t('tours.search')"
                                prepend-inner-icon="mdi-magnify"
                                variant="outlined"
                                density="compact"
                                clearable
                                hide-details
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="2">
                            <v-text-field
                                :model-value="serverFilters.tourDate"
                                v-native-testid="'tour-date-filter'"
                                :label="t('common.date')"
                                type="date"
                                variant="outlined"
                                density="compact"
                                hide-details
                                @update:model-value="applyServerFilter('tourDate', $event)"
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="2">
                            <v-select
                                :model-value="serverFilters.vehicleId"
                                :label="t('common.vehicle')"
                                :items="vehicleOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="tour-vehicle-filter"
                                @update:model-value="applyServerFilter('vehicleId', $event)"
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="2">
                            <v-select
                                :model-value="serverFilters.driverId"
                                :label="t('common.driver')"
                                :items="driverOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="tour-driver-filter"
                                @update:model-value="applyServerFilter('driverId', $event)"
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="2">
                            <v-select
                                v-model="statusFilter"
                                :label="t('common.status')"
                                :items="statusOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="tour-status-filter"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="errorMessage" class="text-error error-line" data-testid="tour-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredTours"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('tours.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="tour-table"
                >
                    <template #item.tourDate="{ item }">
                        <span class="font-weight-bold" data-testid="tour-date">{{ formatCalendarDate(item.tourDate, locale) }}</span>
                    </template>
                    <template #item.vehicleLicensePlate="{ item }">
                        <span data-testid="tour-vehicle">{{ item.vehicleLicensePlate }}</span>
                    </template>
                    <template #item.driverName="{ item }">
                        <span data-testid="tour-driver">{{ item.driverName }}</span>
                    </template>
                    <template #item.stops="{ item }">
                        <span data-testid="tour-stops">{{ item.stops.length }}</span>
                    </template>
                    <template #item.status="{ item }">
                        <v-chip :color="statusColor(item.status)" size="small" variant="tonal" data-testid="tour-status">{{
                            item.status
                        }}</v-chip>
                    </template>
                    <template #item.actions="{ item }">
                        <div class="row-actions">
                            <v-tooltip :text="t('common.open')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-eye-outline"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.open')"
                                        data-testid="tour-open"
                                        @click="router.push(`/tours/${item.id}`)"
                                    />
                                </template>
                            </v-tooltip>
                        </div>
                    </template>
                </v-data-table>
            </v-card>
        </template>
        <p v-else>{{ t('tours.signInHint') }}</p>
    </v-container>
</template>
