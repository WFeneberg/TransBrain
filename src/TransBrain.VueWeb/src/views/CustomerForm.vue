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
import { catalogSelectOptions, listVehicleCatalog, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import CountrySelect from '../components/CountrySelect.vue';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MAX_PAYMENT_TERM_DAYS = 365;
// Mirrors CustomerAdditionalInfo on the API.
const MAX_HEADCOUNT = 1_000_000;
const MIN_FOUNDING_YEAR = 1800;
const COMMENT_MAX_LENGTH = 2000;
const SECONDARY_MAX_LENGTHS = {
    secondaryName: 200,
    secondaryStreet: 200,
    secondaryPostalCode: 20,
    secondaryCity: 200,
} as const;

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

// The "Zusatz" card. Numbers stay `number | string` because a cleared number field yields ''.
const additional = reactive({
    secondaryName: '',
    secondaryStreet: '',
    secondaryPostalCode: '',
    secondaryCity: '',
    secondaryCountry: '',
    foundedOn: '',
    workerCount: 0 as number | string,
    salariedEmployeeCount: 0 as number | string,
    requiresPalletCheck: false,
    excludeTollFromInvoice: false,
    reimburseToll: false,
    customerClass: '',
    comment: '',
});

type AdditionalErrorField = Exclude<
    keyof typeof additional,
    'requiresPalletCheck' | 'excludeTollFromInvoice' | 'reimburseToll'
>;

const additionalErrors = reactive<Record<AdditionalErrorField, string | null>>({
    secondaryName: null,
    secondaryStreet: null,
    secondaryPostalCode: null,
    secondaryCity: null,
    secondaryCountry: null,
    foundedOn: null,
    workerCount: null,
    salariedEmployeeCount: null,
    customerClass: null,
    comment: null,
});

type AdditionalTextField = 'secondaryName' | 'secondaryStreet' | 'secondaryPostalCode' | 'secondaryCity' | 'secondaryCountry';

const secondaryAddressFields: { name: AdditionalTextField; label: MessageKey; sm: number }[] = [
    { name: 'secondaryName', label: 'customers.additional.secondaryName', sm: 12 },
    { name: 'secondaryStreet', label: 'common.street', sm: 12 },
    { name: 'secondaryCountry', label: 'customers.additional.secondaryCountry', sm: 4 },
    { name: 'secondaryPostalCode', label: 'common.postalCode', sm: 3 },
    { name: 'secondaryCity', label: 'common.city', sm: 5 },
];

const flagFields: { name: 'requiresPalletCheck' | 'excludeTollFromInvoice' | 'reimburseToll'; label: MessageKey }[] = [
    { name: 'requiresPalletCheck', label: 'customers.additional.requiresPalletCheck' },
    { name: 'excludeTollFromInvoice', label: 'customers.additional.excludeTollFromInvoice' },
    { name: 'reimburseToll', label: 'customers.additional.reimburseToll' },
];

const customerClasses = ref<VehicleCatalogEntry[]>([]);
// The class stored on the customer stays selectable even when it has been deactivated since.
const storedCustomerClass = ref<string | null>(null);

const customerClassOptions = computed(() => [
    { title: '–', value: '' },
    ...catalogSelectOptions(customerClasses.value, storedCustomerClass.value, (name) =>
        t('vehicles.inactiveOption', { name }),
    ),
]);

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
    const catalogLoad = loadCustomerClasses();
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
            const data = customer.additional;
            additional.secondaryName = data.secondaryName ?? '';
            additional.secondaryStreet = data.secondaryStreet ?? '';
            additional.secondaryPostalCode = data.secondaryPostalCode ?? '';
            additional.secondaryCity = data.secondaryCity ?? '';
            additional.secondaryCountry = data.secondaryCountry ?? '';
            additional.foundedOn = data.foundedOn ?? '';
            additional.workerCount = data.workerCount;
            additional.salariedEmployeeCount = data.salariedEmployeeCount;
            additional.requiresPalletCheck = data.requiresPalletCheck;
            additional.excludeTollFromInvoice = data.excludeTollFromInvoice;
            additional.reimburseToll = data.reimburseToll;
            additional.customerClass = data.customerClass ?? '';
            additional.comment = data.comment ?? '';
            storedCustomerClass.value = data.customerClass;
        } catch (error) {
            formError.value = describeFailure(error, t('customers.error.load'));
        }
        await loadLocations();
    }
    await catalogLoad;
});

// Without the list the stored class could not even be displayed, so the failure is reported.
async function loadCustomerClasses(): Promise<void> {
    try {
        customerClasses.value = await listVehicleCatalog('CustomerClass');
    } catch {
        const message = t('customers.error.catalogLoad');
        formError.value = formError.value ? `${formError.value} ${message}` : message;
    }
}

const locations = ref<CustomerLocation[]>([]);
const locationsLoading = ref(false);
const locationError = ref<string | null>(null);
const locationDeleteDialogOpen = ref(false);
const locationDeleteTarget = ref<CustomerLocation | null>(null);

const locationHeaders = computed(() => [
    { title: t('customers.location.name'), key: 'name' },
    { title: t('common.city'), key: 'city', value: (location: CustomerLocation) => `${location.postalCode} ${location.city}` },
    { title: t('customers.location.contacts'), key: 'contacts', sortable: false },
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
    validateAdditional();
    return (
        Object.values(fieldErrors).every((message) => message === null) &&
        Object.values(additionalErrors).every((message) => message === null)
    );
}

function validateAdditional(): void {
    for (const [field, max] of Object.entries(SECONDARY_MAX_LENGTHS) as [keyof typeof SECONDARY_MAX_LENGTHS, number][]) {
        additionalErrors[field] = additional[field].trim().length > max ? t('vehicleCatalog.maxLength', { max }) : null;
    }
    // The dropdown only offers valid ISO codes, so no format check is needed any more.
    additionalErrors.secondaryCountry = null;
    additionalErrors.foundedOn =
        !additional.foundedOn || Number(additional.foundedOn.slice(0, 4)) >= MIN_FOUNDING_YEAR
            ? null
            : t('customers.additional.foundedOnTooEarly', { year: MIN_FOUNDING_YEAR });
    additionalErrors.workerCount = headcountError(additional.workerCount);
    additionalErrors.salariedEmployeeCount = headcountError(additional.salariedEmployeeCount);
    additionalErrors.customerClass = null;
    additionalErrors.comment =
        additional.comment.trim().length > COMMENT_MAX_LENGTH
            ? t('vehicleCatalog.maxLength', { max: COMMENT_MAX_LENGTH })
            : null;
}

function headcountError(value: number | string): string | null {
    const count = Number(value);
    return value !== '' && Number.isInteger(count) && count >= 0 && count <= MAX_HEADCOUNT ? null : t('common.invalid');
}

function optionalText(value: string): string | null {
    const trimmed = value.trim();
    return trimmed ? trimmed : null;
}

function additionalRequest(): CustomerWriteRequest['additional'] {
    return {
        secondaryName: optionalText(additional.secondaryName),
        secondaryStreet: optionalText(additional.secondaryStreet),
        secondaryPostalCode: optionalText(additional.secondaryPostalCode),
        secondaryCity: optionalText(additional.secondaryCity),
        secondaryCountry: optionalText(additional.secondaryCountry)?.toUpperCase() ?? null,
        foundedOn: additional.foundedOn || null,
        workerCount: Number(additional.workerCount),
        salariedEmployeeCount: Number(additional.salariedEmployeeCount),
        requiresPalletCheck: additional.requiresPalletCheck,
        excludeTollFromInvoice: additional.excludeTollFromInvoice,
        reimburseToll: additional.reimburseToll,
        customerClass: additional.customerClass || null,
        comment: optionalText(additional.comment),
    };
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request: CustomerWriteRequest = {
        ...form,
        paymentTermDays: Number(form.paymentTermDays),
        additional: additionalRequest(),
    };

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

function additionalErrorsFor(field: AdditionalErrorField): string[] {
    const message = additionalErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

// Keys are .NET property names (PascalCase); lowercase the first letter to find the field.
// The additional data comes as a dotted path ("Additional.WorkerCount").
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const message = messages.join(' ');
                if (field.startsWith('Additional.')) {
                    const nested = field.slice('Additional.'.length);
                    const nestedName = nested.charAt(0).toLowerCase() + nested.slice(1);
                    if (nestedName in additionalErrors) {
                        additionalErrors[nestedName as AdditionalErrorField] = message;
                        continue;
                    }
                }
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
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
                                    <CountrySelect
                                        v-if="field.name === 'country'"
                                        v-model="form.country"
                                        :label="labelFor('country', field.label)"
                                        testid="customer-country"
                                        :error-messages="errorsFor('country')"
                                    />
                                    <v-text-field
                                        v-else
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

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="customer-additional">
                        <v-card-item prepend-icon="mdi-card-account-details-outline" :title="t('customers.section.additional')" />
                        <v-card-text>
                            <v-row>
                                <v-col cols="12" md="6">
                                    <div class="section-group-title">{{ t('customers.additional.secondaryAddress') }}</div>
                                    <v-row dense>
                                        <v-col v-for="field in secondaryAddressFields" :key="field.name" cols="12" :sm="field.sm">
                                            <CountrySelect
                                                v-if="field.name === 'secondaryCountry'"
                                                v-model="additional.secondaryCountry"
                                                :label="t(field.label)"
                                                testid="customer-secondaryCountry"
                                                clearable
                                                :error-messages="additionalErrorsFor('secondaryCountry')"
                                            />
                                            <v-text-field
                                                v-else
                                                v-model="additional[field.name]"
                                                v-native-testid="'customer-' + field.name"
                                                :label="t(field.label)"
                                                :error-messages="additionalErrorsFor(field.name)"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span :data-testid="'customer-' + field.name + '-error'">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                    </v-row>
                                </v-col>
                                <v-col cols="12" md="6">
                                    <div class="section-group-title">{{ t('customers.additional.companyFigures') }}</div>
                                    <v-row dense>
                                        <v-col cols="12" sm="4">
                                            <v-text-field
                                                v-model="additional.foundedOn"
                                                v-native-testid="'customer-foundedOn'"
                                                :label="t('customers.additional.foundedOn')"
                                                type="date"
                                                :error-messages="additionalErrorsFor('foundedOn')"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span data-testid="customer-foundedOn-error">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                        <v-col
                                            v-for="field in (['workerCount', 'salariedEmployeeCount'] as const)"
                                            :key="field"
                                            cols="12"
                                            sm="4"
                                        >
                                            <v-text-field
                                                v-model.number="additional[field]"
                                                v-native-testid="'customer-' + field"
                                                :label="t(`customers.additional.${field}`)"
                                                type="number"
                                                min="0"
                                                step="1"
                                                :error-messages="additionalErrorsFor(field)"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span :data-testid="'customer-' + field + '-error'">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                        <v-col cols="12">
                                            <v-select
                                                v-model="additional.customerClass"
                                                :label="t('customers.additional.customerClass')"
                                                :items="customerClassOptions"
                                                :error-messages="additionalErrorsFor('customerClass')"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                                data-testid="customer-customerClass"
                                            >
                                                <template #message="{ message }">
                                                    <span data-testid="customer-customerClass-error">{{ message }}</span>
                                                </template>
                                            </v-select>
                                        </v-col>
                                        <v-col cols="12">
                                            <v-checkbox
                                                v-for="flag in flagFields"
                                                :key="flag.name"
                                                v-model="additional[flag.name]"
                                                v-native-testid="'customer-' + flag.name"
                                                :label="t(flag.label)"
                                                density="compact"
                                                hide-details
                                            />
                                        </v-col>
                                    </v-row>
                                </v-col>
                                <v-col cols="12">
                                    <v-textarea
                                        v-model="additional.comment"
                                        v-native-testid="'customer-comment'"
                                        :label="t('customers.additional.comment')"
                                        :counter="COMMENT_MAX_LENGTH"
                                        :error-messages="additionalErrorsFor('comment')"
                                        rows="3"
                                        auto-grow
                                        variant="outlined"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="customer-comment-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
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
                                <template #item.contacts="{ item }">
                                    <div class="py-2" data-testid="location-contacts">
                                        <div v-for="(contact, index) in item.contacts" :key="index" class="text-no-wrap">
                                            <span class="font-weight-medium">{{ contact.name }}</span>
                                            <template v-if="contact.phone"> · {{ contact.phone }}</template>
                                            <template v-if="contact.email"> · {{ contact.email }}</template>
                                        </div>
                                        <template v-if="item.contacts.length === 0">–</template>
                                    </div>
                                </template>
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
