<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createCustomer, getCustomer, updateCustomer, type CustomerWriteRequest } from '../api/customers';
import {
    deleteCustomerLocation,
    listCustomerLocations,
    type CustomerLocation,
} from '../api/customerLocations';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MAX_PAYMENT_TERM_DAYS = 365;

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const customerId = route.params.id as string | undefined;
const isEditMode = computed(() => customerId !== undefined);

const form = reactive({
    name: '',
    street: '',
    postalCode: '',
    city: '',
    country: '',
    contactPerson: '',
    email: '',
    phone: '',
    vatId: '',
    paymentTermDays: 30,
});

const fieldErrors = reactive<Record<keyof typeof form, string | null>>({
    name: null,
    street: null,
    postalCode: null,
    city: null,
    country: null,
    contactPerson: null,
    email: null,
    phone: null,
    vatId: null,
    paymentTermDays: null,
});

// Gates field errors the same way the Angular form does: shown only after the first submit.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

const requiredFields = ['name', 'street', 'postalCode', 'city', 'country'] as const;

type TextFieldName = Exclude<keyof typeof form, 'paymentTermDays'>;

interface SectionField {
    name: TextFieldName;
    label: MessageKey;
    sm: number;
}

const sections: { key: string; icon: string; title: MessageKey; fields: SectionField[] }[] = [
    {
        key: 'company',
        icon: 'mdi-domain',
        title: 'customers.section.company',
        fields: [
            { name: 'name', label: 'common.name', sm: 6 },
            { name: 'vatId', label: 'customers.vatId', sm: 3 },
        ],
    },
    {
        key: 'address',
        icon: 'mdi-map-marker-outline',
        title: 'customers.section.address',
        fields: [
            { name: 'street', label: 'common.street', sm: 12 },
            { name: 'postalCode', label: 'common.postalCode', sm: 4 },
            { name: 'city', label: 'common.city', sm: 8 },
            { name: 'country', label: 'common.country', sm: 12 },
        ],
    },
    {
        key: 'contact',
        icon: 'mdi-phone-outline',
        title: 'customers.section.contact',
        fields: [
            { name: 'contactPerson', label: 'customers.contactPerson', sm: 12 },
            { name: 'email', label: 'customers.email', sm: 6 },
            { name: 'phone', label: 'customers.phone', sm: 6 },
        ],
    },
];

onMounted(async () => {
    if (isEditMode.value) {
        try {
            const customer = await getCustomer(customerId!);
            form.name = customer.name;
            form.street = customer.street;
            form.postalCode = customer.postalCode;
            form.city = customer.city;
            form.country = customer.country;
            form.contactPerson = customer.contactPerson ?? '';
            form.email = customer.email ?? '';
            form.phone = customer.phone ?? '';
            form.vatId = customer.vatId ?? '';
            form.paymentTermDays = customer.paymentTermDays;
        } catch (error) {
            formError.value = describeFailure(error, t('customers.error.load'));
        }
        await loadLocations();
    }
});

const locations = ref<CustomerLocation[]>([]);
const locationsLoading = ref(false);
const locationError = ref<string | null>(null);
const locationDeleteDialogOpen = ref(false);
const locationDeleteTarget = ref<CustomerLocation | null>(null);

const locationHeaders = computed(() => [
    { title: t('customers.location.name'), key: 'name' },
    { title: t('common.city'), key: 'city', value: (location: CustomerLocation) => `${location.postalCode} ${location.city}` },
    { title: t('customers.contactPerson'), key: 'contactPerson' },
    { title: t('customers.location.contactPhone'), key: 'phone' },
    { title: t('customers.location.contactEmail'), key: 'email' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

async function loadLocations(): Promise<void> {
    locationsLoading.value = true;
    try {
        locations.value = await listCustomerLocations(customerId!);
    } catch (error) {
        locationError.value = describeFailure(error, t('customers.location.error.list'));
    } finally {
        locationsLoading.value = false;
    }
}

function askRemoveLocation(location: CustomerLocation): void {
    locationDeleteTarget.value = location;
    locationDeleteDialogOpen.value = true;
}

async function confirmRemoveLocation(): Promise<void> {
    if (locationDeleteTarget.value) {
        await removeLocation(locationDeleteTarget.value);
    }
}

async function removeLocation(location: CustomerLocation): Promise<void> {
    locationError.value = null;
    try {
        await deleteCustomerLocation(customerId!, location.id);
        await loadLocations();
    } catch (error) {
        locationError.value = describeFailure(error, t('customers.location.error.delete'));
    }
}

function validate(): boolean {
    for (const field of requiredFields) {
        fieldErrors[field] = form[field] ? null : t('common.required');
    }
    const days = Number(form.paymentTermDays);
    fieldErrors.paymentTermDays =
        Number.isInteger(days) && days >= 0 && days <= MAX_PAYMENT_TERM_DAYS ? null : t('common.invalid');
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
        fieldErrors.email = t('common.invalid');
    } else {
        fieldErrors.email = null;
    }
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request: CustomerWriteRequest = { ...form, paymentTermDays: Number(form.paymentTermDays) };

    try {
        if (isEditMode.value) {
            await updateCustomer(customerId!, request);
        } else {
            await createCustomer(request);
        }
        await router.push('/customers');
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push('/customers');
}

function labelFor(name: keyof typeof form, label: MessageKey): string {
    return (requiredFields as readonly string[]).includes(name) ? `${t(label)} *` : t(label);
}

function errorsFor(field: keyof typeof form): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

// Keys are .NET property names (PascalCase); lowercase the first letter to find the field.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
                const message = messages.join(' ');
                if (fieldName in fieldErrors) {
                    fieldErrors[fieldName as keyof typeof fieldErrors] = message;
                } else {
                    formError.value = formError.value ? `${formError.value} ${message}` : message;
                }
            }
            return;
        }

        formError.value = describeFailure(error, t('customers.error.save'));
        return;
    }

    formError.value = t('customers.error.save');
}

function describeFailure(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container class="page-container">
        <div class="page-header form-header">
            <div class="title-row">
                <h1 class="text-h4">{{ isEditMode ? t('customers.edit') : t('customers.new') }}</h1>
            </div>
        </div>
        <p v-if="formError" class="text-error form-error" data-testid="customer-form-error">{{ formError }}</p>
        <!-- novalidate: see DriverForm.vue. v-native-testid puts the testids on the native inputs
             (see VehicleForm.vue). -->
        <form novalidate @submit.prevent="save">
            <v-row>
                <v-col v-for="section in sections" :key="section.key" cols="12" :md="section.key === 'company' ? 12 : 6">
                    <v-card border flat class="section-card">
                        <v-card-item :prepend-icon="section.icon" :title="t(section.title)" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-for="field in section.fields" :key="field.name" cols="12" :sm="field.sm">
                                    <v-text-field
                                        v-model="form[field.name]"
                                        v-native-testid="'customer-' + field.name"
                                        :label="labelFor(field.name, field.label)"
                                        :error-messages="errorsFor(field.name)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="'customer-' + field.name + '-error'">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col v-if="section.key === 'company'" cols="12" sm="3">
                                    <v-text-field
                                        v-model.number="form.paymentTermDays"
                                        v-native-testid="'customer-paymentTermDays'"
                                        :label="t('customers.paymentTermDays')"
                                        type="number"
                                        min="0"
                                        max="365"
                                        :error-messages="errorsFor('paymentTermDays')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="customer-paymentTermDays-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-if="isEditMode" cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-map-marker-multiple-outline" :title="t('customers.locations')">
                            <template #append>
                                <v-btn
                                    color="primary"
                                    variant="tonal"
                                    prepend-icon="mdi-plus"
                                    data-testid="location-add"
                                    @click="router.push(`/customers/${customerId}/locations/new`)"
                                    >{{ t('customers.location.add') }}</v-btn
                                >
                            </template>
                        </v-card-item>
                        <v-card-text>
                            <p v-if="locationError" class="text-error error-line" data-testid="location-action-error">
                                {{ locationError }}
                            </p>
                            <v-data-table
                                :headers="locationHeaders"
                                :items="locations"
                                :items-per-page="-1"
                                :loading="locationsLoading"
                                :no-data-text="t('customers.location.empty')"
                                item-value="id"
                                hide-default-footer
                                hover
                                data-testid="location-table"
                            >
                                <template #item.name="{ item }">
                                    <span class="font-weight-bold" data-testid="location-name">{{ item.name }}</span>
                                </template>
                                <template #item.contactPerson="{ value }">{{ value || '–' }}</template>
                                <template #item.phone="{ value }">{{ value || '–' }}</template>
                                <template #item.email="{ value }">{{ value || '–' }}</template>
                                <template #item.actions="{ item }">
                                    <div class="row-actions">
                                        <v-tooltip :text="t('common.edit')" location="top">
                                            <template #activator="{ props: tooltip }">
                                                <v-btn
                                                    v-bind="tooltip"
                                                    icon="mdi-pencil"
                                                    variant="text"
                                                    size="small"
                                                    :aria-label="t('common.edit')"
                                                    data-testid="location-edit"
                                                    @click="router.push(`/customers/${customerId}/locations/${item.id}`)"
                                                />
                                            </template>
                                        </v-tooltip>
                                        <v-tooltip :text="t('common.delete')" location="top">
                                            <template #activator="{ props: tooltip }">
                                                <v-btn
                                                    v-bind="tooltip"
                                                    icon="mdi-delete"
                                                    variant="text"
                                                    size="small"
                                                    color="error"
                                                    :aria-label="t('common.delete')"
                                                    data-testid="location-delete"
                                                    @click="askRemoveLocation(item)"
                                                />
                                            </template>
                                        </v-tooltip>
                                    </div>
                                </template>
                            </v-data-table>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="customer-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="customer-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>

        <ConfirmDialog
            v-model="locationDeleteDialogOpen"
            :title="t('customers.location.deleteTitle')"
            :message="t('customers.location.deleteConfirm', { name: locationDeleteTarget?.name ?? '' })"
            test-id-prefix="location-delete"
            @confirm="confirmRemoveLocation"
        />
    </v-container>
</template>
