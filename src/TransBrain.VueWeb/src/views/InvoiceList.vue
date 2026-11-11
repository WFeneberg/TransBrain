<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
    cancelInvoice,
    issueInvoice,
    listInvoices,
    markInvoicePaid,
    type Invoice,
} from '../api/invoices';
import { INVOICE_STATUSES, formatCalendarDate, statusColor } from '../composables/statusColor';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

/** The API's maximum page size - same stopgap as the other lists until real paging exists. */
const LIST_PAGE_SIZE = 100;

const auth = useAuthStore();
const { locale, t } = useI18n();
const router = useRouter();
const invoices = ref<Invoice[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed transition must not hide the table the way a failed
// list load does (mirrors OrderList.vue's actionError).
const actionError = ref<string | null>(null);
const search = ref<string | null>('');
const statusFilter = ref('');
const pendingCancelId = ref<string | null>(null);

const statusOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...INVOICE_STATUSES.map((status) => ({ title: status, value: status })),
]);

const headers = computed(() => [
    { title: t('invoices.number'), key: 'invoiceNumber' },
    { title: t('invoices.customer'), key: 'customerName' },
    { title: t('invoices.netTotal'), key: 'netTotal', align: 'end' as const },
    { title: t('invoices.grossTotal'), key: 'grossTotal', align: 'end' as const },
    { title: t('invoices.dueDate'), key: 'dueDate' },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredInvoices = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    if (!term) {
        return invoices.value;
    }
    return invoices.value.filter((invoice) =>
        [invoice.invoiceNumber, invoice.customerName, invoice.status].some((value) =>
            value.toLowerCase().includes(term),
        ),
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
        invoices.value = (await listInvoices(statusFilter.value, LIST_PAGE_SIZE)).items;
        errorMessage.value = null;
    } catch (error) {
        errorMessage.value = describe(error, t('invoices.listLoadFailed'));
    } finally {
        loading.value = false;
    }
}

// Server-side, unlike the search: the status is a query parameter of the invoice list.
async function filterByStatus(status: string | null): Promise<void> {
    statusFilter.value = status ?? '';
    pendingCancelId.value = null;
    await refresh();
}

async function issue(invoice: Invoice): Promise<void> {
    actionError.value = null;
    try {
        await issueInvoice(invoice.id);
        await refresh();
    } catch (error) {
        actionError.value = describe(error, t('invoices.issueFailed'));
    }
}

async function markPaid(invoice: Invoice): Promise<void> {
    actionError.value = null;
    try {
        await markInvoicePaid(invoice.id);
        await refresh();
    } catch (error) {
        actionError.value = describe(error, t('invoices.payFailed'));
    }
}

function askToCancel(invoice: Invoice): void {
    actionError.value = null;
    pendingCancelId.value = invoice.id;
}

function abortCancel(): void {
    pendingCancelId.value = null;
}

async function cancel(invoice: Invoice): Promise<void> {
    actionError.value = null;
    pendingCancelId.value = null;
    try {
        await cancelInvoice(invoice.id);
        await refresh();
    } catch (error) {
        actionError.value = describe(error, t('invoices.cancelFailed'));
    }
}

function money(value: number): string {
    return `${value.toLocaleString(locale.value === 'de' ? 'de-DE' : 'en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })} €`;
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
                    <h1 class="text-h4">{{ t('nav.invoices') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">
                        {{ t('invoices.count', { count: invoices.length }) }}
                    </p>
                </div>
                <div class="header-actions">
                    <v-btn
                        v-if="auth.can('dispatch.write')"
                        color="primary"
                        prepend-icon="mdi-plus"
                        data-testid="invoice-add"
                        @click="router.push('/invoices/new')"
                        >{{ t('invoices.add') }}</v-btn
                    >
                </div>
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'invoice-search'"
                                :label="t('invoices.search')"
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
                                data-testid="invoice-status-filter"
                                @update:model-value="filterByStatus"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="actionError" class="text-error error-line" data-testid="invoice-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="invoice-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": one pagination - the server's - is enough (see OrderList). -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredInvoices"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('invoices.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="invoice-table"
                >
                    <template #item.invoiceNumber="{ item }">
                        <router-link
                            :to="`/invoices/${item.id}`"
                            class="font-weight-bold"
                            data-testid="invoice-number"
                            >{{ item.invoiceNumber }}</router-link
                        >
                    </template>
                    <template #item.customerName="{ item }">
                        <span data-testid="invoice-customer">{{ item.customerName }}</span>
                    </template>
                    <template #item.netTotal="{ item }">
                        <span data-testid="invoice-net">{{ money(item.netTotal) }}</span>
                    </template>
                    <template #item.grossTotal="{ item }">
                        <span data-testid="invoice-gross">{{ money(item.grossTotal) }}</span>
                    </template>
                    <template #item.dueDate="{ item }">
                        {{ item.dueDate ? formatCalendarDate(item.dueDate, locale) : '—' }}
                    </template>
                    <template #item.status="{ item }">
                        <v-chip
                            :color="statusColor(item.status)"
                            size="small"
                            variant="tonal"
                            data-testid="invoice-status"
                            >{{ item.status }}</v-chip
                        >
                    </template>
                    <template #item.actions="{ item }">
                        <div v-if="auth.can('dispatch.write')" class="row-actions">
                            <template v-if="pendingCancelId === item.id">
                                <v-btn
                                    color="error"
                                    variant="tonal"
                                    size="small"
                                    data-testid="invoice-cancel-confirm"
                                    @click="cancel(item)"
                                    >{{ t('invoices.confirmCancel') }}</v-btn
                                >
                                <v-btn
                                    variant="text"
                                    size="small"
                                    data-testid="invoice-cancel-abort"
                                    @click="abortCancel()"
                                    >{{ t('invoices.keep') }}</v-btn
                                >
                            </template>
                            <template v-else>
                                <v-tooltip v-if="item.status === 'Draft'" :text="t('invoices.issue')" location="top">
                                    <template #activator="{ props: tooltip }">
                                        <v-btn
                                            v-bind="tooltip"
                                            icon="mdi-send"
                                            variant="text"
                                            size="small"
                                            :aria-label="t('invoices.issue')"
                                            data-testid="invoice-issue"
                                            @click="issue(item)"
                                        />
                                    </template>
                                </v-tooltip>
                                <v-tooltip v-if="item.status === 'Issued'" :text="t('invoices.pay')" location="top">
                                    <template #activator="{ props: tooltip }">
                                        <v-btn
                                            v-bind="tooltip"
                                            icon="mdi-cash-check"
                                            variant="text"
                                            size="small"
                                            color="success"
                                            :aria-label="t('invoices.pay')"
                                            data-testid="invoice-pay"
                                            @click="markPaid(item)"
                                        />
                                    </template>
                                </v-tooltip>
                                <v-tooltip
                                    v-if="item.status === 'Draft' || item.status === 'Issued'"
                                    :text="t('invoices.cancel')"
                                    location="top"
                                >
                                    <template #activator="{ props: tooltip }">
                                        <v-btn
                                            v-bind="tooltip"
                                            icon="mdi-cancel"
                                            variant="text"
                                            size="small"
                                            color="error"
                                            :aria-label="t('invoices.cancel')"
                                            data-testid="invoice-cancel"
                                            @click="askToCancel(item)"
                                        />
                                    </template>
                                </v-tooltip>
                            </template>
                        </div>
                    </template>
                </v-data-table>
            </v-card>
        </template>
        <p v-else>{{ t('invoices.signInHint') }}</p>
    </v-container>
</template>

<style scoped>
.row-actions {
    align-items: center;
}
</style>
