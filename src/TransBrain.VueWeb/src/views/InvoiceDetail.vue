<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import {
    cancelInvoice,
    getInvoice,
    issueInvoice,
    markInvoicePaid,
    type Invoice,
} from '../api/invoices';
import { formatCalendarDate, statusColor } from '../composables/statusColor';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

const auth = useAuthStore();
const { locale, t } = useI18n();
const route = useRoute();

const invoice = ref<Invoice | null>(null);
const errorMessage = ref<string | null>(null);
const confirmingCancel = ref(false);

const lineHeaders = computed(() => [
    { title: t('invoices.position'), key: 'position', width: 64 },
    { title: t('orders.orderNumber'), key: 'orderNumber' },
    { title: t('invoices.description'), key: 'description', sortable: false },
    { title: t('invoices.netAmount'), key: 'netAmount', align: 'end' as const },
]);

onMounted(async () => {
    try {
        invoice.value = await getInvoice(String(route.params.id));
    } catch (error) {
        errorMessage.value = describe(error, t('invoices.loadFailed'));
    }
});

async function issue(): Promise<void> {
    await transition(() => issueInvoice(invoice.value!.id), t('invoices.issueFailed'));
}

async function markPaid(): Promise<void> {
    await transition(() => markInvoicePaid(invoice.value!.id), t('invoices.payFailed'));
}

async function cancel(): Promise<void> {
    confirmingCancel.value = false;
    await transition(() => cancelInvoice(invoice.value!.id), t('invoices.cancelFailed'));
}

async function transition(request: () => Promise<Invoice>, fallback: string): Promise<void> {
    errorMessage.value = null;
    try {
        invoice.value = await request();
    } catch (error) {
        errorMessage.value = describe(error, fallback);
    }
}

function formatInstant(iso: string): string {
    return new Date(iso).toLocaleString(locale.value === 'de' ? 'de-DE' : 'en-US');
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
        <p v-if="errorMessage" class="text-error error-line" data-testid="invoice-detail-error">{{ errorMessage }}</p>

        <template v-if="invoice">
            <div class="page-header">
                <div>
                    <h1 class="text-h4" data-testid="invoice-detail-number">
                        {{ t('invoices.detailTitle', { number: invoice.invoiceNumber }) }}
                        <v-chip
                            :color="statusColor(invoice.status)"
                            size="small"
                            variant="tonal"
                            class="ml-2"
                            data-testid="invoice-detail-status"
                            >{{ invoice.status }}</v-chip
                        >
                    </h1>
                    <p class="text-medium-emphasis page-subtitle" data-testid="invoice-detail-customer">{{ invoice.customerName }}</p>
                </div>
                <div v-if="auth.can('dispatch.write')" class="header-actions">
                    <template v-if="confirmingCancel">
                        <v-btn
                            color="error"
                            variant="tonal"
                            data-testid="invoice-cancel-confirm"
                            @click="cancel()"
                            >{{ t('invoices.confirmCancel') }}</v-btn
                        >
                        <v-btn variant="text" data-testid="invoice-cancel-abort" @click="confirmingCancel = false">{{
                            t('invoices.keep')
                        }}</v-btn>
                    </template>
                    <template v-else>
                        <v-btn
                            v-if="invoice.status === 'Draft'"
                            color="primary"
                            prepend-icon="mdi-send"
                            data-testid="invoice-detail-issue"
                            @click="issue()"
                            >{{ t('invoices.issue') }}</v-btn
                        >
                        <v-btn
                            v-if="invoice.status === 'Issued'"
                            color="success"
                            prepend-icon="mdi-cash-check"
                            data-testid="invoice-detail-pay"
                            @click="markPaid()"
                            >{{ t('invoices.pay') }}</v-btn
                        >
                        <v-btn
                            v-if="invoice.status === 'Draft' || invoice.status === 'Issued'"
                            color="error"
                            variant="outlined"
                            prepend-icon="mdi-cancel"
                            data-testid="invoice-detail-cancel"
                            @click="confirmingCancel = true"
                            >{{ t('invoices.cancel') }}</v-btn
                        >
                    </template>
                </div>
            </div>

            <v-row dense class="mb-2">
                <v-col cols="12" md="6">
                    <v-card border flat class="fill-height">
                        <v-card-title>
                            <v-icon icon="mdi-domain" class="mr-2" />{{ t('invoices.billTo') }}
                        </v-card-title>
                        <v-card-text>
                            <p class="font-weight-bold">{{ invoice.customerName }}</p>
                            <p>
                                {{ invoice.billingAddress.street }}<br />
                                {{ invoice.billingAddress.postalCode }} {{ invoice.billingAddress.city }}<br />
                                {{ invoice.billingAddress.country }}
                            </p>
                            <p v-if="invoice.customerVatId" class="mt-2">
                                {{ t('invoices.vatId') }}: {{ invoice.customerVatId }}
                            </p>
                            <p>
                                {{ t('invoices.paymentTerm') }}:
                                {{ t('invoices.paymentTermDays', { days: invoice.paymentTermDays }) }}
                            </p>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="6">
                    <v-card border flat class="fill-height">
                        <v-card-title>
                            <v-icon icon="mdi-calendar" class="mr-2" />{{ t('invoices.dates') }}
                        </v-card-title>
                        <v-card-text>
                            <p>{{ t('invoices.createdAt') }}: {{ formatInstant(invoice.createdAt) }}</p>
                            <p data-testid="invoice-detail-issuedAt">
                                {{ t('invoices.issuedAt') }}:
                                {{ invoice.issuedAt ? formatInstant(invoice.issuedAt) : '—' }}
                            </p>
                            <p data-testid="invoice-detail-dueDate">
                                {{ t('invoices.dueDate') }}:
                                {{ invoice.dueDate ? formatCalendarDate(invoice.dueDate, locale) : '—' }}
                            </p>
                            <p data-testid="invoice-detail-paidAt">
                                {{ t('invoices.paidAt') }}:
                                {{ invoice.paidAt ? formatInstant(invoice.paidAt) : '—' }}
                            </p>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <v-card border flat class="table-card">
                <v-data-table
                    :headers="lineHeaders"
                    :items="invoice.lines"
                    :items-per-page="-1"
                    item-value="position"
                    hide-default-footer
                    data-testid="invoice-detail-lines"
                >
                    <template #item.orderNumber="{ item }">
                        <span class="font-weight-bold">{{ item.orderNumber }}</span>
                    </template>
                    <template #item.netAmount="{ item }">
                        <span data-testid="invoice-line-net">{{ money(item.netAmount) }}</span>
                    </template>
                </v-data-table>
                <v-card-text class="totals" data-testid="invoice-detail-totals">
                    <p>
                        {{ t('invoices.netTotal') }}:
                        <span class="font-weight-bold">{{ money(invoice.netTotal) }}</span>
                    </p>
                    <p>
                        {{ t('invoices.vatAmount') }} ({{ invoice.vatRatePercent }} %):
                        <span class="font-weight-bold">{{ money(invoice.vatAmount) }}</span>
                    </p>
                    <p>
                        {{ t('invoices.grossTotal') }}:
                        <span class="font-weight-bold">{{ money(invoice.grossTotal) }}</span>
                    </p>
                </v-card-text>
            </v-card>

            <div class="form-actions mt-4">
                <v-btn variant="text" prepend-icon="mdi-arrow-left" to="/invoices" data-testid="invoice-detail-back">{{
                    t('invoices.backToList')
                }}</v-btn>
            </div>
        </template>
    </v-container>
</template>

<style scoped>
.totals {
    display: flex;
    gap: 2rem;
    justify-content: flex-end;
}
</style>
