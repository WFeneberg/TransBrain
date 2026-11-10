<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    changeVehicleStatus,
    createVehicle,
    getVehicle,
    updateVehicle,
    VEHICLE_TYPES,
    type Vehicle,
    type VehicleStatus,
    type VehicleWriteRequest,
} from '../api/vehicles';
import { listDrivers, type Driver } from '../api/drivers';
import { listVehicleCatalog, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import { useVehicleLabels } from '../composables/useVehicleLabels';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

/** The API caps a page at 100 rows - see VehicleList.vue's LIST_PAGE_SIZE. */
const DRIVER_PAGE_SIZE = 100;

const auth = useAuthStore();
const { t } = useI18n();
const { typeLabel, statusLabel, statusColor, formatDateTime } = useVehicleLabels();
const route = useRoute();
const router = useRouter();

const vehicleId = route.params.id as string | undefined;
const isEditMode = computed(() => vehicleId !== undefined);

const form = reactive({
    licensePlate: '',
    isLicensePlateConfidential: false,
    type: 'Tractor',
    vehicleClass: '',
    make: '',
    model: '',
    carrierGroup: '',
    defaultDriverId: '',
    manufacturedOn: '',
    powerKw: '',
    chassisNumber: '',
    engineNumber: '',
    nextInspectionDue: '',
    lengthMm: '',
    widthMm: '',
    heightMm: '',
    loadMeters: '0',
    payloadKg: '0',
    curbWeightKg: '',
    grossWeightKg: '',
    maxFifthWheelLoadKg: '',
    notes: '',
});

type FormField = keyof typeof form;
type TextField = { [K in FormField]: (typeof form)[K] extends string ? K : never }[FormField];

interface InputField {
    key: TextField;
    label: MessageKey;
    type?: 'text' | 'number' | 'date' | 'month';
    step?: string;
    required?: boolean;
    half?: boolean;
}

const technicalFields: InputField[] = [
    { key: 'manufacturedOn', label: 'vehicles.manufacturedOn', type: 'month', half: true },
    { key: 'powerKw', label: 'vehicles.powerKw', type: 'number', half: true },
    { key: 'chassisNumber', label: 'vehicles.chassisNumber', half: true },
    { key: 'engineNumber', label: 'vehicles.engineNumber', half: true },
    { key: 'nextInspectionDue', label: 'vehicles.nextInspectionDue', type: 'date', required: true },
];

const dimensionFields: InputField[] = [
    { key: 'lengthMm', label: 'vehicles.lengthMm', type: 'number', half: true },
    { key: 'widthMm', label: 'vehicles.widthMm', type: 'number', half: true },
    { key: 'heightMm', label: 'vehicles.heightMm', type: 'number', half: true },
    { key: 'loadMeters', label: 'common.loadMeters', type: 'number', step: '0.01', required: true, half: true },
];

const weightFields: InputField[] = [
    { key: 'payloadKg', label: 'vehicles.payload', type: 'number', required: true, half: true },
    { key: 'curbWeightKg', label: 'vehicles.curbWeightKg', type: 'number', half: true },
    { key: 'grossWeightKg', label: 'vehicles.grossWeightKg', type: 'number', half: true },
    { key: 'maxFifthWheelLoadKg', label: 'vehicles.maxFifthWheelLoadKg', type: 'number', half: true },
];

const inputSections: { title: MessageKey; icon: string; fields: InputField[] }[] = [
    { title: 'vehicles.section.technical', icon: 'mdi-engine-outline', fields: technicalFields },
    { title: 'vehicles.section.dimensions', icon: 'mdi-ruler-square', fields: dimensionFields },
    { title: 'vehicles.section.weights', icon: 'mdi-weight-kilogram', fields: weightFields },
];

const OPTIONAL_POSITIVE_FIELDS: TextField[] = [
    'powerKw',
    'lengthMm',
    'widthMm',
    'heightMm',
    'curbWeightKg',
    'grossWeightKg',
    'maxFifthWheelLoadKg',
];

const fieldErrors = reactive<Record<string, string | null>>(
    Object.fromEntries(Object.keys(form).map((field) => [field, null])),
);

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything. Set once on the first submit attempt and never reset.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

const vehicle = ref<Vehicle | null>(null);
const drivers = ref<Driver[]>([]);
const vehicleClasses = ref<VehicleCatalogEntry[]>([]);
const carrierGroups = ref<VehicleCatalogEntry[]>([]);
const statusBusy = ref(false);
const decommissionDialogOpen = ref(false);

const typeOptions = computed(() => VEHICLE_TYPES.map((type) => ({ title: typeLabel(type), value: type })));

const classOptions = computed(() => catalogOptions(vehicleClasses.value, vehicle.value?.vehicleClass ?? null));
const carrierGroupOptions = computed(() => catalogOptions(carrierGroups.value, vehicle.value?.carrierGroup ?? null));

const driverOptions = computed(() => [
    { title: t('vehicles.noDefaultDriver'), value: '' },
    ...[...drivers.value]
        .sort((a, b) => a.lastName.localeCompare(b.lastName) || a.firstName.localeCompare(b.firstName))
        .map((driver) => ({ title: `${driver.lastName}, ${driver.firstName}`, value: driver.id })),
]);

const status = computed(() => vehicle.value?.status ?? null);
const canChangeStatus = computed(
    () => isEditMode.value && auth.can('masterData.write') && status.value !== null && status.value !== 'Decommissioned',
);

const modifiedLine = computed(() => {
    if (!vehicle.value?.modifiedAt) {
        return null;
    }
    return t('vehicles.modified', {
        date: formatDateTime(vehicle.value.modifiedAt),
        user: vehicle.value.modifiedBy ?? '–',
    });
});

onMounted(async () => {
    await Promise.all([loadDrivers(), loadCatalogs(), isEditMode.value ? loadVehicle() : Promise.resolve()]);
});

/**
 * Only active entries are offered for a new choice. The vehicle's own current code stays
 * selectable even when it has been deactivated since - the API accepts keeping it - and is
 * marked as inactive; a code the catalog does not know at all is shown as-is.
 */
function catalogOptions(entries: VehicleCatalogEntry[], current: string | null): { title: string; value: string }[] {
    const currentKey = current?.toLowerCase() ?? null;
    const options = entries
        .filter((entry) => entry.isActive || entry.code.toLowerCase() === currentKey)
        .map((entry) => ({
            title: entry.isActive ? entry.name : t('vehicles.inactiveOption', { name: entry.name }),
            value: entry.code,
        }));
    if (current && !entries.some((entry) => entry.code.toLowerCase() === currentKey)) {
        options.push({ title: current, value: current });
    }
    return [{ title: '–', value: '' }, ...options];
}

// Unlike the driver picker, the catalogs are not optional: without them the class and carrier
// group a vehicle already has could not even be displayed, so the failure is reported.
async function loadCatalogs(): Promise<void> {
    try {
        const [classes, groups] = await Promise.all([
            listVehicleCatalog('VehicleClass'),
            listVehicleCatalog('CarrierGroup'),
        ]);
        vehicleClasses.value = classes;
        carrierGroups.value = groups;
    } catch {
        const message = t('vehicles.catalogLoadFailed');
        formError.value = formError.value ? `${formError.value} ${message}` : message;
    }
}

// The driver picker is optional: without the list only "No default driver" is offered, and the
// rest of the form still works.
async function loadDrivers(): Promise<void> {
    try {
        drivers.value = (await listDrivers(DRIVER_PAGE_SIZE)).items;
    } catch {
        drivers.value = [];
    }
}

async function loadVehicle(): Promise<void> {
    try {
        const loaded = await getVehicle(vehicleId!);
        vehicle.value = loaded;
        form.licensePlate = loaded.licensePlate;
        form.isLicensePlateConfidential = loaded.isLicensePlateConfidential ?? false;
        form.type = loaded.type;
        form.vehicleClass = loaded.vehicleClass ?? '';
        form.make = loaded.make ?? '';
        form.model = loaded.model ?? '';
        form.carrierGroup = loaded.carrierGroup ?? '';
        form.defaultDriverId = loaded.defaultDriverId ?? '';
        form.manufacturedOn = loaded.manufacturedOn?.slice(0, 7) ?? '';
        form.powerKw = numberText(loaded.powerKw);
        form.chassisNumber = loaded.chassisNumber ?? '';
        form.engineNumber = loaded.engineNumber ?? '';
        form.nextInspectionDue = loaded.nextInspectionDue;
        form.lengthMm = numberText(loaded.lengthMm);
        form.widthMm = numberText(loaded.widthMm);
        form.heightMm = numberText(loaded.heightMm);
        form.loadMeters = String(loaded.loadMeters);
        form.payloadKg = String(loaded.payloadKg);
        form.curbWeightKg = numberText(loaded.curbWeightKg);
        form.grossWeightKg = numberText(loaded.grossWeightKg);
        form.maxFifthWheelLoadKg = numberText(loaded.maxFifthWheelLoadKg);
        form.notes = loaded.notes ?? '';
    } catch (error) {
        // A failed load is not a failed save - give it its own wording so a dispatcher
        // opening a deleted vehicle isn't told a save just failed.
        formError.value = describeFailure(error, t('vehicles.loadFailed'));
    }
}

function numberText(value: number | null | undefined): string {
    return value === null || value === undefined ? '' : String(value);
}

// v-text-field hands back whatever the input holds; String() keeps a stray number harmless.
function trimmed(value: string): string {
    return String(value ?? '').trim();
}

function optionalText(value: string): string | null {
    return trimmed(value) === '' ? null : trimmed(value);
}

function optionalNumber(value: string): number | null {
    return trimmed(value) === '' ? null : Number(trimmed(value));
}

function errorsFor(field: FormField): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function labelFor(field: InputField): string {
    return field.required ? `${t(field.label)} *` : t(field.label);
}

function validate(): boolean {
    for (const field of Object.keys(fieldErrors)) {
        fieldErrors[field] = null;
    }
    fieldErrors.licensePlate = trimmed(form.licensePlate) ? null : t('common.required');
    fieldErrors.type = form.type ? null : t('common.required');
    fieldErrors.payloadKg = trimmed(form.payloadKg) !== '' ? null : t('common.required');
    fieldErrors.loadMeters = trimmed(form.loadMeters) !== '' ? null : t('common.required');
    fieldErrors.nextInspectionDue = form.nextInspectionDue ? null : t('common.required');
    for (const field of OPTIONAL_POSITIVE_FIELDS) {
        const value = optionalNumber(form[field]);
        if (value !== null && !(value > 0)) {
            fieldErrors[field] = t('common.mustBePositive');
        }
    }
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    const request: VehicleWriteRequest = {
        licensePlate: trimmed(form.licensePlate),
        type: form.type,
        payloadKg: Number(form.payloadKg),
        loadMeters: Number(form.loadMeters),
        nextInspectionDue: form.nextInspectionDue,
        isLicensePlateConfidential: form.isLicensePlateConfidential,
        make: optionalText(form.make),
        model: optionalText(form.model),
        // <input type="month"> holds "YYYY-MM"; the API stores the first of that month.
        manufacturedOn: form.manufacturedOn ? `${form.manufacturedOn}-01` : null,
        powerKw: optionalNumber(form.powerKw),
        vehicleClass: form.vehicleClass || null,
        carrierGroup: form.carrierGroup || null,
        defaultDriverId: form.defaultDriverId || null,
        chassisNumber: optionalText(form.chassisNumber),
        engineNumber: optionalText(form.engineNumber),
        lengthMm: optionalNumber(form.lengthMm),
        widthMm: optionalNumber(form.widthMm),
        heightMm: optionalNumber(form.heightMm),
        curbWeightKg: optionalNumber(form.curbWeightKg),
        grossWeightKg: optionalNumber(form.grossWeightKg),
        maxFifthWheelLoadKg: optionalNumber(form.maxFifthWheelLoadKg),
        notes: optionalText(form.notes),
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

// A status change is its own request, independent of Save: unsaved edits in the form stay
// where they are, only the chip and the audit line follow the server's answer.
async function changeStatus(next: VehicleStatus): Promise<void> {
    formError.value = null;
    statusBusy.value = true;
    try {
        vehicle.value = await changeVehicleStatus(vehicleId!, next);
    } catch (error) {
        formError.value = describeFailure(error, t('vehicles.statusChangeFailed'));
    } finally {
        statusBusy.value = false;
    }
}

async function confirmDecommission(): Promise<void> {
    decommissionDialogOpen.value = false;
    await changeStatus('Decommissioned');
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

        formError.value = describeFailure(error, t('vehicles.saveFailed'));
        return;
    }

    formError.value = t('vehicles.saveFailed');
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
    <v-container class="vehicle-page">
        <div class="page-header">
            <div class="title-row">
                <h1 class="text-h4">{{ isEditMode ? t('vehicles.edit') : t('vehicles.new') }}</h1>
                <v-chip v-if="isEditMode && status" :color="statusColor(status)" variant="tonal" data-testid="vehicle-status">{{
                    statusLabel(status)
                }}</v-chip>
            </div>
            <div v-if="canChangeStatus" class="status-actions">
                <v-btn
                    v-if="status === 'Available'"
                    color="warning"
                    variant="tonal"
                    prepend-icon="mdi-wrench"
                    :disabled="statusBusy"
                    data-testid="vehicle-status-workshop"
                    @click="changeStatus('InWorkshop')"
                    >{{ t('vehicles.action.sendToWorkshop') }}</v-btn
                >
                <v-btn
                    v-if="status === 'InWorkshop'"
                    color="success"
                    variant="tonal"
                    prepend-icon="mdi-check-circle-outline"
                    :disabled="statusBusy"
                    data-testid="vehicle-status-available"
                    @click="changeStatus('Available')"
                    >{{ t('vehicles.action.returnToService') }}</v-btn
                >
                <v-btn
                    color="error"
                    variant="tonal"
                    prepend-icon="mdi-archive-outline"
                    :disabled="statusBusy"
                    data-testid="vehicle-status-decommission"
                    @click="decommissionDialogOpen = true"
                    >{{ t('vehicles.action.decommission') }}</v-btn
                >
            </div>
            <p v-else-if="status === 'Decommissioned'" class="text-medium-emphasis status-hint">
                <v-icon icon="mdi-information-outline" size="small" />
                {{ t('vehicles.decommissionedHint') }}
            </p>
        </div>
        <p v-if="isEditMode && modifiedLine" class="text-medium-emphasis audit-line" data-testid="vehicle-modified">
            <v-icon icon="mdi-history" size="small" />
            {{ modifiedLine }}
        </p>
        <p v-if="formError" class="text-error form-error" data-testid="vehicle-form-error">{{ formError }}</p>
        <!-- novalidate: Angular's [formGroup] directive sets this automatically so its own
             reactive-forms validation is the only validation that runs; without the Vue
             equivalent here, a plain <input type="number"> with no `step` attribute defaults
             to step=1 and the browser silently blocks the native "submit" event on a fractional
             loadMeters value (e.g. 13.6) before this component's @submit handler ever runs -
             no console error, no navigation, nothing. Verified during execution. -->
        <form novalidate @submit.prevent="save">
            <!-- v-native-testid, not data-testid, on every text field, textarea and checkbox:
                 Vuetify puts data-* attributes on the field's root <div>, and Playwright's
                 fill()/check() then fail because the resolved element is a div, not an editable
                 control. The directive tags the native <input>/<textarea> instead, the same
                 element Angular's matInput-decorated <input> carries the testid on. The
                 v-selects keep data-testid on the root: the e2e opens them by clicking it. -->
            <v-row>
                <v-col cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-truck-outline" :title="t('vehicles.section.general')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col cols="12" sm="7">
                                    <v-text-field
                                        v-model="form.licensePlate"
                                        v-native-testid="'vehicle-licensePlate'"
                                        :label="`${t('vehicles.licensePlate')} *`"
                                        :error-messages="errorsFor('licensePlate')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-licensePlate-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="5">
                                    <v-checkbox
                                        v-model="form.isLicensePlateConfidential"
                                        v-native-testid="'vehicle-isLicensePlateConfidential'"
                                        :label="t('vehicles.isLicensePlateConfidential')"
                                        :error-messages="errorsFor('isLicensePlateConfidential')"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-isLicensePlateConfidential-error">{{ message }}</span>
                                        </template>
                                    </v-checkbox>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-select
                                        v-model="form.type"
                                        :label="`${t('vehicles.vehicleType')} *`"
                                        :items="typeOptions"
                                        :error-messages="errorsFor('type')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="vehicle-type"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-type-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-select
                                        v-model="form.vehicleClass"
                                        :label="t('vehicles.vehicleClass')"
                                        :items="classOptions"
                                        :error-messages="errorsFor('vehicleClass')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="vehicle-vehicleClass"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-vehicleClass-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.make"
                                        v-native-testid="'vehicle-make'"
                                        :label="t('vehicles.make')"
                                        :error-messages="errorsFor('make')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-make-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-text-field
                                        v-model="form.model"
                                        v-native-testid="'vehicle-model'"
                                        :label="t('vehicles.model')"
                                        :error-messages="errorsFor('model')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-model-error">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-select
                                        v-model="form.carrierGroup"
                                        :label="t('vehicles.carrierGroup')"
                                        :items="carrierGroupOptions"
                                        :error-messages="errorsFor('carrierGroup')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="vehicle-carrierGroup"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-carrierGroup-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                                <v-col cols="12" sm="6">
                                    <v-select
                                        v-model="form.defaultDriverId"
                                        :label="t('vehicles.defaultDriver')"
                                        :items="driverOptions"
                                        :error-messages="errorsFor('defaultDriverId')"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        data-testid="vehicle-defaultDriverId"
                                    >
                                        <template #message="{ message }">
                                            <span data-testid="vehicle-defaultDriverId-error">{{ message }}</span>
                                        </template>
                                    </v-select>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col v-for="section in inputSections" :key="section.title" cols="12" md="6">
                    <v-card border flat class="section-card">
                        <v-card-item :prepend-icon="section.icon" :title="t(section.title)" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-for="field in section.fields" :key="field.key" cols="12" :sm="field.half ? 6 : 12">
                                    <v-text-field
                                        v-model="form[field.key]"
                                        v-native-testid="`vehicle-${field.key}`"
                                        :label="labelFor(field)"
                                        :type="field.type ?? 'text'"
                                        :step="field.step"
                                        :error-messages="errorsFor(field.key)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`vehicle-${field.key}-error`">{{ message }}</span>
                                        </template>
                                    </v-text-field>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-note-text-outline" :title="t('vehicles.section.notes')" />
                        <v-card-text>
                            <v-textarea
                                v-model="form.notes"
                                v-native-testid="'vehicle-notes'"
                                :label="t('vehicles.notes')"
                                :error-messages="errorsFor('notes')"
                                :counter="2000"
                                rows="4"
                                auto-grow
                                variant="outlined"
                                hide-details="auto"
                            >
                                <template #message="{ message }">
                                    <span data-testid="vehicle-notes-error">{{ message }}</span>
                                </template>
                            </v-textarea>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="vehicle-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="vehicle-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>

        <v-dialog v-model="decommissionDialogOpen" max-width="480">
            <v-card>
                <v-card-title>{{ t('vehicles.decommissionTitle') }}</v-card-title>
                <v-card-text>{{ t('vehicles.decommissionConfirm', { plate: vehicle?.licensePlate ?? '' }) }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="vehicle-decommission-cancel" @click="decommissionDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="vehicle-decommission-confirm" @click="confirmDecommission">{{
                        t('vehicles.action.decommission')
                    }}</v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>
    </v-container>
</template>

<style scoped>
.vehicle-page {
    max-width: 1200px;
}

.page-header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 8px;
}

.title-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
}

.status-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}

.status-hint,
.audit-line {
    display: flex;
    align-items: center;
    gap: 6px;
    margin: 0;
}

.audit-line {
    margin-bottom: 8px;
}

.form-error {
    margin: 8px 0;
}

.section-card {
    height: 100%;
}

.form-actions {
    position: sticky;
    bottom: 0;
    z-index: 1;
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
    padding: 12px 0;
    border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
    background: rgb(var(--v-theme-background));
}
</style>
