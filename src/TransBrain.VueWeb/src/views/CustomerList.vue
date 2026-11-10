<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteCustomer, listCustomers, type Customer } from '../api/customers';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

/** The API's maximum page size. There are no pagination controls yet; see DriverList.vue. */
const LIST_PAGE_SIZE = 100;

const auth = useAuthStore();
const { t } = useI18n();
const router = useRouter();
const customers = ref<Customer[]>([]);
const loading = ref(false);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage so a failed delete does not hide the table (see DriverList.vue).
const actionError = ref<string | null>(null);

const search = ref<string | null>('');

const deleteDialogOpen = ref(false);
const deleteTarget = ref<Customer | null>(null);

const headers = computed(() => [
    { title: t('common.name'), key: 'name' },
    { title: t('common.city'), key: 'city', value: (customer: Customer) => `${customer.postalCode} ${customer.city}` },
    { title: t('common.country'), key: 'country' },
    { title: t('customers.contactPerson'), key: 'contactPerson' },
    { title: t('customers.email'), key: 'email' },
    { title: t('customers.phone'), key: 'phone' },
    { title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredCustomers = computed(() => {
    const term = (search.value ?? '').trim().toLowerCase();
    if (!term) {
        return customers.value;
    }
    return customers.value.filter((customer) =>
        [
            customer.name,
            customer.postalCode,
            customer.city,
            customer.country,
            customer.contactPerson,
            customer.email,
            customer.phone,
            customer.vatId,
        ].some((value) => value?.toLowerCase().includes(term)),
    );
});

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        customers.value = (await listCustomers(1, LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('customers.error.list'));
    } finally {
        loading.value = false;
    }
}

function askDelete(customer: Customer): void {
    deleteTarget.value = customer;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    if (deleteTarget.value) {
        await remove(deleteTarget.value);
    }
}

async function remove(customer: Customer): Promise<void> {
    actionError.value = null;
    try {
        await deleteCustomer(customer.id);
        await refresh();
    } catch (error) {
        actionError.value = describe(error, t('customers.error.delete'));
    }
}

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
                    <h1 class="text-h4">{{ t('nav.customers') }}</h1>
                    <p class="text-medium-emphasis page-subtitle">{{ t('customers.count', { count: customers.length }) }}</p>
                </div>
                <div class="header-actions">
                    <v-btn
                        v-if="auth.can('masterData.write')"
                        color="primary"
                        prepend-icon="mdi-plus"
                        data-testid="customer-add"
                        @click="router.push('/customers/new')"
                        >{{ t('customers.add') }}</v-btn
                    >
                </div>
            </div>

            <v-card border flat class="filter-bar">
                <v-card-text>
                    <v-row dense>
                        <v-col cols="12" md="6">
                            <v-text-field
                                v-model="search"
                                v-native-testid="'customer-search'"
                                :label="t('customers.search')"
                                prepend-inner-icon="mdi-magnify"
                                variant="outlined"
                                density="compact"
                                clearable
                                hide-details
                            />
                        </v-col>
                    </v-row>
                </v-card-text>
            </v-card>

            <p v-if="actionError" class="text-error error-line" data-testid="customer-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" class="text-error error-line" data-testid="customer-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": one pagination only, the server's (see DriverList.vue). -->
            <v-card v-else border flat class="table-card">
                <v-data-table
                    :headers="headers"
                    :items="filteredCustomers"
                    :items-per-page="-1"
                    :loading="loading"
                    :no-data-text="t('customers.empty')"
                    item-value="id"
                    hide-default-footer
                    hover
                    data-testid="customer-table"
                >
                    <template #item.name="{ item }">
                        <span class="font-weight-bold" data-testid="customer-name">{{ item.name }}</span>
                    </template>
                    <template #item.contactPerson="{ value }">{{ value || '–' }}</template>
                    <template #item.email="{ value }">{{ value || '–' }}</template>
                    <template #item.phone="{ value }">{{ value || '–' }}</template>
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
                                        data-testid="customer-edit"
                                        @click="router.push(`/customers/${item.id}`)"
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
                                        data-testid="customer-delete"
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
                :title="t('customers.deleteTitle')"
                :message="t('customers.deleteConfirm', { name: deleteTarget?.name ?? '' })"
                test-id-prefix="customer-delete"
                @confirm="confirmDelete"
            />
        </template>
        <p v-else>{{ t('customers.signIn') }}</p>
    </v-container>
</template>
