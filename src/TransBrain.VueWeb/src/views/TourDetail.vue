<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { listOrders, type Order } from '../api/orders';
import {
    assignOrderToTour,
    completeTour,
    getTour,
    removeOrderFromTour,
    startTour,
    type Tour,
} from '../api/tours';

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

const stopHeaders = [
    { title: '#', key: 'sequence' },
    { title: 'Order', key: 'orderNumber' },
    { title: 'Type', key: 'stopType' },
    { title: 'Actions', key: 'actions', sortable: false },
];

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
    } catch (error) {
        // The load path gets its own wording: "could not be assigned" on a failed get would be
        // plainly wrong.
        loadError.value = describe(error, 'The tour could not be loaded.');
    }
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
        actionError.value = describe(error, 'The assignable orders could not be loaded.');
    }
}

async function assign(): Promise<void> {
    if (!selectedOrderId.value) {
        actionError.value = 'Choose an order to assign first.';
        return;
    }

    await run(() => assignOrderToTour(tourId, selectedOrderId.value), 'The order could not be assigned.');
}

async function remove(transportOrderId: string): Promise<void> {
    await run(() => removeOrderFromTour(tourId, transportOrderId), 'The order could not be removed.');
}

async function start(): Promise<void> {
    await run(() => startTour(tourId), 'The tour could not be started.');
}

async function complete(): Promise<void> {
    await run(() => completeTour(tourId), 'The tour could not be completed.');
}

async function run(action: () => Promise<Tour>, fallback: string): Promise<void> {
    actionError.value = null;
    try {
        tour.value = await action();
        selectedOrderId.value = '';
        await loadDraftOrders();
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
        <h1>Tour</h1>
        <p v-if="loadError" data-testid="tour-detail-error">{{ loadError }}</p>

        <template v-if="tour">
            <!-- tour-detail-* rather than tour-*: the list uses tour-status/tour-date on its
                 cells, and a page-wide locator matching both screens would be ambiguous the
                 moment a test navigated between them. Mirrors the Angular detail page. -->
            <dl>
                <dt>Date</dt>
                <dd data-testid="tour-detail-date">{{ tour.tourDate }}</dd>
                <dt>Vehicle</dt>
                <dd data-testid="tour-detail-vehicle">{{ tour.vehicleLicensePlate }}</dd>
                <dt>Driver</dt>
                <dd data-testid="tour-detail-driver">{{ tour.driverName }}</dd>
                <dt>Status</dt>
                <dd data-testid="tour-detail-status">{{ tour.status }}</dd>
            </dl>

            <!-- The headroom a dispatcher needs before choosing the next order. The API already
                 sends both numbers, so showing them costs nothing extra. -->
            <section>
                <h2>Capacity</h2>
                <p data-testid="tour-capacity-weight">
                    {{ tour.totalWeightKg }} / {{ tour.vehiclePayloadKg }} kg
                </p>
                <progress :value="tour.totalWeightKg" :max="tour.vehiclePayloadKg"></progress>
                <p data-testid="tour-capacity-meters">
                    {{ tour.totalLoadMeters }} / {{ tour.vehicleLoadMeters }} load meters
                </p>
                <progress :value="tour.totalLoadMeters" :max="tour.vehicleLoadMeters"></progress>
            </section>

            <p v-if="actionError" data-testid="tour-action-error">{{ actionError }}</p>

            <section>
                <h2>Stops</h2>
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
                            v-if="item.stopType === 'Pickup'"
                            data-testid="tour-remove"
                            @click="remove(item.transportOrderId)"
                        >
                            Remove
                        </v-btn>
                    </template>
                </v-data-table>
            </section>

            <section>
                <h2>Assign an order</h2>
                <!-- Only Draft orders are offered: any other status is refused by the domain,
                     and offering a choice the server will reject is worse than not offering it. -->
                <div>
                    <label for="tour-assign-select">Order</label>
                    <select id="tour-assign-select" v-model="selectedOrderId" data-testid="tour-assign-select">
                        <option value="">-</option>
                        <option v-for="order in assignableOrders" :key="order.id" :value="order.id">
                            {{ order.orderNumber }} — {{ order.consignor.name }} ({{ order.cargoWeightKg }} kg)
                        </option>
                    </select>
                </div>
                <v-btn data-testid="tour-assign" @click="assign">Assign</v-btn>
            </section>

            <section>
                <!-- Hidden rather than shown-and-refused: a button that can only ever answer 409
                     is noise. Assign and Remove stay visible, matching how the order screens
                     treat Cancel - there the refusal message teaches the rule. -->
                <v-btn v-if="tour.status === 'Planned'" data-testid="tour-start" @click="start">
                    Start tour
                </v-btn>
                <v-btn v-if="tour.status === 'InProgress'" data-testid="tour-complete" @click="complete">
                    Complete tour
                </v-btn>
            </section>

            <v-btn data-testid="tour-back" @click="router.push('/tours')">Back to tours</v-btn>
        </template>
    </v-container>
</template>
