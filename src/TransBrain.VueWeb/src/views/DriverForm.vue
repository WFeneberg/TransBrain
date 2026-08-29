<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createDriver, getDriver, updateDriver, type DriverWriteRequest } from '../api/drivers';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

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
            form.firstName = driver.firstName;
            form.lastName = driver.lastName;
            form.licenseClasses = [...driver.licenseClasses];
            form.licenseValidUntil = driver.licenseValidUntil;
        } catch (error) {
            // A failed load is not a failed save - give it its own wording so a dispatcher
            // opening a deleted driver isn't told a save just failed.
            formError.value = describeFailure(error, 'The driver could not be loaded.');
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
    fieldErrors.firstName = form.firstName ? null : 'This field is required.';
    fieldErrors.lastName = form.lastName ? null : 'This field is required.';
    fieldErrors.licenseClasses = form.licenseClasses.length > 0 ? null : 'This field is required.';
    fieldErrors.licenseValidUntil = form.licenseValidUntil ? null : 'This field is required.';
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

        formError.value = describeFailure(error, 'The driver could not be saved.');
        return;
    }

    formError.value = 'The driver could not be saved.';
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
    <v-container>
        <h1>{{ isEditMode ? 'Edit driver' : 'New driver' }}</h1>
        <p v-if="formError" data-testid="driver-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue for why - Angular's [formGroup] directive sets this
             automatically, and without the Vue equivalent a native input constraint (e.g. a
             number field's default step=1) can silently block the "submit" event before this
             component's handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <!-- Plain <input> elements, not <v-text-field>: Vuetify's control wraps the real
                 <input> in a root <div>, and a data-testid on the component lands on that div
                 (verified during execution - Playwright's fill() then fails because the
                 resolved element is a div, not an editable control). Attaching the testid to
                 the native input directly keeps fill()/toHaveValue() working the same way they
                 do against Angular's matInput-decorated <input>. -->
            <div>
                <label for="driver-firstName">First name</label>
                <input id="driver-firstName" v-model="form.firstName" data-testid="driver-firstName" />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.firstName" data-testid="driver-firstName-error">
                {{ fieldErrors.firstName }}
            </p>

            <div>
                <label for="driver-lastName">Last name</label>
                <input id="driver-lastName" v-model="form.lastName" data-testid="driver-lastName" />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.lastName" data-testid="driver-lastName-error">
                {{ fieldErrors.lastName }}
            </p>

            <fieldset>
                <legend>License classes</legend>
                <label v-for="licenseClass in licenseClassOptions" :key="licenseClass">
                    <input
                        type="checkbox"
                        :checked="isLicenseClassSelected(licenseClass)"
                        :data-testid="'driver-licenseClass-' + licenseClass"
                        @change="toggleLicenseClass(licenseClass)"
                    />
                    {{ licenseClass }}
                </label>
                <p v-if="attemptedSubmit && fieldErrors.licenseClasses" data-testid="driver-licenseClasses-error">
                    {{ fieldErrors.licenseClasses }}
                </p>
            </fieldset>

            <div>
                <label for="driver-licenseValidUntil">License valid until</label>
                <input
                    id="driver-licenseValidUntil"
                    v-model="form.licenseValidUntil"
                    type="date"
                    data-testid="driver-licenseValidUntil"
                />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.licenseValidUntil" data-testid="driver-licenseValidUntil-error">
                {{ fieldErrors.licenseValidUntil }}
            </p>

            <v-btn type="submit" data-testid="driver-save">Save</v-btn>
            <v-btn type="button" data-testid="driver-cancel" @click="cancel">Cancel</v-btn>
        </form>
    </v-container>
</template>
