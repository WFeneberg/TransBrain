<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { listDrivers, type Driver } from '../api/drivers';
import { listTours, type Tour } from '../api/tours';
import { listVehicles, type Vehicle } from '../api/vehicles';
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
const { t } = useI18n();
/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

const router = useRouter();
const tours = ref<Tour[]>([]);
const vehicles = ref<Vehicle[]>([]);
const drivers = ref<Driver[]>([]);
const errorMessage = ref<string | null>(null);
const dateFilter = ref('');
const vehicleFilter = ref('');
const driverFilter = ref('');

const headers = computed(() => [
    { title: t('common.date'), key: 'tourDate' },
    { title: t('common.vehicle'), key: 'vehicleLicensePlate' },
    { title: t('common.driver'), key: 'driverName' },
    { title: t('tours.stops'), key: 'stops', sortable: false },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
        await loadFilterOptions();
    }
});

async function refresh(): Promise<void> {
    try {
        tours.value = (
            await listTours({
                tourDate: dateFilter.value,
                vehicleId: vehicleFilter.value,
                driverId: driverFilter.value,
                pageSize: LIST_PAGE_SIZE,
            })
        ).items;
    } catch (error) {
        errorMessage.value = describe(error, t('tours.listLoadFailed'));
    }
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
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>{{ t('nav.tours') }}</h1>
            <v-btn
                v-if="auth.can('dispatch.write')"
                data-testid="tour-add"
                @click="router.push('/tours/new')"
                >{{ t('tours.add') }}</v-btn
            >

            <!-- Plain <input>/<select>, not Vuetify controls: a data-testid on a Vuetify field
                 lands on its wrapper div and Playwright's fill()/selectOption() then fail.
                 See DriverForm.vue. -->
            <div>
                <label for="tour-date-filter">{{ t('common.date') }}</label>
                <input
                    id="tour-date-filter"
                    v-model="dateFilter"
                    type="date"
                    data-testid="tour-date-filter"
                    @change="refresh"
                />
            </div>
            <div>
                <label for="tour-vehicle-filter">{{ t('common.vehicle') }}</label>
                <select
                    id="tour-vehicle-filter"
                    v-model="vehicleFilter"
                    data-testid="tour-vehicle-filter"
                    @change="refresh"
                >
                    <option value="">{{ t('common.all') }}</option>
                    <option v-for="vehicle in vehicles" :key="vehicle.id" :value="vehicle.id">
                        {{ vehicle.licensePlate }}
                    </option>
                </select>
            </div>
            <div>
                <label for="tour-driver-filter">{{ t('common.driver') }}</label>
                <select
                    id="tour-driver-filter"
                    v-model="driverFilter"
                    data-testid="tour-driver-filter"
                    @change="refresh"
                >
                    <option value="">{{ t('common.all') }}</option>
                    <option v-for="driver in drivers" :key="driver.id" :value="driver.id">
                        {{ driver.lastName }}, {{ driver.firstName }}
                    </option>
                </select>
            </div>

            <p v-if="errorMessage" data-testid="tour-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-data-table
                v-else
                :headers="headers"
                :items="tours"
                :items-per-page="-1"
                item-value="id"
                data-testid="tour-table"
            >
                <template #item.tourDate="{ item }">
                    <span data-testid="tour-date">{{ item.tourDate }}</span>
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
                    <span data-testid="tour-status">{{ item.status }}</span>
                </template>
                <template #item.actions="{ item }">
                    <v-btn data-testid="tour-open" @click="router.push(`/tours/${item.id}`)">{{ t('common.open') }}</v-btn>
                </template>
            </v-data-table>
        </template>
        <p v-else>{{ t('tours.signInHint') }}</p>
    </v-container>
</template>
