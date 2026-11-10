<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createDriver, getDriver, updateDriver, type DriverWriteRequest } from '../api/drivers';
import { statusColor } from '../composables/statusColor';
import { vNativeTestid } from '../directives/nativeTestId';
import { useI18n } from '../i18n';

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
    if (isEditMode.value) {
        try {
            const driver = await getDriver(driverId!);
            externalUserId = driver.externalUserId;
            status.value = driver.status;
            form.firstName = driver.firstName;
            form.lastName = driver.lastName;
            form.licenseClasses = [...driver.licenseClasses];
            form.licenseValidUntil = driver.licenseValidUntil;
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

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validateRequired()) {
        return;
    }

    const request: DriverWriteRequest = {
        firstName: form.firstName,
        lastName: form.lastName,
        licenseClasses: form.licenseClasses,
        licenseValidUntil: form.licenseValidUntil,
        externalUserId,
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
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
                const message = messages.join(' ');
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
</style>
