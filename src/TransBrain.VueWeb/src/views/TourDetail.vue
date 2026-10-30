<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { listOrders, type Order } from '../api/orders';
import {
    assignOrderToTour,
    completeTour,
    getTour,
    getTourRoute,
    removeOrderFromTour,
    startTour,
    type ScheduleEntry,
    type Tour,
    type TourRoute,
} from '../api/tours';
import LocationMap from '../components/LocationMap.vue';
import { useAuthStore } from '../stores/auth';
import { useI18n, type MessageKey } from '../i18n';

const auth = useAuthStore();
const { locale, t } = useI18n();
const route = useRoute();
const router = useRouter();

const tourId = route.params.id as string;
const tour = ref<Tour | null>(null);
const draftOrders = ref<Order[]>([]);
const selectedOrderId = ref('');
const loadError = ref<string | null>(null);
// Separate from loadError: a refused assignment must not blank the page the way a failed load
// does. This is where a 409 from the capacity or status rules reaches the user.
const actionError = ref<string | null>(null);
const tourRoute = ref<TourRoute | null>(null);
// Its own slot like actionError: a route the service cannot compute must not blank the tour.
const routeError = ref<string | null>(null);

const stopHeaders = computed(() => [
    { title: '#', key: 'sequence' },
    { title: t('common.order'), key: 'orderNumber' },
    { title: t('common.type'), key: 'stopType' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

const scheduleHeaders = computed(() => [
    { title: t('common.type'), key: 'kind' },
    { title: t('route.start'), key: 'start' },
    { title: t('route.end'), key: 'end' },
    { title: t('orders.country'), key: 'countryCode' },
]);

const kindLabels: Record<ScheduleEntry['kind'], MessageKey> = {
    Driving: 'route.kind.Driving',
    Break: 'route.kind.Break',
    Service: 'route.kind.Service',
};

const stopTypeLabels: Record<'Pickup' | 'Delivery', MessageKey> = {
    Pickup: 'route.stopType.Pickup',
    Delivery: 'route.stopType.Delivery',
};

const routeMarkers = computed(() =>
    (tourRoute.value?.stops ?? []).map((stop) => ({
        latitude: stop.latitude,
        longitude: stop.longitude,
        text: String(stop.sequence),
        title: `${stop.sequence}. ${stop.orderNumber} (${t(stopTypeLabels[stop.stopType])}, ${stop.place})`,
    })),
);

// Orders already on this tour are no longer Draft, but the picker's list is refreshed only
// alongside the tour, so filter defensively rather than offering a stale choice.
const assignableOrders = computed(() => {
    const assigned = new Set(tour.value?.stops.map((stop) => stop.transportOrderId) ?? []);
    return draftOrders.value.filter((order) => !assigned.has(order.id));
});

onMounted(refresh);

async function refresh(): Promise<void> {
    try {
        tour.value = await getTour(tourId);
        await loadDraftOrders();
        await loadRoute();
    } catch (error) {
        // The load path gets its own wording: "could not be assigned" on a failed get would be
        // plainly wrong.
        loadError.value = describe(error, t('tours.loadFailed'));
    }
}

// The route depends on what is assigned, so it is reloaded after every change to the stops.
// 503 means the routing service is not configured or unreachable - a friendly message, not the raw error.
async function loadRoute(): Promise<void> {
    try {
        tourRoute.value = await getTourRoute(tourId);
        routeError.value = null;
    } catch (error) {
        tourRoute.value = null;
        routeError.value =
            axios.isAxiosError(error) && error.response?.status === 503
                ? t('map.unavailable')
                : describe(error, t('route.loadFailed'));
    }
}

function formatKm(meters: number): string {
    return (meters / 1000).toLocaleString(locale.value, { maximumFractionDigits: 1 });
}

// h:mm, not decimal hours: dispatchers read driving time as a clock value.
function formatDuration(seconds: number): string {
    const totalMinutes = Math.round(seconds / 60);
    return `${Math.floor(totalMinutes / 60)}:${String(totalMinutes % 60).padStart(2, '0')}`;
}

function formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString(locale.value, { dateStyle: 'short', timeStyle: 'short' });
}

// Orders come back sorted by order number ASCENDING, so page 1 is the OLDEST drafts - in a
// running installation the ones nobody has planned for weeks, not the ones a dispatcher is
// working on today. Fetch the last page instead: one extra request only when there are more
// drafts than fit on a page. Beyond the API's 100-row cap a searchable picker is needed.
async function loadDraftOrders(): Promise<void> {
    const pageSize = 100;
    try {
        const first = await listOrders('Draft', pageSize);
        const lastPage = Math.max(1, Math.ceil(first.totalCount / pageSize));
        draftOrders.value = lastPage === 1 ? first.items : (await listOrders('Draft', pageSize, lastPage)).items;
    } catch (error) {
        actionError.value = describe(error, t('tours.assignableFailed'));
    }
}

async function assign(): Promise<void> {
    if (!selectedOrderId.value) {
        actionError.value = t('tours.chooseOrderFirst');
        return;
    }

    await run(() => assignOrderToTour(tourId, selectedOrderId.value), t('tours.assignFailed'));
}

async function remove(transportOrderId: string): Promise<void> {
    await run(() => removeOrderFromTour(tourId, transportOrderId), t('tours.removeFailed'));
}

async function start(): Promise<void> {
    await run(() => startTour(tourId), t('tours.startFailed'));
}

async function complete(): Promise<void> {
    await run(() => completeTour(tourId), t('tours.completeFailed'));
}

async function run(action: () => Promise<Tour>, fallback: string): Promise<void> {
    actionError.value = null;
    try {
        tour.value = await action();
        selectedOrderId.value = '';
        await loadDraftOrders();
        await loadRoute();
    } catch (error) {
        actionError.value = describe(error, fallback);
    }
}

// A 403 from the authorization middleware carries no ProblemDetails body at all, while a 403
// from TourAccess does - hence a per-call-site fallback rather than one shared string.
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
        <h1>{{ t('tours.detail') }}</h1>
        <p v-if="loadError" data-testid="tour-detail-error">{{ loadError }}</p>

        <template v-if="tour">
            <!-- tour-detail-* rather than tour-*: the list uses tour-status/tour-date on its
                 cells, and a page-wide locator matching both screens would be ambiguous the
                 moment a test navigated between them. Mirrors the Angular detail page. -->
            <dl>
                <dt>{{ t('common.date') }}</dt>
                <dd data-testid="tour-detail-date">{{ tour.tourDate }}</dd>
                <dt>{{ t('common.vehicle') }}</dt>
                <dd data-testid="tour-detail-vehicle">{{ tour.vehicleLicensePlate }}</dd>
                <dt>{{ t('common.driver') }}</dt>
                <dd data-testid="tour-detail-driver">{{ tour.driverName }}</dd>
                <dt>{{ t('common.status') }}</dt>
                <dd data-testid="tour-detail-status">{{ tour.status }}</dd>
            </dl>

            <!-- The headroom a dispatcher needs before choosing the next order. The API already
                 sends both numbers, so showing them costs nothing extra. -->
            <section>
                <h2>{{ t('tours.capacity') }}</h2>
                <p data-testid="tour-capacity-weight">
                    {{ tour.totalWeightKg }} / {{ tour.vehiclePayloadKg }} kg
                </p>
                <progress :value="tour.totalWeightKg" :max="tour.vehiclePayloadKg"></progress>
                <p data-testid="tour-capacity-meters">
                    {{ tour.totalLoadMeters }} / {{ tour.vehicleLoadMeters }} {{ t('tours.loadMetersUnit') }}
                </p>
                <progress :value="tour.totalLoadMeters" :max="tour.vehicleLoadMeters"></progress>
            </section>

            <p v-if="actionError" data-testid="tour-action-error">{{ actionError }}</p>

            <section>
                <h2>{{ t('tours.stops') }}</h2>
                <v-data-table
                    :headers="stopHeaders"
                    :items="tour.stops"
                    :items-per-page="-1"
                    item-value="sequence"
                    data-testid="tour-stop-table"
                >
                    <template #item.sequence="{ item }">
                        <span data-testid="tour-stop-sequence">{{ item.sequence }}</span>
                    </template>
                    <template #item.orderNumber="{ item }">
                        <span data-testid="tour-stop-order">{{ item.orderNumber }}</span>
                    </template>
                    <template #item.stopType="{ item }">
                        <span data-testid="tour-stop-type">{{ item.stopType }}</span>
                    </template>
                    <template #item.actions="{ item }">
                        <v-btn
                            v-if="item.stopType === 'Pickup' && auth.can('dispatch.write')"
                            data-testid="tour-remove"
                            @click="remove(item.transportOrderId)"
                        >
                            {{ t('tours.remove') }}
                        </v-btn>
                    </template>
                </v-data-table>
            </section>

            <section data-testid="tour-route-section">
                <h2>{{ t('common.route') }}</h2>
                <p v-if="routeError" data-testid="tour-route-error">{{ routeError }}</p>
                <template v-if="tourRoute">
                    <dl>
                        <dt>{{ t('route.distance') }}</dt>
                        <dd data-testid="tour-route-distance">{{ formatKm(tourRoute.distanceMeters) }}</dd>
                        <dt>{{ t('route.drivingTime') }}</dt>
                        <dd data-testid="tour-route-driving">{{ formatDuration(tourRoute.drivingSeconds) }}</dd>
                        <dt>{{ t('route.earliestNextStart') }}</dt>
                        <dd data-testid="tour-route-earliest">{{ formatDateTime(tourRoute.earliestNextStart) }}</dd>
                    </dl>

                    <LocationMap :markers="routeMarkers" :polyline="tourRoute.geometry" data-testid="tour-route-map" />

                    <h3>{{ t('route.schedule') }}</h3>
                    <v-data-table
                        :headers="scheduleHeaders"
                        :items="tourRoute.schedule"
                        :items-per-page="-1"
                        item-value="start"
                        data-testid="tour-route-schedule"
                    >
                        <template #item.kind="{ item }">{{ t(kindLabels[item.kind]) }}</template>
                        <template #item.start="{ item }">{{ formatDateTime(item.start) }}</template>
                        <template #item.end="{ item }">{{ formatDateTime(item.end) }}</template>
                        <template #item.countryCode="{ item }">{{ item.countryCode ?? '' }}</template>
                    </v-data-table>

                    <h3>{{ t('route.violations') }}</h3>
                    <p v-if="tourRoute.violations.length === 0" data-testid="tour-route-no-violations">
                        {{ t('route.noViolations') }}
                    </p>
                    <v-alert
                        v-for="(violation, index) in tourRoute.violations"
                        :key="index"
                        :type="violation.severity === 'Violation' ? 'error' : 'warning'"
                        variant="tonal"
                        data-testid="tour-route-violation"
                    >
                        {{ violation.message }}
                    </v-alert>
                </template>
            </section>

            <section v-if="auth.can('dispatch.write')">
                <h2>{{ t('tours.assignOrder') }}</h2>
                <!-- Only Draft orders are offered: any other status is refused by the domain,
                     and offering a choice the server will reject is worse than not offering it. -->
                <div>
                    <label for="tour-assign-select">{{ t('common.order') }}</label>
                    <select id="tour-assign-select" v-model="selectedOrderId" data-testid="tour-assign-select">
                        <option value="">-</option>
                        <option v-for="order in assignableOrders" :key="order.id" :value="order.id">
                            {{ order.orderNumber }} — {{ order.consignor.name }} ({{ order.cargoWeightKg }} kg)
                        </option>
                    </select>
                </div>
                <v-btn data-testid="tour-assign" @click="assign">{{ t('tours.assign') }}</v-btn>
            </section>

            <!-- tourStatus.write, not dispatch.write: a fahrer may run their own tour without
                 being allowed to change what is on it. -->
            <section v-if="auth.can('tourStatus.write')">
                <!-- Hidden rather than shown-and-refused: a button that can only ever answer 409
                     is noise. Assign and Remove stay visible, matching how the order screens
                     treat Cancel - there the refusal message teaches the rule. -->
                <v-btn v-if="tour.status === 'Planned'" data-testid="tour-start" @click="start">
                    {{ t('tours.start') }}
                </v-btn>
                <v-btn v-if="tour.status === 'InProgress'" data-testid="tour-complete" @click="complete">
                    {{ t('tours.complete') }}
                </v-btn>
            </section>

            <v-btn data-testid="tour-back" @click="router.push('/tours')">{{ t('tours.back') }}</v-btn>
        </template>
    </v-container>
</template>
