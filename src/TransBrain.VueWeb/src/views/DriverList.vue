<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteDriver, listDrivers, type Driver } from '../api/drivers';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

const auth = useAuthStore();
const { t } = useI18n();
const router = useRouter();
const drivers = ref<Driver[]>([]);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed delete must not hide the table the way a failed list
// load does (mirrors VehicleList.vue's actionError - see that file for why).
const actionError = ref<string | null>(null);

const headers = computed(() => [
    { title: t('drivers.lastName'), key: 'lastName' },
    { title: t('drivers.firstName'), key: 'firstName' },
    { title: t('drivers.licenseClasses'), key: 'licenseClasses' },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    try {
        drivers.value = (await listDrivers(LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('drivers.listLoadFailed'));
    }
}

// See VehicleList.vue's delete for why this action is shown to every authenticated user rather
// than hidden by role: this SPA has no role-decoding infrastructure yet, and a non-admin gets a
// clear 403-driven message here instead of a hidden button.
async function remove(driver: Driver): Promise<void> {
    actionError.value = null;
    try {
        await deleteDriver(driver.id);
        await refresh();
    } catch (error) {
        // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
        // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
        // the fallback text is therefore action-specific, not the list-load one.
        actionError.value = describe(error, t('drivers.deleteFailed'));
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
            <h1>{{ t('nav.drivers') }}</h1>
            <v-btn
                v-if="auth.can('masterData.write')"
                data-testid="driver-add"
                @click="router.push('/drivers/new')"
                >{{ t('drivers.add') }}</v-btn
            >
            <p v-if="actionError" data-testid="driver-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" data-testid="driver-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-data-table
                v-else
                :headers="headers"
                :items="drivers"
                :items-per-page="-1"
                item-value="id"
                data-testid="driver-table"
            >
                <template #item.lastName="{ item }">
                    <span data-testid="driver-lastname">{{ item.lastName }}</span>
                </template>
                <template #item.firstName="{ item }">
                    <span data-testid="driver-firstname">{{ item.firstName }}</span>
                </template>
                <template #item.licenseClasses="{ item }">
                    {{ item.licenseClasses.join(', ') }}
                </template>
                <template #item.actions="{ item }">
                    <template v-if="auth.can('masterData.write')">
                        <v-btn data-testid="driver-edit" @click="router.push(`/drivers/${item.id}`)">{{ t('common.edit') }}</v-btn>
                        <v-btn data-testid="driver-delete" @click="remove(item)">{{ t('common.delete') }}</v-btn>
                    </template>
                </template>
            </v-data-table>
        </template>
        <p v-else>{{ t('drivers.signInHint') }}</p>
    </v-container>
</template>
