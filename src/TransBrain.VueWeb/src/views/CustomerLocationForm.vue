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
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n, type MessageKey } from '../i18n';

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

type FieldName = keyof typeof form;

interface SectionField {
    name: FieldName;
    label: MessageKey;
    sm: number;
}

const sections: { key: string; icon: string; title: MessageKey; fields: SectionField[] }[] = [
    {
        key: 'address',
        icon: 'mdi-map-marker-outline',
        title: 'customers.location.section.address',
        fields: [
            { name: 'name', label: 'customers.location.name', sm: 12 },
            { name: 'street', label: 'common.street', sm: 12 },
            { name: 'postalCode', label: 'common.postalCode', sm: 4 },
            { name: 'city', label: 'common.city', sm: 8 },
            { name: 'country', label: 'common.country', sm: 12 },
        ],
    },
    {
        key: 'contact',
        icon: 'mdi-phone-outline',
        title: 'customers.location.section.contact',
        fields: [
            { name: 'contactPerson', label: 'customers.contactPerson', sm: 12 },
            { name: 'email', label: 'customers.location.contactEmail', sm: 12 },
            { name: 'phone', label: 'customers.location.contactPhone', sm: 12 },
        ],
    },
];

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

function labelFor(name: FieldName, label: MessageKey): string {
    return (requiredFields as readonly string[]).includes(name) ? `${t(label)} *` : t(label);
}

function errorsFor(field: FieldName): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
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
    <v-container class="page-container">
        <div class="page-header form-header">
            <div class="title-row">
                <h1 class="text-h4">{{ isEditMode ? t('customers.location.edit') : t('customers.location.new') }}</h1>
            </div>
        </div>
        <p v-if="formError" class="text-error form-error" data-testid="location-form-error">{{ formError }}</p>
        <!-- novalidate: see DriverForm.vue. v-native-testid puts the testids on the native inputs
             (see VehicleForm.vue). -->
        <form novalidate @submit.prevent="save">
            <v-row>
                <v-col v-for="section in sections" :key="section.key" cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item :prepend-icon="section.icon" :title="t(section.title)" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-for="field in section.fields" :key="field.name" cols="12" :sm="field.sm">
                                    <v-text-field
                                        v-model="form[field.name]"
                                        v-native-testid="'location-' + field.name"
                                        :label="labelFor(field.name, field.label)"
                                        :error-messages="errorsFor(field.name)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="'location-' + field.name + '-error'">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-if="isEditMode" cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-map-outline" :title="t('customers.location.map')" />
                        <v-card-text>
                            <p v-if="mapError" class="text-error error-line" data-testid="location-map-error">{{ mapError }}</p>
                            <div v-if="markers.length" class="map-frame">
                                <LocationMap :markers="markers" data-testid="location-map" />
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="location-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="location-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>
    </v-container>
</template>
