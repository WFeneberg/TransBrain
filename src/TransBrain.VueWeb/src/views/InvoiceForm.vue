<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { listCustomerLocations, type CustomerLocation } from '../api/customerLocations';
import { listCustomers, type Customer } from '../api/customers';
import { createInvoice, listBillableOrders, type InvoiceLinePayload } from '../api/invoices';
import type { Order } from '../api/orders';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n } from '../i18n';

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

/** One billable order as shown in the picker table, with its editable line fields. */
interface LineDraft {
    order: Order;
    selected: boolean;
    netAmount: string;
    description: string;
}

const { t } = useI18n();
const router = useRouter();

const customers = ref<Customer[]>([]);
const lines = ref<LineDraft[]>([]);
const customerId = ref<string | null>(null);
const billToLocationId = ref<string>('');
const billableLocations = ref<CustomerLocation[]>([]);
const vatRatePercent = ref('19');
const formError = ref<string | null>(null);
const saving = ref(false);

const headers = computed(() => [
    { title: '', key: 'selected', sortable: false, width: 48 },
    { title: t('orders.orderNumber'), key: 'orderNumber' },
    { title: t('invoices.route'), key: 'route', sortable: false },
    { title: t('orders.cargo'), key: 'cargo', sortable: false },
    { title: t('invoices.netAmount'), key: 'netAmount', sortable: false },
    { title: t('invoices.descriptionOptional'), key: 'description', sortable: false },
]);

const customerOptions = computed(() =>
    customers.value.map((customer) => ({ title: customer.name, value: customer.id })),
);

// '' bills the customer itself; only locations with their own company name qualify as recipients.
const billToOptions = computed(() => [
    { title: t('invoices.billToCustomer'), value: '' },
    ...billableLocations.value.map((location) => ({
        title: `${location.companyName} (${location.name})`,
        value: location.id,
    })),
]);

watch(customerId, async (id) => {
    billToLocationId.value = '';
    billableLocations.value = [];
    if (!id) {
        return;
    }
    try {
        const locations = await listCustomerLocations(id);
        billableLocations.value = locations.filter((location) => location.companyName !== null);
    } catch {
        // The picker is optional; a load failure must not block invoicing the customer.
    }
});

const selectedNetTotal = computed(() =>
    lines.value
        .filter((line) => line.selected)
        .reduce((sum, line) => sum + (Number(line.netAmount) || 0), 0),
);

onMounted(async () => {
    try {
        const [customerPage, billable] = await Promise.all([
            listCustomers(1, PICKER_PAGE_SIZE),
            listBillableOrders(),
        ]);
        customers.value = customerPage.items;
        lines.value = billable.map((order) => ({
            order,
            selected: false,
            netAmount: '',
            description: '',
        }));
    } catch (error) {
        formError.value = describe(error, t('invoices.billableLoadFailed'));
    }
});

async function save(): Promise<void> {
    formError.value = null;

    if (!customerId.value) {
        formError.value = t('common.required');
        return;
    }
    const vatRate = Number(vatRatePercent.value);
    if (!(vatRate >= 0 && vatRate <= 100)) {
        formError.value = t('common.invalid');
        return;
    }
    const selected = lines.value.filter((line) => line.selected);
    if (selected.length === 0) {
        formError.value = t('invoices.selectAtLeastOne');
        return;
    }
    const invalidAmount = selected.find((line) => !(Number(line.netAmount) > 0));
    if (invalidAmount) {
        formError.value = t('invoices.amountMissing', {
            number: invalidAmount.order.orderNumber,
        });
        return;
    }

    const payload: InvoiceLinePayload[] = selected.map((line) => ({
        orderId: line.order.id,
        netAmount: Number(line.netAmount),
        description: line.description.trim() === '' ? null : line.description.trim(),
    }));

    saving.value = true;
    try {
        const invoice = await createInvoice({
            customerId: customerId.value,
            vatRatePercent: vatRate,
            lines: payload,
            billToLocationId: billToLocationId.value || null,
        });
        await router.push(`/invoices/${invoice.id}`);
    } catch (error) {
        formError.value = describe(error, t('invoices.saveFailed'));
    } finally {
        saving.value = false;
    }
}

function money(value: number): string {
    return `${value.toFixed(2)} €`;
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as
            | { title?: string; detail?: string; errors?: Record<string, string[]> }
            | undefined;
        const validationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
        const sentence = validationMessage ?? problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container class="page-container">
        <div class="page-header">
            <h1 class="text-h4">{{ t('invoices.new') }}</h1>
        </div>

        <p v-if="formError" class="text-error error-line" data-testid="invoice-form-error">{{ formError }}</p>

        <v-form @submit.prevent="save">
            <v-card border flat class="mb-4">
                <v-card-title>
                    <v-icon icon="mdi-domain" class="mr-2" />{{ t('invoices.billTo') }}
                </v-card-title>
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-select
                                v-model="customerId"
                                :label="t('invoices.customer')"
                                :items="customerOptions"
                                variant="outlined"
                                density="compact"
                                data-testid="invoice-customerId"
                            />
                        </v-col>
                        <v-col v-if="billableLocations.length > 0" cols="12" md="6">
                            <v-select
                                v-model="billToLocationId"
                                :label="t('invoices.billToLocation')"
                                :items="billToOptions"
                                :hint="t('invoices.billToHint')"
                                persistent-hint
                                variant="outlined"
                                density="compact"
                                data-testid="invoice-billTo"
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="3">
                            <v-text-field
                                v-model="vatRatePercent"
                                v-native-testid="'invoice-vatRate'"
                                :label="t('invoices.vatRate')"
                                type="number"
                                step="0.1"
                                min="0"
                                max="100"
                                variant="outlined"
                                density="compact"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <v-card border flat class="mb-4">
                <v-card-title>
                    <v-icon icon="mdi-receipt-text" class="mr-2" />{{ t('invoices.billableOrders') }}
                </v-card-title>
                <v-card-text>
                    <p v-if="lines.length === 0" data-testid="invoice-no-billable">
                        {{ t('invoices.noBillable') }}
                    </p>
                    <template v-else>
                        <v-data-table
                            :headers="headers"
                            :items="lines"
                            :items-per-page="-1"
                            item-value="order.id"
                            hide-default-footer
                            data-testid="invoice-line-table"
                        >
                            <template #item.selected="{ item }">
                                <v-checkbox-btn
                                    v-model="item.selected"
                                    data-testid="invoice-line-select"
                                />
                            </template>
                            <template #item.orderNumber="{ item }">
                                <span class="font-weight-bold">{{ item.order.orderNumber }}</span>
                            </template>
                            <template #item.route="{ item }">
                                {{ item.order.consignor.city }} → {{ item.order.consignee.city }}
                            </template>
                            <template #item.cargo="{ item }">
                                {{ item.order.cargoDescription }}
                            </template>
                            <template #item.netAmount="{ item }">
                                <v-text-field
                                    v-model="item.netAmount"
                                    v-native-testid="'invoice-line-amount'"
                                    type="number"
                                    step="0.01"
                                    min="0.01"
                                    :disabled="!item.selected"
                                    variant="outlined"
                                    density="compact"
                                    hide-details
                                />
                            </template>
                            <template #item.description="{ item }">
                                <v-text-field
                                    v-model="item.description"
                                    v-native-testid="'invoice-line-description'"
                                    :disabled="!item.selected"
                                    variant="outlined"
                                    density="compact"
                                    hide-details
                                />
                            </template>
                        </v-data-table>
                        <p class="text-medium-emphasis mt-2" data-testid="invoice-net-preview">
                            {{ t('invoices.netTotal') }}: {{ money(selectedNetTotal) }}
                        </p>
                    </template>
                </v-card-text>
            </v-card>

            <div class="form-actions">
                <v-btn variant="text" data-testid="invoice-form-cancel" @click="router.push('/invoices')">{{
                    t('common.cancel')
                }}</v-btn>
                <v-btn
                    color="primary"
                    type="submit"
                    prepend-icon="mdi-content-save"
                    :loading="saving"
                    data-testid="invoice-save"
                    >{{ t('common.save') }}</v-btn
                >
            </div>
        </v-form>
    </v-container>
</template>
