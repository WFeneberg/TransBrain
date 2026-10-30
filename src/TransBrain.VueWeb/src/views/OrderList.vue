<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { cancelOrder, listOrders, type Order } from '../api/orders';
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
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed cancel must not hide the table the way a failed list
// load does (mirrors DriverList.vue's actionError).
const actionError = ref<string | null>(null);
const statusFilter = ref('');
const pendingCancelId = ref<string | null>(null);

const statusOptions = ['Draft', 'Planned', 'InTransit', 'Delivered', 'Cancelled'];

const headers = computed(() => [
    { title: t('orders.orderNumber'), key: 'orderNumber' },
    { title: t('orders.consignor'), key: 'consignor' },
    { title: t('orders.consignee'), key: 'consignee' },
    { title: t('orders.cargo'), key: 'cargoDescription' },
    { title: t('orders.pickup'), key: 'pickupFrom' },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    try {
        orders.value = (await listOrders(statusFilter.value, LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('orders.listLoadFailed'));
    }
}

async function filterByStatus(status: string): Promise<void> {
    statusFilter.value = status;
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
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>{{ t('nav.orders') }}</h1>
            <v-btn
                v-if="auth.can('dispatch.write')"
                data-testid="order-add"
                @click="router.push('/orders/new')"
                >{{ t('orders.add') }}</v-btn
            >
            <!-- A plain <select>, not <v-select>: Vuetify's select renders an overlay listbox
                 rather than native options, and the same testid-lands-on-a-wrapper-div problem
                 DriverForm.vue documents for v-text-field applies to it. -->
            <div>
                <label for="order-status-filter">{{ t('common.status') }}</label>
                <select
                    id="order-status-filter"
                    data-testid="order-status-filter"
                    :value="statusFilter"
                    @change="filterByStatus(($event.target as HTMLSelectElement).value)"
                >
                    <option value="">{{ t('common.all') }}</option>
                    <option v-for="status in statusOptions" :key="status" :value="status">{{ status }}</option>
                </select>
            </div>
            <p v-if="actionError" data-testid="order-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" data-testid="order-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-data-table
                v-else
                :headers="headers"
                :items="orders"
                :items-per-page="-1"
                item-value="id"
                data-testid="order-table"
            >
                <template #item.orderNumber="{ item }">
                    <span data-testid="order-number">{{ item.orderNumber }}</span>
                </template>
                <template #item.consignor="{ item }">
                    <span data-testid="order-consignor">{{ item.consignor.name }}</span>
                </template>
                <template #item.consignee="{ item }">
                    <span data-testid="order-consignee">{{ item.consignee.name }}</span>
                </template>
                <template #item.cargoDescription="{ item }">
                    <span data-testid="order-cargo">{{ item.cargoDescription }}</span>
                </template>
                <template #item.pickupFrom="{ item }">
                    {{ formatInstant(item.pickupFrom) }}
                </template>
                <template #item.status="{ item }">
                    <span data-testid="order-status">{{ item.status }}</span>
                </template>
                <template #item.actions="{ item }">
                    <template v-if="auth.can('dispatch.write')">
                    <v-btn data-testid="order-edit" @click="router.push(`/orders/${item.id}`)">{{ t('common.edit') }}</v-btn>
                    <!-- Inline confirmation rather than window.confirm(): a native dialog blocks
                         until a Playwright dialog handler answers it, and an in-DOM confirmation
                         is what the e2e spec can assert. Mirrors the Angular list. -->
                    <template v-if="pendingCancelId === item.id">
                        <v-btn data-testid="order-cancel-confirm" @click="cancel(item)">{{ t('orders.confirmCancel') }}</v-btn>
                        <v-btn data-testid="order-cancel-abort" @click="abortCancel()">{{ t('orders.keep') }}</v-btn>
                    </template>
                    <v-btn v-else data-testid="order-cancel" @click="askToCancel(item)">{{ t('orders.cancel') }}</v-btn>
                    </template>
                </template>
            </v-data-table>
        </template>
        <p v-else>{{ t('orders.signInHint') }}</p>
    </v-container>
</template>
