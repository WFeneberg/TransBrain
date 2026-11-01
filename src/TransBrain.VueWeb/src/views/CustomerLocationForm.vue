<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    createCustomerLocation,
    getCustomerLocation,
    getCustomerLocationPosition,
    updateCustomerLocation,
    type CustomerLocationPosition,
    type CustomerLocationWriteRequest,
} from '../api/customerLocations';
import LocationMap from '../components/LocationMap.vue';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const customerId = route.params.id as string;
const locationId = route.params.locationId as string | undefined;
const isEditMode = computed(() => locationId !== undefined);

const form = reactive({
    name: '',
    street: '',
    postalCode: '',
    city: '',
    country: '',
    contactPerson: '',
    email: '',
    phone: '',
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
});

const requiredFields = ['name', 'street', 'postalCode', 'city', 'country'] as const;

const textFields = [
    { name: 'name', label: 'customers.location.name' },
    { name: 'street', label: 'common.street' },
    { name: 'postalCode', label: 'common.postalCode' },
    { name: 'city', label: 'common.city' },
    { name: 'country', label: 'common.country' },
    { name: 'contactPerson', label: 'customers.contactPerson' },
    { name: 'email', label: 'customers.location.contactEmail' },
    { name: 'phone', label: 'customers.location.contactPhone' },
] as const;

const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);
const position = ref<CustomerLocationPosition | null>(null);
const mapError = ref<string | null>(null);

const markers = computed(() =>
    position.value
        ? [{ latitude: position.value.latitude, longitude: position.value.longitude, text: 'A', title: `${position.value.name}: ${position.value.address}` }]
        : [],
);

// The map is advisory: a failure must not block editing. 404 = address matches no place,
// 503 = geocoding is not configured or unreachable.
async function loadPosition(): Promise<void> {
    try {
        position.value = await getCustomerLocationPosition(customerId, locationId!);
    } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 503) {
            mapError.value = t('map.unavailable');
        } else if (axios.isAxiosError(error) && error.response?.status === 404) {
            mapError.value = t('customers.location.map.notFound');
        } else {
            mapError.value = t('customers.location.map.error');
        }
    }
}

onMounted(async () => {
    if (isEditMode.value) {
        try {
            const location = await getCustomerLocation(customerId, locationId!);
            if (!location) {
                formError.value = t('customers.location.error.load');
                return;
            }
            form.name = location.name;
            form.street = location.street;
            form.postalCode = location.postalCode;
            form.city = location.city;
            form.country = location.country;
            form.contactPerson = location.contactPerson ?? '';
            form.email = location.email ?? '';
            form.phone = location.phone ?? '';
        } catch (error) {
            formError.value = describeFailure(error, t('customers.location.error.load'));
        }
        await loadPosition();
    }
});

function validate(): boolean {
    for (const field of requiredFields) {
        fieldErrors[field] = form[field] ? null : t('common.required');
    }
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

    const request: CustomerLocationWriteRequest = { ...form };

    try {
        if (isEditMode.value) {
            await updateCustomerLocation(customerId, locationId!, request);
        } else {
            await createCustomerLocation(customerId, request);
        }
        await router.push(`/customers/${customerId}`);
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push(`/customers/${customerId}`);
}

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

        formError.value = describeFailure(error, t('customers.location.error.save'));
        return;
    }

    formError.value = t('customers.location.error.save');
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
        <h1>{{ isEditMode ? t('customers.location.edit') : t('customers.location.new') }}</h1>
        <p v-if="formError" data-testid="location-form-error">{{ formError }}</p>
        <!-- novalidate and plain inputs: see DriverForm.vue. -->
        <form novalidate @submit.prevent="save">
            <div v-for="field in textFields" :key="field.name">
                <label :for="'location-' + field.name">{{ t(field.label) }}</label>
                <input :id="'location-' + field.name" v-model="form[field.name]" :data-testid="'location-' + field.name" />
                <p
                    v-if="attemptedSubmit && fieldErrors[field.name]"
                    :data-testid="'location-' + field.name + '-error'"
                >
                    {{ fieldErrors[field.name] }}
                </p>
            </div>

            <v-btn type="submit" data-testid="location-save">{{ t('common.save') }}</v-btn>
            <v-btn type="button" data-testid="location-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
        </form>

        <template v-if="isEditMode">
            <h2>{{ t('customers.location.map') }}</h2>
            <p v-if="mapError" data-testid="location-map-error">{{ mapError }}</p>
            <LocationMap v-if="markers.length" :markers="markers" data-testid="location-map" />
        </template>
    </v-container>
</template>
