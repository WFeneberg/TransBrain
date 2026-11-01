<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { deleteCustomer, listCustomers, type Customer } from '../api/customers';
import { useAuthStore } from '../stores/auth';
import { useI18n } from '../i18n';

/** The API's maximum page size. There are no pagination controls yet; see DriverList.vue. */
const LIST_PAGE_SIZE = 100;

const auth = useAuthStore();
const { t } = useI18n();
const router = useRouter();
const customers = ref<Customer[]>([]);
const errorMessage = ref<string | null>(null);
// Separate from errorMessage so a failed delete does not hide the table (see DriverList.vue).
const actionError = ref<string | null>(null);

const headers = computed(() => [
    { title: t('common.name'), key: 'name' },
    { title: t('common.city'), key: 'city' },
    { title: t('customers.contactPerson'), key: 'contactPerson' },
    { title: t('customers.email'), key: 'email' },
    { title: t('common.actions'), key: 'actions', sortable: false },
]);

onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});

async function refresh(): Promise<void> {
    try {
        customers.value = (await listCustomers(1, LIST_PAGE_SIZE)).items;
    } catch (error) {
        errorMessage.value = describe(error, t('customers.error.list'));
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
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>{{ t('nav.customers') }}</h1>
            <v-btn
                v-if="auth.can('masterData.write')"
                data-testid="customer-add"
                @click="router.push('/customers/new')"
                >{{ t('customers.add') }}</v-btn
            >
            <p v-if="actionError" data-testid="customer-action-error">{{ actionError }}</p>
            <p v-if="errorMessage" data-testid="customer-list-error">{{ errorMessage }}</p>
            <!-- items-per-page="-1": one pagination only, the server's (see DriverList.vue). -->
            <v-data-table
                v-else
                :headers="headers"
                :items="customers"
                :items-per-page="-1"
                item-value="id"
                data-testid="customer-table"
            >
                <template #item.name="{ item }">
                    <span data-testid="customer-name">{{ item.name }}</span>
                </template>
                <template #item.city="{ item }">{{ item.postalCode }} {{ item.city }}</template>
                <template #item.actions="{ item }">
                    <template v-if="auth.can('masterData.write')">
                        <v-btn data-testid="customer-edit" @click="router.push(`/customers/${item.id}`)">{{ t('common.edit') }}</v-btn>
                        <v-btn data-testid="customer-delete" @click="remove(item)">{{ t('common.delete') }}</v-btn>
                    </template>
                </template>
            </v-data-table>
        </template>
        <p v-else>{{ t('customers.signIn') }}</p>
    </v-container>
</template>
