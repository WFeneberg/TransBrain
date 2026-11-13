<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    createCustomerLocation,
    getCustomerLocation,
    getCustomerLocationPosition,
    updateCustomerLocation,
    MAX_LOCATION_CONTACTS,
    type CustomerLocationContact,
    type CustomerLocationPosition,
    type CustomerLocationWriteRequest,
} from '../api/customerLocations';
import LocationMap from '../components/LocationMap.vue';
import CountrySelect from '../components/CountrySelect.vue';
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
    companyName: '',
    vatId: '',
});

const fieldErrors = reactive<Record<keyof typeof form, string | null>>({
    name: null,
    street: null,
    postalCode: null,
    city: null,
    country: null,
    companyName: null,
    vatId: null,
});

type ContactField = 'name' | 'phone' | 'email';

interface ContactRow {
    key: number;
    name: string;
    phone: string;
    email: string;
    errors: Record<ContactField, string | null>;
}

const contactFields: { name: ContactField; label: MessageKey; sm: number }[] = [
    { name: 'name', label: 'customers.location.contactName', sm: 12 },
    { name: 'phone', label: 'customers.location.contactPhone', sm: 6 },
    { name: 'email', label: 'customers.location.contactEmail', sm: 6 },
];

const contacts = ref<ContactRow[]>([]);
let nextContactKey = 0;

function addContact(contact?: CustomerLocationContact): void {
    contacts.value.push({
        key: nextContactKey++,
        name: contact?.name ?? '',
        phone: contact?.phone ?? '',
        email: contact?.email ?? '',
        errors: { name: null, phone: null, email: null },
    });
}

function removeContact(index: number): void {
    contacts.value.splice(index, 1);
}

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
        key: 'company',
        icon: 'mdi-receipt-text-outline',
        title: 'customers.location.section.company',
        fields: [
            { name: 'companyName', label: 'customers.location.companyName', sm: 12 },
            { name: 'vatId', label: 'customers.location.vatId', sm: 12 },
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
            form.companyName = location.companyName ?? '';
            form.vatId = location.vatId ?? '';
            contacts.value = [];
            location.contacts.forEach((contact) => addContact(contact));
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
    for (const contact of contacts.value) {
        contact.errors.name = contact.name.trim() ? null : t('common.required');
        contact.errors.phone = null;
        contact.errors.email =
            contact.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contact.email.trim()) ? t('common.invalid') : null;
    }
    return (
        Object.values(fieldErrors).every((message) => message === null) &&
        contacts.value.every((c) => Object.values(c.errors).every((message) => message === null))
    );
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request: CustomerLocationWriteRequest = {
        ...form,
        companyName: form.companyName.trim() || null,
        vatId: form.vatId.trim() || null,
        contacts: contacts.value.map((c) => ({
            name: c.name.trim(),
            phone: c.phone.trim() || null,
            email: c.email.trim() || null,
        })),
    };

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

function contactErrorsFor(contact: ContactRow, field: ContactField): string[] {
    const message = contact.errors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

// "Contacts[1].Name" from the server marks the field of that contact row.
function applyContactError(field: string, message: string): boolean {
    const match = /^Contacts\[(\d+)\]\.(Name|Phone|Email)$/.exec(field);
    const contact = match ? contacts.value[Number(match[1])] : undefined;
    if (!match || !contact) {
        return false;
    }
    contact.errors[match[2].toLowerCase() as ContactField] = message;
    return true;
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
                } else if (!applyContactError(field, message)) {
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
                                    <CountrySelect
                                        v-if="field.name === 'country'"
                                        v-model="form.country"
                                        :label="labelFor('country', field.label)"
                                        testid="location-country"
                                        :error-messages="errorsFor('country')"
                                    />
                                    <v-text-field
                                        v-else
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

                <v-col cols="12" md="6">
                    <v-card border flat class="section-card" data-testid="location-contacts-section">
                        <v-card-item prepend-icon="mdi-card-account-phone-outline" :title="t('customers.location.contacts')" />
                        <v-card-text>
                            <p v-if="contacts.length === 0" class="text-medium-emphasis mb-3" data-testid="location-contacts-empty">
                                {{ t('customers.location.noContacts') }}
                            </p>
                            <div v-for="(contact, index) in contacts" :key="contact.key" class="contact-row" data-testid="location-contact-row">
                                <v-row dense>
                                    <v-col v-for="field in contactFields" :key="field.name" cols="12" :sm="field.sm">
                                        <v-text-field
                                            v-model="contact[field.name]"
                                            v-native-testid="'location-contact-' + field.name"
                                            :label="field.name === 'name' ? `${t(field.label)} *` : t(field.label)"
                                            :error-messages="contactErrorsFor(contact, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                        >
                                            <template #message="{ message }">
                                                <span :data-testid="'location-contact-' + field.name + '-error'">{{ message }}</span>
                                            </template>
                                            <template v-if="field.name === 'name'" #append>
                                                <v-btn
                                                    icon="mdi-delete-outline"
                                                    variant="text"
                                                    color="error"
                                                    density="comfortable"
                                                    data-testid="location-contact-remove"
                                                    :aria-label="t('customers.location.removeContact')"
                                                    @click="removeContact(index)"
                                                />
                                            </template>
                                        </v-text-field>
                                    </v-col>
                                </v-row>
                            </div>
                            <v-btn
                                variant="tonal"
                                prepend-icon="mdi-account-plus-outline"
                                data-testid="location-contact-add"
                                :disabled="contacts.length >= MAX_LOCATION_CONTACTS"
                                @click="addContact()"
                                >{{ t('customers.location.addContact') }}</v-btn
                            >
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

<style scoped>
.contact-row {
    margin-bottom: 12px;
    padding-bottom: 4px;
    border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
