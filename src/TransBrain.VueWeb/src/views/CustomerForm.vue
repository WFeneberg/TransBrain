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
import { useI18n } from '../i18n';

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

const textFields = [
    { name: 'name', label: 'common.name' },
    { name: 'street', label: 'common.street' },
    { name: 'postalCode', label: 'common.postalCode' },
    { name: 'city', label: 'common.city' },
    { name: 'country', label: 'common.country' },
    { name: 'contactPerson', label: 'customers.contactPerson' },
    { name: 'email', label: 'customers.email' },
    { name: 'phone', label: 'customers.phone' },
    { name: 'vatId', label: 'customers.vatId' },
] as const;

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
const locationError = ref<string | null>(null);

const locationHeaders = computed(() => [
    { title: t('customers.location.name'), key: 'name' },
    { title: t('common.city'), key: 'city' },
    { title: t('customers.contactPerson'), key: 'contactPerson' },
    { title: t('customers.location.contactPhone'), key: 'phone' },
    { title: t('customers.location.contactEmail'), key: 'email' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

async function loadLocations(): Promise<void> {
    try {
        locations.value = await listCustomerLocations(customerId!);
    } catch (error) {
        locationError.value = describeFailure(error, t('customers.location.error.list'));
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
    <v-container>
        <h1>{{ isEditMode ? t('customers.edit') : t('customers.new') }}</h1>
        <p v-if="formError" data-testid="customer-form-error">{{ formError }}</p>
        <!-- novalidate: see DriverForm.vue. Plain inputs carry the testids (see DriverForm.vue). -->
        <form novalidate @submit.prevent="save">
            <div v-for="field in textFields" :key="field.name">
                <label :for="'customer-' + field.name">{{ t(field.label) }}</label>
                <input :id="'customer-' + field.name" v-model="form[field.name]" :data-testid="'customer-' + field.name" />
                <p
                    v-if="attemptedSubmit && fieldErrors[field.name]"
                    :data-testid="'customer-' + field.name + '-error'"
                >
                    {{ fieldErrors[field.name] }}
                </p>
            </div>

            <div>
                <label for="customer-paymentTermDays">{{ t('customers.paymentTermDays') }}</label>
                <input
                    id="customer-paymentTermDays"
                    v-model.number="form.paymentTermDays"
                    type="number"
                    min="0"
                    max="365"
                    data-testid="customer-paymentTermDays"
                />
                <p v-if="attemptedSubmit && fieldErrors.paymentTermDays" data-testid="customer-paymentTermDays-error">
                    {{ fieldErrors.paymentTermDays }}
                </p>
            </div>

            <v-btn type="submit" data-testid="customer-save">{{ t('common.save') }}</v-btn>
            <v-btn type="button" data-testid="customer-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
        </form>

        <template v-if="isEditMode">
            <h2>{{ t('customers.locations') }}</h2>
            <v-btn data-testid="location-add" @click="router.push(`/customers/${customerId}/locations/new`)">
                {{ t('customers.location.add') }}
            </v-btn>
            <p v-if="locationError" data-testid="location-action-error">{{ locationError }}</p>
            <v-data-table
                :headers="locationHeaders"
                :items="locations"
                :items-per-page="-1"
                item-value="id"
                data-testid="location-table"
            >
                <template #item.name="{ item }">
                    <span data-testid="location-name">{{ item.name }}</span>
                </template>
                <template #item.city="{ item }">{{ item.postalCode }} {{ item.city }}</template>
                <template #item.actions="{ item }">
                    <v-btn data-testid="location-edit" @click="router.push(`/customers/${customerId}/locations/${item.id}`)">
                        {{ t('common.edit') }}
                    </v-btn>
                    <v-btn data-testid="location-delete" @click="removeLocation(item)">{{ t('common.delete') }}</v-btn>
                </template>
            </v-data-table>
        </template>
    </v-container>
</template>
