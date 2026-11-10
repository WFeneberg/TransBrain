<script setup lang="ts">
import axios from 'axios';
import { onMounted, ref } from 'vue';
import { useAuthStore } from '../stores/auth';
import type { Area, Capability } from '../auth/capabilities';
import { listVehicles } from '../api/vehicles';
import { listDrivers } from '../api/drivers';
import { listOrders, type Order } from '../api/orders';
import { completeTour, listTours, startTour, type Tour } from '../api/tours';
import { statusColor } from '../composables/statusColor';
import { useI18n, type MessageKey } from '../i18n';

interface AreaTile {
    area: Area;
    icon: string;
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
        icon: 'mdi-truck-outline',
        title: 'nav.vehicles',
        description: 'home.tile.vehicles',
        route: '/vehicles',
        addLabel: 'vehicles.add',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        icon: 'mdi-account-outline',
        title: 'nav.drivers',
        description: 'home.tile.drivers',
        route: '/drivers',
        addLabel: 'drivers.add',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        icon: 'mdi-clipboard-list-outline',
        title: 'nav.orders',
        description: 'home.tile.orders',
        route: '/orders',
        addLabel: 'orders.new',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        icon: 'mdi-map-marker-path',
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
    <v-container class="page-container">
        <template v-if="auth.isAuthenticated">
            <div class="page-header">
                <div>
                    <h1 class="text-h4" data-testid="home-greeting">{{ t('home.welcome', { name: auth.displayName }) }}</h1>
                    <div class="role-chips">
                        <v-chip
                            v-for="role in auth.roles"
                            :key="role"
                            color="primary"
                            size="small"
                            variant="tonal"
                            prepend-icon="mdi-shield-account-outline"
                            data-testid="home-role-chip"
                            >{{ role }}</v-chip
                        >
                    </div>
                </div>
            </div>

            <v-row>
                <v-col v-if="auth.areas.has('vehicles')" cols="12" sm="6" md="3">
                    <v-card border flat class="section-card">
                        <v-card-text class="kpi">
                            <v-avatar color="primary" variant="tonal" size="48"><v-icon icon="mdi-truck" /></v-avatar>
                            <div class="kpi-body">
                                <div class="text-subtitle-2 text-medium-emphasis">{{ t('nav.vehicles') }}</div>
                                <p v-if="vehicleError" class="text-error kpi-error" data-testid="home-kpi-vehicles-error">
                                    {{ vehicleError }}
                                </p>
                                <div v-else class="kpi-figures">
                                    <div>
                                        <div class="text-h4 font-weight-bold" data-testid="home-kpi-vehicles-available">{{ vehiclesAvailable }}</div>
                                        <div class="text-caption text-medium-emphasis">{{ t('home.available') }}</div>
                                    </div>
                                    <div>
                                        <div class="text-h4 font-weight-bold" data-testid="home-kpi-vehicles-workshop">{{ vehiclesInWorkshop }}</div>
                                        <div class="text-caption text-medium-emphasis">{{ t('home.inWorkshop') }}</div>
                                    </div>
                                </div>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('drivers')" cols="12" sm="6" md="3">
                    <v-card border flat class="section-card">
                        <v-card-text class="kpi">
                            <v-avatar color="primary" variant="tonal" size="48"><v-icon icon="mdi-account" /></v-avatar>
                            <div class="kpi-body">
                                <div class="text-subtitle-2 text-medium-emphasis">{{ t('nav.drivers') }}</div>
                                <p v-if="driverError" class="text-error kpi-error" data-testid="home-kpi-drivers-error">
                                    {{ driverError }}
                                </p>
                                <div v-else>
                                    <div class="text-h4 font-weight-bold" data-testid="home-kpi-drivers-available">{{ driversAvailable }}</div>
                                    <div class="text-caption text-medium-emphasis">{{ t('home.available') }}</div>
                                </div>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('orders')" cols="12" sm="6" md="3">
                    <v-card border flat class="section-card">
                        <v-card-text class="kpi">
                            <v-avatar color="primary" variant="tonal" size="48"><v-icon icon="mdi-clipboard-list" /></v-avatar>
                            <div class="kpi-body">
                                <div class="text-subtitle-2 text-medium-emphasis">{{ t('nav.orders') }}</div>
                                <p v-if="orderError" class="text-error kpi-error" data-testid="home-kpi-orders-error">
                                    {{ orderError }}
                                </p>
                                <div v-else>
                                    <div class="text-h4 font-weight-bold" data-testid="home-kpi-orders-draft">{{ ordersInDraft }}</div>
                                    <div class="text-caption text-medium-emphasis">{{ t('home.inDraft') }}</div>
                                </div>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" sm="6" md="3">
                    <v-card border flat class="section-card">
                        <v-card-text class="kpi">
                            <v-avatar color="primary" variant="tonal" size="48"><v-icon icon="mdi-map-marker-path" /></v-avatar>
                            <div class="kpi-body">
                                <div class="text-subtitle-2 text-medium-emphasis">{{ t('nav.tours') }}</div>
                                <p v-if="tourError" class="text-error kpi-error" data-testid="home-kpi-tours-error">{{ tourError }}</p>
                                <div v-else>
                                    <div class="text-h4 font-weight-bold" data-testid="home-kpi-tours-today">{{ toursToday }}</div>
                                    <div class="text-caption text-medium-emphasis">{{ t('home.today') }}</div>
                                </div>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <v-card v-if="auth.can('dispatch.write')" border flat class="table-card home-section" data-testid="home-draft-orders">
                <v-card-item prepend-icon="mdi-clipboard-list-outline" :title="t('home.draftOrdersTitle')">
                    <!-- No per-row "Plan" button: an order is put onto a tour by the picker on
                         /tours/:id, and no endpoint creates a tour from an order. -->
                    <template v-if="!draftOrdersError" #append>
                        <v-btn color="primary" variant="tonal" prepend-icon="mdi-plus" to="/tours/new" data-testid="home-plan-tour">{{
                            t('home.planTour')
                        }}</v-btn>
                    </template>
                </v-card-item>
                <v-card-text v-if="draftOrdersError">
                    <p class="text-error" data-testid="home-draft-orders-error">{{ draftOrdersError }}</p>
                </v-card-text>
                <v-table v-else hover>
                    <thead>
                        <tr>
                            <th>{{ t('orders.orderNumber') }}</th>
                            <th>{{ t('orders.consignor') }}</th>
                            <th>{{ t('orders.consignee') }}</th>
                            <th class="text-end">{{ t('common.actions') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="order in draftOrders" :key="order.id" data-testid="home-draft-order-row">
                            <td class="font-weight-bold">{{ order.orderNumber }}</td>
                            <td>{{ order.consignor.city }}</td>
                            <td>{{ order.consignee.city }}</td>
                            <td class="text-end">
                                <v-tooltip :text="t('common.open')" location="top">
                                    <template #activator="{ props: tooltip }">
                                        <v-btn
                                            v-bind="tooltip"
                                            icon="mdi-eye-outline"
                                            variant="text"
                                            size="small"
                                            :aria-label="t('common.open')"
                                            :to="`/orders/${order.id}`"
                                            data-testid="home-draft-order-open"
                                        />
                                    </template>
                                </v-tooltip>
                            </td>
                        </tr>
                        <tr v-if="draftOrders.length === 0">
                            <td colspan="4" class="text-center text-medium-emphasis">{{ t('home.draftOrdersEmpty') }}</td>
                        </tr>
                    </tbody>
                </v-table>
            </v-card>

            <v-card v-if="auth.hasRole('fahrer')" border flat class="table-card home-section" data-testid="home-my-tours">
                <v-card-item prepend-icon="mdi-steering" :title="t('home.myToursToday')" />
                <v-card-text v-if="myToursError">
                    <p class="text-error" data-testid="home-my-tours-error">{{ myToursError }}</p>
                </v-card-text>
                <v-table hover>
                    <thead>
                        <tr>
                            <th>{{ t('common.vehicle') }}</th>
                            <th>{{ t('tours.stops') }}</th>
                            <th>{{ t('common.status') }}</th>
                            <th class="text-end">{{ t('common.actions') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="tour in myTours" :key="tour.id" data-testid="home-my-tour-row">
                            <td class="font-weight-bold">{{ tour.vehicleLicensePlate }}</td>
                            <td>{{ tour.stops.length }}</td>
                            <td>
                                <v-chip :color="statusColor(tour.status)" size="small" variant="tonal">{{ tour.status }}</v-chip>
                            </td>
                            <td class="text-end">
                                <div class="row-actions my-tour-actions">
                                    <v-btn
                                        v-if="tour.status === 'Planned'"
                                        color="primary"
                                        variant="tonal"
                                        size="small"
                                        prepend-icon="mdi-play"
                                        data-testid="home-my-tour-start"
                                        @click="start(tour)"
                                        >{{ t('tours.start') }}</v-btn
                                    >
                                    <v-btn
                                        v-if="tour.status === 'InProgress'"
                                        color="success"
                                        variant="tonal"
                                        size="small"
                                        prepend-icon="mdi-flag-checkered"
                                        data-testid="home-my-tour-complete"
                                        @click="complete(tour)"
                                        >{{ t('tours.complete') }}</v-btn
                                    >
                                    <v-tooltip :text="t('common.open')" location="top">
                                        <template #activator="{ props: tooltip }">
                                            <v-btn
                                                v-bind="tooltip"
                                                icon="mdi-eye-outline"
                                                variant="text"
                                                size="small"
                                                :aria-label="t('common.open')"
                                                :to="`/tours/${tour.id}`"
                                            />
                                        </template>
                                    </v-tooltip>
                                </div>
                            </td>
                        </tr>
                        <tr v-if="myTours.length === 0">
                            <td colspan="4" class="text-center text-medium-emphasis">{{ t('home.myToursEmpty') }}</td>
                        </tr>
                    </tbody>
                </v-table>
            </v-card>

            <v-row class="home-section">
                <template v-for="tile in tiles" :key="tile.area">
                    <v-col v-if="auth.areas.has(tile.area)" cols="12" sm="6" md="3">
                        <v-card border flat class="section-card tile" :data-testid="`home-tile-${tile.area}`">
                            <v-card-item :prepend-icon="tile.icon" :title="t(tile.title)" />
                            <v-card-text class="tile-text">{{ t(tile.description) }}</v-card-text>
                            <v-card-actions>
                                <v-btn variant="text" append-icon="mdi-arrow-right" :to="tile.route">{{ t('common.open') }}</v-btn>
                                <v-spacer />
                                <v-btn
                                    v-if="auth.can(tile.addCapability)"
                                    color="primary"
                                    variant="tonal"
                                    prepend-icon="mdi-plus"
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
        <v-btn v-else color="primary" prepend-icon="mdi-login" data-testid="login" @click="auth.login()">{{
            t('common.signIn')
        }}</v-btn>
    </v-container>
</template>

<style scoped>
.role-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 8px;
}

.kpi {
    display: flex;
    align-items: flex-start;
    gap: 16px;
}

.kpi-body {
    min-width: 0;
}

.kpi-figures {
    display: flex;
    gap: 24px;
}

.kpi-error {
    margin: 4px 0 0;
}

.home-section {
    margin-top: 24px;
}

.my-tour-actions {
    align-items: center;
}

.tile {
    display: flex;
    flex-direction: column;
}

.tile-text {
    flex-grow: 1;
}
</style>
