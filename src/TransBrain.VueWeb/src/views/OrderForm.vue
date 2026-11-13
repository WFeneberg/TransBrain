<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { listAllCustomerLocations, type CustomerLocationOption } from '../api/customerLocations';
import {
    createOrder,
    getOrder,
    getOrderLocations,
    updateOrder,
    type OrderLocations,
    type OrderWriteRequest,
} from '../api/orders';
import { listVehicleCatalog, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import LocationMap from '../components/LocationMap.vue';
import CountrySelect from '../components/CountrySelect.vue';
import OrderCharges from '../components/OrderCharges.vue';
import OrderTolls from '../components/OrderTolls.vue';
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

const orderId = route.params.id as string | undefined;
const isEditMode = computed(() => orderId !== undefined);

const parties = ['consignor', 'consignee'] as const;
type Party = (typeof parties)[number];

const NOTES_MAX_LENGTH = 2000;

const partyLabels: Record<Party, MessageKey> = { consignor: 'orders.consignor', consignee: 'orders.consignee' };
const partyPinLabels: Record<Party, string> = { consignor: 'A', consignee: 'B' };
const partyIcons: Record<Party, string> = {
    consignor: 'mdi-account-arrow-right-outline',
    consignee: 'mdi-account-arrow-left-outline',
};

const addressFields = [
    { name: 'name', label: 'common.name', sm: 12 },
    { name: 'street', label: 'orders.street', sm: 12 },
    { name: 'postalCode', label: 'orders.postalCode', sm: 4 },
    { name: 'city', label: 'orders.city', sm: 4 },
    { name: 'country', label: 'orders.country', sm: 4 },
] as const;

const windows = [
    {
        key: 'pickup',
        icon: 'mdi-calendar-arrow-right',
        title: 'orders.pickupWindow',
        from: 'pickupFrom',
        to: 'pickupTo',
    },
    {
        key: 'delivery',
        icon: 'mdi-calendar-check-outline',
        title: 'orders.deliveryWindow',
        from: 'deliveryFrom',
        to: 'deliveryTo',
    },
] as const;

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
    consigneeLocationId: '',
    notes: '',
    confirmationNotes: '',
    requiresPartitionWall: false,
    requiresDoubleDeck: false,
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
    notes: null,
    confirmationNotes: null,
});

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything. Set once on the first submit attempt and never reset.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);
const locations = ref<OrderLocations | null>(null);
const locationsError = ref<string | null>(null);
const locationOptions = ref<CustomerLocationOption[]>([]);
const locationPickerError = ref<string | null>(null);
const tollCategories = ref<VehicleCatalogEntry[]>([]);
const units = ref<VehicleCatalogEntry[]>([]);
const catalogsError = ref<string | null>(null);

const unlocatedParties = computed(() => parties.filter((party) => locations.value?.[party] === null));

const locationItems = computed(() => [
    { title: t('orders.consigneeLocation.none'), value: '' },
    ...locationOptions.value.map((option) => ({
        title: `${option.customerName}: ${option.name}, ${option.city}`,
        value: option.id,
    })),
]);

const mapMarkers = computed(() =>
    parties.flatMap((party) => {
        const location = locations.value?.[party];
        return location
            ? [
                  {
                      latitude: location.latitude,
                      longitude: location.longitude,
                      text: partyPinLabels[party],
                      title: `${t(partyLabels[party])}: ${location.name}, ${location.address}`,
                  },
              ]
            : [];
    }),
);

onMounted(async () => {
    await loadLocationOptions();
    if (isEditMode.value) {
        await loadCatalogs();
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
            form.consigneeLocationId = order.consigneeLocationId ?? '';
            form.notes = order.notes ?? '';
            form.confirmationNotes = order.confirmationNotes ?? '';
            form.requiresPartitionWall = order.requiresPartitionWall;
            form.requiresDoubleDeck = order.requiresDoubleDeck;
        } catch (error) {
            // A failed load is not a failed save - give it its own wording so a dispatcher
            // opening a deleted order isn't told a save just failed.
            formError.value = describeFailure(error, t('orders.loadFailed'));
        }

        await loadLocations();
    }
});

// The map is an aid, not part of the order: a failure here must not block editing, so it gets
// its own error slot. 503 means the routing/geocoding service is not configured or unreachable.
async function loadLocations(): Promise<void> {
    try {
        locations.value = await getOrderLocations(orderId!);
    } catch (error) {
        locationsError.value =
            axios.isAxiosError(error) && error.response?.status === 503
                ? t('map.unavailable')
                : describeFailure(error, t('map.loadFailed'));
    }
}

// The catalogs only label the toll/charge tables; raw codes still work, so a failure here
// must not block editing the order itself.
async function loadCatalogs(): Promise<void> {
    try {
        const [tollCategoryEntries, unitEntries] = await Promise.all([
            listVehicleCatalog('TollCategory'),
            listVehicleCatalog('Unit'),
        ]);
        tollCategories.value = tollCategoryEntries;
        units.value = unitEntries;
    } catch (error) {
        catalogsError.value = t('orders.catalogsLoadFailed');
    }
}

function validateRequired(): boolean {
    for (const party of parties) {
        fieldErrors[`${party}.name`] = form[party].name ? null : t('common.required');
        fieldErrors[`${party}.street`] = form[party].street ? null : t('common.required');
        fieldErrors[`${party}.postalCode`] = form[party].postalCode ? null : t('common.required');
        fieldErrors[`${party}.city`] = form[party].city ? null : t('common.required');
        fieldErrors[`${party}.country`] = form[party].country ? null : t('common.required');
    }

    fieldErrors.cargoDescription = form.cargoDescription ? null : t('common.required');
    fieldErrors.cargoWeightKg = Number(form.cargoWeightKg) > 0 ? null : t('common.mustBePositive');
    fieldErrors.cargoLoadMeters = Number(form.cargoLoadMeters) > 0 ? null : t('common.mustBePositive');
    fieldErrors.pickupFrom = form.pickupFrom ? null : t('common.required');
    fieldErrors.pickupTo = form.pickupTo ? null : t('common.required');
    fieldErrors.deliveryFrom = form.deliveryFrom ? null : t('common.required');
    fieldErrors.deliveryTo = form.deliveryTo ? null : t('common.required');

    return Object.values(fieldErrors).every((message) => message === null);
}

async function loadLocationOptions(): Promise<void> {
    try {
        locationOptions.value = (await listAllCustomerLocations()).items;
    } catch (error) {
        locationPickerError.value = t('orders.consigneeLocation.error');
    }
}

// Picking a location only copies its address into the consignee fields. The order keeps that
// copy and the reference, so later edits to the location do not change this order.
function onLocationChange(id: string | null): void {
    form.consigneeLocationId = id ?? '';
    const option = locationOptions.value.find((o) => o.id === id);
    if (!option) {
        return;
    }
    form.consignee = {
        name: option.name,
        street: option.street,
        postalCode: option.postalCode,
        city: option.city,
        country: option.country,
    };
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
        consigneeLocationId: form.consigneeLocationId || null,
        notes: form.notes.trim() || null,
        confirmationNotes: form.confirmationNotes.trim() || null,
        requiresPartitionWall: form.requiresPartitionWall,
        requiresDoubleDeck: form.requiresDoubleDeck,
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

function errorsFor(field: string): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
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

        formError.value = describeFailure(error, t('orders.saveFailed'));
        return;
    }

    formError.value = t('orders.saveFailed');
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
    <v-container class="page-container">
        <div class="page-header form-header">
            <div class="title-row">
                <h1 class="text-h4">{{ isEditMode ? t('orders.edit') : t('orders.new') }}</h1>
            </div>
        </div>
        <p v-if="formError" class="text-error form-error" data-testid="order-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue for why - Angular's [formGroup] directive sets this
             automatically, and without the Vue equivalent a native input constraint (e.g. a
             number field's default step=1 rejecting 8.4 load meters) silently blocks the
             "submit" event before this component's handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <!-- v-native-testid, not data-testid, on every text field: Playwright's fill() needs
                 the testid on the native <input>, not on Vuetify's root <div> (see
                 VehicleForm.vue). The v-select keeps data-testid on the root. -->
            <v-row>
                <v-col v-for="party in parties" :key="party" cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item :prepend-icon="partyIcons[party]" :title="t(partyLabels[party])" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-if="party === 'consignee'" cols="12">
                                    <v-select
                                        :model-value="form.consigneeLocationId"
                                        :label="t('orders.consigneeLocation')"
                                        :items="locationItems"
                                        :error-messages="locationPickerError ? [locationPickerError] : []"
                                        prepend-inner-icon="mdi-map-marker-outline"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="order-consigneeLocation"
                                        @update:model-value="onLocationChange"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-consigneeLocation-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                                <v-col v-for="field in addressFields" :key="field.name" cols="12" :sm="field.sm">
                                    <CountrySelect
                                        v-if="field.name === 'country'"
                                        v-model="form[party].country"
                                        :label="`${t(field.label)} *`"
                                        :testid="`order-${party}-country`"
                                        :error-messages="errorsFor(`${party}.country`)"
                                    />
                                    <v-text-field
                                        v-else
                                        v-model="form[party][field.name]"
                                        v-native-testid="`order-${party}-${field.name}`"
                                        :label="`${t(field.label)} *`"
                                        :error-messages="errorsFor(`${party}.${field.name}`)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`order-${party}-${field.name}-error`">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-package-variant-closed" :title="t('orders.cargo')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" md="6">
                                    <v-text-field
                                        v-model="form.cargoDescription"
                                        v-native-testid="'order-cargoDescription'"
                                        :label="`${t('orders.description')} *`"
                                        :error-messages="errorsFor('cargoDescription')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-cargoDescription-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6" md="3">
                                    <v-text-field
                                        v-model="form.cargoWeightKg"
                                        v-native-testid="'order-cargoWeightKg'"
                                        :label="`${t('orders.weight')} *`"
                                        type="number"
                                        :error-messages="errorsFor('cargoWeightKg')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-cargoWeightKg-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6" md="3">
                                    <!-- step="any" as well as the form's novalidate: the default
                                         step=1 makes a native number input reject a decimal like
                                         8.4 outright. -->
                                    <v-text-field
                                        v-model="form.cargoLoadMeters"
                                        v-native-testid="'order-cargoLoadMeters'"
                                        :label="`${t('common.loadMeters')} *`"
                                        type="number"
                                        step="any"
                                        :error-messages="errorsFor('cargoLoadMeters')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-cargoLoadMeters-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-for="timeWindow in windows" :key="timeWindow.key" cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item :prepend-icon="timeWindow.icon" :title="t(timeWindow.title)" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form[timeWindow.from]"
                                        v-native-testid="`order-${timeWindow.from}`"
                                        :label="`${t('common.from')} *`"
                                        type="datetime-local"
                                        :error-messages="errorsFor(timeWindow.from)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`order-${timeWindow.from}-error`">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form[timeWindow.to]"
                                        v-native-testid="`order-${timeWindow.to}`"
                                        :label="`${t('common.to')} *`"
                                        type="datetime-local"
                                        :error-messages="errorsFor(timeWindow.to)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`order-${timeWindow.to}-error`">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-note-text-outline" :title="t('orders.special')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" md="6">
                                    <v-textarea
                                        v-model="form.notes"
                                        v-native-testid="'order-notes'"
                                        :label="t('orders.notes')"
                                        rows="3"
                                        :counter="NOTES_MAX_LENGTH"
                                        :maxlength="NOTES_MAX_LENGTH"
                                        :error-messages="errorsFor('notes')"
                                        variant="outlined"
                                        density="comfortable"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-notes-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </v-col>
                                <v-col cols="12" md="6">
                                    <v-textarea
                                        v-model="form.confirmationNotes"
                                        v-native-testid="'order-confirmationNotes'"
                                        :label="t('orders.confirmationNotes')"
                                        rows="3"
                                        :counter="NOTES_MAX_LENGTH"
                                        :maxlength="NOTES_MAX_LENGTH"
                                        :error-messages="errorsFor('confirmationNotes')"
                                        variant="outlined"
                                        density="comfortable"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="order-confirmationNotes-error">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </v-col>
                            </v-row>
                            <div class="flags-row">
                                <v-checkbox
                                    v-model="form.requiresPartitionWall"
                                    :label="t('orders.requiresPartitionWall')"
                                    density="comfortable"
                                    hide-details
                                    data-testid="order-requiresPartitionWall"
                                />
                                <v-checkbox
                                    v-model="form.requiresDoubleDeck"
                                    :label="t('orders.requiresDoubleDeck')"
                                    density="comfortable"
                                    hide-details
                                    data-testid="order-requiresDoubleDeck"
                                />
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-if="isEditMode" cols="12">
                    <v-card border flat class="section-card" data-testid="order-map-section">
                        <v-card-item prepend-icon="mdi-map-outline" :title="t('map.title')" />
                        <v-card-text>
                            <p v-if="locationsError" class="text-error error-line" data-testid="order-map-error">{{ locationsError }}</p>
                            <p
                                v-for="party in unlocatedParties"
                                :key="party"
                                class="text-medium-emphasis section-hint error-line"
                                data-testid="order-map-not-located"
                            >
                                <v-icon icon="mdi-map-marker-off-outline" size="small" />
                                {{ t('map.notLocated', { party: t(partyLabels[party]) }) }}
                            </p>
                            <div v-if="mapMarkers.length" class="map-frame">
                                <LocationMap :markers="mapMarkers" data-testid="order-map" />
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-if="isEditMode && orderId" cols="12">
                    <p v-if="catalogsError" class="text-error error-line" data-testid="order-catalogs-error">
                        {{ catalogsError }}
                    </p>
                    <OrderTolls :order-id="orderId" :toll-categories="tollCategories" />
                </v-col>

                <v-col v-if="isEditMode && orderId" cols="12">
                    <OrderCharges :order-id="orderId" :units="units" />
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="order-cancel-edit" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="order-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>
    </v-container>
</template>
