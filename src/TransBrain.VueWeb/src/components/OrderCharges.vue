<script setup lang="ts">
import axios from 'axios';
import { computed, reactive, ref, watch } from 'vue';
import {
    createOrderCharge,
    listOrderCharges,
    removeOrderCharge,
    updateOrderCharge,
    type OrderCharge,
} from '../api/orders';
import { catalogName, catalogSelectOptions, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import { useVehicleLabels } from '../composables/useVehicleLabels';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

type ChargeField = 'description' | 'quantity' | 'unit' | 'unitPrice';

const DESCRIPTION_MAX_LENGTH = 200;
const MAX_QUANTITY = 1_000_000;
const MAX_UNIT_PRICE = 1_000_000;

const props = defineProps<{
    orderId: string;
    units: readonly VehicleCatalogEntry[];
}>();

const auth = useAuthStore();
const { t } = useI18n();
const { formatDateTime } = useVehicleLabels();

const canWrite = computed(() => auth.can('dispatch.write'));

const charges = ref<OrderCharge[]>([]);
const loaded = ref(false);
const listError = ref<string | null>(null);

const dialogOpen = ref(false);
const editTarget = ref<OrderCharge | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive<Record<ChargeField, string>>({
    description: '',
    quantity: '1',
    unit: '',
    unitPrice: '0',
});
const fieldErrors = reactive<Record<ChargeField, string | null>>({
    description: null,
    quantity: null,
    unit: null,
    unitPrice: null,
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<OrderCharge | null>(null);

const unitOptions = computed(() => [
    { title: t('orders.charges.noUnit'), value: '' },
    ...catalogSelectOptions(props.units, editTarget.value?.unit, (name) => t('vehicles.inactiveOption', { name })),
]);

// Footer sum mirroring the Gesamt box of the old software's additional-charges grid.
const total = computed(() => charges.value.reduce((sum, charge) => sum + charge.total, 0));

watch(
    () => props.orderId,
    async (id) => {
        if (id) {
            await refresh();
        }
    },
    { immediate: true },
);

async function refresh(): Promise<void> {
    try {
        charges.value = await listOrderCharges(props.orderId);
        listError.value = null;
    } catch (error) {
        listError.value = describe(error, t('orders.charges.loadFailed'));
    } finally {
        loaded.value = true;
    }
}

function unitText(charge: OrderCharge): string {
    return catalogName(props.units, charge.unit);
}

function money(value: number): string {
    return `${value.toFixed(2)} €`;
}

function modifiedText(charge: OrderCharge): string {
    return t('orders.modifiedLine', {
        date: formatDateTime(charge.modifiedAt),
        user: charge.modifiedBy ?? '–',
    });
}

function openCreate(): void {
    editTarget.value = null;
    form.description = '';
    form.quantity = '1';
    form.unit = '';
    form.unitPrice = '0';
    openDialog();
}

function openEdit(charge: OrderCharge): void {
    editTarget.value = charge;
    form.description = charge.description;
    form.quantity = String(charge.quantity);
    form.unit = charge.unit ?? '';
    form.unitPrice = String(charge.unitPrice);
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    clearFieldErrors();
    dialogOpen.value = true;
}

function clearFieldErrors(): void {
    for (const field of Object.keys(fieldErrors) as ChargeField[]) {
        fieldErrors[field] = null;
    }
}

function errorsFor(field: ChargeField): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function validate(): boolean {
    clearFieldErrors();
    fieldErrors.description = form.description.trim() ? null : t('common.required');
    if (form.description.trim().length > DESCRIPTION_MAX_LENGTH) {
        fieldErrors.description = t('common.maxLength', { max: DESCRIPTION_MAX_LENGTH });
    }
    const quantity = Number(form.quantity);
    if (form.quantity === '' || Number.isNaN(quantity) || quantity <= 0) {
        fieldErrors.quantity = t('common.mustBePositive');
    } else if (quantity > MAX_QUANTITY) {
        fieldErrors.quantity = t('common.mustNotExceed', { max: MAX_QUANTITY });
    }
    const unitPrice = Number(form.unitPrice);
    if (form.unitPrice === '' || Number.isNaN(unitPrice)) {
        fieldErrors.unitPrice = t('common.invalid');
    } else if (unitPrice < 0) {
        fieldErrors.unitPrice = t('common.mustNotBeNegative');
    } else if (unitPrice > MAX_UNIT_PRICE) {
        fieldErrors.unitPrice = t('common.mustNotExceed', { max: MAX_UNIT_PRICE });
    }
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    dialogError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request = {
        description: form.description.trim(),
        quantity: Number(form.quantity),
        unit: form.unit || null,
        unitPrice: Number(form.unitPrice),
    };

    saving.value = true;
    try {
        if (editTarget.value) {
            await updateOrderCharge(props.orderId, editTarget.value.id, request);
        } else {
            await createOrderCharge(props.orderId, request);
        }
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        applyServerErrors(error);
    } finally {
        saving.value = false;
    }
}

// The `errors` dictionary is keyed by the .NET property name ("UnitPrice"); a key this dialog
// has no field for still has to reach the user, so it goes to the dialog-level line.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [key, messages] of Object.entries(problem.errors)) {
                const field = key.charAt(0).toLowerCase() + key.slice(1);
                const message = messages.join(' ');
                if (field in fieldErrors) {
                    fieldErrors[field as ChargeField] = message;
                } else {
                    dialogError.value = dialogError.value ? `${dialogError.value} ${message}` : message;
                }
            }
            return;
        }
    }
    dialogError.value = describe(error, t('orders.charges.saveFailed'));
}

function askDelete(charge: OrderCharge): void {
    deleteTarget.value = charge;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    const charge = deleteTarget.value;
    if (!charge) {
        return;
    }
    listError.value = null;
    try {
        await removeOrderCharge(props.orderId, charge.id);
        await refresh();
    } catch (error) {
        listError.value = describe(error, t('orders.charges.deleteFailed'));
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-card border flat class="section-card" data-testid="order-charges">
        <v-card-item prepend-icon="mdi-playlist-plus" :title="t('orders.charges.title')">
            <template v-if="canWrite" #append>
                <v-btn
                    type="button"
                    color="primary"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    data-testid="order-charge-add"
                    @click="openCreate"
                    >{{ t('orders.charges.add') }}</v-btn
                >
            </template>
        </v-card-item>
        <v-card-text>
            <p v-if="listError" class="text-error list-error" data-testid="order-charges-error">{{ listError }}</p>
            <p
                v-if="loaded && !listError && charges.length === 0"
                class="text-medium-emphasis hint"
                data-testid="order-charges-empty"
            >
                {{ t('orders.charges.empty') }}
            </p>
            <v-table v-else-if="charges.length > 0" density="comfortable" class="charges-table">
                <thead>
                    <tr>
                        <th>{{ t('orders.charges.description') }}</th>
                        <th class="text-end">{{ t('orders.charges.quantity') }}</th>
                        <th>{{ t('orders.charges.unit') }}</th>
                        <th class="text-end">{{ t('orders.charges.unitPrice') }}</th>
                        <th class="text-end">{{ t('orders.charges.total') }}</th>
                        <th>{{ t('orders.modified') }}</th>
                        <th v-if="canWrite" class="text-end">{{ t('common.actions') }}</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-for="charge in charges" :key="charge.id" data-testid="order-charge-row">
                        <td class="font-weight-medium" data-testid="order-charge-description">{{ charge.description }}</td>
                        <td class="text-end">{{ charge.quantity }}</td>
                        <td data-testid="order-charge-unit">{{ unitText(charge) }}</td>
                        <td class="text-end">{{ money(charge.unitPrice) }}</td>
                        <td class="text-end" data-testid="order-charge-total">{{ money(charge.total) }}</td>
                        <td class="text-medium-emphasis" data-testid="order-charge-modified">{{ modifiedText(charge) }}</td>
                        <td v-if="canWrite" class="text-end">
                            <div class="row-actions">
                                <v-btn
                                    type="button"
                                    icon="mdi-pencil"
                                    variant="text"
                                    size="small"
                                    :aria-label="t('common.edit')"
                                    data-testid="order-charge-edit"
                                    @click="openEdit(charge)"
                                />
                                <v-btn
                                    type="button"
                                    icon="mdi-delete"
                                    variant="text"
                                    size="small"
                                    color="error"
                                    :aria-label="t('common.delete')"
                                    data-testid="order-charge-delete"
                                    @click="askDelete(charge)"
                                />
                            </div>
                        </td>
                    </tr>
                </tbody>
                <tfoot>
                    <tr class="sum-row">
                        <td>{{ t('orders.sum') }}</td>
                        <td></td>
                        <td></td>
                        <td></td>
                        <td class="text-end" data-testid="order-charge-total-sum">{{ money(total) }}</td>
                        <td></td>
                        <td v-if="canWrite"></td>
                    </tr>
                </tfoot>
            </v-table>
        </v-card-text>

        <v-dialog v-model="dialogOpen" max-width="560" :persistent="saving">
            <v-card data-testid="charge-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'"
                    :title="editTarget ? t('orders.charges.edit') : t('orders.charges.new')"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="charge-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-text-field
                            v-model="form.description"
                            v-native-testid="'charge-description'"
                            :label="`${t('orders.charges.description')} *`"
                            :counter="DESCRIPTION_MAX_LENGTH"
                            :error-messages="errorsFor('description')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                        >
                            <template #message="{ message }">
                                <span data-testid="charge-description-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                        <v-row dense>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.quantity"
                                    v-native-testid="'charge-quantity'"
                                    :label="`${t('orders.charges.quantity')} *`"
                                    type="number"
                                    step="any"
                                    :error-messages="errorsFor('quantity')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="charge-quantity-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                            <v-col cols="12" sm="6">
                                <v-select
                                    v-model="form.unit"
                                    :label="t('orders.charges.unit')"
                                    :items="unitOptions"
                                    :error-messages="errorsFor('unit')"
                                    variant="outlined"
                                    density="comfortable"
                                    data-testid="charge-unit"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="charge-unit-error">{{ message }}</span>
                                    </template>
                                </v-select>
                            </v-col>
                        </v-row>
                        <v-text-field
                            v-model="form.unitPrice"
                            v-native-testid="'charge-unitPrice'"
                            :label="`${t('orders.charges.unitPrice')} *`"
                            type="number"
                            step="any"
                            suffix="€"
                            :error-messages="errorsFor('unitPrice')"
                            variant="outlined"
                            density="comfortable"
                        >
                            <template #message="{ message }">
                                <span data-testid="charge-unitPrice-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn type="button" variant="text" data-testid="charge-cancel" :disabled="saving" @click="dialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="charge-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <v-dialog v-model="deleteDialogOpen" max-width="440">
            <v-card>
                <v-card-title>{{ t('orders.charges.deleteTitle') }}</v-card-title>
                <v-card-text>{{
                    t('orders.charges.deleteConfirm', { description: deleteTarget?.description ?? '' })
                }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="order-charge-delete-cancel" @click="deleteDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="order-charge-delete-confirm" @click="confirmDelete">{{
                        t('common.delete')
                    }}</v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>
    </v-card>
</template>

<style scoped>
.hint {
    margin: 0;
}

.list-error {
    margin-bottom: 8px;
}

.charges-table {
    overflow-x: auto;
}

.sum-row td {
    font-weight: 600;
}

.row-actions {
    display: inline-flex;
    gap: 4px;
}

.dialog-error {
    margin-bottom: 16px;
}

.dialog-field {
    margin-bottom: 8px;
}
</style>
