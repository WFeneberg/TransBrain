<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createVehicle, getVehicle, updateVehicle, type VehicleWriteRequest } from '../api/vehicles';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

const route = useRoute();
const router = useRouter();

const vehicleId = route.params.id as string | undefined;
const isEditMode = computed(() => vehicleId !== undefined);

const vehicleTypes = ['Tractor', 'RigidTruck', 'Van'];

const form = reactive({
    licensePlate: '',
    type: 'Tractor',
    payloadKg: '0',
    loadMeters: '0',
    nextInspectionDue: '',
});

const fieldErrors = reactive<Record<string, string | null>>({
    licensePlate: null,
    type: null,
    payloadKg: null,
    loadMeters: null,
    nextInspectionDue: null,
});

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything. Set once on the first submit attempt and never reset.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

onMounted(async () => {
    if (isEditMode.value) {
        try {
            const vehicle = await getVehicle(vehicleId!);
            form.licensePlate = vehicle.licensePlate;
            form.type = vehicle.type;
            form.payloadKg = String(vehicle.payloadKg);
            form.loadMeters = String(vehicle.loadMeters);
            form.nextInspectionDue = vehicle.nextInspectionDue;
        } catch (error) {
            // A failed load is not a failed save - give it its own wording so a dispatcher
            // opening a deleted vehicle isn't told a save just failed.
            formError.value = describeFailure(error, 'The vehicle could not be loaded.');
        }
    }
});

function validateRequired(): boolean {
    fieldErrors.licensePlate = form.licensePlate ? null : 'This field is required.';
    fieldErrors.type = form.type ? null : 'This field is required.';
    fieldErrors.payloadKg = form.payloadKg !== '' ? null : 'This field is required.';
    fieldErrors.loadMeters = form.loadMeters !== '' ? null : 'This field is required.';
    fieldErrors.nextInspectionDue = form.nextInspectionDue ? null : 'This field is required.';
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validateRequired()) {
        return;
    }

    const request: VehicleWriteRequest = {
        licensePlate: form.licensePlate,
        type: form.type,
        payloadKg: Number(form.payloadKg),
        loadMeters: Number(form.loadMeters),
        nextInspectionDue: form.nextInspectionDue,
    };

    try {
        if (isEditMode.value) {
            await updateVehicle(vehicleId!, request);
        } else {
            await createVehicle(request);
        }
        await router.push('/vehicles');
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push('/vehicles');
}

// Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed by
// error code (e.g. "Vehicle.PayloadKgNotPositive") rather than by the field name a form field
// could match against. Task 1 of the 2026-08-29 master-data-completion phase fixed the API to
// group every failure under its real field name instead, so binding it is now correct - this
// supersedes that earlier rule. The dictionary key is still the .NET property name (PascalCase,
// e.g. "LicensePlate"), while this form's fields use the camelCase names the rest of the wire
// format uses, so each key is lowercased on its first character before the field lookup.
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

        formError.value = describeFailure(error, 'The vehicle could not be saved.');
        return;
    }

    formError.value = 'The vehicle could not be saved.';
}

// No `errors` dictionary means this was not a per-field validation failure but a domain-level
// one (404 not found, 409 conflict on a duplicate plate, or a domain invariant rejected before
// any field-level validator ran) - `detail` is the stable, human-readable message for all of
// those. `fallback` is supplied per call site (loading vs. saving) - a bodyless failure (e.g. a
// policy-rejected 403 with no ProblemDetails at all, since ASP.NET's authorization middleware
// rejects it before the endpoint runs) must not report "could not be saved" for what was
// actually a failed load, or vice versa.
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
        <h1>{{ isEditMode ? 'Edit vehicle' : 'New vehicle' }}</h1>
        <p v-if="formError" data-testid="vehicle-form-error">{{ formError }}</p>
        <!-- novalidate: Angular's [formGroup] directive sets this automatically so its own
             reactive-forms validation is the only validation that runs; without the Vue
             equivalent here, a plain <input type="number"> with no `step` attribute defaults
             to step=1 and the browser silently blocks the native "submit" event on a fractional
             loadMeters value (e.g. 13.6) before this component's @submit handler ever runs -
             no console error, no navigation, nothing. Verified during execution. -->
        <form novalidate @submit.prevent="save">
            <!-- Plain <input> elements, not <v-text-field>: Vuetify's control wraps the real
                 <input> in a root <div>, and a data-testid on the component lands on that div
                 (verified during execution - Playwright's fill() then fails because the
                 resolved element is a div, not an editable control). Attaching the testid to
                 the native input directly keeps fill()/toHaveValue() working the same way they
                 do against Angular's matInput-decorated <input>. -->
            <div>
                <label for="vehicle-licensePlate">License plate</label>
                <input id="vehicle-licensePlate" v-model="form.licensePlate" data-testid="vehicle-licensePlate" />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.licensePlate" data-testid="vehicle-licensePlate-error">
                {{ fieldErrors.licensePlate }}
            </p>

            <v-select v-model="form.type" label="Type" :items="vehicleTypes" data-testid="vehicle-type" />
            <p v-if="attemptedSubmit && fieldErrors.type" data-testid="vehicle-type-error">
                {{ fieldErrors.type }}
            </p>

            <div>
                <label for="vehicle-payloadKg">Payload (kg)</label>
                <input
                    id="vehicle-payloadKg"
                    v-model="form.payloadKg"
                    type="number"
                    data-testid="vehicle-payloadKg"
                />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.payloadKg" data-testid="vehicle-payloadKg-error">
                {{ fieldErrors.payloadKg }}
            </p>

            <div>
                <label for="vehicle-loadMeters">Load meters</label>
                <input
                    id="vehicle-loadMeters"
                    v-model="form.loadMeters"
                    type="number"
                    data-testid="vehicle-loadMeters"
                />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.loadMeters" data-testid="vehicle-loadMeters-error">
                {{ fieldErrors.loadMeters }}
            </p>

            <div>
                <label for="vehicle-nextInspectionDue">Next inspection due</label>
                <input
                    id="vehicle-nextInspectionDue"
                    v-model="form.nextInspectionDue"
                    type="date"
                    data-testid="vehicle-nextInspectionDue"
                />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.nextInspectionDue" data-testid="vehicle-nextInspectionDue-error">
                {{ fieldErrors.nextInspectionDue }}
            </p>

            <v-btn type="submit" data-testid="vehicle-save">Save</v-btn>
            <v-btn type="button" data-testid="vehicle-cancel" @click="cancel">Cancel</v-btn>
        </form>
    </v-container>
</template>
