<script setup lang="ts">
import axios from 'axios';
import { onMounted, ref } from 'vue';
import { listVehicles, type Vehicle } from '../api/vehicles';
import { useAuthStore } from '../stores/auth';

const auth = useAuthStore();
const vehicles = ref<Vehicle[]>([]);
const errorMessage = ref<string | null>(null);

const headers = [
    { title: 'License plate', key: 'licensePlate' },
    { title: 'Type', key: 'type' },
    { title: 'Payload (kg)', key: 'payloadKg' },
];

onMounted(async () => {
    await auth.load();
    if (auth.isAuthenticated) {
        try {
            vehicles.value = (await listVehicles()).items;
        } catch (error) {
            errorMessage.value = describe(error);
        }
    }
});

// ProblemDetails' `errors` dictionary is keyed by error code (for example
// "Vehicle.PayloadKgNotPositive") rather than by field name - a known API defect awaiting its
// own fix - so it is deliberately not read here. `title`/`detail` are stable enough to surface
// directly, with a generic fallback when neither is present.
function describe(error: unknown): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as { title?: string; detail?: string } | undefined;
        const sentence = problem?.detail ?? problem?.title ?? 'The vehicle list could not be loaded.';
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return 'The vehicle list could not be loaded.';
}
</script>

<template>
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>Vehicles</h1>
            <p v-if="errorMessage" data-testid="vehicle-list-error">{{ errorMessage }}</p>
            <v-data-table v-else :headers="headers" :items="vehicles" item-value="id" data-testid="vehicle-table">
                <template #item.licensePlate="{ item }">
                    <span data-testid="vehicle-plate">{{ item.licensePlate }}</span>
                </template>
            </v-data-table>
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
</template>
