<script setup lang="ts">
import axios from 'axios';
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue';
import {
    getInvoiceDesign,
    previewInvoiceDesign,
    updateInvoiceDesign,
    type InvoiceDesignPayload,
    type InvoiceDesignResponse,
    type InvoiceLogoPosition,
} from '../api/invoiceDesign';
import CountrySelect from '../components/CountrySelect.vue';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

/** Mirrors the API's limits: MaxLogoBytes and the payload validator's MaximumLength rules. */
const MAX_LOGO_BYTES = 1024 * 1024;
const TEXT_MAX_LENGTH = 1000;
const ALLOWED_LOGO_TYPES = ['image/png', 'image/jpeg'];
const ACCENT_COLOR_PATTERN = /^#[0-9A-Fa-f]{6}$/;
const FALLBACK_SWATCH = '#1F3864';

const { t, locale } = useI18n();

const form = reactive({
    companyName: '',
    street: '',
    postalCode: '',
    city: '',
    countryCode: '',
    vatId: '',
    taxNumber: '',
    email: '',
    phone: '',
    iban: '',
    bic: '',
    bankName: '',
    accentColor: FALLBACK_SWATCH,
    fontSizePoints: 10,
    logoPosition: 'Left' as InvoiceLogoPosition,
    logoBase64: null as string | null,
    logoContentType: null as string | null,
    headerText: '',
    footerText: '',
});

type TextField =
    | 'companyName'
    | 'street'
    | 'postalCode'
    | 'city'
    | 'countryCode'
    | 'vatId'
    | 'taxNumber'
    | 'email'
    | 'phone'
    | 'iban'
    | 'bic'
    | 'bankName'
    | 'headerText'
    | 'footerText';

const REQUIRED_TEXT_FIELDS: readonly TextField[] = ['companyName', 'street', 'postalCode', 'city', 'vatId', 'email', 'iban'];

const MAX_LENGTHS: ReadonlyArray<[TextField, number]> = [
    ['companyName', 200],
    ['street', 200],
    ['postalCode', 20],
    ['city', 200],
    ['vatId', 20],
    ['taxNumber', 30],
    ['email', 200],
    ['phone', 50],
    ['iban', 34],
    ['bic', 11],
    ['bankName', 100],
    ['headerText', TEXT_MAX_LENGTH],
    ['footerText', TEXT_MAX_LENGTH],
];

const fieldErrors = reactive<Record<string, string | null>>(
    Object.fromEntries(Object.keys(form).map((field) => [field, null])),
);

// Same gate as VehicleForm: field errors only render once the user has tried to submit,
// a freshly loaded form must not greet them with "This field is required".
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);
const savedMessage = ref<string | null>(null);
const logoError = ref<string | null>(null);

const loading = ref(true);
const saving = ref(false);
const previewing = ref(false);
const isCustomized = ref(true);
const updatedAt = ref<string | null>(null);
const logoFile = ref<File | File[] | null>(null);
const previewUrl = ref<string | null>(null);

const logoPositionOptions = computed(() => [
    { title: t('invoiceDesign.logoLeft'), value: 'Left' },
    { title: t('invoiceDesign.logoRight'), value: 'Right' },
]);

const logoDataUrl = computed(() =>
    form.logoBase64 ? `data:${form.logoContentType ?? 'image/png'};base64,${form.logoBase64}` : null,
);

// The native color input rejects anything but "#rrggbb", so a half-typed hex falls back to a
// valid swatch instead of flooding the console with warnings.
const swatchColor = computed(() =>
    ACCENT_COLOR_PATTERN.test(form.accentColor.trim()) ? form.accentColor.trim() : FALLBACK_SWATCH,
);

const updatedLine = computed(() =>
    updatedAt.value
        ? t('invoiceDesign.updatedLine', { date: new Date(updatedAt.value).toLocaleString(locale.value) })
        : null,
);

onMounted(async () => {
    try {
        applyResponse(await getInvoiceDesign());
    } catch (error) {
        formError.value = describeFailure(error, t('invoiceDesign.loadFailed'));
    } finally {
        loading.value = false;
    }
});

onBeforeUnmount(() => setPreviewBlob(null));

function applyResponse(design: InvoiceDesignResponse): void {
    form.companyName = design.companyName;
    form.street = design.street;
    form.postalCode = design.postalCode;
    form.city = design.city;
    form.countryCode = design.countryCode;
    form.vatId = design.vatId;
    form.taxNumber = design.taxNumber ?? '';
    form.email = design.email;
    form.phone = design.phone ?? '';
    form.iban = design.iban;
    form.bic = design.bic ?? '';
    form.bankName = design.bankName ?? '';
    form.accentColor = design.accentColor;
    form.fontSizePoints = design.fontSizePoints;
    form.logoPosition = design.logoPosition;
    form.logoBase64 = design.logoBase64;
    form.logoContentType = design.logoContentType;
    form.headerText = design.headerText ?? '';
    form.footerText = design.footerText ?? '';
    isCustomized.value = design.isCustomized;
    updatedAt.value = design.updatedAt;
}

function trimmed(value: string): string {
    return value.trim();
}

function optionalText(value: string): string | null {
    return trimmed(value) === '' ? null : trimmed(value);
}

function errorsFor(field: string): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function validate(): boolean {
    for (const field of Object.keys(fieldErrors)) {
        fieldErrors[field] = null;
    }
    for (const field of REQUIRED_TEXT_FIELDS) {
        fieldErrors[field] = trimmed(form[field]) === '' ? t('common.required') : null;
    }
    const country = trimmed(form.countryCode);
    fieldErrors.countryCode = country === '' ? t('common.required') : /^[A-Za-z]{2}$/.test(country) ? null : t('common.invalid');
    if (!fieldErrors.email && !form.email.includes('@')) {
        fieldErrors.email = t('common.invalid');
    }
    const color = trimmed(form.accentColor);
    fieldErrors.accentColor = color === '' ? t('common.required') : ACCENT_COLOR_PATTERN.test(color) ? null : t('common.invalid');
    if (!Number.isInteger(form.fontSizePoints) || form.fontSizePoints < 8 || form.fontSizePoints > 14) {
        fieldErrors.fontSizePoints = t('common.invalid');
    }
    for (const [field, max] of MAX_LENGTHS) {
        if (!fieldErrors[field] && trimmed(form[field]).length > max) {
            fieldErrors[field] = t('common.maxLength', { max });
        }
    }
    return Object.values(fieldErrors).every((message) => message === null) && logoError.value === null;
}

function toPayload(): InvoiceDesignPayload {
    return {
        companyName: trimmed(form.companyName),
        street: trimmed(form.street),
        postalCode: trimmed(form.postalCode),
        city: trimmed(form.city),
        countryCode: trimmed(form.countryCode).toUpperCase(),
        vatId: trimmed(form.vatId),
        taxNumber: optionalText(form.taxNumber),
        email: trimmed(form.email),
        phone: optionalText(form.phone),
        iban: trimmed(form.iban),
        bic: optionalText(form.bic),
        bankName: optionalText(form.bankName),
        accentColor: trimmed(form.accentColor).toUpperCase(),
        fontSizePoints: form.fontSizePoints,
        logoPosition: form.logoPosition,
        logoBase64: form.logoBase64,
        logoContentType: form.logoBase64 ? form.logoContentType : null,
        headerText: optionalText(form.headerText),
        footerText: optionalText(form.footerText),
    };
}

function onColorPicked(event: Event): void {
    // The API stores the color uppercased; writing it back the same way keeps save idempotent.
    form.accentColor = (event.target as HTMLInputElement).value.toUpperCase();
}

async function onLogoChosen(file: File | File[] | null): Promise<void> {
    logoError.value = null;
    const single = Array.isArray(file) ? file[0] : file;
    if (!single) {
        return;
    }
    if (!ALLOWED_LOGO_TYPES.includes(single.type)) {
        logoError.value = t('invoiceDesign.logoWrongType');
        logoFile.value = null;
        return;
    }
    if (single.size > MAX_LOGO_BYTES) {
        logoError.value = t('invoiceDesign.logoTooLarge');
        logoFile.value = null;
        return;
    }
    try {
        const dataUrl = await readAsDataUrl(single);
        // The API expects the raw base64, not the "data:image/png;base64," envelope.
        form.logoBase64 = dataUrl.slice(dataUrl.indexOf(',') + 1);
        form.logoContentType = single.type;
    } catch {
        logoError.value = t('invoiceDesign.logoReadFailed');
        logoFile.value = null;
    }
}

function readAsDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result as string);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
    });
}

function removeLogo(): void {
    form.logoBase64 = null;
    form.logoContentType = null;
    logoFile.value = null;
    logoError.value = null;
}

/** Swaps the preview document and revokes the stale object URL - they leak per render otherwise. */
function setPreviewBlob(blob: Blob | null): void {
    if (previewUrl.value) {
        URL.revokeObjectURL(previewUrl.value);
    }
    previewUrl.value = blob ? URL.createObjectURL(blob) : null;
}

async function preview(): Promise<void> {
    formError.value = null;
    savedMessage.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }
    previewing.value = true;
    try {
        setPreviewBlob(await previewInvoiceDesign(toPayload()));
    } catch (error) {
        applyServerErrors(error, t('invoiceDesign.previewFailed'));
    } finally {
        previewing.value = false;
    }
}

async function save(): Promise<void> {
    formError.value = null;
    savedMessage.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }
    saving.value = true;
    try {
        applyResponse(await updateInvoiceDesign(toPayload()));
        savedMessage.value = t('invoiceDesign.saved');
    } catch (error) {
        applyServerErrors(error, t('invoiceDesign.saveFailed'));
    } finally {
        saving.value = false;
    }
}

/**
 * Binds the ProblemDetails errors dictionary to the matching fields, VehicleForm-style. The
 * update command nests the payload as its `Design` property, so keys arrive as
 * "Design.CompanyName" - the leading segment is dropped because this form's fields are flat.
 */
function applyServerErrors(error: unknown, fallback: string): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const segments = field
                    .split('.')
                    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1));
                const fieldName = (segments[0] === 'design' ? segments.slice(1) : segments).join('.');
                const message = messages.join(' ');
                if (fieldName in fieldErrors) {
                    fieldErrors[fieldName] = message;
                } else {
                    formError.value = formError.value ? `${formError.value} ${message}` : message;
                }
            }
            return;
        }
        formError.value = describeFailure(error, fallback);
        return;
    }
    formError.value = fallback;
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

<!--
    The Rechnungsdesign page: the tenant's own company data, bank details and letterhead styling
    for every rendered invoice document. Admin-only (masterData.write), like the export - the
    route guard and the nav entry enforce it. The Vorschau button renders a sample invoice with
    the CURRENT, possibly unsaved form content server-side and shows the PDF inline.
-->
<template>
    <v-container class="page-container">
        <header class="mb-4">
            <h1 class="text-h4">{{ t('invoiceDesign.title') }}</h1>
            <p class="text-medium-emphasis">{{ t('invoiceDesign.subtitle') }}</p>
        </header>

        <v-alert
            v-if="!loading && !isCustomized"
            type="info"
            variant="tonal"
            class="mb-4"
            data-testid="design-defaults-hint"
        >
            {{ t('invoiceDesign.defaultsHint') }}
        </v-alert>
        <p v-if="updatedLine" class="text-medium-emphasis audit-line" data-testid="design-updated">
            <v-icon icon="mdi-history" size="small" />
            {{ updatedLine }}
        </p>
        <p v-if="formError" class="text-error form-error" data-testid="design-form-error">{{ formError }}</p>
        <v-alert v-if="savedMessage" type="success" variant="tonal" class="mb-4" data-testid="design-save-success">
            {{ savedMessage }}
        </v-alert>

        <form novalidate @submit.prevent="save">
            <v-row>
                <v-col cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-domain" :title="t('invoiceDesign.section.company')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12">
                                    <v-text-field
                                        v-model="form.companyName"
                                        v-native-testid="'design-companyName'"
                                        :label="`${t('invoiceDesign.companyName')} *`"
                                        :error-messages="errorsFor('companyName')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-companyName-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12">
                                    <v-text-field
                                        v-model="form.street"
                                        v-native-testid="'design-street'"
                                        :label="`${t('common.street')} *`"
                                        :error-messages="errorsFor('street')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-street-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="4">
                                    <v-text-field
                                        v-model="form.postalCode"
                                        v-native-testid="'design-postalCode'"
                                        :label="`${t('common.postalCode')} *`"
                                        :error-messages="errorsFor('postalCode')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-postalCode-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="8">
                                    <v-text-field
                                        v-model="form.city"
                                        v-native-testid="'design-city'"
                                        :label="`${t('common.city')} *`"
                                        :error-messages="errorsFor('city')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-city-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12">
                                    <CountrySelect
                                        v-model="form.countryCode"
                                        :label="`${t('common.country')} *`"
                                        testid="design-countryCode"
                                        :error-messages="errorsFor('countryCode')"
                                    />
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.vatId"
                                        v-native-testid="'design-vatId'"
                                        :label="`${t('invoiceDesign.vatId')} *`"
                                        :error-messages="errorsFor('vatId')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-vatId-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.taxNumber"
                                        v-native-testid="'design-taxNumber'"
                                        :label="t('invoiceDesign.taxNumber')"
                                        :error-messages="errorsFor('taxNumber')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-taxNumber-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.email"
                                        v-native-testid="'design-email'"
                                        :label="`${t('invoiceDesign.email')} *`"
                                        type="email"
                                        :error-messages="errorsFor('email')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-email-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.phone"
                                        v-native-testid="'design-phone'"
                                        :label="t('invoiceDesign.phone')"
                                        :error-messages="errorsFor('phone')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-phone-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-bank-outline" :title="t('invoiceDesign.section.bank')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12">
                                    <v-text-field
                                        v-model="form.iban"
                                        v-native-testid="'design-iban'"
                                        :label="`${t('invoiceDesign.iban')} *`"
                                        :error-messages="errorsFor('iban')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-iban-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.bic"
                                        v-native-testid="'design-bic'"
                                        :label="t('invoiceDesign.bic')"
                                        :error-messages="errorsFor('bic')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-bic-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.bankName"
                                        v-native-testid="'design-bankName'"
                                        :label="t('invoiceDesign.bankName')"
                                        :error-messages="errorsFor('bankName')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-bankName-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat>
                        <v-card-item prepend-icon="mdi-palette-outline" :title="t('invoiceDesign.section.design')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" sm="6" md="4">
                                    <v-text-field
                                        v-model="form.accentColor"
                                        v-native-testid="'design-accentColor'"
                                        :label="`${t('invoiceDesign.accentColor')} *`"
                                        :hint="t('invoiceDesign.accentColorHint')"
                                        persistent-hint
                                        :error-messages="errorsFor('accentColor')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #append-inner>
                                            <input
                                                type="color"
                                                class="color-swatch"
                                                :value="swatchColor"
                                                :aria-label="t('invoiceDesign.accentColor')"
                                                data-testid="design-accentColor-picker"
                                                @input="onColorPicked"
                                            />
                                        </template>
                                        <template #message="{ message }">
                                            <span data-testid="design-accentColor-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6" md="4">
                                    <v-select
                                        v-model="form.logoPosition"
                                        :items="logoPositionOptions"
                                        :label="`${t('invoiceDesign.logoPosition')} *`"
                                        :error-messages="errorsFor('logoPosition')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="design-logoPosition"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-logoPosition-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                                <v-col cols="12" md="4">
                                    <p class="section-group-title">
                                        {{ t('invoiceDesign.fontSize') }}: {{ form.fontSizePoints }} pt
                                    </p>
                                    <v-slider
                                        v-model="form.fontSizePoints"
                                        :min="8"
                                        :max="14"
                                        :step="1"
                                        show-ticks="always"
                                        thumb-label
                                        density="comfortable"
                                        hide-details
                                        data-testid="design-fontSize"
                                    />
                                </v-col>
                                <v-col cols="12" md="6">
                                    <v-file-input
                                        v-model="logoFile"
                                        accept="image/png,image/jpeg"
                                        :label="t('invoiceDesign.logo')"
                                        prepend-icon="mdi-image-outline"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        :error-messages="logoError ? [logoError] : []"
                                        data-testid="design-logo-input"
                                        @update:model-value="onLogoChosen"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-logo-error">{{ message }}</span>
                                        </template>
                                    </v-file-input>
                                </v-col>
                                <v-col v-if="logoDataUrl" cols="12" md="6">
                                    <div class="logo-preview">
                                        <img
                                            :src="logoDataUrl"
                                            :alt="t('invoiceDesign.logoAlt')"
                                            data-testid="design-logo-preview"
                                        />
                                        <v-btn
                                            variant="text"
                                            color="error"
                                            prepend-icon="mdi-image-remove-outline"
                                            data-testid="design-logo-remove"
                                            @click="removeLogo"
                                        >
                                            {{ t('invoiceDesign.removeLogo') }}
                                        </v-btn>
                                    </div>
                                </v-col>
                                <v-col cols="12" md="6">
                                    <v-textarea
                                        v-model="form.headerText"
                                        v-native-testid="'design-headerText'"
                                        :label="t('invoiceDesign.headerText')"
                                        :counter="TEXT_MAX_LENGTH"
                                        rows="3"
                                        :error-messages="errorsFor('headerText')"
                                        variant="outlined"
                                        density="comfortable"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-headerText-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </v-col>
                                <v-col cols="12" md="6">
                                    <v-textarea
                                        v-model="form.footerText"
                                        v-native-testid="'design-footerText'"
                                        :label="t('invoiceDesign.footerText')"
                                        :counter="TEXT_MAX_LENGTH"
                                        rows="3"
                                        :error-messages="errorsFor('footerText')"
                                        variant="outlined"
                                        density="comfortable"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="design-footerText-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-if="previewUrl" cols="12">
                    <v-card border flat>
                        <v-card-item prepend-icon="mdi-file-eye-outline" :title="t('invoiceDesign.previewTitle')" />
                        <v-card-text>
                            <iframe
                                :src="previewUrl"
                                class="preview-frame"
                                :title="t('invoiceDesign.previewTitle')"
                                data-testid="design-preview-frame"
                            ></iframe>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn
                    variant="tonal"
                    type="button"
                    prepend-icon="mdi-file-eye-outline"
                    :loading="previewing"
                    :disabled="saving || loading"
                    data-testid="design-preview"
                    @click="preview"
                    >{{ t('invoiceDesign.preview') }}</v-btn
                >
                <v-btn
                    color="primary"
                    type="submit"
                    prepend-icon="mdi-content-save"
                    :loading="saving"
                    :disabled="previewing || loading"
                    data-testid="design-save"
                    >{{ t('common.save') }}</v-btn
                >
            </div>
        </form>
    </v-container>
</template>

<style scoped>
.color-swatch {
    width: 36px;
    height: 28px;
    padding: 0;
    border: none;
    background: none;
    cursor: pointer;
}

.logo-preview {
    display: flex;
    align-items: center;
    gap: 16px;
}

.logo-preview img {
    max-width: 240px;
    max-height: 72px;
    object-fit: contain;
    border: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
    border-radius: 4px;
    padding: 4px;
    background: #fff;
}

.preview-frame {
    width: 100%;
    height: 640px;
    border: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
    border-radius: 4px;
}
</style>
