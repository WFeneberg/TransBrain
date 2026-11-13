<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
    createVehicleCatalogEntry,
    deleteVehicleCatalogEntry,
    listVehicleCatalog,
    updateVehicleCatalogEntry,
    CUSTOMER_CATALOG_KINDS,
    DRIVER_CATALOG_KINDS,
    VEHICLE_CATALOG_KINDS,
    type VehicleCatalogEntry,
    type VehicleCatalogKind,
} from '../api/vehicleCatalog';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const CODE_MAX_LENGTH = 20;
const NAME_MAX_LENGTH = 100;
const CODE_PATTERN = /^[A-Za-z0-9_-]+$/;

const KIND_ICONS: Record<VehicleCatalogKind, string> = {
    VehicleClass: 'mdi-truck-outline',
    CarrierGroup: 'mdi-account-group-outline',
    InsuranceCompany: 'mdi-shield-car',
    DocumentType: 'mdi-file-certificate-outline',
    CustomerClass: 'mdi-account-star-outline',
    PhoneType: 'mdi-phone-outline',
    DriverDocumentType: 'mdi-card-account-details-outline',
};

/**
 * The page serves three routes: '/vehicle-catalog' with the lists behind the vehicle form,
 * '/customer-catalog' (route meta `catalogScope: 'customer'`) with those behind the customer form
 * and '/driver-catalog' (`catalogScope: 'driver'`) with those behind the driver form.
 */
interface CatalogScope {
    kinds: readonly VehicleCatalogKind[];
    title: MessageKey;
    subtitle: MessageKey;
    back: MessageKey;
    backPath: string;
    usage: MessageKey;
    testIdPrefix: string;
}

const SCOPES: Record<'vehicle' | 'customer' | 'driver', CatalogScope> = {
    vehicle: {
        kinds: VEHICLE_CATALOG_KINDS,
        title: 'vehicleCatalog.title',
        subtitle: 'vehicleCatalog.subtitle',
        back: 'vehicleCatalog.back',
        backPath: '/vehicles',
        usage: 'vehicleCatalog.usage',
        testIdPrefix: 'vehicle-catalog',
    },
    customer: {
        kinds: CUSTOMER_CATALOG_KINDS,
        title: 'customerCatalog.title',
        subtitle: 'customerCatalog.subtitle',
        back: 'customerCatalog.back',
        backPath: '/customers',
        usage: 'customerCatalog.usage',
        testIdPrefix: 'customer-catalog',
    },
    driver: {
        kinds: DRIVER_CATALOG_KINDS,
        title: 'driverCatalog.title',
        subtitle: 'driverCatalog.subtitle',
        back: 'driverCatalog.back',
        backPath: '/drivers',
        usage: 'driverCatalog.usage',
        testIdPrefix: 'driver-catalog',
    },
};

const auth = useAuthStore();
const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const canWrite = computed(() => auth.can('masterData.write'));
const scope = computed(() => {
    const value = route.meta.catalogScope;
    return SCOPES[value === 'customer' || value === 'driver' ? value : 'vehicle'];
});

const kind = ref<VehicleCatalogKind>(kindFromQuery(route.query.kind));
const entries = ref<VehicleCatalogEntry[]>([]);
const loading = ref(false);
// One message for the page: a failed load hides the table, a failed delete keeps it.
const errorMessage = ref<string | null>(null);
const loadFailed = ref(false);

const dialogOpen = ref(false);
const editTarget = ref<VehicleCatalogEntry | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive({ code: '', name: '', isActive: true });
const fieldErrors = reactive<Record<'code' | 'name', string | null>>({ code: null, name: null });

const deleteDialogOpen = ref(false);
const deleteTarget = ref<VehicleCatalogEntry | null>(null);

const headers = computed(() => [
    { title: t('vehicleCatalog.code'), key: 'code' },
    { title: t('vehicleCatalog.name'), key: 'name' },
    { title: t('vehicleCatalog.status'), key: 'isActive' },
    { title: t('vehicleCatalog.usedBy'), key: 'usageCount', align: 'end' as const },
    ...(canWrite.value ? [{ title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const }] : []),
]);

function kindFromQuery(value: unknown): VehicleCatalogKind {
    return scope.value.kinds.find((candidate) => candidate === value) ?? scope.value.kinds[0];
}

// Both routes render this component, so a direct switch between them reuses the instance.
watch(scope, () => {
    kind.value = kindFromQuery(route.query.kind);
});

onMounted(refresh);

watch(kind, async (next) => {
    errorMessage.value = null;
    entries.value = [];
    await router.replace({ query: { ...route.query, kind: next } });
    await refresh();
});

async function refresh(): Promise<void> {
    const requested = kind.value;
    loading.value = true;
    try {
        const loaded = await listVehicleCatalog(requested);
        // A fast tab switch must not show the answer for the previous tab.
        if (requested === kind.value) {
            entries.value = loaded;
            loadFailed.value = false;
        }
    } catch (error) {
        if (requested === kind.value) {
            loadFailed.value = true;
            errorMessage.value = describe(error, t('vehicleCatalog.loadFailed'));
        }
    } finally {
        if (requested === kind.value) {
            loading.value = false;
        }
    }
}

function openCreate(): void {
    editTarget.value = null;
    form.code = '';
    form.name = '';
    form.isActive = true;
    openDialog();
}

function openEdit(entry: VehicleCatalogEntry): void {
    editTarget.value = entry;
    form.code = entry.code;
    form.name = entry.name;
    form.isActive = entry.isActive;
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    fieldErrors.code = null;
    fieldErrors.name = null;
    dialogOpen.value = true;
}

function errorsFor(field: 'code' | 'name'): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function validate(): boolean {
    const code = form.code.trim();
    const name = form.name.trim();
    fieldErrors.code = null;
    fieldErrors.name = null;
    if (!editTarget.value) {
        if (!code) {
            fieldErrors.code = t('common.required');
        } else if (code.length > CODE_MAX_LENGTH) {
            fieldErrors.code = t('vehicleCatalog.maxLength', { max: CODE_MAX_LENGTH });
        } else if (!CODE_PATTERN.test(code)) {
            fieldErrors.code = t('vehicleCatalog.codeInvalid');
        }
    }
    if (!name) {
        fieldErrors.name = t('common.required');
    } else if (name.length > NAME_MAX_LENGTH) {
        fieldErrors.name = t('vehicleCatalog.maxLength', { max: NAME_MAX_LENGTH });
    }
    return fieldErrors.code === null && fieldErrors.name === null;
}

async function save(): Promise<void> {
    dialogError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    saving.value = true;
    try {
        if (editTarget.value) {
            await updateVehicleCatalogEntry(kind.value, editTarget.value.id, {
                name: form.name.trim(),
                isActive: form.isActive,
            });
        } else {
            await createVehicleCatalogEntry(kind.value, {
                code: form.code.trim(),
                name: form.name.trim(),
                isActive: form.isActive,
            });
        }
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        applyServerErrors(error);
    } finally {
        saving.value = false;
    }
}

// The `errors` dictionary is keyed by the .NET property name ("Code", "Name"); a key this
// dialog has no field for still has to reach the user, so it goes to the dialog-level line.
function applyServerErrors(error: unknown): void {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const fieldName = field.charAt(0).toLowerCase() + field.slice(1);
                const message = messages.join(' ');
                if (fieldName === 'code' || fieldName === 'name') {
                    fieldErrors[fieldName] = message;
                } else {
                    dialogError.value = dialogError.value ? `${dialogError.value} ${message}` : message;
                }
            }
            return;
        }
    }
    dialogError.value = describe(error, t('vehicleCatalog.saveFailed'));
}

function askDelete(entry: VehicleCatalogEntry): void {
    deleteTarget.value = entry;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    deleteDialogOpen.value = false;
    const entry = deleteTarget.value;
    if (!entry) {
        return;
    }
    errorMessage.value = null;
    try {
        await deleteVehicleCatalogEntry(kind.value, entry.id);
        await refresh();
    } catch (error) {
        errorMessage.value = describe(error, t('vehicleCatalog.deleteFailed'));
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container class="catalog-page">
        <v-btn
            variant="text"
            prepend-icon="mdi-arrow-left"
            class="back-button"
            :data-testid="`${scope.testIdPrefix}-back`"
            @click="router.push(scope.backPath)"
            >{{ t(scope.back) }}</v-btn
        >

        <div class="page-header">
            <div>
                <h1 class="text-h4">{{ t(scope.title) }}</h1>
                <p class="text-medium-emphasis page-subtitle">{{ t(scope.subtitle) }}</p>
            </div>
        </div>

        <p v-if="errorMessage" class="text-error error-line" data-testid="catalog-error">{{ errorMessage }}</p>

        <v-card border flat class="table-card">
            <div class="card-toolbar">
                <v-tabs v-model="kind" color="primary" density="comfortable" show-arrows>
                    <v-tab
                        v-for="tabKind in scope.kinds"
                        :key="tabKind"
                        :value="tabKind"
                        :prepend-icon="KIND_ICONS[tabKind]"
                        :data-testid="`${scope.testIdPrefix}-tab-${tabKind}`"
                        >{{ t(`vehicleCatalog.kind.${tabKind}`) }}</v-tab
                    >
                </v-tabs>
                <v-btn
                    v-if="canWrite"
                    color="primary"
                    prepend-icon="mdi-plus"
                    class="add-button"
                    data-testid="catalog-add"
                    @click="openCreate"
                    >{{ t('vehicleCatalog.add') }}</v-btn
                >
            </div>
            <v-divider />
            <v-data-table
                v-if="!loadFailed"
                :headers="headers"
                :items="entries"
                :items-per-page="-1"
                :loading="loading"
                :row-props="() => ({ 'data-testid': 'catalog-row' })"
                item-value="id"
                hide-default-footer
                hover
                data-testid="catalog-table"
            >
                <template #no-data>
                    <div class="empty-state" data-testid="catalog-empty">
                        <v-icon icon="mdi-format-list-bulleted" size="large" color="medium-emphasis" />
                        <span>{{ t('vehicleCatalog.empty') }}</span>
                    </div>
                </template>
                <template #item.code="{ item }">
                    <code class="entry-code" data-testid="catalog-entry-code">{{ item.code }}</code>
                </template>
                <template #item.name="{ item }">
                    <span class="font-weight-medium" data-testid="catalog-entry-name">{{ item.name }}</span>
                </template>
                <template #item.isActive="{ item }">
                    <v-chip
                        :color="item.isActive ? 'success' : 'grey'"
                        :prepend-icon="item.isActive ? 'mdi-check-circle-outline' : 'mdi-pause-circle-outline'"
                        size="small"
                        variant="tonal"
                        data-testid="catalog-entry-status"
                        >{{ item.isActive ? t('vehicleCatalog.active') : t('vehicleCatalog.inactive') }}</v-chip
                    >
                </template>
                <template #item.usageCount="{ item }">
                    <span
                        :class="{ 'text-medium-emphasis': item.usageCount === 0 }"
                        data-testid="catalog-entry-usage"
                        >{{ t(scope.usage, { count: item.usageCount }) }}</span
                    >
                </template>
                <template #item.actions="{ item }">
                    <div class="row-actions">
                        <v-tooltip :text="t('common.edit')" location="top">
                            <template #activator="{ props: tooltip }">
                                <v-btn
                                    v-bind="tooltip"
                                    icon="mdi-pencil"
                                    variant="text"
                                    size="small"
                                    :aria-label="t('common.edit')"
                                    data-testid="catalog-edit"
                                    @click="openEdit(item)"
                                />
                            </template>
                        </v-tooltip>
                        <!-- A disabled button gets no pointer events, so the tooltip hangs on a
                             wrapping span - otherwise the "why" would never show. -->
                        <v-tooltip :text="item.usageCount > 0 ? t('vehicleCatalog.inUse') : t('common.delete')" location="top">
                            <template #activator="{ props: tooltip }">
                                <span v-bind="tooltip">
                                    <v-btn
                                        icon="mdi-delete"
                                        variant="text"
                                        size="small"
                                        color="error"
                                        :disabled="item.usageCount > 0"
                                        :aria-label="item.usageCount > 0 ? t('vehicleCatalog.inUse') : t('common.delete')"
                                        data-testid="catalog-delete"
                                        @click="askDelete(item)"
                                    />
                                </span>
                            </template>
                        </v-tooltip>
                    </div>
                </template>
            </v-data-table>
        </v-card>

        <v-dialog v-model="dialogOpen" max-width="520" :persistent="saving">
            <v-card data-testid="catalog-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'"
                    :title="editTarget ? t('vehicleCatalog.edit') : t('vehicleCatalog.new')"
                    :subtitle="t(`vehicleCatalog.kind.${kind}`)"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="catalog-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-text-field
                            v-model="form.code"
                            v-native-testid="'catalog-code'"
                            :label="`${t('vehicleCatalog.code')} *`"
                            :hint="t('vehicleCatalog.codeHint')"
                            :persistent-hint="!editTarget"
                            :disabled="!!editTarget"
                            :counter="editTarget ? undefined : CODE_MAX_LENGTH"
                            :error-messages="errorsFor('code')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                        >
                            <template #message="{ message }">
                                <span :data-testid="errorsFor('code').length ? 'catalog-code-error' : undefined">{{ message }}</span>
                            </template>
                        </v-text-field>
                        <v-text-field
                            v-model="form.name"
                            v-native-testid="'catalog-name'"
                            :label="`${t('vehicleCatalog.name')} *`"
                            :counter="NAME_MAX_LENGTH"
                            :error-messages="errorsFor('name')"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                        >
                            <template #message="{ message }">
                                <span data-testid="catalog-name-error">{{ message }}</span>
                            </template>
                        </v-text-field>
                        <v-switch
                            v-model="form.isActive"
                            v-native-testid="'catalog-active'"
                            :label="t('vehicleCatalog.active')"
                            color="success"
                            density="comfortable"
                            inset
                            hide-details
                        />
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn type="button" variant="text" data-testid="catalog-cancel" @click="dialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="catalog-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <v-dialog v-model="deleteDialogOpen" max-width="440">
            <v-card>
                <v-card-title>{{ t('vehicleCatalog.deleteTitle') }}</v-card-title>
                <v-card-text>{{ t('vehicleCatalog.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}</v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" data-testid="catalog-delete-cancel" @click="deleteDialogOpen = false">{{
                        t('common.cancel')
                    }}</v-btn>
                    <v-btn color="error" variant="flat" data-testid="catalog-delete-confirm" @click="confirmDelete">{{
                        t('common.delete')
                    }}</v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>
    </v-container>
</template>

<style scoped>
.catalog-page {
    max-width: 1200px;
}

.back-button {
    margin: 0 0 8px -8px;
}

.page-header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 24px;
}

.page-subtitle {
    margin: 4px 0 0;
}

.error-line {
    margin-bottom: 16px;
}

.card-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    padding-right: 16px;
}

.add-button {
    margin: 8px 0;
}

.entry-code {
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(var(--v-theme-on-surface), 0.06);
    font-size: 0.875rem;
}

.empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
    padding: 24px 0;
}

.row-actions {
    display: inline-flex;
    gap: 4px;
}

.dialog-error {
    margin-bottom: 16px;
}

.dialog-field {
    margin-bottom: 8px;
}
</style>
