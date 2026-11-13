<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    createDriver,
    getDriver,
    updateDriver,
    type DriverPhoneNumber,
    type DriverRelative,
    type DriverSocialInsurance,
    type DriverSpecialDocument,
    type DriverWriteRequest,
} from '../api/drivers';
import { catalogSelectOptions, listVehicleCatalog, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import { statusColor } from '../composables/statusColor';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const driverId = route.params.id as string | undefined;
const isEditMode = computed(() => driverId !== undefined);

const licenseClassOptions = ['B', 'C1', 'C', 'CE'];

const form = reactive({
    firstName: '',
    lastName: '',
    licenseClasses: [] as string[],
    licenseValidUntil: '',
});

let externalUserId: string | null = null;
const status = ref<string | null>(null);

// The "Sozv-Daten" card. Every field is optional; empty string means "not set".
const social = reactive({
    dateOfBirth: '',
    placeOfBirth: '',
    nationality: '',
    socialInsuranceNumber: '',
    entryDate: '',
    healthInsurance: '',
    bankName: '',
    bankCode: '',
    accountNumber: '',
    bic: '',
    iban: '',
    familyDoctor: '',
    familyDoctorPhone: '',
});

type SocialField = keyof typeof social;
type SocialTextField = Exclude<SocialField, 'dateOfBirth' | 'entryDate'>;

// Mirrors DriverSocialInsuranceInfo's limits so the user sees the failure before the request.
const SOCIAL_MAX_LENGTHS: Record<SocialTextField, number> = {
    placeOfBirth: 200,
    nationality: 100,
    socialInsuranceNumber: 50,
    healthInsurance: 500,
    bankName: 200,
    bankCode: 20,
    accountNumber: 34,
    bic: 11,
    iban: 34,
    familyDoctor: 500,
    familyDoctorPhone: 50,
};

const MIN_YEAR = 1900;

// The licence document fields ("Dokumente" in the legacy app) live in the licence card.
const documents = reactive({
    licenseNumber: '',
    issuingAuthority: '',
    issueDate: '',
});

const documentsErrors = reactive<Record<keyof typeof documents, string | null>>({
    licenseNumber: null,
    issuingAuthority: null,
    issueDate: null,
});

const clothing = reactive({
    clothingSize: '',
    shoeSize: '',
    gloveSize: '',
});

const clothingErrors = reactive<Record<keyof typeof clothing, string | null>>({
    clothingSize: null,
    shoeSize: null,
    gloveSize: null,
});

type ClothingField = keyof typeof clothing;

const clothingFields: { name: ClothingField; label: MessageKey }[] = [
    { name: 'clothingSize', label: 'drivers.clothing.clothingSize' },
    { name: 'shoeSize', label: 'drivers.clothing.shoeSize' },
    { name: 'gloveSize', label: 'drivers.clothing.gloveSize' },
];

// All three driver lists share the same backend cap (Driver.MaxRelatives etc.).
const MAX_ROWS = 20;

type RelativeField = 'lastName' | 'firstName' | 'relationship' | 'dateOfBirth' | 'phone';

interface RelativeRow {
    key: number;
    lastName: string;
    firstName: string;
    relationship: string;
    dateOfBirth: string;
    phone: string;
    errors: Record<RelativeField, string | null>;
}

const relativeFields: { name: RelativeField; label: MessageKey; type: 'text' | 'date'; max: number | null; sm: number }[] = [
    { name: 'lastName', label: 'drivers.lastName', type: 'text', max: 100, sm: 4 },
    { name: 'firstName', label: 'drivers.firstName', type: 'text', max: 100, sm: 4 },
    { name: 'relationship', label: 'drivers.relatives.relationship', type: 'text', max: 100, sm: 4 },
    { name: 'dateOfBirth', label: 'drivers.social.dateOfBirth', type: 'date', max: null, sm: 6 },
    { name: 'phone', label: 'drivers.relatives.phone', type: 'text', max: 50, sm: 6 },
];

const relatives = ref<RelativeRow[]>([]);
let nextRelativeKey = 0;

function addRelative(relative?: DriverRelative): void {
    relatives.value.push({
        key: nextRelativeKey++,
        lastName: relative?.lastName ?? '',
        firstName: relative?.firstName ?? '',
        relationship: relative?.relationship ?? '',
        dateOfBirth: relative?.dateOfBirth?.slice(0, 10) ?? '',
        phone: relative?.phone ?? '',
        errors: { lastName: null, firstName: null, relationship: null, dateOfBirth: null, phone: null },
    });
}

function removeRelative(index: number): void {
    relatives.value.splice(index, 1);
}

type SpecialDocumentField = 'type' | 'lastInspectionDate' | 'validUntil' | 'comment';

interface SpecialDocumentRow {
    key: number;
    type: string;
    lastInspectionDate: string;
    validUntil: string;
    comment: string;
    errors: Record<SpecialDocumentField, string | null>;
}

const specialDocumentFields: {
    name: SpecialDocumentField;
    label: MessageKey;
    type: 'text' | 'date';
    max: number | null;
    sm: number;
}[] = [
    { name: 'type', label: 'drivers.specialDocuments.type', type: 'text', max: null, sm: 6 },
    { name: 'lastInspectionDate', label: 'drivers.specialDocuments.lastInspectionDate', type: 'date', max: null, sm: 3 },
    { name: 'validUntil', label: 'drivers.specialDocuments.validUntil', type: 'date', max: null, sm: 3 },
    { name: 'comment', label: 'drivers.specialDocuments.comment', type: 'text', max: 500, sm: 12 },
];

const specialDocuments = ref<SpecialDocumentRow[]>([]);
let nextSpecialDocumentKey = 0;

function addSpecialDocument(document?: DriverSpecialDocument): void {
    specialDocuments.value.push({
        key: nextSpecialDocumentKey++,
        type: document?.type ?? '',
        lastInspectionDate: document?.lastInspectionDate?.slice(0, 10) ?? '',
        validUntil: document?.validUntil?.slice(0, 10) ?? '',
        comment: document?.comment ?? '',
        errors: { type: null, lastInspectionDate: null, validUntil: null, comment: null },
    });
}

function removeSpecialDocument(index: number): void {
    specialDocuments.value.splice(index, 1);
}

type PhoneNumberField = 'number' | 'type' | 'comment';

interface PhoneNumberRow {
    key: number;
    number: string;
    type: string;
    comment: string;
    errors: Record<PhoneNumberField, string | null>;
}

const phoneNumberFields: { name: PhoneNumberField; label: MessageKey; max: number; sm: number }[] = [
    { name: 'type', label: 'drivers.phoneNumbers.type', max: 50, sm: 3 },
    { name: 'number', label: 'drivers.phoneNumbers.number', max: 50, sm: 4 },
    { name: 'comment', label: 'drivers.phoneNumbers.comment', max: 200, sm: 5 },
];

/** The maintainable phone types ("Fahrer-Stammdaten"), loaded from the shared catalog. */
const phoneTypes = ref<VehicleCatalogEntry[]>([]);

/** The maintainable special paper types, from the same catalog. */
const documentTypes = ref<VehicleCatalogEntry[]>([]);

/** Active entries plus the row's current code, so an inactive stored type is never lost. */
function phoneTypeOptions(row: PhoneNumberRow): { title: string; value: string }[] {
    return [
        { title: '–', value: '' },
        ...catalogSelectOptions(phoneTypes.value, row.type || null, (name) => t('vehicles.inactiveOption', { name })),
    ];
}

/** Same keep-the-stored-code rule, but without an empty option - the type is required. */
function documentTypeOptions(row: SpecialDocumentRow): { title: string; value: string }[] {
    return catalogSelectOptions(documentTypes.value, row.type || null, (name) => t('vehicles.inactiveOption', { name }));
}

const phoneNumbers = ref<PhoneNumberRow[]>([]);
let nextPhoneNumberKey = 0;

function addPhoneNumber(phoneNumber?: DriverPhoneNumber): void {
    phoneNumbers.value.push({
        key: nextPhoneNumberKey++,
        number: phoneNumber?.number ?? '',
        type: phoneNumber?.type ?? '',
        comment: phoneNumber?.comment ?? '',
        errors: { number: null, type: null, comment: null },
    });
}

function removePhoneNumber(index: number): void {
    phoneNumbers.value.splice(index, 1);
}

const socialErrors = reactive<Record<SocialField, string | null>>({
    dateOfBirth: null,
    placeOfBirth: null,
    nationality: null,
    socialInsuranceNumber: null,
    entryDate: null,
    healthInsurance: null,
    bankName: null,
    bankCode: null,
    accountNumber: null,
    bic: null,
    iban: null,
    familyDoctor: null,
    familyDoctorPhone: null,
});

const insuranceFields: { name: SocialField; label: MessageKey; type: 'text' | 'date'; sm: number }[] = [
    { name: 'dateOfBirth', label: 'drivers.social.dateOfBirth', type: 'date', sm: 6 },
    { name: 'placeOfBirth', label: 'drivers.social.placeOfBirth', type: 'text', sm: 6 },
    { name: 'nationality', label: 'drivers.social.nationality', type: 'text', sm: 6 },
    { name: 'socialInsuranceNumber', label: 'drivers.social.socialInsuranceNumber', type: 'text', sm: 6 },
    { name: 'entryDate', label: 'drivers.social.entryDate', type: 'date', sm: 6 },
];

const bankFields: { name: SocialField; label: MessageKey; sm: number }[] = [
    { name: 'bankName', label: 'drivers.social.bankName', sm: 12 },
    { name: 'bankCode', label: 'drivers.social.bankCode', sm: 6 },
    { name: 'accountNumber', label: 'drivers.social.accountNumber', sm: 6 },
    { name: 'bic', label: 'drivers.social.bic', sm: 6 },
    { name: 'iban', label: 'drivers.social.iban', sm: 6 },
];

const fieldErrors = reactive<Record<string, string | null>>({
    firstName: null,
    lastName: null,
    licenseClasses: null,
    licenseValidUntil: null,
});

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything. Set once on the first submit attempt and never reset.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

onMounted(async () => {
    try {
        [phoneTypes.value, documentTypes.value] = await Promise.all([
            listVehicleCatalog('PhoneType'),
            listVehicleCatalog('DriverDocumentType'),
        ]);
    } catch {
        formError.value = t('drivers.catalogsFailed');
    }
    if (isEditMode.value) {
        try {
            const driver = await getDriver(driverId!);
            externalUserId = driver.externalUserId;
            status.value = driver.status;
            form.firstName = driver.firstName;
            form.lastName = driver.lastName;
            form.licenseClasses = [...driver.licenseClasses];
            form.licenseValidUntil = driver.licenseValidUntil;
            const stored = driver.socialInsurance;
            social.dateOfBirth = stored.dateOfBirth?.slice(0, 10) ?? '';
            social.entryDate = stored.entryDate?.slice(0, 10) ?? '';
            for (const field of Object.keys(SOCIAL_MAX_LENGTHS) as SocialTextField[]) {
                social[field] = stored[field] ?? '';
            }
            documents.licenseNumber = driver.documents?.licenseNumber ?? '';
            documents.issuingAuthority = driver.documents?.issuingAuthority ?? '';
            documents.issueDate = driver.documents?.issueDate?.slice(0, 10) ?? '';
            clothing.clothingSize = driver.clothingSizes?.clothingSize ?? '';
            clothing.shoeSize = driver.clothingSizes?.shoeSize ?? '';
            clothing.gloveSize = driver.clothingSizes?.gloveSize ?? '';
            relatives.value = [];
            (driver.relatives ?? []).forEach((relative) => addRelative(relative));
            specialDocuments.value = [];
            (driver.specialDocuments ?? []).forEach((document) => addSpecialDocument(document));
            phoneNumbers.value = [];
            (driver.phoneNumbers ?? []).forEach((phoneNumber) => addPhoneNumber(phoneNumber));
        } catch (error) {
            // A failed load is not a failed save - give it its own wording so a dispatcher
            // opening a deleted driver isn't told a save just failed.
            formError.value = describeFailure(error, t('drivers.loadFailed'));
        }
    }
});

function isLicenseClassSelected(licenseClass: string): boolean {
    return form.licenseClasses.includes(licenseClass);
}

function toggleLicenseClass(licenseClass: string): void {
    form.licenseClasses = isLicenseClassSelected(licenseClass)
        ? form.licenseClasses.filter((c) => c !== licenseClass)
        : [...form.licenseClasses, licenseClass];
}

function validateRequired(): boolean {
    fieldErrors.firstName = form.firstName ? null : t('common.required');
    fieldErrors.lastName = form.lastName ? null : t('common.required');
    fieldErrors.licenseClasses = form.licenseClasses.length > 0 ? null : t('common.required');
    fieldErrors.licenseValidUntil = form.licenseValidUntil ? null : t('common.required');
    return Object.values(fieldErrors).every((message) => message === null);
}

function validateSocial(): boolean {
    for (const [field, max] of Object.entries(SOCIAL_MAX_LENGTHS) as [SocialTextField, number][]) {
        socialErrors[field] = social[field].trim().length > max ? t('vehicleCatalog.maxLength', { max }) : null;
    }
    socialErrors.dateOfBirth = dateTooEarlyError(social.dateOfBirth);
    socialErrors.entryDate = dateTooEarlyError(social.entryDate);
    return Object.values(socialErrors).every((message) => message === null);
}

function dateTooEarlyError(value: string): string | null {
    return !value || Number(value.slice(0, 4)) >= MIN_YEAR ? null : t('drivers.social.dateTooEarly', { year: MIN_YEAR });
}

function maxLengthError(value: string, max: number): string | null {
    return value.trim().length > max ? t('vehicleCatalog.maxLength', { max }) : null;
}

function validateDocuments(): boolean {
    documentsErrors.licenseNumber = maxLengthError(documents.licenseNumber, 50);
    documentsErrors.issuingAuthority = maxLengthError(documents.issuingAuthority, 500);
    documentsErrors.issueDate = dateTooEarlyError(documents.issueDate);
    return Object.values(documentsErrors).every((message) => message === null);
}

function validateClothing(): boolean {
    clothingErrors.clothingSize = maxLengthError(clothing.clothingSize, 50);
    clothingErrors.shoeSize = maxLengthError(clothing.shoeSize, 50);
    clothingErrors.gloveSize = maxLengthError(clothing.gloveSize, 50);
    return Object.values(clothingErrors).every((message) => message === null);
}

function validateRelatives(): boolean {
    let valid = true;
    for (const row of relatives.value) {
        row.errors.lastName = row.lastName.trim() ? maxLengthError(row.lastName, 100) : t('common.required');
        row.errors.firstName = maxLengthError(row.firstName, 100);
        row.errors.relationship = maxLengthError(row.relationship, 100);
        row.errors.dateOfBirth = dateTooEarlyError(row.dateOfBirth);
        row.errors.phone = maxLengthError(row.phone, 50);
        valid = valid && Object.values(row.errors).every((message) => message === null);
    }
    return valid;
}

function validateSpecialDocuments(): boolean {
    let valid = true;
    for (const row of specialDocuments.value) {
        row.errors.type = row.type.trim() ? null : t('common.required');
        row.errors.lastInspectionDate = dateTooEarlyError(row.lastInspectionDate);
        row.errors.validUntil = dateTooEarlyError(row.validUntil);
        row.errors.comment = maxLengthError(row.comment, 500);
        valid = valid && Object.values(row.errors).every((message) => message === null);
    }
    return valid;
}

function validatePhoneNumbers(): boolean {
    let valid = true;
    for (const row of phoneNumbers.value) {
        row.errors.number = row.number.trim() ? maxLengthError(row.number, 50) : t('common.required');
        row.errors.type = maxLengthError(row.type, 50);
        row.errors.comment = maxLengthError(row.comment, 200);
        valid = valid && Object.values(row.errors).every((message) => message === null);
    }
    return valid;
}

function optionalText(value: string): string | null {
    const trimmed = value.trim();
    return trimmed ? trimmed : null;
}

function socialRequest(): DriverSocialInsurance {
    return {
        dateOfBirth: social.dateOfBirth || null,
        placeOfBirth: optionalText(social.placeOfBirth),
        nationality: optionalText(social.nationality),
        socialInsuranceNumber: optionalText(social.socialInsuranceNumber),
        entryDate: social.entryDate || null,
        healthInsurance: optionalText(social.healthInsurance),
        bankName: optionalText(social.bankName),
        bankCode: optionalText(social.bankCode),
        accountNumber: optionalText(social.accountNumber),
        bic: optionalText(social.bic)?.toUpperCase() ?? null,
        iban: optionalText(social.iban)?.toUpperCase() ?? null,
        familyDoctor: optionalText(social.familyDoctor),
        familyDoctorPhone: optionalText(social.familyDoctorPhone),
    };
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    // Run every validator unconditionally so every invalid field shows its message at once.
    const results = [
        validateRequired(),
        validateSocial(),
        validateDocuments(),
        validateClothing(),
        validateRelatives(),
        validateSpecialDocuments(),
        validatePhoneNumbers(),
    ];
    if (results.includes(false)) {
        return;
    }

    const request: DriverWriteRequest = {
        firstName: form.firstName,
        lastName: form.lastName,
        licenseClasses: form.licenseClasses,
        licenseValidUntil: form.licenseValidUntil,
        externalUserId,
        socialInsurance: socialRequest(),
        documents: {
            licenseNumber: optionalText(documents.licenseNumber),
            issuingAuthority: optionalText(documents.issuingAuthority),
            issueDate: documents.issueDate || null,
        },
        clothingSizes: {
            clothingSize: optionalText(clothing.clothingSize),
            shoeSize: optionalText(clothing.shoeSize),
            gloveSize: optionalText(clothing.gloveSize),
        },
        relatives: relatives.value.map((row) => ({
            lastName: row.lastName.trim(),
            firstName: optionalText(row.firstName),
            relationship: optionalText(row.relationship),
            dateOfBirth: row.dateOfBirth || null,
            phone: optionalText(row.phone),
        })),
        specialDocuments: specialDocuments.value.map((row) => ({
            type: row.type.trim(),
            lastInspectionDate: row.lastInspectionDate || null,
            validUntil: row.validUntil || null,
            comment: optionalText(row.comment),
        })),
        phoneNumbers: phoneNumbers.value.map((row) => ({
            number: row.number.trim(),
            type: optionalText(row.type),
            comment: optionalText(row.comment),
        })),
    };

    try {
        if (isEditMode.value) {
            await updateDriver(driverId!, request);
        } else {
            await createDriver(request);
        }
        await router.push('/drivers');
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push('/drivers');
}

function errorsFor(field: string): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function errorsForSocial(field: SocialField): string[] {
    const message = socialErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function errorsForDocuments(field: keyof typeof documents): string[] {
    const message = documentsErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function errorsForClothing(field: ClothingField): string[] {
    const message = clothingErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function errorsForRow<TField extends string>(row: { errors: Record<TField, string | null> }, field: TField): string[] {
    const message = row.errors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

// "Relatives[1].LastName" from the server marks the field of that list row.
function applyRowError(field: string, message: string): boolean {
    const match = /^(Relatives|SpecialDocuments|PhoneNumbers)\[(\d+)\]\.(\w+)$/.exec(field);
    if (!match) {
        return false;
    }
    const index = Number(match[2]);
    const property = match[3].charAt(0).toLowerCase() + match[3].slice(1);
    const row: { errors: Record<string, string | null> } | undefined =
        match[1] === 'Relatives' ? relatives.value[index]
        : match[1] === 'SpecialDocuments' ? specialDocuments.value[index]
        : phoneNumbers.value[index];
    if (!row || !(property in row.errors)) {
        return false;
    }
    row.errors[property] = message;
    return true;
}

// Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed by
// error code rather than by field name. Task 1 of the 2026-08-29 master-data-completion phase
// fixed the API to group every failure under its real field name instead, so binding it is now
// correct - this supersedes that earlier rule. The dictionary key is still the .NET property
// name (PascalCase, e.g. "LicenseValidUntil"), while this form's fields use the camelCase names
// the rest of the wire format uses, so each key is lowercased on its first character before the
// field lookup.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const message = messages.join(' ');
                // The nested value groups come as dotted paths ("SocialInsurance.Iban",
                // "Documents.IssueDate"), the lists as indexed ones ("Relatives[1].LastName").
                if (field.startsWith('SocialInsurance.')) {
                    const nested = field.slice('SocialInsurance.'.length);
                    const nestedName = nested.charAt(0).toLowerCase() + nested.slice(1);
                    if (nestedName in socialErrors) {
                        socialErrors[nestedName as SocialField] = message;
                        continue;
                    }
                }
                if (field.startsWith('Documents.')) {
                    const nested = field.slice('Documents.'.length);
                    const nestedName = nested.charAt(0).toLowerCase() + nested.slice(1);
                    if (nestedName in documentsErrors) {
                        documentsErrors[nestedName as keyof typeof documents] = message;
                        continue;
                    }
                }
                if (field.startsWith('ClothingSizes.')) {
                    const nested = field.slice('ClothingSizes.'.length);
                    const nestedName = nested.charAt(0).toLowerCase() + nested.slice(1);
                    if (nestedName in clothingErrors) {
                        clothingErrors[nestedName as ClothingField] = message;
                        continue;
                    }
                }
                if (applyRowError(field, message)) {
                    continue;
                }
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
                if (fieldName in fieldErrors) {
                    fieldErrors[fieldName] = message;
                } else {
                    // A failure keyed to something this form has no field for still needs to
                    // reach the user - fall back to the form-level message.
                    formError.value = formError.value ? `${formError.value} ${message}` : message;
                }
            }
            return;
        }

        formError.value = describeFailure(error, t('drivers.saveFailed'));
        return;
    }

    formError.value = t('drivers.saveFailed');
}

// No `errors` dictionary means this was not a per-field validation failure but a domain-level
// one (404, 409, or a rejected domain invariant) - `detail` is the stable, human-readable
// message for all of those. `fallback` is supplied per call site (loading vs. saving) - a
// bodyless failure (e.g. a policy-rejected 403 with no ProblemDetails at all, since ASP.NET's
// authorization middleware rejects it before the endpoint runs) must not report "could not be
// saved" for what was actually a failed load, or vice versa.
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
                <h1 class="text-h4">{{ isEditMode ? t('drivers.edit') : t('drivers.new') }}</h1>
                <v-chip v-if="isEditMode && status" :color="statusColor(status)" variant="tonal" data-testid="driver-status">{{
                    status
                }}</v-chip>
            </div>
        </div>
        <p v-if="formError" class="text-error form-error" data-testid="driver-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue for why - Angular's [formGroup] directive sets this
             automatically, and without the Vue equivalent a native input constraint (e.g. a
             number field's default step=1) can silently block the "submit" event before this
             component's handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <!-- v-native-testid, not data-testid: see VehicleForm.vue - Playwright's fill() and
                 click() need the testid on the native <input>, not on Vuetify's root <div>. -->
            <v-row>
                <v-col cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-account-outline" :title="t('drivers.section.personal')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.firstName"
                                        v-native-testid="'driver-firstName'"
                                        :label="`${t('drivers.firstName')} *`"
                                        :error-messages="errorsFor('firstName')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="driver-firstName-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.lastName"
                                        v-native-testid="'driver-lastName'"
                                        :label="`${t('drivers.lastName')} *`"
                                        :error-messages="errorsFor('lastName')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="driver-lastName-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-card-account-details-outline" :title="t('drivers.section.license')" />
                        <v-card-text>
                            <div class="section-group-title">{{ t('drivers.licenseClasses') }} *</div>
                            <v-input :error-messages="errorsFor('licenseClasses')" hide-details="auto">
                                <div class="license-classes">
                                    <v-checkbox
                                        v-for="licenseClass in licenseClassOptions"
                                        :key="licenseClass"
                                        v-native-testid="'driver-licenseClass-' + licenseClass"
                                        :model-value="isLicenseClassSelected(licenseClass)"
                                        :label="licenseClass"
                                        :error="errorsFor('licenseClasses').length > 0"
                                        density="comfortable"
                                        hide-details
                                        @update:model-value="toggleLicenseClass(licenseClass)"
                                    />
                                </div>
                                <template #message="{ message }">
                                    <span data-testid="driver-licenseClasses-error">{{ message }}</span>
                                </template>
                            </v-input>
                            <v-text-field
                                v-model="form.licenseValidUntil"
                                v-native-testid="'driver-licenseValidUntil'"
                                class="license-valid-until"
                                :label="`${t('drivers.licenseValidUntil')} *`"
                                type="date"
                                :error-messages="errorsFor('licenseValidUntil')"
                                variant="outlined"
                                density="comfortable"
                                hide-details="auto"
                            >
                                <template #message="{ message }">
                                    <span data-testid="driver-licenseValidUntil-error">{{ message }}</span>
                                </template>
                            </v-text-field>
                            <v-row dense class="mt-2">
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="documents.licenseNumber"
                                        v-native-testid="'driver-licenseNumber'"
                                        :label="t('drivers.documents.licenseNumber')"
                                        :error-messages="errorsForDocuments('licenseNumber')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="driver-licenseNumber-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="documents.issueDate"
                                        v-native-testid="'driver-issueDate'"
                                        :label="t('drivers.documents.issueDate')"
                                        type="date"
                                        :error-messages="errorsForDocuments('issueDate')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="driver-issueDate-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12">
                                    <v-textarea
                                        v-model="documents.issuingAuthority"
                                        v-native-testid="'driver-issuingAuthority'"
                                        :label="t('drivers.documents.issuingAuthority')"
                                        :counter="500"
                                        :error-messages="errorsForDocuments('issuingAuthority')"
                                        rows="3"
                                        auto-grow
                                        variant="outlined"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="driver-issuingAuthority-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12" md="6">
                    <v-card border flat class="section-card" data-testid="driver-clothingSizes">
                        <v-card-item prepend-icon="mdi-tshirt-crew-outline" :title="t('drivers.section.clothing')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-for="field in clothingFields" :key="field.name" cols="12" sm="4">
                                    <v-text-field
                                        v-model="clothing[field.name]"
                                        v-native-testid="'driver-' + field.name"
                                        :label="t(field.label)"
                                        :error-messages="errorsForClothing(field.name)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="'driver-' + field.name + '-error'">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="driver-socialInsurance">
                        <v-card-item prepend-icon="mdi-shield-account-outline" :title="t('drivers.section.socialInsurance')" />
                        <v-card-text>
                            <v-row>
                                <v-col cols="12" md="6">
                                    <div class="section-group-title">{{ t('drivers.social.group.insurance') }}</div>
                                    <v-row dense>
                                        <v-col v-for="field in insuranceFields" :key="field.name" cols="12" :sm="field.sm">
                                            <v-text-field
                                                v-model="social[field.name]"
                                                v-native-testid="'driver-' + field.name"
                                                :label="t(field.label)"
                                                :type="field.type"
                                                :error-messages="errorsForSocial(field.name)"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span :data-testid="'driver-' + field.name + '-error'">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                        <v-col cols="12">
                                            <v-textarea
                                                v-model="social.healthInsurance"
                                                v-native-testid="'driver-healthInsurance'"
                                                :label="t('drivers.social.healthInsurance')"
                                                :counter="SOCIAL_MAX_LENGTHS.healthInsurance"
                                                :error-messages="errorsForSocial('healthInsurance')"
                                                rows="3"
                                                auto-grow
                                                variant="outlined"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span data-testid="driver-healthInsurance-error">{{ message }}</span>
                                                </template>
                                            </v-textarea>
                                        </v-col>
                                    </v-row>
                                </v-col>
                                <v-col cols="12" md="6">
                                    <div class="section-group-title">{{ t('drivers.social.group.bank') }}</div>
                                    <v-row dense>
                                        <v-col v-for="field in bankFields" :key="field.name" cols="12" :sm="field.sm">
                                            <v-text-field
                                                v-model="social[field.name]"
                                                v-native-testid="'driver-' + field.name"
                                                :label="t(field.label)"
                                                :error-messages="errorsForSocial(field.name)"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span :data-testid="'driver-' + field.name + '-error'">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                    </v-row>
                                </v-col>
                                <v-col cols="12">
                                    <div class="section-group-title">{{ t('drivers.social.group.familyDoctor') }}</div>
                                    <v-row dense>
                                        <v-col cols="12" md="8">
                                            <v-textarea
                                                v-model="social.familyDoctor"
                                                v-native-testid="'driver-familyDoctor'"
                                                :label="t('drivers.social.familyDoctor')"
                                                :counter="SOCIAL_MAX_LENGTHS.familyDoctor"
                                                :error-messages="errorsForSocial('familyDoctor')"
                                                rows="3"
                                                auto-grow
                                                variant="outlined"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span data-testid="driver-familyDoctor-error">{{ message }}</span>
                                                </template>
                                            </v-textarea>
                                        </v-col>
                                        <v-col cols="12" md="4">
                                            <v-text-field
                                                v-model="social.familyDoctorPhone"
                                                v-native-testid="'driver-familyDoctorPhone'"
                                                :label="t('drivers.social.familyDoctorPhone')"
                                                :error-messages="errorsForSocial('familyDoctorPhone')"
                                                variant="outlined"
                                                density="comfortable"
                                                hide-details="auto"
                                            >
                                                <template #message="{ message }">
                                                    <span data-testid="driver-familyDoctorPhone-error">{{ message }}</span>
                                                </template>
                                            </v-text-field>
                                        </v-col>
                                    </v-row>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="driver-relatives-section">
                        <v-card-item prepend-icon="mdi-account-group-outline" :title="t('drivers.section.relatives')" />
                        <v-card-text>
                            <p v-if="relatives.length === 0" class="text-medium-emphasis mb-3" data-testid="driver-relatives-empty">
                                {{ t('drivers.relatives.none') }}
                            </p>
                            <div v-for="(row, index) in relatives" :key="row.key" class="list-row" data-testid="driver-relative-row">
                                <v-row dense>
                                    <v-col v-for="field in relativeFields" :key="field.name" cols="12" :sm="field.sm">
                                        <v-text-field
                                            v-model="row[field.name]"
                                            v-native-testid="'driver-relative-' + field.name"
                                            :label="field.name === 'lastName' ? `${t(field.label)} *` : t(field.label)"
                                            :type="field.type"
                                            :error-messages="errorsForRow(row, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                        >
                                            <template #message="{ message }">
                                                <span :data-testid="'driver-relative-' + field.name + '-error'">{{ message }}</span>
                                            </template>
                                            <template v-if="field.name === 'lastName'" #append>
                                                <v-btn
                                                    icon="mdi-delete-outline"
                                                    variant="text"
                                                    color="error"
                                                    density="comfortable"
                                                    data-testid="driver-relative-remove"
                                                    :aria-label="t('drivers.relatives.remove')"
                                                    @click="removeRelative(index)"
                                                />
                                            </template>
                                        </v-text-field>
                                    </v-col>
                                </v-row>
                            </div>
                            <v-btn
                                variant="tonal"
                                prepend-icon="mdi-account-plus-outline"
                                data-testid="driver-relative-add"
                                :disabled="relatives.length >= MAX_ROWS"
                                @click="addRelative()"
                                >{{ t('drivers.relatives.add') }}</v-btn
                            >
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="driver-specialDocuments-section">
                        <v-card-item prepend-icon="mdi-file-certificate-outline" :title="t('drivers.section.specialDocuments')" />
                        <v-card-text>
                            <p
                                v-if="specialDocuments.length === 0"
                                class="text-medium-emphasis mb-3"
                                data-testid="driver-specialDocuments-empty"
                            >
                                {{ t('drivers.specialDocuments.none') }}
                            </p>
                            <div
                                v-for="(row, index) in specialDocuments"
                                :key="row.key"
                                class="list-row"
                                data-testid="driver-specialDocument-row"
                            >
                                <v-row dense>
                                    <v-col v-for="field in specialDocumentFields" :key="field.name" cols="12" :sm="field.sm">
                                        <!-- The type comes from the maintainable driver catalog; the v-select keeps
                                             data-testid on the root (see OrderForm.vue). -->
                                        <v-select
                                            v-if="field.name === 'type'"
                                            v-model="row.type"
                                            :items="documentTypeOptions(row)"
                                            :label="`${t(field.label)} *`"
                                            :error-messages="errorsForRow(row, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                            data-testid="driver-specialDocument-type"
                                        >
                                            <template #message="{ message }">
                                                <span data-testid="driver-specialDocument-type-error">{{ message }}</span>
                                            </template>
                                            <template #append>
                                                <v-btn
                                                    icon="mdi-delete-outline"
                                                    variant="text"
                                                    color="error"
                                                    density="comfortable"
                                                    data-testid="driver-specialDocument-remove"
                                                    :aria-label="t('drivers.specialDocuments.remove')"
                                                    @click="removeSpecialDocument(index)"
                                                />
                                            </template>
                                        </v-select>
                                        <v-text-field
                                            v-else
                                            v-model="row[field.name]"
                                            v-native-testid="'driver-specialDocument-' + field.name"
                                            :label="t(field.label)"
                                            :type="field.type"
                                            :error-messages="errorsForRow(row, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                        >
                                            <template #message="{ message }">
                                                <span :data-testid="'driver-specialDocument-' + field.name + '-error'">{{
                                                    message
                                                }}</span>
                                            </template>
                                        </v-text-field>
                                    </v-col>
                                </v-row>
                            </div>
                            <v-btn
                                variant="tonal"
                                prepend-icon="mdi-file-plus-outline"
                                data-testid="driver-specialDocument-add"
                                :disabled="specialDocuments.length >= MAX_ROWS"
                                @click="addSpecialDocument()"
                                >{{ t('drivers.specialDocuments.add') }}</v-btn
                            >
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="driver-phoneNumbers-section">
                        <v-card-item prepend-icon="mdi-phone-outline" :title="t('drivers.section.phoneNumbers')" />
                        <v-card-text>
                            <p
                                v-if="phoneNumbers.length === 0"
                                class="text-medium-emphasis mb-3"
                                data-testid="driver-phoneNumbers-empty"
                            >
                                {{ t('drivers.phoneNumbers.none') }}
                            </p>
                            <div
                                v-for="(row, index) in phoneNumbers"
                                :key="row.key"
                                class="list-row"
                                data-testid="driver-phoneNumber-row"
                            >
                                <v-row dense>
                                    <v-col v-for="field in phoneNumberFields" :key="field.name" cols="12" :sm="field.sm">
                                        <!-- The type comes from the maintainable driver catalog; the v-select keeps
                                             data-testid on the root (see OrderForm.vue). -->
                                        <v-select
                                            v-if="field.name === 'type'"
                                            v-model="row.type"
                                            :items="phoneTypeOptions(row)"
                                            :label="t(field.label)"
                                            :error-messages="errorsForRow(row, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                            data-testid="driver-phoneNumber-type"
                                        >
                                            <template #message="{ message }">
                                                <span data-testid="driver-phoneNumber-type-error">{{ message }}</span>
                                            </template>
                                        </v-select>
                                        <v-text-field
                                            v-else
                                            v-model="row[field.name]"
                                            v-native-testid="'driver-phoneNumber-' + field.name"
                                            :label="field.name === 'number' ? `${t(field.label)} *` : t(field.label)"
                                            :error-messages="errorsForRow(row, field.name)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                        >
                                            <template #message="{ message }">
                                                <span :data-testid="'driver-phoneNumber-' + field.name + '-error'">{{ message }}</span>
                                            </template>
                                            <template v-if="field.name === 'number'" #append>
                                                <v-btn
                                                    icon="mdi-delete-outline"
                                                    variant="text"
                                                    color="error"
                                                    density="comfortable"
                                                    data-testid="driver-phoneNumber-remove"
                                                    :aria-label="t('drivers.phoneNumbers.remove')"
                                                    @click="removePhoneNumber(index)"
                                                />
                                            </template>
                                        </v-text-field>
                                    </v-col>
                                </v-row>
                            </div>
                            <v-btn
                                variant="tonal"
                                prepend-icon="mdi-phone-plus-outline"
                                data-testid="driver-phoneNumber-add"
                                :disabled="phoneNumbers.length >= MAX_ROWS"
                                @click="addPhoneNumber()"
                                >{{ t('drivers.phoneNumbers.add') }}</v-btn
                            >
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="driver-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="driver-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>
    </v-container>
</template>

<style scoped>
.license-classes {
    display: flex;
    flex-wrap: wrap;
    column-gap: 16px;
}

.license-valid-until {
    margin-top: 16px;
}

.list-row + .list-row {
    margin-top: 8px;
}

.list-row {
    margin-bottom: 12px;
}
</style>
