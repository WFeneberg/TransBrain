<script setup lang="ts">
import axios from 'axios';
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { listDrivers, type Driver } from '../api/drivers';
import { createTour } from '../api/tours';
import { listVehicles, type Vehicle } from '../api/vehicles';
import { useI18n } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

const { t } = useI18n();
const router = useRouter();

const vehicles = ref<Vehicle[]>([]);
const drivers = ref<Driver[]>([]);

const form = reactive({
    tourDate: '',
    vehicleId: '',
    driverId: '',
});

const fieldErrors = reactive<Record<string, string | null>>({
    tourDate: null,
    vehicleId: null,
    driverId: null,
});

// Gates field error rendering the same way Angular Material's default ErrorStateMatcher does
// (invalid && (touched || submitted)) - a fresh form must not show "This field is required"
// before the user has done anything.
const attemptedSubmit = ref(false);
const formError = ref<string | null>(null);

onMounted(async () => {
    try {
        vehicles.value = (await listVehicles(PICKER_PAGE_SIZE)).items.filter((vehicle) => vehicle.type === 'Tractor');
        drivers.value = (await listDrivers(PICKER_PAGE_SIZE)).items;
    } catch (error) {
        // A failed load is not a failed save - give it its own wording.
        formError.value = describeFailure(error, t('tours.formLoadFailed'));
    }
});

function validateRequired(): boolean {
    fieldErrors.tourDate = form.tourDate ? null : t('common.required');
    fieldErrors.vehicleId = form.vehicleId ? null : t('common.required');
    fieldErrors.driverId = form.driverId ? null : t('common.required');
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    formError.value = null;
    attemptedSubmit.value = true;
    if (!validateRequired()) {
        return;
    }

    try {
        const tour = await createTour({ ...form });
        // To the detail page, not the list: a dispatcher's next action after planning a tour is
        // always to put orders on it.
        await router.push(`/tours/${tour.id}`);
    } catch (error) {
        applyServerErrors(error);
    }
}

function cancel(): void {
    router.push('/tours');
}

function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
                const message = messages.join(' ');
                if (fieldName in fieldErrors) {
                    fieldErrors[fieldName] = message;
                    attemptedSubmit.value = true;
                } else {
                    formError.value = formError.value ? `${formError.value} ${message}` : message;
                }
            }
            return;
        }

        // A double booking, a vehicle in the workshop and an expired licence all arrive here as
        // a 409 whose detail names the reason - exactly what the dispatcher needs, so it is
        // shown verbatim rather than replaced by a generic sentence.
        formError.value = describeFailure(error, t('tours.saveFailed'));
        return;
    }

    formError.value = t('tours.saveFailed');
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
    <v-container>
        <h1>{{ t('tours.new') }}</h1>
        <p v-if="formError" data-testid="tour-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue - without it a native input constraint can silently
             block the submit event before this handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <div>
                <label for="tour-tourDate">{{ t('tours.tourDate') }}</label>
                <input id="tour-tourDate" v-model="form.tourDate" type="date" data-testid="tour-tourDate" />
            </div>
            <p v-if="attemptedSubmit && fieldErrors.tourDate" data-testid="tour-tourDate-error">
                {{ fieldErrors.tourDate }}
            </p>

            <div>
                <label for="tour-vehicleId">{{ t('common.vehicle') }}</label>
                <select id="tour-vehicleId" v-model="form.vehicleId" data-testid="tour-vehicleId">
                    <option value="">-</option>
                    <option v-for="vehicle in vehicles" :key="vehicle.id" :value="vehicle.id">
                        {{ vehicle.licensePlate }}
                    </option>
                </select>
            </div>
            <p v-if="attemptedSubmit && fieldErrors.vehicleId" data-testid="tour-vehicleId-error">
                {{ fieldErrors.vehicleId }}
            </p>

            <div>
                <label for="tour-driverId">{{ t('common.driver') }}</label>
                <select id="tour-driverId" v-model="form.driverId" data-testid="tour-driverId">
                    <option value="">-</option>
                    <option v-for="driver in drivers" :key="driver.id" :value="driver.id">
                        {{ driver.lastName }}, {{ driver.firstName }}
                    </option>
                </select>
            </div>
            <p v-if="attemptedSubmit && fieldErrors.driverId" data-testid="tour-driverId-error">
                {{ fieldErrors.driverId }}
            </p>

            <v-btn type="submit" data-testid="tour-save">{{ t('common.save') }}</v-btn>
            <v-btn type="button" data-testid="tour-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
        </form>
    </v-container>
</template>
