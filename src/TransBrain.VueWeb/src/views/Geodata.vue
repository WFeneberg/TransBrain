<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import {
    deleteGeodataOverride,
    getMapDataStatus,
    listGeodata,
    resolveGeodata,
    setGeodataOverride,
    type GeodataEntry,
    type GeodataSource,
    type MapDataStatus,
} from '../api/geodata';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import { vNativeTestid } from '../directives/nativeTestId';
import { useAuthStore } from '../stores/auth';
import { useI18n, type MessageKey } from '../i18n';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const SOURCE_LABELS: Record<GeodataSource, MessageKey> = {
    manual: 'geodata.source.manual',
    geocoded: 'geodata.source.geocoded',
    unresolved: 'geodata.source.unresolved',
};

const SOURCE_COLORS: Record<GeodataSource, string> = {
    manual: 'primary',
    geocoded: 'success',
    unresolved: 'error',
};

const SOURCE_ICONS: Record<GeodataSource, string> = {
    manual: 'mdi-map-marker-account',
    geocoded: 'mdi-map-marker-check-outline',
    unresolved: 'mdi-map-marker-question-outline',
};

const auth = useAuthStore();
const { t, locale } = useI18n();

const canWrite = computed(() => auth.can('masterData.write'));

const entries = ref<GeodataEntry[]>([]);
const loading = ref(false);
const loadFailed = ref(false);
const errorMessage = ref<string | null>(null);
const filterText = ref('');
const sourceFilter = ref<'all' | GeodataSource>('all');
/** null while loading or when the status call itself failed — the card is simply absent. */
const mapStatus = ref<MapDataStatus | null>(null);
/** The address key of the row whose resolve request is in flight. */
const resolvingKey = ref<string | null>(null);

const dialogOpen = ref(false);
const editTarget = ref<GeodataEntry | null>(null);
const saving = ref(false);
const attemptedSubmit = ref(false);
const dialogError = ref<string | null>(null);
const form = reactive({ street: '', postalCode: '', city: '', country: '', latitude: '', longitude: '' });
type FieldName = 'street' | 'postalCode' | 'city' | 'country' | 'latitude' | 'longitude';
const fieldErrors = reactive<Record<FieldName, string | null>>({
    street: null,
    postalCode: null,
    city: null,
    country: null,
    latitude: null,
    longitude: null,
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<GeodataEntry | null>(null);

const headers = computed(() => [
    { title: t('geodata.address'), key: 'street' },
    { title: t('geodata.coordinates'), key: 'latitude', sortable: false },
    { title: t('common.status'), key: 'source' },
    { title: t('geodata.usedBy'), key: 'orderCount', align: 'end' as const },
    ...(canWrite.value ? [{ title: t('common.actions'), key: 'actions', sortable: false, align: 'end' as const }] : []),
]);

const filtered = computed(() => {
    const text = filterText.value.trim().toLowerCase();
    return entries.value.filter((entry) => {
        if (sourceFilter.value !== 'all' && entry.source !== sourceFilter.value) {
            return false;
        }
        if (text.length === 0) {
            return true;
        }
        return [entry.street, entry.postalCode, entry.city, entry.country].join(' ').toLowerCase().includes(text);
    });
});

onMounted(() => {
    void refresh();
    void loadMapStatus();
});

// Loaded once per visit: the extract on the server does not change while the page is open.
async function loadMapStatus(): Promise<void> {
    try {
        mapStatus.value = await getMapDataStatus();
    } catch {
        mapStatus.value = null;
    }
}

function formatOsmDate(iso: string): string {
    return new Date(iso).toLocaleString(locale.value === 'de' ? 'de-DE' : 'en-US', {
        dateStyle: 'short',
        timeStyle: 'short',
    });
}

async function refresh(): Promise<void> {
    loading.value = true;
    try {
        entries.value = await listGeodata();
        loadFailed.value = false;
    } catch (error) {
        loadFailed.value = true;
        errorMessage.value = describe(error, t('geodata.error.list'));
    } finally {
        loading.value = false;
    }
}

function key(entry: GeodataEntry): string {
    return `${entry.country}|${entry.postalCode}|${entry.city}|${entry.street}`.toLowerCase();
}

function formatCoordinate(value: number): string {
    return value.toFixed(5);
}

function openCreate(): void {
    editTarget.value = null;
    form.street = '';
    form.postalCode = '';
    form.city = '';
    form.country = '';
    form.latitude = '';
    form.longitude = '';
    openDialog();
}

function openEdit(entry: GeodataEntry): void {
    editTarget.value = entry;
    form.street = entry.street;
    form.postalCode = entry.postalCode;
    form.city = entry.city;
    form.country = entry.country;
    form.latitude = entry.latitude === null ? '' : String(entry.latitude);
    form.longitude = entry.longitude === null ? '' : String(entry.longitude);
    openDialog();
}

function openDialog(): void {
    attemptedSubmit.value = false;
    dialogError.value = null;
    for (const field of Object.keys(fieldErrors) as FieldName[]) {
        fieldErrors[field] = null;
    }
    dialogOpen.value = true;
}

function errorsFor(field: FieldName): string[] {
    const message = fieldErrors[field];
    return attemptedSubmit.value && message ? [message] : [];
}

function validate(): boolean {
    for (const field of Object.keys(fieldErrors) as FieldName[]) {
        fieldErrors[field] = null;
    }
    if (!form.street.trim()) {
        fieldErrors.street = t('common.required');
    }
    if (!form.postalCode.trim()) {
        fieldErrors.postalCode = t('common.required');
    }
    if (!form.city.trim()) {
        fieldErrors.city = t('common.required');
    }
    if (form.country.trim().length !== 2) {
        fieldErrors.country = t('geodata.countryInvalid');
    }
    const latitude = Number(form.latitude);
    if (form.latitude.trim() === '' || Number.isNaN(latitude) || latitude < -90 || latitude > 90) {
        fieldErrors.latitude = t('geodata.latitudeRange');
    }
    const longitude = Number(form.longitude);
    if (form.longitude.trim() === '' || Number.isNaN(longitude) || longitude < -180 || longitude > 180) {
        fieldErrors.longitude = t('geodata.longitudeRange');
    }
    return Object.values(fieldErrors).every((message) => message === null);
}

async function save(): Promise<void> {
    dialogError.value = null;
    attemptedSubmit.value = true;
    if (!validate()) {
        return;
    }

    saving.value = true;
    try {
        await setGeodataOverride({
            street: form.street.trim(),
            postalCode: form.postalCode.trim(),
            city: form.city.trim(),
            country: form.country.trim(),
            latitude: Number(form.latitude),
            longitude: Number(form.longitude),
        });
        dialogOpen.value = false;
        await refresh();
    } catch (error) {
        dialogError.value = describe(error, t('geodata.error.save'));
    } finally {
        saving.value = false;
    }
}

function askDelete(entry: GeodataEntry): void {
    deleteTarget.value = entry;
    deleteDialogOpen.value = true;
}

async function confirmDelete(): Promise<void> {
    const entry = deleteTarget.value;
    if (!entry?.overrideId) {
        return;
    }
    errorMessage.value = null;
    try {
        await deleteGeodataOverride(entry.overrideId);
        await refresh();
    } catch (error) {
        errorMessage.value = describe(error, t('geodata.error.delete'));
    }
}

/** Asks the server to resolve the address now - a geocoder success lands in the cache. */
async function resolve(entry: GeodataEntry): Promise<void> {
    errorMessage.value = null;
    resolvingKey.value = key(entry);
    try {
        await resolveGeodata({
            street: entry.street,
            postalCode: entry.postalCode,
            city: entry.city,
            country: entry.country,
        });
        await refresh();
    } catch (error) {
        const status = axios.isAxiosError(error) ? error.response?.status : undefined;
        errorMessage.value = describe(
            error,
            t(status === 503 || status === 404 ? 'geodata.error.resolveFailed' : 'geodata.error.resolve'),
        );
    } finally {
        resolvingKey.value = null;
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as ProblemDetailsBody | undefined;
        const fieldMessages = problem?.errors ? Object.values(problem.errors).flat() : [];
        const sentence = fieldMessages[0] ?? problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
</script>

<template>
    <v-container class="geodata-page">
        <div class="page-header">
            <div>
                <h1 class="text-h4">{{ t('geodata.title') }}</h1>
                <p class="text-medium-emphasis page-subtitle">{{ t('geodata.subtitle') }}</p>
            </div>
            <v-btn
                v-if="canWrite"
                color="primary"
                prepend-icon="mdi-map-marker-plus-outline"
                data-testid="geodata-add"
                @click="openCreate"
                >{{ t('geodata.add') }}</v-btn
            >
        </div>

        <p v-if="errorMessage" class="text-error error-line" data-testid="geodata-error">{{ errorMessage }}</p>

        <v-card v-if="mapStatus" border flat class="map-status-card" data-testid="geodata-map-status">
            <div class="map-services">
                <span
                    class="map-service"
                    :class="{ down: !mapStatus.geocoder.available }"
                    data-testid="geodata-map-geocoder"
                >
                    <v-icon
                        :icon="mapStatus.geocoder.available ? 'mdi-check-circle-outline' : 'mdi-cloud-off-outline'"
                        :color="mapStatus.geocoder.available ? 'success' : 'error'"
                        size="18"
                    />
                    {{ t('geodata.mapData.geocoder') }}:
                    {{ t(mapStatus.geocoder.available ? 'geodata.mapData.ready' : 'geodata.mapData.unavailable') }}
                    <template v-if="mapStatus.geocoder.osmDataUpdatedAt">
                        · {{ t('geodata.mapData.dataFrom', { date: formatOsmDate(mapStatus.geocoder.osmDataUpdatedAt) }) }}
                    </template>
                </span>
                <span
                    class="map-service"
                    :class="{ down: !mapStatus.routing.available }"
                    data-testid="geodata-map-routing"
                >
                    <v-icon
                        :icon="mapStatus.routing.available ? 'mdi-check-circle-outline' : 'mdi-cloud-off-outline'"
                        :color="mapStatus.routing.available ? 'success' : 'error'"
                        size="18"
                    />
                    {{ t('geodata.mapData.routing') }}:
                    {{ t(mapStatus.routing.available ? 'geodata.mapData.ready' : 'geodata.mapData.unavailable') }}
                    <template v-if="mapStatus.routing.osmDataUpdatedAt">
                        · {{ t('geodata.mapData.dataFrom', { date: formatOsmDate(mapStatus.routing.osmDataUpdatedAt) }) }}
                    </template>
                </span>
            </div>
            <p class="map-hint text-medium-emphasis">{{ t('geodata.mapData.hint') }}</p>
        </v-card>

        <v-card border flat class="table-card">
            <div class="card-toolbar">
                <v-text-field
                    v-model="filterText"
                    v-native-testid="'geodata-filter'"
                    :label="t('geodata.filter')"
                    prepend-inner-icon="mdi-magnify"
                    variant="outlined"
                    density="compact"
                    hide-details
                    clearable
                    class="filter-field"
                />
                <v-btn-toggle v-model="sourceFilter" density="comfortable" color="primary" mandatory>
                    <v-btn value="all" size="small" data-testid="geodata-filter-all">{{ t('common.all') }}</v-btn>
                    <v-btn value="manual" size="small" data-testid="geodata-filter-manual">{{
                        t('geodata.source.manual')
                    }}</v-btn>
                    <v-btn value="geocoded" size="small" data-testid="geodata-filter-geocoded">{{
                        t('geodata.source.geocoded')
                    }}</v-btn>
                    <v-btn value="unresolved" size="small" data-testid="geodata-filter-unresolved">{{
                        t('geodata.source.unresolved')
                    }}</v-btn>
                </v-btn-toggle>
                <span class="text-medium-emphasis count" data-testid="geodata-count">{{
                    t('geodata.count', { count: filtered.length })
                }}</span>
            </div>
            <v-divider />
            <v-data-table
                v-if="!loadFailed"
                :headers="headers"
                :items="filtered"
                :items-per-page="-1"
                :loading="loading"
                :row-props="() => ({ 'data-testid': 'geodata-row' })"
                item-value="street"
                hide-default-footer
                hover
                data-testid="geodata-table"
            >
                <template #no-data>
                    <div class="empty-state" data-testid="geodata-empty">
                        <v-icon icon="mdi-map-marker-off-outline" size="large" color="medium-emphasis" />
                        <span>{{ t('geodata.empty') }}</span>
                    </div>
                </template>
                <template #item.street="{ item }">
                    <div class="font-weight-medium" data-testid="geodata-entry-street">{{ item.street }}</div>
                    <div class="text-medium-emphasis text-body-2">
                        {{ item.postalCode }} {{ item.city }}, {{ item.country }}
                    </div>
                </template>
                <template #item.latitude="{ item }">
                    <code v-if="item.latitude !== null" class="coords" data-testid="geodata-entry-coords"
                        >{{ formatCoordinate(item.latitude) }}, {{ formatCoordinate(item.longitude!) }}</code
                    >
                    <span v-else class="text-medium-emphasis" data-testid="geodata-entry-coords">—</span>
                </template>
                <template #item.source="{ item }">
                    <v-chip
                        :color="SOURCE_COLORS[item.source]"
                        :prepend-icon="SOURCE_ICONS[item.source]"
                        size="small"
                        variant="tonal"
                        data-testid="geodata-entry-source"
                        >{{ t(SOURCE_LABELS[item.source]) }}</v-chip
                    >
                </template>
                <template #item.orderCount="{ item }">
                    <span
                        :class="{ 'text-medium-emphasis font-italic': !item.inUse }"
                        data-testid="geodata-entry-usage"
                        >{{
                            item.inUse
                                ? t('geodata.usage', { orders: item.orderCount, locations: item.locationCount })
                                : t('geodata.notInUse')
                        }}</span
                    >
                </template>
                <template #item.actions="{ item }">
                    <div class="row-actions">
                        <v-tooltip v-if="item.source !== 'manual'" :text="t('geodata.resolve')" location="top">
                            <template #activator="{ props: tooltip }">
                                <v-btn
                                    v-bind="tooltip"
                                    icon="mdi-map-search-outline"
                                    variant="text"
                                    size="small"
                                    :loading="resolvingKey === key(item)"
                                    :aria-label="t('geodata.resolve')"
                                    data-testid="geodata-resolve"
                                    @click="resolve(item)"
                                />
                            </template>
                        </v-tooltip>
                        <v-tooltip :text="t('geodata.setCoordinates')" location="top">
                            <template #activator="{ props: tooltip }">
                                <v-btn
                                    v-bind="tooltip"
                                    icon="mdi-map-marker-radius-outline"
                                    variant="text"
                                    size="small"
                                    :aria-label="t('geodata.setCoordinates')"
                                    data-testid="geodata-edit"
                                    @click="openEdit(item)"
                                />
                            </template>
                        </v-tooltip>
                        <v-tooltip v-if="item.overrideId" :text="t('geodata.deleteOverride')" location="top">
                            <template #activator="{ props: tooltip }">
                                <v-btn
                                    v-bind="tooltip"
                                    icon="mdi-map-marker-remove-outline"
                                    variant="text"
                                    size="small"
                                    color="error"
                                    :aria-label="t('geodata.deleteOverride')"
                                    data-testid="geodata-delete"
                                    @click="askDelete(item)"
                                />
                            </template>
                        </v-tooltip>
                    </div>
                </template>
            </v-data-table>
        </v-card>

        <v-dialog v-model="dialogOpen" max-width="560" :persistent="saving">
            <v-card data-testid="geodata-dialog">
                <v-card-item
                    :prepend-icon="editTarget ? 'mdi-map-marker-radius-outline' : 'mdi-map-marker-plus-outline'"
                    :title="editTarget ? t('geodata.editTitle') : t('geodata.newTitle')"
                    :subtitle="editTarget ? `${editTarget.street}, ${editTarget.city}` : undefined"
                />
                <form novalidate @submit.prevent="save">
                    <v-card-text>
                        <p v-if="dialogError" class="text-error dialog-error" data-testid="geodata-dialog-error">
                            {{ dialogError }}
                        </p>
                        <v-text-field
                            v-model="form.street"
                            v-native-testid="'geodata-street'"
                            :label="`${t('common.street')} *`"
                            :disabled="!!editTarget"
                            :error-messages="errorsFor('street')"
                            maxlength="200"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field"
                        />
                        <div class="field-row">
                            <v-text-field
                                v-model="form.postalCode"
                                v-native-testid="'geodata-postal'"
                                :label="`${t('common.postalCode')} *`"
                                :disabled="!!editTarget"
                                :error-messages="errorsFor('postalCode')"
                                maxlength="20"
                                variant="outlined"
                                density="comfortable"
                                class="dialog-field postal-field"
                            />
                            <v-text-field
                                v-model="form.city"
                                v-native-testid="'geodata-city'"
                                :label="`${t('common.city')} *`"
                                :disabled="!!editTarget"
                                :error-messages="errorsFor('city')"
                                maxlength="200"
                                variant="outlined"
                                density="comfortable"
                                class="dialog-field city-field"
                            />
                        </div>
                        <v-text-field
                            v-model="form.country"
                            v-native-testid="'geodata-country'"
                            :label="`${t('common.country')} *`"
                            :hint="t('geodata.countryHint')"
                            :disabled="!!editTarget"
                            :error-messages="errorsFor('country')"
                            maxlength="2"
                            variant="outlined"
                            density="comfortable"
                            class="dialog-field country-field"
                        />
                        <div class="field-row">
                            <v-text-field
                                v-model="form.latitude"
                                v-native-testid="'geodata-latitude'"
                                :label="`${t('geodata.latitude')} *`"
                                :error-messages="errorsFor('latitude')"
                                type="number"
                                step="any"
                                variant="outlined"
                                density="comfortable"
                                class="dialog-field"
                            >
                                <template #message="{ message }">
                                    <span data-testid="geodata-latitude-error">{{ message }}</span>
                                </template>
                            </v-text-field>
                            <v-text-field
                                v-model="form.longitude"
                                v-native-testid="'geodata-longitude'"
                                :label="`${t('geodata.longitude')} *`"
                                :error-messages="errorsFor('longitude')"
                                type="number"
                                step="any"
                                variant="outlined"
                                density="comfortable"
                                class="dialog-field"
                            >
                                <template #message="{ message }">
                                    <span data-testid="geodata-longitude-error">{{ message }}</span>
                                </template>
                            </v-text-field>
                        </div>
                        <p class="text-medium-emphasis text-body-2 hint-line">{{ t('geodata.coordinatesHint') }}</p>
                    </v-card-text>
                    <v-card-actions>
                        <v-spacer />
                        <v-btn type="button" variant="text" data-testid="geodata-cancel" @click="dialogOpen = false">{{
                            t('common.cancel')
                        }}</v-btn>
                        <v-btn
                            type="submit"
                            color="primary"
                            variant="flat"
                            prepend-icon="mdi-content-save-outline"
                            :loading="saving"
                            data-testid="geodata-save"
                            >{{ t('common.save') }}</v-btn
                        >
                    </v-card-actions>
                </form>
            </v-card>
        </v-dialog>

        <ConfirmDialog
            v-model="deleteDialogOpen"
            :title="t('geodata.deleteTitle')"
            :message="t('geodata.deleteConfirm', { street: deleteTarget?.street ?? '', city: deleteTarget?.city ?? '' })"
            test-id-prefix="geodata-delete"
            @confirm="confirmDelete"
        />
    </v-container>
</template>

<style scoped>
.geodata-page {
    max-width: 1200px;
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

.map-status-card {
    margin-bottom: 16px;
    padding: 12px 16px;
}

.map-services {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 24px;
}

.map-service {
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.map-hint {
    margin: 8px 0 0;
    font-size: 0.875rem;
}

.card-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 16px;
    padding: 12px 16px;
}

.filter-field {
    flex: 1 1 240px;
    max-width: 360px;
}

.count {
    margin-left: auto;
    white-space: nowrap;
}

.coords {
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(var(--v-theme-on-surface), 0.06);
    font-size: 0.875rem;
    white-space: nowrap;
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

.field-row {
    display: flex;
    gap: 12px;
}

.field-row > * {
    flex: 1;
}

.postal-field {
    flex: 0 0 140px;
}

.country-field {
    max-width: 160px;
}

.hint-line {
    margin: 0;
}
</style>
