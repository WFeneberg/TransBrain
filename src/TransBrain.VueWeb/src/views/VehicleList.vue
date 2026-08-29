<script setup lang="ts">
import axios from 'axios';
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteVehicle, listVehicles, type Vehicle } from '../api/vehicles';
import { useAuthStore } from '../stores/auth';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

const auth = useAuthStore();
const router = useRouter();
const vehicles = ref<Vehicle[]>([]);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed delete must not hide the table the way a failed list
// load does.
const actionError = ref<string | null>(null);

const headers = [
    { title: 'License plate', key: 'licensePlate' },
    { title: 'Type', key: 'type' },
    { title: 'Payload (kg)', key: 'payloadKg' },
    { title: 'Actions', key: 'actions', sortable: false },
];

onMounted(async () => {
    await auth.load();
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    try {
        vehicles.value = (await listVehicles(LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, 'The vehicle list could not be loaded.');
    }
}

// Any authenticated user sees the Add/Edit/Delete controls above - the app has no
// role-decoding infrastructure yet (auth.isAuthenticated is the only auth state tracked
// anywhere in this SPA), and building one just to hide three buttons is out of scope. A
// non-admin (dispo/fahrer/viewer) who uses them gets a 403 from the API, which surfaces here
// via actionError rather than silently failing.
async function remove(vehicle: Vehicle): Promise<void> {
    actionError.value = null;
    try {
        await deleteVehicle(vehicle.id);
        await refresh();
    } catch (error) {
        // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
        // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
        // the fallback text is therefore action-specific, not the list-load one.
        actionError.value = describe(error, 'The vehicle could not be deleted.');
    }
}

// ProblemDetails' `errors` dictionary is now keyed by field name (Task 1 of the
// 2026-08-29 master-data-completion phase fixed the previous error-code keying), but this
// component has no form fields to bind those keys onto - it only ever needs the free-text
// summary, which `title`/`detail` already provide.
function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as { title?: string; detail?: string } | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>Vehicles</h1>
            <v-btn data-testid="vehicle-add" @click="router.push('/vehicles/new')">Add vehicle</v-btn>
            <p v-if="actionError" data-testid="vehicle-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" data-testid="vehicle-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-data-table
                v-else
                :headers="headers"
                :items="vehicles"
                :items-per-page="-1"
                item-value="id"
                data-testid="vehicle-table"
            >
                <template #item.licensePlate="{ item }">
                    <span data-testid="vehicle-plate">{{ item.licensePlate }}</span>
                </template>
                <template #item.actions="{ item }">
                    <v-btn data-testid="vehicle-edit" @click="router.push(`/vehicles/${item.id}`)">Edit</v-btn>
                    <v-btn data-testid="vehicle-delete" @click="remove(item)">Delete</v-btn>
                </template>
            </v-data-table>
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
</template>
