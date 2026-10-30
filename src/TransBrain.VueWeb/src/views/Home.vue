<script setup lang="ts">
import axios from 'axios';
import { onMounted, ref } from 'vue';
import { useAuthStore } from '../stores/auth';
import type { Area, Capability } from '../auth/capabilities';
import { listVehicles } from '../api/vehicles';
import { listDrivers } from '../api/drivers';
import { listOrders, type Order } from '../api/orders';
import { completeTour, listTours, startTour, type Tour } from '../api/tours';
import { useI18n, type MessageKey } from '../i18n';

interface AreaTile {
    area: Area;
    title: MessageKey;
    description: MessageKey;
    route: string;
    addLabel: MessageKey;
    addRoute: string;
    addCapability: Capability;
}

const tiles: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'nav.vehicles',
        description: 'home.tile.vehicles',
        route: '/vehicles',
        addLabel: 'vehicles.add',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'nav.drivers',
        description: 'home.tile.drivers',
        route: '/drivers',
        addLabel: 'drivers.add',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'nav.orders',
        description: 'home.tile.orders',
        route: '/orders',
        addLabel: 'orders.new',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'nav.tours',
        description: 'home.tile.tours',
        route: '/tours',
        addLabel: 'tours.plan',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

/** One row is enough when only totalCount is read. */
const COUNT_ONLY = 1;

/** Five rows fit the block; the rest is one click away on /orders. */
const DRAFT_PREVIEW_SIZE = 5;

function today(): string {
    // The API takes a DateOnly, so an ISO date without a time part. Local date, not UTC:
    // toISOString() would roll over a day early for anyone east of Greenwich after their evening.
    const now = new Date();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
}

const auth = useAuthStore();
const { t } = useI18n();

const vehiclesAvailable = ref(0);
const vehiclesInWorkshop = ref(0);
const driversAvailable = ref(0);
const ordersInDraft = ref(0);
const toursToday = ref(0);
const draftOrders = ref<Order[]>([]);
const myTours = ref<Tour[]>([]);

// One error ref per block, not one for the page: a failing vehicle count must not blank the work
// list next to it.
const vehicleError = ref<string | null>(null);
const driverError = ref<string | null>(null);
const orderError = ref<string | null>(null);
const tourError = ref<string | null>(null);
const draftOrdersError = ref<string | null>(null);
const myToursError = ref<string | null>(null);

onMounted(async () => {
    await auth.load();
    if (auth.isAuthenticated) {
        await loadBlocks();
    }
});

async function loadBlocks(): Promise<void> {
    const jobs: Promise<void>[] = [];

    if (auth.areas.has('vehicles')) {
        jobs.push(
            (async () => {
                try {
                    vehiclesAvailable.value = (await listVehicles(COUNT_ONLY, 'Available')).totalCount;
                    vehiclesInWorkshop.value = (await listVehicles(COUNT_ONLY, 'InWorkshop')).totalCount;
                } catch (error) {
                    vehicleError.value = describe(error, t('home.error.vehicleCounts'));
                }
            })(),
        );
    }

    if (auth.areas.has('drivers')) {
        jobs.push(
            (async () => {
                try {
                    driversAvailable.value = (await listDrivers(COUNT_ONLY, 'Available')).totalCount;
                } catch (error) {
                    driverError.value = describe(error, t('home.error.driverCounts'));
                }
            })(),
        );
    }

    if (auth.areas.has('orders')) {
        jobs.push(
            (async () => {
                try {
                    ordersInDraft.value = (await listOrders('Draft', COUNT_ONLY)).totalCount;
                } catch (error) {
                    orderError.value = describe(error, t('home.error.orderCounts'));
                }
            })(),
        );
    }

    // Every role gets this one - for a fahrer the API narrows it to their own tours, see
    // ListToursQueryHandler. That is why the frontend never needs to know its own driverId.
    jobs.push(
        (async () => {
            try {
                const page = await listTours({ tourDate: today() });
                toursToday.value = page.totalCount;
                if (auth.hasRole('fahrer')) {
                    myTours.value = page.items;
                }
            } catch (error) {
                tourError.value = describe(error, t('home.error.todayTours'));
            }
        })(),
    );

    if (auth.can('dispatch.write')) {
        jobs.push(
            (async () => {
                try {
                    draftOrders.value = (await listOrders('Draft', DRAFT_PREVIEW_SIZE)).items;
                } catch (error) {
                    draftOrdersError.value = describe(error, t('home.error.draftOrders'));
                }
            })(),
        );
    }

    await Promise.all(jobs);
}

async function reloadMyTours(): Promise<void> {
    try {
        const page = await listTours({ tourDate: today() });
        myTours.value = page.items;
        toursToday.value = page.totalCount;
    } catch (error) {
        myToursError.value = describe(error, t('home.error.myToursReload'));
    }
}

async function start(tour: Tour): Promise<void> {
    myToursError.value = null;
    try {
        await startTour(tour.id);
        await reloadMyTours();
    } catch (error) {
        myToursError.value = describe(error, t('tours.startFailed'));
    }
}

async function complete(tour: Tour): Promise<void> {
    myToursError.value = null;
    try {
        await completeTour(tour.id);
        await reloadMyTours();
    } catch (error) {
        myToursError.value = describe(error, t('tours.completeFailed'));
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
            <h1 data-testid="home-greeting">{{ t('home.welcome', { name: auth.displayName }) }}</h1>
            <v-chip v-for="role in auth.roles" :key="role" data-testid="home-role-chip">{{ role }}</v-chip>

            <v-row class="mt-4">
                <v-col v-if="auth.areas.has('vehicles')" cols="12" md="3">
                    <v-card>
                        <v-card-title>{{ t('nav.vehicles') }}</v-card-title>
                        <v-card-text>
                            <p v-if="vehicleError" data-testid="home-kpi-vehicles-error">{{ vehicleError }}</p>
                            <template v-else>
                                <p>
                                    {{ t('home.available') }}
                                    <strong data-testid="home-kpi-vehicles-available">{{ vehiclesAvailable }}</strong>
                                </p>
                                <p>
                                    {{ t('home.inWorkshop') }}
                                    <strong data-testid="home-kpi-vehicles-workshop">{{ vehiclesInWorkshop }}</strong>
                                </p>
                            </template>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('drivers')" cols="12" md="3">
                    <v-card>
                        <v-card-title>{{ t('nav.drivers') }}</v-card-title>
                        <v-card-text>
                            <p v-if="driverError" data-testid="home-kpi-drivers-error">{{ driverError }}</p>
                            <p v-else>
                                {{ t('home.available') }}
                                <strong data-testid="home-kpi-drivers-available">{{ driversAvailable }}</strong>
                            </p>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('orders')" cols="12" md="3">
                    <v-card>
                        <v-card-title>{{ t('nav.orders') }}</v-card-title>
                        <v-card-text>
                            <p v-if="orderError" data-testid="home-kpi-orders-error">{{ orderError }}</p>
                            <p v-else>
                                {{ t('home.inDraft') }} <strong data-testid="home-kpi-orders-draft">{{ ordersInDraft }}</strong>
                            </p>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="3">
                    <v-card>
                        <v-card-title>{{ t('home.toursToday') }}</v-card-title>
                        <v-card-text>
                            <p v-if="tourError" data-testid="home-kpi-tours-error">{{ tourError }}</p>
                            <p v-else><strong data-testid="home-kpi-tours-today">{{ toursToday }}</strong></p>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <section v-if="auth.can('dispatch.write')" data-testid="home-draft-orders" class="mt-4">
                <h2>{{ t('home.draftOrdersTitle') }}</h2>
                <p v-if="draftOrdersError" data-testid="home-draft-orders-error">{{ draftOrdersError }}</p>
                <template v-else>
                    <v-list>
                        <v-list-item v-for="order in draftOrders" :key="order.id" data-testid="home-draft-order-row">
                            <v-list-item-title>
                                {{ order.orderNumber }} — {{ order.consignor.city }} → {{ order.consignee.city }}
                            </v-list-item-title>
                            <template #append>
                                <v-btn :to="`/orders/${order.id}`" data-testid="home-draft-order-open">{{ t('common.open') }}</v-btn>
                            </template>
                        </v-list-item>
                    </v-list>
                    <!-- No per-row "Plan" button: an order is put onto a tour by the picker on
                         /tours/:id, and no endpoint creates a tour from an order. -->
                    <v-btn to="/tours/new" data-testid="home-plan-tour">{{ t('home.planTour') }}</v-btn>
                </template>
            </section>

            <section v-if="auth.hasRole('fahrer')" data-testid="home-my-tours" class="mt-4">
                <h2>{{ t('home.myToursToday') }}</h2>
                <p v-if="myToursError" data-testid="home-my-tours-error">{{ myToursError }}</p>
                <v-card v-for="tour in myTours" :key="tour.id" class="mb-2" data-testid="home-my-tour-row">
                    <v-card-title>{{ tour.vehicleLicensePlate }} — {{ tour.status }}</v-card-title>
                    <v-card-actions>
                        <v-btn :to="`/tours/${tour.id}`">{{ t('common.open') }}</v-btn>
                        <v-btn v-if="tour.status === 'Planned'" data-testid="home-my-tour-start" @click="start(tour)">
                            {{ t('tours.start') }}
                        </v-btn>
                        <v-btn
                            v-if="tour.status === 'InProgress'"
                            data-testid="home-my-tour-complete"
                            @click="complete(tour)"
                        >
                            {{ t('tours.complete') }}
                        </v-btn>
                    </v-card-actions>
                </v-card>
            </section>

            <v-row class="mt-4">
                <template v-for="tile in tiles" :key="tile.area">
                    <v-col v-if="auth.areas.has(tile.area)" cols="12" md="3">
                        <v-card :data-testid="`home-tile-${tile.area}`">
                            <v-card-title>{{ t(tile.title) }}</v-card-title>
                            <v-card-text>{{ t(tile.description) }}</v-card-text>
                            <v-card-actions>
                                <v-btn :to="tile.route">{{ t('common.open') }}</v-btn>
                                <v-btn
                                    v-if="auth.can(tile.addCapability)"
                                    :to="tile.addRoute"
                                    :data-testid="`home-tile-${tile.area}-add`"
                                    >{{ t(tile.addLabel) }}</v-btn
                                >
                            </v-card-actions>
                        </v-card>
                    </v-col>
                </template>
            </v-row>
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">{{ t('common.signIn') }}</v-btn>
    </v-container>
</template>
