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
import { formatCalendarDate, statusColor } from '../composables/statusColor';
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

function percentOf(value: number, max: number): number {
    return max > 0 ? Math.min(100, (value / max) * 100) : 0;
}

function capacityColor(value: number, max: number): string {
    const ratio = max > 0 ? value / max : 0;
    return ratio > 1 ? 'error' : ratio >= 0.9 ? 'warning' : 'primary';
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
    <v-container class="page-container">
        <v-btn
            variant="text"
            prepend-icon="mdi-arrow-left"
            class="back-button"
            data-testid="tour-back"
            @click="router.push('/tours')"
            >{{ t('tours.back') }}</v-btn
        >

        <div class="page-header form-header">
            <div class="title-row">
                <h1 class="text-h4">{{ t('tours.detail') }}</h1>
                <v-chip
                    v-if="tour"
                    :color="statusColor(tour.status)"
                    size="small"
                    variant="tonal"
                    data-testid="tour-detail-status"
                    >{{ tour.status }}</v-chip
                >
            </div>
            <!-- tourStatus.write, not dispatch.write: a fahrer may run their own tour without
                 being allowed to change what is on it. Hidden rather than shown-and-refused: a
                 button that can only ever answer 409 is noise. Assign and Remove stay visible,
                 matching how the order screens treat Cancel - there the refusal message teaches
                 the rule. -->
            <div v-if="tour && auth.can('tourStatus.write')" class="header-actions">
                <v-btn
                    v-if="tour.status === 'Planned'"
                    color="primary"
                    prepend-icon="mdi-play"
                    data-testid="tour-start"
                    @click="start"
                    >{{ t('tours.start') }}</v-btn
                >
                <v-btn
                    v-if="tour.status === 'InProgress'"
                    color="success"
                    prepend-icon="mdi-flag-checkered"
                    data-testid="tour-complete"
                    @click="complete"
                    >{{ t('tours.complete') }}</v-btn
                >
            </div>
        </div>

        <p v-if="loadError" class="text-error error-line" data-testid="tour-detail-error">{{ loadError }}</p>

        <template v-if="tour">
            <p v-if="actionError" class="text-error error-line" data-testid="tour-action-error">{{ actionError }}</p>

            <v-row>
                <v-col cols="12" md="6">
                    <!-- tour-detail-* rather than tour-*: the list uses tour-status/tour-date on
                         its cells, and a page-wide locator matching both screens would be
                         ambiguous the moment a test navigated between them. Mirrors the Angular
                         detail page. -->
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-information-outline" :title="t('tours.section.data')" />
                        <v-card-text>
                            <dl class="detail-list">
                                <dt class="text-medium-emphasis">{{ t('common.date') }}</dt>
                                <dd data-testid="tour-detail-date">{{ formatCalendarDate(tour.tourDate, locale) }}</dd>
                                <dt class="text-medium-emphasis">{{ t('common.vehicle') }}</dt>
                                <dd data-testid="tour-detail-vehicle">{{ tour.vehicleLicensePlate }}</dd>
                                <dt class="text-medium-emphasis">{{ t('common.driver') }}</dt>
                                <dd data-testid="tour-detail-driver">{{ tour.driverName }}</dd>
                            </dl>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="6">
                    <!-- The headroom a dispatcher needs before choosing the next order. The API
                         already sends both numbers, so showing them costs nothing extra. -->
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-gauge" :title="t('tours.capacity')" />
                        <v-card-text>
                            <div class="capacity-line">
                                <span class="text-medium-emphasis">{{ t('tours.capacityWeight') }}</span>
                                <span data-testid="tour-capacity-weight">{{ tour.totalWeightKg }} / {{ tour.vehiclePayloadKg }} kg</span>
                            </div>
                            <v-progress-linear
                                :model-value="percentOf(tour.totalWeightKg, tour.vehiclePayloadKg)"
                                :color="capacityColor(tour.totalWeightKg, tour.vehiclePayloadKg)"
                                height="8"
                                rounded
                                class="capacity-bar"
                            />
                            <div class="capacity-line">
                                <span class="text-medium-emphasis">{{ t('common.loadMeters') }}</span>
                                <span data-testid="tour-capacity-meters"
                                    >{{ tour.totalLoadMeters }} / {{ tour.vehicleLoadMeters }} {{ t('tours.loadMetersUnit') }}</span
                                >
                            </div>
                            <v-progress-linear
                                :model-value="percentOf(tour.totalLoadMeters, tour.vehicleLoadMeters)"
                                :color="capacityColor(tour.totalLoadMeters, tour.vehicleLoadMeters)"
                                height="8"
                                rounded
                                class="capacity-bar"
                            />
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="table-card">
                        <v-card-item prepend-icon="mdi-map-marker-path" :title="t('tours.stops')" />
                        <v-data-table
                            :headers="stopHeaders"
                            :items="tour.stops"
                            :items-per-page="-1"
                            :no-data-text="t('tours.stopsEmpty')"
                            item-value="sequence"
                            hide-default-footer
                            hover
                            data-testid="tour-stop-table"
                        >
                            <template #item.sequence="{ item }">
                                <span class="font-weight-bold" data-testid="tour-stop-sequence">{{ item.sequence }}</span>
                            </template>
                            <template #item.orderNumber="{ item }">
                                <span data-testid="tour-stop-order">{{ item.orderNumber }}</span>
                            </template>
                            <template #item.stopType="{ item }">
                                <v-chip
                                    :color="item.stopType === 'Pickup' ? 'info' : 'success'"
                                    :prepend-icon="item.stopType === 'Pickup' ? 'mdi-arrow-up-bold-box-outline' : 'mdi-arrow-down-bold-box-outline'"
                                    size="small"
                                    variant="tonal"
                                    data-testid="tour-stop-type"
                                    >{{ item.stopType }}</v-chip
                                >
                            </template>
                            <template #item.actions="{ item }">
                                <div class="row-actions">
                                    <v-tooltip
                                        v-if="item.stopType === 'Pickup' && auth.can('dispatch.write')"
                                        :text="t('tours.remove')"
                                        location="top"
                                    >
                                        <template #activator="{ props: tooltip }">
                                            <v-btn
                                                v-bind="tooltip"
                                                icon="mdi-link-variant-off"
                                                variant="text"
                                                size="small"
                                                color="error"
                                                :aria-label="t('tours.remove')"
                                                data-testid="tour-remove"
                                                @click="remove(item.transportOrderId)"
                                            />
                                        </template>
                                    </v-tooltip>
                                </div>
                            </template>
                        </v-data-table>
                    </v-card>
                </v-col>

                <v-col v-if="auth.can('dispatch.write')" cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-clipboard-plus-outline" :title="t('tours.assignOrder')" />
                        <v-card-text>
                            <!-- Only Draft orders are offered: any other status is refused by the
                                 domain, and offering a choice the server will reject is worse than
                                 not offering it. Deliberately a native <select>: the e2e picks the
                                 option by its consignor text via #tour-assign-select. -->
                            <div class="assign-row">
                                <div class="assign-field">
                                    <label for="tour-assign-select" class="text-caption text-medium-emphasis">{{
                                        t('common.order')
                                    }}</label>
                                    <select
                                        id="tour-assign-select"
                                        v-model="selectedOrderId"
                                        class="native-select"
                                        data-testid="tour-assign-select"
                                    >
                                        <option value="">-</option>
                                        <option v-for="order in assignableOrders" :key="order.id" :value="order.id">
                                            {{ order.orderNumber }} — {{ order.consignor.name }} ({{ order.cargoWeightKg }} kg)
                                        </option>
                                    </select>
                                </div>
                                <v-btn color="primary" prepend-icon="mdi-plus" data-testid="tour-assign" @click="assign">{{
                                    t('tours.assign')
                                }}</v-btn>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="tour-route-section">
                        <v-card-item prepend-icon="mdi-routes" :title="t('common.route')" />
                        <v-card-text>
                            <p v-if="routeError" class="text-error error-line" data-testid="tour-route-error">{{ routeError }}</p>
                            <template v-if="tourRoute">
                                <dl class="route-figures">
                                    <div>
                                        <dt class="text-medium-emphasis">{{ t('route.distance') }}</dt>
                                        <dd class="text-h6" data-testid="tour-route-distance">{{ formatKm(tourRoute.distanceMeters) }}</dd>
                                    </div>
                                    <div>
                                        <dt class="text-medium-emphasis">{{ t('route.drivingTime') }}</dt>
                                        <dd class="text-h6" data-testid="tour-route-driving">{{ formatDuration(tourRoute.drivingSeconds) }}</dd>
                                    </div>
                                    <div>
                                        <dt class="text-medium-emphasis">{{ t('route.earliestNextStart') }}</dt>
                                        <dd class="text-h6" data-testid="tour-route-earliest">{{ formatDateTime(tourRoute.earliestNextStart) }}</dd>
                                    </div>
                                </dl>

                                <div class="map-frame">
                                    <LocationMap :markers="routeMarkers" :polyline="tourRoute.geometry" data-testid="tour-route-map" />
                                </div>

                                <div class="section-group-title route-subtitle">{{ t('route.schedule') }}</div>
                                <v-card border flat class="table-card">
                                    <v-data-table
                                        :headers="scheduleHeaders"
                                        :items="tourRoute.schedule"
                                        :items-per-page="-1"
                                        item-value="start"
                                        hide-default-footer
                                        density="compact"
                                        data-testid="tour-route-schedule"
                                    >
                                        <template #item.kind="{ item }">{{ t(kindLabels[item.kind]) }}</template>
                                        <template #item.start="{ item }">{{ formatDateTime(item.start) }}</template>
                                        <template #item.end="{ item }">{{ formatDateTime(item.end) }}</template>
                                        <template #item.countryCode="{ item }">{{ item.countryCode ?? '' }}</template>
                                    </v-data-table>
                                </v-card>

                                <div class="section-group-title route-subtitle">{{ t('route.violations') }}</div>
                                <p v-if="tourRoute.violations.length === 0" class="section-hint" data-testid="tour-route-no-violations">
                                    <v-icon icon="mdi-check-circle-outline" color="success" size="small" />
                                    {{ t('route.noViolations') }}
                                </p>
                                <v-alert
                                    v-for="(violation, index) in tourRoute.violations"
                                    :key="index"
                                    :type="violation.severity === 'Violation' ? 'error' : 'warning'"
                                    variant="tonal"
                                    density="compact"
                                    class="violation"
                                    data-testid="tour-route-violation"
                                >
                                    {{ violation.message }}
                                </v-alert>
                            </template>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>
        </template>
    </v-container>
</template>

<style scoped>
.detail-list {
    display: grid;
    grid-template-columns: max-content 1fr;
    gap: 8px 24px;
    margin: 0;
}

.detail-list dd {
    margin: 0;
    font-weight: 500;
}

.capacity-line {
    display: flex;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 6px;
}

.capacity-bar {
    margin-bottom: 16px;
}

.assign-row {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    gap: 12px;
}

.assign-field {
    display: flex;
    flex: 1 1 320px;
    flex-direction: column;
    gap: 4px;
}

.native-select {
    min-height: 48px;
    padding: 0 12px;
    border: thin solid rgba(var(--v-theme-on-surface), 0.38);
    border-radius: 4px;
    background: rgb(var(--v-theme-surface));
    color: rgb(var(--v-theme-on-surface));
    font: inherit;
}

.native-select:focus {
    outline: 2px solid rgb(var(--v-theme-primary));
    outline-offset: -1px;
}

.route-figures {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 16px;
    margin: 0 0 16px;
}

.route-figures dd {
    margin: 0;
}

.route-subtitle {
    margin-top: 16px;
}

.violation + .violation {
    margin-top: 8px;
}
</style>
