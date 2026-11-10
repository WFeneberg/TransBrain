<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteDriver, listDrivers, type Driver } from '../api/drivers';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import { DRIVER_STATUSES, formatCalendarDate, statusColor } from '../composables/statusColor';
import { vNativeTestid } from '../directives/nativeTestId';
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
const { locale, t } = useI18n();
const router = useRouter();
const drivers = ref<Driver[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage: a failed delete must not hide the table the way a failed list
// load does (mirrors VehicleList.vue's actionError - see that file for why).
const actionError = ref<string | null>(null);

const search = ref<string | null>('');
const statusFilter = ref('');

const deleteDialogOpen = ref(false);
const deleteTarget = ref<Driver | null>(null);

const statusOptions = computed(() => [
    { title: t('common.all'), value: '' },
    ...DRIVER_STATUSES.map((status) => ({ title: status, value: status })),
]);

const headers = computed(() => [
    { title: t('drivers.lastName'), key: 'lastName' },
    { title: t('drivers.firstName'), key: 'firstName' },
    { title: t('drivers.licenseClasses'), key: 'licenseClasses', value: (driver: Driver) => driver.licenseClasses.join(', ') },
    { title: t('drivers.licenseValidUntil'), key: 'licenseValidUntil' },
    { title: t('common.status'), key: 'status' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredDrivers = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    return drivers.value.filter((driver) => {
        if (statusFilter.value && driver.status !== statusFilter.value) {
            return false;
        }
        if (!term) {
            return true;
        }
        return [driver.firstName, driver.lastName, ...driver.licenseClasses].some((value) =>
            value.toLowerCase().includes(term),
        );
    });
});

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        drivers.value = (await listDrivers(LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('drivers.listLoadFailed'));
    } finally {
        loading.value = false;
    }
}

function askDelete(driver: Driver): void {
    deleteTarget.value = driver;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    if (deleteTarget.value) {
        await remove(deleteTarget.value);
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
    <v-container class="page-container">
        <template v-if="auth.isAuthenticated">
            <div class="page-header">
                <div>
                    <h1 class="text-h4">{{ t('nav.drivers') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('drivers.count', { count: drivers.length }) }}</p>
                </div>
                <div class="header-actions">
                    <v-btn
                        v-if="auth.can('masterData.write')"
                        color="primary"
                        prepend-icon="mdi-plus"
                        data-testid="driver-add"
                        @click="router.push('/drivers/new')"
                        >{{ t('drivers.add') }}</v-btn
                    >
                </div>
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'driver-search'"
                                :label="t('drivers.search')"
                                prepend-inner-icon="mdi-magnify"
                                variant="outlined"
                                density="compact"
                                clearable
                                hide-details
                            />
                        </v-col>
                        <v-col cols="12" sm="6" md="3">
                            <v-select
                                v-model="statusFilter"
                                :label="t('common.status')"
                                :items="statusOptions"
                                variant="outlined"
                                density="compact"
                                hide-details
                                data-testid="driver-status-filter"
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="actionError" class="text-error error-line" data-testid="driver-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="driver-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": the API already returned one page, and Vuetify would
                 paginate that page AGAIN at ten rows, so a record the server did return could
                 still be invisible. One pagination - the server's - is enough, and it is what
                 the Angular app does. -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredDrivers"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('drivers.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="driver-table"
                >
                    <template #item.lastName="{ item }">
                        <span class="font-weight-bold" data-testid="driver-lastname">{{ item.lastName }}</span>
                    </template>
                    <template #item.firstName="{ item }">
                        <span data-testid="driver-firstname">{{ item.firstName }}</span>
                    </template>
                    <template #item.licenseClasses="{ item }">
                        <div class="license-chips">
                            <v-chip v-for="licenseClass in item.licenseClasses" :key="licenseClass" size="small" variant="outlined">{{
                                licenseClass
                            }}</v-chip>
                        </div>
                    </template>
                    <template #item.licenseValidUntil="{ item }">{{ formatCalendarDate(item.licenseValidUntil, locale) }}</template>
                    <template #item.status="{ item }">
                        <v-chip :color="statusColor(item.status)" size="small" variant="tonal" data-testid="driver-status">{{
                            item.status
                        }}</v-chip>
                    </template>
                    <template #item.actions="{ item }">
                        <div v-if="auth.can('masterData.write')" class="row-actions">
                            <v-tooltip :text="t('common.edit')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-pencil"
                                        variant="text"
                                        size="small"
                                        :aria-label="t('common.edit')"
                                        data-testid="driver-edit"
                                        @click="router.push(`/drivers/${item.id}`)"
                                    />
                                </template>
                            </v-tooltip>
                            <v-tooltip :text="t('common.delete')" location="top">
                                <template #activator="{ props: tooltip }">
                                    <v-btn
                                        v-bind="tooltip"
                                        icon="mdi-delete"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :aria-label="t('common.delete')"
                                        data-testid="driver-delete"
                                        @click="askDelete(item)"
                                    />
                                </template>
                            </v-tooltip>
                        </div>
                    </template>
                </v-data-table>
            </v-card>

            <ConfirmDialog
                v-model="deleteDialogOpen"
                :title="t('drivers.deleteTitle')"
                :message="
                    t('drivers.deleteConfirm', { name: deleteTarget ? `${deleteTarget.lastName}, ${deleteTarget.firstName}` : '' })
                "
                test-id-prefix="driver-delete"
                @confirm="confirmDelete"
            />
        </template>
        <p v-else>{{ t('drivers.signInHint') }}</p>
    </v-container>
</template>

<style scoped>
.license-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
}
</style>

