<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { listDrivers, type Driver } from '../api/drivers';
import { createTour } from '../api/tours';
import { listVehicles, type Vehicle } from '../api/vehicles';
import { vNativeTestid } from '../directives/nativeTestId';
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

const vehicleOptions = computed(() => vehicles.value.map((vehicle) => ({ title: vehicle.licensePlate, value: vehicle.id })));
const driverOptions = computed(() =>
    drivers.value.map((driver) => ({ title: `${driver.lastName}, ${driver.firstName}`, value: driver.id })),
);

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

function errorsFor(field: string): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
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
    <v-container class="page-container">
        <div class="page-header form-header">
            <div class="title-row">
                <h1 class="text-h4">{{ t('tours.new') }}</h1>
            </div>
        </div>
        <p v-if="formError" class="text-error form-error" data-testid="tour-form-error">{{ formError }}</p>
        <!-- novalidate: see VehicleForm.vue - without it a native input constraint can silently
             block the submit event before this handler ever runs. -->
        <form novalidate @submit.prevent="save">
            <!-- The date field carries its testid on the native <input> (v-native-testid, see
                 VehicleForm.vue); the v-selects keep data-testid on the root, which the e2e
                 opens by clicking it. -->
            <v-row>
                <v-col cols="12" md="4">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-calendar-outline" :title="t('tours.section.date')" />
                        <v-card-text>
                            <v-text-field
                                v-model="form.tourDate"
                                v-native-testid="'tour-tourDate'"
                                :label="`${t('tours.tourDate')} *`"
                                type="date"
                                :error-messages="errorsFor('tourDate')"
                                variant="outlined"
                                density="comfortable"
                                hide-details="auto"
                            >
                                <template #message="{ message }">
                                    <span data-testid="tour-tourDate-error">{{ message }}</span>
                                </template>
                            </v-text-field>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="4">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-truck-outline" :title="t('common.vehicle')" />
                        <v-card-text>
                            <v-select
                                :model-value="form.vehicleId || null"
                                :label="`${t('common.vehicle')} *`"
                                :items="vehicleOptions"
                                :error-messages="errorsFor('vehicleId')"
                                variant="outlined"
                                density="comfortable"
                                hide-details="auto"
                                data-testid="tour-vehicleId"
                                @update:model-value="form.vehicleId = $event ?? ''"
                            >
                                <template #message="{ message }">
                                    <span data-testid="tour-vehicleId-error">{{ message }}</span>
                                </template>
                            </v-select>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="4">
                    <v-card border flat class="section-card">
                        <v-card-item prepend-icon="mdi-account-outline" :title="t('common.driver')" />
                        <v-card-text>
                            <v-select
                                :model-value="form.driverId || null"
                                :label="`${t('common.driver')} *`"
                                :items="driverOptions"
                                :error-messages="errorsFor('driverId')"
                                variant="outlined"
                                density="comfortable"
                                hide-details="auto"
                                data-testid="tour-driverId"
                                @update:model-value="form.driverId = $event ?? ''"
                            >
                                <template #message="{ message }">
                                    <span data-testid="tour-driverId-error">{{ message }}</span>
                                </template>
                            </v-select>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <div class="form-actions">
                <v-btn type="button" variant="text" data-testid="tour-cancel" @click="cancel">{{ t('common.cancel') }}</v-btn>
                <v-btn type="submit" color="primary" prepend-icon="mdi-content-save-outline" data-testid="tour-save">{{
                    t('common.save')
                }}</v-btn>
            </div>
        </form>
    </v-container>
</template>
