<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { cancelOrder, listOrders, type Order } from '../api/orders';
import { ORDER_STATUSES, statusColor } from '../composables/statusColor';
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
const router = useRouter();
const orders = ref<Order[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed cancel must not hide the table the way a failed list
// load does (mirrors DriverList.vue's actionError).
const actionError = ref<string | null>(null);
const search = ref<string | null>('');
const statusFilter = ref('');
const pendingCancelId = ref<string | null>(null);

const statusOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...ORDER_STATUSES.map((status) => ({ title: status, value: status })),
]);

const headers = computed(() => [
    { title: t('orders.orderNumber'), key: 'orderNumber' },
    { title: t('orders.consignor'), key: 'consignor', value: (order: Order) => order.consignor.name },
    { title: t('orders.consignee'), key: 'consignee', value: (order: Order) => order.consignee.name },
    { title: t('orders.cargo'), key: 'cargoDescription' },
    { title: t('orders.pickup'), key: 'pickupFrom' },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredOrders = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    if (!term) {
        return orders.value;
    }
    return orders.value.filter((order) =>
        [
            order.orderNumber,
            order.consignor.name,
            order.consignor.city,
            order.consignee.name,
            order.consignee.city,
            order.cargoDescription,
        ].some((value) => value.toLowerCase().includes(term)),
    );
});

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        orders.value = (await listOrders(statusFilter.value, LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('orders.listLoadFailed'));
    } finally {
        loading.value = false;
    }
}

// Server-side, unlike the search: the status is a query parameter of the order list.
async function filterByStatus(status: string | null): Promise<void> {
    statusFilter.value = status ?? '';
    pendingCancelId.value = null;
    await refresh();
}

function askToCancel(order: Order): void {
    actionError.value = null;
    pendingCancelId.value = order.id;
}

function abortCancel(): void {
    pendingCancelId.value = null;
}

// Shown for every order rather than hidden for the ones the domain will refuse: an order
// already in transit answers 409 with a message explaining why, which is more useful than a
// button that silently is not there. Same reasoning as the master-data delete actions.
async function cancel(order: Order): Promise<void> {
    actionError.value = null;
    pendingCancelId.value = null;
    try {
        await cancelOrder(order.id);
        await refresh();
    } catch (error) {
        // A policy failure (e.g. a viewer's 403) is rejected by ASP.NET's authorization
        // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
        // the fallback text is therefore action-specific, not the list-load one.
        actionError.value = describe(error, t('orders.cancelFailed'));
    }
}

function formatInstant(iso: string): string {
    return new Date(iso).toLocaleString(locale.value === 'de' ? 'de-DE' : 'en-US');
}

// ProblemDetails' `errors` dictionary is now keyed by field name, but this component has no
// form fields to bind those keys onto - it only ever needs the free-text summary, which
// `title`/`detail` already provide.
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
                    <h1 class="text-h4">{{ t('nav.orders') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('orders.count', { count: orders.length }) }}</p>
                </div>
                <div class="header-actions">
                    <v-btn
                        variant="tonal"
                        prepend-icon="mdi-tune-variant"
                        data-testid="order-catalog-link"
                        @click="router.push('/order-catalog')"
                        >{{ t('orders.catalogLink') }}</v-btn
                    >
                    <v-btn
                        v-if="auth.can('dispatch.write')"
                        color="primary"
                        prepend-icon="mdi-plus"
                        data-testid="order-add"
                        @click="router.push('/orders/new')"
                        >{{ t('orders.add') }}</v-btn
                    >
                </div>
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'order-search'"
                                :label="t('orders.search')"
                                prepend-inner-icon="mdi-magnify"
                                variant="outlined"
                                density="compact"
                                clearable
                                hide-details
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="3">
                            <v-select
                                :model-value="statusFilter"
                                :label="t('common.status')"
                                :items="statusOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="order-status-filter"
                                @update:model-value="filterByStatus"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="actionError" class="text-error error-line" data-testid="order-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="order-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredOrders"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('orders.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="order-table"
                >
                    <template #item.orderNumber="{ item }">
                        <span class="font-weight-bold" data-testid="order-number">{{ item.orderNumber }}</span>
                    </template>
                    <template #item.consignor="{ item }">
                        <span data-testid="order-consignor">{{ item.consignor.name }}</span>
                        <div class="text-caption text-medium-emphasis">{{ item.consignor.postalCode }} {{ item.consignor.city }}</div>
                    </template>
                    <template #item.consignee="{ item }">
                        <span data-testid="order-consignee">{{ item.consignee.name }}</span>
                        <div class="text-caption text-medium-emphasis">{{ item.consignee.postalCode }} {{ item.consignee.city }}</div>
                    </template>
                    <template #item.cargoDescription="{ item }">
                        <span data-testid="order-cargo">{{ item.cargoDescription }}</span>
                    </template>
                    <template #item.pickupFrom="{ item }">{{ formatInstant(item.pickupFrom) }}</template>
                    <template #item.status="{ item }">
                        <v-chip :color="statusColor(item.status)" size="small" variant="tonal" data-testid="order-status">{{
                            item.status
                        }}</v-chip>
                    </template>
                    <template #item.actions="{ item }">
                        <div v-if="auth.can('dispatch.write')" class="row-actions">
                            <v-tooltip :text="t('common.edit')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-pencil"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.edit')"
                                        data-testid="order-edit"
                                        @click="router.push(`/orders/${item.id}`)"
                                    />
                                </template>
                            </v-tooltip>
                            <!-- Inline confirmation rather than window.confirm(): a native dialog
                                 blocks until a Playwright dialog handler answers it, and an in-DOM
                                 confirmation is what the e2e spec can assert. Mirrors the Angular
                                 list. -->
                            <template v-if="pendingCancelId === item.id">
                                <v-btn
                                    color="error"
                                    variant="tonal"
                                    size="small"
                                    data-testid="order-cancel-confirm"
                                    @click="cancel(item)"
                                    >{{ t('orders.confirmCancel') }}</v-btn
                                >
                                <v-btn variant="text" size="small" data-testid="order-cancel-abort" @click="abortCancel()">{{
                                    t('orders.keep')
                                }}</v-btn>
                            </template>
                            <v-tooltip v-else :text="t('orders.cancel')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-cancel"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :aria-label="t('orders.cancel')"
                                        data-testid="order-cancel"
                                        @click="askToCancel(item)"
                                    />
                                </template>
                            </v-tooltip>
                        </div>
                    </template>
                </v-data-table>
            </v-card>
        </template>
        <p v-else>{{ t('orders.signInHint') }}</p>
    </v-container>
</template>

<style scoped>
.row-actions {
    align-items: center;
}
</style>
