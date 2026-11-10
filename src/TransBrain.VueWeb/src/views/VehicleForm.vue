<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    changeVehicleStatus,
    createVehicle,
    getVehicle,
    MARBLE_RACK_MATERIALS,
    updateVehicle,
    VEHICLE_TYPES,
    type MarbleRackMaterial,
    type Vehicle,
    type VehicleCommercial,
    type VehicleEquipment,
    type VehicleEquipmentFlag,
    type VehicleEquipmentNumber,
    type VehicleStatus,
    type VehicleWriteRequest,
} from '../api/vehicles';
import { listDrivers, type Driver } from '../api/drivers';
import { catalogSelectOptions, listVehicleCatalog, type VehicleCatalogEntry } from '../api/vehicleCatalog';
import VehicleDocuments from '../components/VehicleDocuments.vue';
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
    dataSheet: '',
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

interface EquipmentFlagField {
    key: VehicleEquipmentFlag;
    label: MessageKey;
}

interface EquipmentNumberField {
    key: VehicleEquipmentNumber;
    label: MessageKey;
    min: number;
    max: number;
}

const equipmentFlagFields: EquipmentFlagField[] = [
    { key: 'hasPartitionWall', label: 'vehicles.equipment.hasPartitionWall' },
    { key: 'hasPalletBasket', label: 'vehicles.equipment.hasPalletBasket' },
    { key: 'hasDangerousGoodsEquipment', label: 'vehicles.equipment.hasDangerousGoodsEquipment' },
    { key: 'isDangerousGoodsApproved', label: 'vehicles.equipment.isDangerousGoodsApproved' },
    { key: 'hasLiftAxle', label: 'vehicles.equipment.hasLiftAxle' },
    { key: 'hasDoubleDeck', label: 'vehicles.equipment.hasDoubleDeck' },
    { key: 'hasDoubleDeckBeams', label: 'vehicles.equipment.hasDoubleDeckBeams' },
    { key: 'hasSmallLadder', label: 'vehicles.equipment.hasSmallLadder' },
    { key: 'hasLargeLadder', label: 'vehicles.equipment.hasLargeLadder' },
];

const equipmentNumberGroups: { title: MessageKey; fields: EquipmentNumberField[]; withMaterial?: boolean }[] = [
    {
        title: 'vehicles.equipment.group.loadingSpace',
        fields: [
            { key: 'interiorLengthMm', label: 'vehicles.equipment.interiorLengthMm', min: 1, max: 30000 },
            { key: 'interiorWidthMm', label: 'vehicles.equipment.interiorWidthMm', min: 1, max: 5000 },
            { key: 'interiorHeightMm', label: 'vehicles.equipment.interiorHeightMm', min: 1, max: 5000 },
            { key: 'tankVolumeLiters', label: 'vehicles.equipment.tankVolumeLiters', min: 1, max: 100000 },
        ],
    },
    {
        title: 'vehicles.equipment.group.loadSecuring',
        fields: [
            { key: 'supportPoleCount', label: 'vehicles.equipment.supportPoleCount', min: 0, max: 999 },
            { key: 'loadBarCount', label: 'vehicles.equipment.loadBarCount', min: 0, max: 999 },
            { key: 'lashingStrapCount', label: 'vehicles.equipment.lashingStrapCount', min: 0, max: 999 },
            { key: 'chainTensionerCount', label: 'vehicles.equipment.chainTensionerCount', min: 0, max: 999 },
            { key: 'palletCount', label: 'vehicles.equipment.palletCount', min: 0, max: 999 },
        ],
    },
    {
        title: 'vehicles.equipment.group.marbleRacks',
        fields: [{ key: 'marbleRackCount', label: 'vehicles.equipment.marbleRackCount', min: 0, max: 999 }],
        withMaterial: true,
    },
];

const equipmentNumberFields = equipmentNumberGroups.flatMap((group) => group.fields);

const equipmentFlags = reactive(
    Object.fromEntries(equipmentFlagFields.map((field) => [field.key, false])) as Record<VehicleEquipmentFlag, boolean>,
);
const equipmentNumbers = reactive(
    Object.fromEntries(equipmentNumberFields.map((field) => [field.key, ''])) as Record<VehicleEquipmentNumber, string>,
);
const marbleRackMaterial = ref<MarbleRackMaterial | ''>('');

const materialOptions = computed(() => [
    { value: '', label: t('vehicles.equipment.material.none'), testId: 'vehicle-marbleRackMaterial-none' },
    ...MARBLE_RACK_MATERIALS.map((material) => ({
        value: material,
        label: t(`vehicles.equipment.material.${material}`),
        testId: `vehicle-marbleRackMaterial-${material}`,
    })),
]);

const DATA_SHEET_MAX_LENGTH = 10000;
const POLICY_NUMBERS_MAX_LENGTH = 500;
const MIN_YEAR = 1900;

type CommercialKey = keyof VehicleCommercial;
type CommercialInputKey = Exclude<
    CommercialKey,
    'vehicleInsurer' | 'vehicleInsurancePolicyNumbers' | 'transportInsurer' | 'transportInsurancePolicyNumbers'
>;

interface CommercialField {
    key: CommercialInputKey;
    label: MessageKey;
    type: 'text' | 'number' | 'date';
    step?: string;
    suffix?: string;
    min?: number;
    max?: number;
    decimals?: boolean;
    maxLength?: number;
}

const commercialFields: CommercialField[] = [
    {
        key: 'purchasePrice',
        label: 'vehicles.commercial.purchasePrice',
        type: 'number',
        step: '0.01',
        suffix: '€',
        min: 0,
        max: 10000000,
        decimals: true,
    },
    { key: 'purchasedOn', label: 'vehicles.commercial.purchasedOn', type: 'date' },
    {
        key: 'purchaseOdometerKm',
        label: 'vehicles.commercial.purchaseOdometerKm',
        type: 'number',
        step: '1',
        min: 0,
        max: 9999999,
    },
    { key: 'firstRegisteredOn', label: 'vehicles.commercial.firstRegisteredOn', type: 'date' },
    { key: 'usefulLifeYears', label: 'vehicles.commercial.usefulLifeYears', type: 'number', step: '1', min: 1, max: 50 },
    { key: 'usefulLifeKm', label: 'vehicles.commercial.usefulLifeKm', type: 'number', step: '1', min: 1, max: 9999999 },
    { key: 'costCenter', label: 'vehicles.commercial.costCenter', type: 'text', maxLength: 30 },
];

const insuranceGroups: {
    title: MessageKey;
    insurer: 'vehicleInsurer' | 'transportInsurer';
    policyNumbers: 'vehicleInsurancePolicyNumbers' | 'transportInsurancePolicyNumbers';
}[] = [
    { title: 'vehicles.insurance.vehicle', insurer: 'vehicleInsurer', policyNumbers: 'vehicleInsurancePolicyNumbers' },
    {
        title: 'vehicles.insurance.transport',
        insurer: 'transportInsurer',
        policyNumbers: 'transportInsurancePolicyNumbers',
    },
];

const COMMERCIAL_KEYS: CommercialKey[] = [
    ...commercialFields.map((field) => field.key),
    ...insuranceGroups.flatMap((group) => [group.insurer, group.policyNumbers]),
];

const commercial = reactive(
    Object.fromEntries(COMMERCIAL_KEYS.map((key) => [key, ''])) as Record<CommercialKey, string>,
);

// Equipment and commercial errors are keyed by the dotted path the API reports them under, camel-cased.
const fieldErrors = reactive<Record<string, string | null>>(
    Object.fromEntries(
        [
            ...Object.keys(form),
            ...equipmentNumberFields.map((field) => `equipment.${field.key}`),
            'equipment.marbleRackMaterial',
            ...COMMERCIAL_KEYS.map((key) => `commercial.${key}`),
        ].map((field) => [field, null]),
    ),
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
const insuranceCompanies = ref<VehicleCatalogEntry[]>([]);
const documentTypes = ref<VehicleCatalogEntry[]>([]);
const statusBusy = ref(false);
const decommissionDialogOpen = ref(false);

const typeOptions = computed(() => VEHICLE_TYPES.map((type) => ({ title: typeLabel(type), value: type })));

const classOptions = computed(() => catalogOptions(vehicleClasses.value, vehicle.value?.vehicleClass ?? null));
const carrierGroupOptions = computed(() => catalogOptions(carrierGroups.value, vehicle.value?.carrierGroup ?? null));
const insurerOptions = computed(() => ({
    vehicleInsurer: catalogOptions(insuranceCompanies.value, vehicle.value?.commercial?.vehicleInsurer ?? null),
    transportInsurer: catalogOptions(insuranceCompanies.value, vehicle.value?.commercial?.transportInsurer ?? null),
}));

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
 * Only active entries are offered for a new choice; the vehicle's own current code stays
 * selectable even when it has been deactivated since, see catalogSelectOptions.
 */
function catalogOptions(entries: VehicleCatalogEntry[], current: string | null): { title: string; value: string }[] {
    return [
        { title: '–', value: '' },
        ...catalogSelectOptions(entries, current, (name) => t('vehicles.inactiveOption', { name })),
    ];
}

// Unlike the driver picker, the catalogs are not optional: without them the codes a vehicle
// already has could not even be displayed, so the failure is reported.
async function loadCatalogs(): Promise<void> {
    try {
        const [classes, groups, insurers, types] = await Promise.all([
            listVehicleCatalog('VehicleClass'),
            listVehicleCatalog('CarrierGroup'),
            listVehicleCatalog('InsuranceCompany'),
            listVehicleCatalog('DocumentType'),
        ]);
        vehicleClasses.value = classes;
        carrierGroups.value = groups;
        insuranceCompanies.value = insurers;
        documentTypes.value = types;
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
        form.dataSheet = loaded.dataSheet ?? '';
        loadEquipment(loaded.equipment);
        loadCommercial(loaded.commercial);
    } catch (error) {
        // A failed load is not a failed save - give it its own wording so a dispatcher
        // opening a deleted vehicle isn't told a save just failed.
        formError.value = describeFailure(error, t('vehicles.loadFailed'));
    }
}

// A response without `equipment` (or with fields missing) falls back to the new-vehicle defaults.
function loadEquipment(equipment: Partial<VehicleEquipment> | null | undefined): void {
    for (const field of equipmentFlagFields) {
        equipmentFlags[field.key] = equipment?.[field.key] ?? false;
    }
    for (const field of equipmentNumberFields) {
        equipmentNumbers[field.key] = numberText(equipment?.[field.key]);
    }
    marbleRackMaterial.value = equipment?.marbleRackMaterial ?? '';
}

function equipmentRequest(): VehicleEquipment {
    const numbers = Object.fromEntries(
        equipmentNumberFields.map((field) => [field.key, optionalNumber(equipmentNumbers[field.key])]),
    ) as Record<VehicleEquipmentNumber, number | null>;
    return { ...equipmentFlags, ...numbers, marbleRackMaterial: marbleRackMaterial.value || null };
}

// A response without `commercial` (or with fields missing) falls back to the new-vehicle defaults.
function loadCommercial(loaded: Partial<VehicleCommercial> | null | undefined): void {
    for (const key of COMMERCIAL_KEYS) {
        const value = loaded?.[key];
        commercial[key] = typeof value === 'number' ? String(value) : (value ?? '');
    }
}

function commercialRequest(): VehicleCommercial {
    return {
        purchasePrice: optionalNumber(commercial.purchasePrice),
        purchasedOn: commercial.purchasedOn || null,
        purchaseOdometerKm: optionalNumber(commercial.purchaseOdometerKm),
        firstRegisteredOn: commercial.firstRegisteredOn || null,
        usefulLifeYears: optionalNumber(commercial.usefulLifeYears),
        usefulLifeKm: optionalNumber(commercial.usefulLifeKm),
        costCenter: optionalText(commercial.costCenter),
        vehicleInsurer: commercial.vehicleInsurer || null,
        vehicleInsurancePolicyNumbers: optionalText(commercial.vehicleInsurancePolicyNumbers),
        transportInsurer: commercial.transportInsurer || null,
        transportInsurancePolicyNumbers: optionalText(commercial.transportInsurancePolicyNumbers),
    };
}

function commercialFieldError(field: CommercialField): string | null {
    const raw = commercial[field.key];
    if (field.type === 'date') {
        return raw && Number(raw.slice(0, 4)) < MIN_YEAR ? t('common.yearTooEarly') : null;
    }
    if (field.type === 'text') {
        return lengthError(trimmed(raw), field.maxLength!);
    }
    const value = optionalNumber(raw);
    return field.decimals ? decimalError(value, field.min!, field.max!) : rangeError(value, field.min!, field.max!);
}

function decimalError(value: number | null, min: number, max: number): string | null {
    if (value === null) {
        return null;
    }
    if (Number.isNaN(value)) {
        return t('common.invalid');
    }
    if (value < min) {
        return min > 0 ? t('common.mustBePositive') : t('common.mustNotBeNegative');
    }
    if (value > max) {
        return t('common.mustNotExceed', { max });
    }
    return Math.abs(value * 100 - Math.round(value * 100)) > 1e-6 ? t('common.atMostTwoDecimals') : null;
}

function lengthError(value: string, max: number): string | null {
    return value.length > max ? t('common.maxLength', { max }) : null;
}

function rangeError(value: number | null, min: number, max: number): string | null {
    if (value === null) {
        return null;
    }
    if (!Number.isInteger(value)) {
        return t('common.mustBeWholeNumber');
    }
    if (value < min) {
        return min > 0 ? t('common.mustBePositive') : t('common.mustNotBeNegative');
    }
    return value > max ? t('common.mustNotExceed', { max }) : null;
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

function errorsFor(field: string): string[] {
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
    for (const field of equipmentNumberFields) {
        fieldErrors[`equipment.${field.key}`] = rangeError(
            optionalNumber(equipmentNumbers[field.key]),
            field.min,
            field.max,
        );
    }
    for (const field of commercialFields) {
        fieldErrors[`commercial.${field.key}`] = commercialFieldError(field);
    }
    for (const group of insuranceGroups) {
        fieldErrors[`commercial.${group.policyNumbers}`] = lengthError(
            trimmed(commercial[group.policyNumbers]),
            POLICY_NUMBERS_MAX_LENGTH,
        );
    }
    fieldErrors.dataSheet = lengthError(form.dataSheet, DATA_SHEET_MAX_LENGTH);
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
        dataSheet: form.dataSheet.trim() === '' ? null : form.dataSheet,
        equipment: equipmentRequest(),
        commercial: commercialRequest(),
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
// format uses, so each key is lowercased on its first character before the field lookup. Nested
// properties come as a dotted path ("Equipment.TankVolumeLiters"), so every segment is
// lowercased that way ("equipment.tankVolumeLiters").
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const fieldName = field
                    .split('.')
                    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
                    .join('.');
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
                    <v-card border flat class="section-card" data-testid="vehicle-equipment">
                        <v-card-item prepend-icon="mdi-toolbox-outline" :title="t('vehicles.section.equipment')" />
                        <v-card-text>
                            <v-row>
                                <v-col cols="12" md="6" lg="3">
                                    <div class="equipment-group-title">{{ t('vehicles.equipment.group.body') }}</div>
                                    <v-checkbox
                                        v-for="field in equipmentFlagFields"
                                        :key="field.key"
                                        v-model="equipmentFlags[field.key]"
                                        v-native-testid="`vehicle-${field.key}`"
                                        :label="t(field.label)"
                                        density="compact"
                                        hide-details
                                    />
                                </v-col>
                                <v-col v-for="group in equipmentNumberGroups" :key="group.title" cols="12" md="6" lg="3">
                                    <div class="equipment-group-title">{{ t(group.title) }}</div>
                                    <div class="equipment-fields">
                                        <v-text-field
                                            v-for="field in group.fields"
                                            :key="field.key"
                                            v-model="equipmentNumbers[field.key]"
                                            v-native-testid="`vehicle-${field.key}`"
                                            :label="t(field.label)"
                                            type="number"
                                            step="1"
                                            :min="field.min"
                                            :max="field.max"
                                            :error-messages="errorsFor(`equipment.${field.key}`)"
                                            variant="outlined"
                                            density="comfortable"
                                            hide-details="auto"
                                        >
                                            <template #message="{ message }">
                                                <span :data-testid="`vehicle-${field.key}-error`">{{ message }}</span>
                                            </template>
                                        </v-text-field>
                                        <v-radio-group
                                            v-if="group.withMaterial"
                                            v-model="marbleRackMaterial"
                                            :label="t('vehicles.equipment.marbleRackMaterial')"
                                            :error-messages="errorsFor('equipment.marbleRackMaterial')"
                                            density="compact"
                                            hide-details="auto"
                                            data-testid="vehicle-marbleRackMaterial"
                                        >
                                            <v-radio
                                                v-for="option in materialOptions"
                                                :key="option.testId"
                                                v-native-testid="option.testId"
                                                :label="option.label"
                                                :value="option.value"
                                            />
                                            <template #message="{ message }">
                                                <span data-testid="vehicle-marbleRackMaterial-error">{{ message }}</span>
                                            </template>
                                        </v-radio-group>
                                    </div>
                                </v-col>
                            </v-row>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12" md="6">
                    <v-card border flat class="section-card" data-testid="vehicle-commercial">
                        <v-card-item prepend-icon="mdi-cash" :title="t('vehicles.section.commercial')" />
                        <v-card-text>
                            <v-row dense>
                                <v-col v-for="field in commercialFields" :key="field.key" cols="12" sm="6">
                                    <v-text-field
                                        v-model="commercial[field.key]"
                                        v-native-testid="`vehicle-${field.key}`"
                                        :label="t(field.label)"
                                        :type="field.type"
                                        :step="field.step"
                                        :min="field.min"
                                        :max="field.max"
                                        :suffix="field.suffix"
                                        :counter="field.maxLength"
                                        :error-messages="errorsFor(`commercial.${field.key}`)"
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

                <v-col cols="12" md="6">
                    <v-card border flat class="section-card" data-testid="vehicle-insurance">
                        <v-card-item prepend-icon="mdi-shield-car" :title="t('vehicles.section.insurance')" />
                        <v-card-text>
                            <div v-for="group in insuranceGroups" :key="group.insurer" class="insurance-group">
                                <div class="equipment-group-title">{{ t(group.title) }}</div>
                                <div class="equipment-fields">
                                    <v-select
                                        v-model="commercial[group.insurer]"
                                        :label="t('vehicles.insurance.company')"
                                        :items="insurerOptions[group.insurer]"
                                        :error-messages="errorsFor(`commercial.${group.insurer}`)"
                                        variant="outlined"
                                        density="comfortable"
                                        hide-details="auto"
                                        :data-testid="`vehicle-${group.insurer}`"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`vehicle-${group.insurer}-error`">{{ message }}</span>
                                        </template>
                                    </v-select>
                                    <v-textarea
                                        v-model="commercial[group.policyNumbers]"
                                        v-native-testid="`vehicle-${group.policyNumbers}`"
                                        :label="t('vehicles.insurance.policyNumbers')"
                                        :counter="POLICY_NUMBERS_MAX_LENGTH"
                                        :error-messages="errorsFor(`commercial.${group.policyNumbers}`)"
                                        rows="3"
                                        variant="outlined"
                                        hide-details="auto"
                                    >
                                        <template #message="{ message }">
                                            <span :data-testid="`vehicle-${group.policyNumbers}-error`">{{ message }}</span>
                                        </template>
                                    </v-textarea>
                                </div>
                            </div>
                        </v-card-text>
                    </v-card>
                </v-col>

                <v-col cols="12">
                    <VehicleDocuments :vehicle-id="vehicleId" :document-types="documentTypes" />
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

                <v-col cols="12">
                    <v-card border flat class="section-card" data-testid="vehicle-dataSheet-card">
                        <v-card-item prepend-icon="mdi-text-box-outline" :title="t('vehicles.section.dataSheet')" />
                        <v-card-text>
                            <v-textarea
                                v-model="form.dataSheet"
                                v-native-testid="'vehicle-dataSheet'"
                                :label="t('vehicles.dataSheet')"
                                :error-messages="errorsFor('dataSheet')"
                                :counter="DATA_SHEET_MAX_LENGTH"
                                rows="12"
                                wrap="off"
                                spellcheck="false"
                                class="data-sheet"
                                variant="outlined"
                                hide-details="auto"
                            >
                                <template #message="{ message }">
                                    <span data-testid="vehicle-dataSheet-error">{{ message }}</span>
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

.equipment-group-title {
    margin-bottom: 8px;
    font-size: 0.875rem;
    font-weight: 500;
    color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
}

.equipment-fields {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.insurance-group + .insurance-group {
    margin-top: 20px;
}

.data-sheet :deep(textarea) {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, 'Liberation Mono', monospace;
    white-space: pre;
    overflow-x: auto;
    overflow-wrap: normal;
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
