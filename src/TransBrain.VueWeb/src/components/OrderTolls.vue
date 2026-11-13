<script setup lang="ts">
import axios from 'axios';
import { computed, reactive, ref, watch } from 'vue';
import {
    createOrderToll,
    listOrderTolls,
    removeOrderToll,
    updateOrderToll,
    type OrderToll,
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

type TollField = 'tollCategory' | 'emptyKm' | 'loadedKm' | 'factorPerKm';

const MAX_KILOMETERS = 100_000;
const MAX_FACTOR = 1_000;

const props = defineProps<{
    orderId: string;
    tollCategories: readonly VehicleCatalogEntry[];
}>();

const auth = useAuthStore();
const { t } = useI18n();
const { formatDateTime } = useVehicleLabels();

const canWrite = computed(() => auth.can('dispatch.write'));

const tolls = ref<OrderToll[]>([]);
const loaded = ref(false);
const listError = ref<string | null>(null);

const dialogOpen = ref(false);
const editTarget = ref<OrderToll | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive<Record<TollField, string>>({
    tollCategory: '',
    emptyKm: '0',
    loadedKm: '0',
    factorPerKm: '0',
});
const fieldErrors = reactive<Record<TollField, string | null>>({
    tollCategory: null,
    emptyKm: null,
    loadedKm: null,
    factorPerKm: null,
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<OrderToll | null>(null);

const categoryOptions = computed(() =>
    catalogSelectOptions(props.tollCategories, editTarget.value?.tollCategory, (name) =>
        t('vehicles.inactiveOption', { name }),
    ),
);

// Footer sums mirroring the Leer-/Last-Betrag boxes of the old software's toll grid.
const totals = computed(() =>
    tolls.value.reduce(
        (sums, toll) => ({
            emptyKm: sums.emptyKm + toll.emptyKm,
            loadedKm: sums.loadedKm + toll.loadedKm,
            emptyAmount: sums.emptyAmount + toll.emptyAmount,
            loadedAmount: sums.loadedAmount + toll.loadedAmount,
        }),
        { emptyKm: 0, loadedKm: 0, emptyAmount: 0, loadedAmount: 0 },
    ),
);

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
        tolls.value = await listOrderTolls(props.orderId);
        listError.value = null;
    } catch (error) {
        listError.value = describe(error, t('orders.tolls.loadFailed'));
    } finally {
        loaded.value = true;
    }
}

function categoryText(toll: OrderToll): string {
    return catalogName(props.tollCategories, toll.tollCategory);
}

function money(value: number): string {
    return `${value.toFixed(2)} €`;
}

function modifiedText(toll: OrderToll): string {
    return t('orders.modifiedLine', {
        date: formatDateTime(toll.modifiedAt),
        user: toll.modifiedBy ?? '–',
    });
}

function openCreate(): void {
    editTarget.value = null;
    form.tollCategory = '';
    form.emptyKm = '0';
    form.loadedKm = '0';
    form.factorPerKm = '0';
    openDialog();
}

function openEdit(toll: OrderToll): void {
    editTarget.value = toll;
    form.tollCategory = toll.tollCategory;
    form.emptyKm = String(toll.emptyKm);
    form.loadedKm = String(toll.loadedKm);
    form.factorPerKm = String(toll.factorPerKm);
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    clearFieldErrors();
    dialogOpen.value = true;
}

function clearFieldErrors(): void {
    for (const field of Object.keys(fieldErrors) as TollField[]) {
        fieldErrors[field] = null;
    }
}

function errorsFor(field: TollField): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function rangeError(value: string, max: number, wholeNumber = false): string | null {
    const parsed = Number(value);
    if (value === '' || Number.isNaN(parsed)) {
        return t('common.invalid');
    }
    if (wholeNumber && !Number.isInteger(parsed)) {
        return t('common.mustBeWholeNumber');
    }
    if (parsed < 0) {
        return t('common.mustNotBeNegative');
    }
    if (parsed > max) {
        return t('common.mustNotExceed', { max });
    }
    return null;
}

function validate(): boolean {
    clearFieldErrors();
    fieldErrors.tollCategory = form.tollCategory ? null : t('common.required');
    fieldErrors.emptyKm = rangeError(form.emptyKm, MAX_KILOMETERS, true);
    fieldErrors.loadedKm = rangeError(form.loadedKm, MAX_KILOMETERS, true);
    fieldErrors.factorPerKm = rangeError(form.factorPerKm, MAX_FACTOR);
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    dialogError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request = {
        tollCategory: form.tollCategory,
        emptyKm: Number(form.emptyKm),
        loadedKm: Number(form.loadedKm),
        factorPerKm: Number(form.factorPerKm),
    };

    saving.value = true;
    try {
        if (editTarget.value) {
            await updateOrderToll(props.orderId, editTarget.value.id, request);
        } else {
            await createOrderToll(props.orderId, request);
        }
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        applyServerErrors(error);
    } finally {
        saving.value = false;
    }
}

// The `errors` dictionary is keyed by the .NET property name ("FactorPerKm"); a key this dialog
// has no field for still has to reach the user, so it goes to the dialog-level line.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [key, messages] of Object.entries(problem.errors)) {
                const field = key.charAt(0).toLowerCase() + key.slice(1);
                const message = messages.join(' ');
                if (field in fieldErrors) {
                    fieldErrors[field as TollField] = message;
                } else {
                    dialogError.value = dialogError.value ? `${dialogError.value} ${message}` : message;
                }
            }
            return;
        }
    }
    dialogError.value = describe(error, t('orders.tolls.saveFailed'));
}

function askDelete(toll: OrderToll): void {
    deleteTarget.value = toll;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    const toll = deleteTarget.value;
    if (!toll) {
        return;
    }
    listError.value = null;
    try {
        await removeOrderToll(props.orderId, toll.id);
        await refresh();
    } catch (error) {
        listError.value = describe(error, t('orders.tolls.deleteFailed'));
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
    <v-card border flat class="section-card" data-testid="order-tolls">
        <v-card-item prepend-icon="mdi-highway" :title="t('orders.tolls.title')">
            <template v-if="canWrite" #append>
                <v-btn
                    type="button"
                    color="primary"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    data-testid="order-toll-add"
                    @click="openCreate"
                    >{{ t('orders.tolls.add') }}</v-btn
                >
            </template>
        </v-card-item>
        <v-card-text>
            <p v-if="listError" class="text-error list-error" data-testid="order-tolls-error">{{ listError }}</p>
            <p
                v-if="loaded && !listError && tolls.length === 0"
                class="text-medium-emphasis hint"
                data-testid="order-tolls-empty"
            >
                {{ t('orders.tolls.empty') }}
            </p>
            <v-table v-else-if="tolls.length > 0" density="comfortable" class="tolls-table">
                <thead>
                    <tr>
                        <th>{{ t('orders.tolls.category') }}</th>
                        <th class="text-end">{{ t('orders.tolls.emptyKm') }}</th>
                        <th class="text-end">{{ t('orders.tolls.loadedKm') }}</th>
                        <th class="text-end">{{ t('orders.tolls.factorPerKm') }}</th>
                        <th class="text-end">{{ t('orders.tolls.emptyAmount') }}</th>
                        <th class="text-end">{{ t('orders.tolls.loadedAmount') }}</th>
                        <th>{{ t('orders.modified') }}</th>
                        <th v-if="canWrite" class="text-end">{{ t('common.actions') }}</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-for="toll in tolls" :key="toll.id" data-testid="order-toll-row">
                        <td class="font-weight-medium" data-testid="order-toll-category">{{ categoryText(toll) }}</td>
                        <td class="text-end">{{ toll.emptyKm }}</td>
                        <td class="text-end">{{ toll.loadedKm }}</td>
                        <td class="text-end">{{ toll.factorPerKm }}</td>
                        <td class="text-end" data-testid="order-toll-emptyAmount">{{ money(toll.emptyAmount) }}</td>
                        <td class="text-end" data-testid="order-toll-loadedAmount">{{ money(toll.loadedAmount) }}</td>
                        <td class="text-medium-emphasis" data-testid="order-toll-modified">{{ modifiedText(toll) }}</td>
                        <td v-if="canWrite" class="text-end">
                            <div class="row-actions">
                                <v-btn
                                    type="button"
                                    icon="mdi-pencil"
                                    variant="text"
                                    size="small"
                                    :aria-label="t('common.edit')"
                                    data-testid="order-toll-edit"
                                    @click="openEdit(toll)"
                                />
                                <v-btn
                                    type="button"
                                    icon="mdi-delete"
                                    variant="text"
                                    size="small"
                                    color="error"
                                    :aria-label="t('common.delete')"
                                    data-testid="order-toll-delete"
                                    @click="askDelete(toll)"
                                />
                            </div>
                        </td>
                    </tr>
                </tbody>
                <tfoot>
                    <tr class="sum-row">
                        <td>{{ t('orders.sum') }}</td>
                        <td class="text-end">{{ totals.emptyKm }}</td>
                        <td class="text-end">{{ totals.loadedKm }}</td>
                        <td></td>
                        <td class="text-end" data-testid="order-toll-emptyAmount-sum">{{ money(totals.emptyAmount) }}</td>
                        <td class="text-end" data-testid="order-toll-loadedAmount-sum">{{ money(totals.loadedAmount) }}</td>
                        <td></td>
                        <td v-if="canWrite"></td>
                    </tr>
                </tfoot>
            </v-table>
        </v-card-text>

        <v-dialog v-model="dialogOpen" max-width="560" :persistent="saving">
            <v-card data-testid="toll-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'"
                    :title="editTarget ? t('orders.tolls.edit') : t('orders.tolls.new')"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="toll-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-select
                            v-model="form.tollCategory"
                            :label="`${t('orders.tolls.category')} *`"
                            :items="categoryOptions"
                            :error-messages="errorsFor('tollCategory')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                            data-testid="toll-category"
                        >
                            <template #message="{ message }">
                                <span data-testid="toll-category-error">{{ message }}</span>
                            </template>
                        </v-select>
                        <v-row dense>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.emptyKm"
                                    v-native-testid="'toll-emptyKm'"
                                    :label="t('orders.tolls.emptyKm')"
                                    type="number"
                                    step="1"
                                    :error-messages="errorsFor('emptyKm')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="toll-emptyKm-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                            <v-col cols="12" sm="6">
                                <v-text-field
                                    v-model="form.loadedKm"
                                    v-native-testid="'toll-loadedKm'"
                                    :label="t('orders.tolls.loadedKm')"
                                    type="number"
                                    step="1"
                                    :error-messages="errorsFor('loadedKm')"
                                    variant="outlined"
                                    density="comfortable"
                                >
                                    <template #message="{ message }">
                                        <span data-testid="toll-loadedKm-error">{{ message }}</span>
                                    </template>
                                </v-text-field>
                            </v-col>
                        </v-row>
                        <v-text-field
                            v-model="form.factorPerKm"
                            v-native-testid="'toll-factorPerKm'"
                            :label="t('orders.tolls.factorPerKm')"
                            type="number"
                            step="any"
                            :hint="t('orders.tolls.factorHint')"
                            :error-messages="errorsFor('factorPerKm')"
                            variant="outlined"
                            density="comfortable"
                        >
                            <template #message="{ message }">
                                <span data-testid="toll-factorPerKm-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn type="button" variant="text" data-testid="toll-cancel" :disabled="saving" @click="dialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="toll-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <v-dialog v-model="deleteDialogOpen" max-width="440">
            <v-card>
                <v-card-title>{{ t('orders.tolls.deleteTitle') }}</v-card-title>
                <v-card-text>{{
                    t('orders.tolls.deleteConfirm', { category: deleteTarget ? categoryText(deleteTarget) : '' })
                }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="order-toll-delete-cancel" @click="deleteDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="order-toll-delete-confirm" @click="confirmDelete">{{
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

.tolls-table {
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
