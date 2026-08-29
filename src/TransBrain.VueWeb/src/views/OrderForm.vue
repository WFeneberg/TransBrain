<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createOrder, getOrder, updateOrder, type OrderWriteRequest } from '../api/orders';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

const route = useRoute();
const router = useRouter();

const orderId = route.params.id as string | undefined;
const isEditMode = computed(() => orderId !== undefined);

const parties = ['consignor', 'consignee'] as const;
type Party = (typeof parties)[number];

const partyLabels: Record<Party, string> = { consignor: 'Consignor', consignee: 'Consignee' };

const form = reactive({
    consignor: { name: '', street: '', postalCode: '', city: '', country: 'DE' },
    consignee: { name: '', street: '', postalCode: '', city: '', country: 'DE' },
    cargoDescription: '',
    cargoWeightKg: '',
    cargoLoadMeters: '',
    pickupFrom: '',
    pickupTo: '',
    deliveryFrom: '',
    deliveryTo: '',
});

// Keyed by the same dotted path the API's nested validator rules produce ("Consignor.Name"
// lowercased segment by segment), so a server error binds onto a field without a lookup table.
const fieldErrors = reactive<Record<string, string | null>>({
    'consignor.name': null,
    'consignor.street': null,
    'consignor.postalCode': null,
    'consignor.city': null,
    'consignor.country': null,
    'consignee.name': null,
    'consignee.street': null,
    'consignee.postalCode': null,
    'consignee.city': null,
    'consignee.country': null,
    cargoDescription: null,
    cargoWeightKg: null,
    cargoLoadMeters: null,
    pickupFrom: null,
    pickupTo: null,
    deliveryFrom: null,
    deliveryTo: null,
});

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything. Set once on the first submit attempt and never reset.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

onMounted(async () => {
    if (isEditMode.value) {
        try {
            const order = await getOrder(orderId!);
            form.consignor = { ...order.consignor };
            form.consignee = { ...order.consignee };
            form.cargoDescription = order.cargoDescription;
            form.cargoWeightKg = String(order.cargoWeightKg);
            form.cargoLoadMeters = String(order.cargoLoadMeters);
            form.pickupFrom = toLocalInput(order.pickupFrom);
            form.pickupTo = toLocalInput(order.pickupTo);
            form.deliveryFrom = toLocalInput(order.deliveryFrom);
            form.deliveryTo = toLocalInput(order.deliveryTo);
        } catch (error) {
            // A failed load is not a failed save - give it its own wording so a dispatcher
            // opening a deleted order isn't told a save just failed.
            formError.value = describeFailure(error, 'The order could not be loaded.');
        }
    }
});

function validateRequired(): boolean {
    for (const party of parties) {
        fieldErrors[`${party}.name`] = form[party].name ? null : 'This field is required.';
        fieldErrors[`${party}.street`] = form[party].street ? null : 'This field is required.';
        fieldErrors[`${party}.postalCode`] = form[party].postalCode ? null : 'This field is required.';
        fieldErrors[`${party}.city`] = form[party].city ? null : 'This field is required.';
        fieldErrors[`${party}.country`] = form[party].country ? null : 'This field is required.';
    }

    fieldErrors.cargoDescription = form.cargoDescription ? null : 'This field is required.';
    fieldErrors.cargoWeightKg = Number(form.cargoWeightKg) > 0 ? null : 'This value must be greater than zero.';
    fieldErrors.cargoLoadMeters = Number(form.cargoLoadMeters) > 0 ? null : 'This value must be greater than zero.';
    fieldErrors.pickupFrom = form.pickupFrom ? null : 'This field is required.';
    fieldErrors.pickupTo = form.pickupTo ? null : 'This field is required.';
    fieldErrors.deliveryFrom = form.deliveryFrom ? null : 'This field is required.';
    fieldErrors.deliveryTo = form.deliveryTo ? null : 'This field is required.';

    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validateRequired()) {
        return;
    }

    const request: OrderWriteRequest = {
        consignor: { ...form.consignor },
        consignee: { ...form.consignee },
        cargoDescription: form.cargoDescription,
        cargoWeightKg: Number(form.cargoWeightKg),
        cargoLoadMeters: Number(form.cargoLoadMeters),
        pickupFrom: toIso(form.pickupFrom),
        pickupTo: toIso(form.pickupTo),
        deliveryFrom: toIso(form.deliveryFrom),
        deliveryTo: toIso(form.deliveryTo),
    };

    try {
        if (isEditMode.value) {
            await updateOrder(orderId!, request);
        } else {
            await createOrder(request);
        }
        await router.push('/orders');
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push('/orders');
}

// A datetime-local input has no zone, so its value is the user's wall-clock time. Sending it
// verbatim would let the API read it as UTC and shift the window by the local offset;
// constructing a Date first makes the browser attach the local zone before serialising.
function toIso(localValue: string): string {
    return new Date(localValue).toISOString();
}

// The inverse: an ISO instant rendered back into the local wall-clock string the input expects
// ("YYYY-MM-DDTHH:mm"), so a round-trip through the edit form is lossless.
function toLocalInput(iso: string): string {
    const date = new Date(iso);
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
}

// Binding ProblemDetails' `errors` dictionary is correct here - see DriverForm.vue for the
// Phase 1 rule this supersedes. Order keys can be DOTTED: the validator's nested rules produce
// "Consignor.Name" and "Consignee.City" alongside flat keys like "CargoWeightKg", so every
// segment is lowercased individually and the result matched against fieldErrors, which is
// keyed by exactly those dotted paths.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const path = field
                    .split('.')
                    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
                    .join('.');
                const message = messages.join(' ');
                if (path in fieldErrors) {
                    fieldErrors[path] = message;
                    attemptedSubmit.value = true;
                } else {
                    // A failure keyed to something this form has no field for still needs to
                    // reach the user - fall back to the form-level message.
                    formError.value = formError.value ? `${formError.value} ${message}` : message;
                }
            }
            return;
        }

        formError.value = describeFailure(error, 'The order could not be saved.');
        return;
    }

    formError.value = 'The order could not be saved.';
}

// No `errors` dictionary means this was not a per-field validation failure but a domain-level
// one (404, 409, or a rejected domain invariant) - `detail` is the stable, human-readable
// message for all of those. `fallback` is supplied per call site (loading vs. saving).
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
        <h1>{{ isEditMode ? 'Edit order' : 'New order' }}</h1>
        <p v-if="formError" data-testid="order-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue for why - Angular's [formGroup] directive sets this
             automatically, and without the Vue equivalent a native input constraint (e.g. a
             number field's default step=1 rejecting 8.4 load meters) silently blocks the
             "submit" event before this component's handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <!-- Plain <input> elements, not <v-text-field>: Vuetify's control wraps the real
                 <input> in a root <div>, and a data-testid on the component lands on that div,
                 which makes Playwright's fill() fail. See DriverForm.vue. -->
            <fieldset v-for="party in parties" :key="party">
                <legend>{{ partyLabels[party] }}</legend>
                <div>
                    <label :for="`order-${party}-name`">Name</label>
                    <input
                        :id="`order-${party}-name`"
                        v-model="form[party].name"
                        :data-testid="`order-${party}-name`"
                    />
                </div>
                <p
                    v-if="attemptedSubmit && fieldErrors[`${party}.name`]"
                    :data-testid="`order-${party}-name-error`"
                >
                    {{ fieldErrors[`${party}.name`] }}
                </p>

                <div>
                    <label :for="`order-${party}-street`">Street</label>
                    <input
                        :id="`order-${party}-street`"
                        v-model="form[party].street"
                        :data-testid="`order-${party}-street`"
                    />
                </div>
                <p
                    v-if="attemptedSubmit && fieldErrors[`${party}.street`]"
                    :data-testid="`order-${party}-street-error`"
                >
                    {{ fieldErrors[`${party}.street`] }}
                </p>

                <div>
                    <label :for="`order-${party}-postalCode`">Postal code</label>
                    <input
                        :id="`order-${party}-postalCode`"
                        v-model="form[party].postalCode"
                        :data-testid="`order-${party}-postalCode`"
                    />
                </div>
                <p
                    v-if="attemptedSubmit && fieldErrors[`${party}.postalCode`]"
                    :data-testid="`order-${party}-postalCode-error`"
                >
                    {{ fieldErrors[`${party}.postalCode`] }}
                </p>

                <div>
                    <label :for="`order-${party}-city`">City</label>
                    <input
                        :id="`order-${party}-city`"
                        v-model="form[party].city"
                        :data-testid="`order-${party}-city`"
                    />
                </div>
                <p
                    v-if="attemptedSubmit && fieldErrors[`${party}.city`]"
                    :data-testid="`order-${party}-city-error`"
                >
                    {{ fieldErrors[`${party}.city`] }}
                </p>

                <div>
                    <label :for="`order-${party}-country`">Country</label>
                    <input
                        :id="`order-${party}-country`"
                        v-model="form[party].country"
                        :data-testid="`order-${party}-country`"
                    />
                </div>
                <p
                    v-if="attemptedSubmit && fieldErrors[`${party}.country`]"
                    :data-testid="`order-${party}-country-error`"
                >
                    {{ fieldErrors[`${party}.country`] }}
                </p>
            </fieldset>

            <fieldset>
                <legend>Cargo</legend>
                <div>
                    <label for="order-cargoDescription">Description</label>
                    <input
                        id="order-cargoDescription"
                        v-model="form.cargoDescription"
                        data-testid="order-cargoDescription"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.cargoDescription" data-testid="order-cargoDescription-error">
                    {{ fieldErrors.cargoDescription }}
                </p>

                <div>
                    <label for="order-cargoWeightKg">Weight (kg)</label>
                    <input
                        id="order-cargoWeightKg"
                        v-model="form.cargoWeightKg"
                        type="number"
                        data-testid="order-cargoWeightKg"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.cargoWeightKg" data-testid="order-cargoWeightKg-error">
                    {{ fieldErrors.cargoWeightKg }}
                </p>

                <div>
                    <label for="order-cargoLoadMeters">Load meters</label>
                    <!-- step="any" as well as the form's novalidate: the default step=1 makes a
                         native number input reject a decimal like 8.4 outright. -->
                    <input
                        id="order-cargoLoadMeters"
                        v-model="form.cargoLoadMeters"
                        type="number"
                        step="any"
                        data-testid="order-cargoLoadMeters"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.cargoLoadMeters" data-testid="order-cargoLoadMeters-error">
                    {{ fieldErrors.cargoLoadMeters }}
                </p>
            </fieldset>

            <fieldset>
                <legend>Pickup window</legend>
                <div>
                    <label for="order-pickupFrom">From</label>
                    <input
                        id="order-pickupFrom"
                        v-model="form.pickupFrom"
                        type="datetime-local"
                        data-testid="order-pickupFrom"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.pickupFrom" data-testid="order-pickupFrom-error">
                    {{ fieldErrors.pickupFrom }}
                </p>

                <div>
                    <label for="order-pickupTo">To</label>
                    <input
                        id="order-pickupTo"
                        v-model="form.pickupTo"
                        type="datetime-local"
                        data-testid="order-pickupTo"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.pickupTo" data-testid="order-pickupTo-error">
                    {{ fieldErrors.pickupTo }}
                </p>
            </fieldset>

            <fieldset>
                <legend>Delivery window</legend>
                <div>
                    <label for="order-deliveryFrom">From</label>
                    <input
                        id="order-deliveryFrom"
                        v-model="form.deliveryFrom"
                        type="datetime-local"
                        data-testid="order-deliveryFrom"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.deliveryFrom" data-testid="order-deliveryFrom-error">
                    {{ fieldErrors.deliveryFrom }}
                </p>

                <div>
                    <label for="order-deliveryTo">To</label>
                    <input
                        id="order-deliveryTo"
                        v-model="form.deliveryTo"
                        type="datetime-local"
                        data-testid="order-deliveryTo"
                    />
                </div>
                <p v-if="attemptedSubmit && fieldErrors.deliveryTo" data-testid="order-deliveryTo-error">
                    {{ fieldErrors.deliveryTo }}
                </p>
            </fieldset>

            <v-btn type="submit" data-testid="order-save">Save</v-btn>
            <v-btn type="button" data-testid="order-cancel-edit" @click="cancel">Cancel</v-btn>
        </form>
    </v-container>
</template>
