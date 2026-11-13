import axios from 'axios';
import { userManager } from '../auth/userManager';

/**
 * The maintainable selection lists - see VehicleCatalog.vue. Despite the name the API also keeps
 * the customer classes here, maintained on '/customer-catalog', and the driver phone types,
 * maintained on '/driver-catalog'.
 */
export type VehicleCatalogKind =
    | 'VehicleClass'
    | 'CarrierGroup'
    | 'InsuranceCompany'
    | 'DocumentType'
    | 'CustomerClass'
    | 'PhoneType'
    | 'DriverDocumentType';

/** The kinds behind the vehicle form, shown on '/vehicle-catalog'. */
export const VEHICLE_CATALOG_KINDS: readonly VehicleCatalogKind[] = [
    'VehicleClass',
    'CarrierGroup',
    'InsuranceCompany',
    'DocumentType',
];

/** The kinds behind the customer form, shown on '/customer-catalog'. */
export const CUSTOMER_CATALOG_KINDS: readonly VehicleCatalogKind[] = ['CustomerClass'];

/** The kinds behind the driver form, shown on '/driver-catalog'. */
export const DRIVER_CATALOG_KINDS: readonly VehicleCatalogKind[] = ['PhoneType', 'DriverDocumentType'];

export interface VehicleCatalogEntry {
    id: string;
    kind: VehicleCatalogKind;
    /** Stored on the vehicle; unique per kind (case-insensitive) and immutable after create. */
    code: string;
    /** User data, shown as-is in every language. */
    name: string;
    /** Inactive entries stay valid on vehicles (or customers) that already use them, but are not offered for new selection. */
    isActive: boolean;
    /** Number of vehicles - for 'CustomerClass' customers, for the driver kinds drivers - using the entry. */
    usageCount: number;
}

export interface VehicleCatalogCreateRequest {
    code: string;
    name: string;
    isActive: boolean;
}

export interface VehicleCatalogUpdateRequest {
    name: string;
    isActive: boolean;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** All entries of a kind, inactive ones included, ordered by name. */
export async function listVehicleCatalog(kind: VehicleCatalogKind): Promise<VehicleCatalogEntry[]> {
    const response = await client.get<VehicleCatalogEntry[]>(`/vehicle-catalog/${kind}`);
    return response.data;
}

export async function createVehicleCatalogEntry(
    kind: VehicleCatalogKind,
    request: VehicleCatalogCreateRequest,
): Promise<VehicleCatalogEntry> {
    const response = await client.post<VehicleCatalogEntry>(`/vehicle-catalog/${kind}`, request);
    return response.data;
}

export async function updateVehicleCatalogEntry(
    kind: VehicleCatalogKind,
    id: string,
    request: VehicleCatalogUpdateRequest,
): Promise<VehicleCatalogEntry> {
    const response = await client.put<VehicleCatalogEntry>(`/vehicle-catalog/${kind}/${id}`, request);
    return response.data;
}

/** Answers 409 while any vehicle still uses the entry - deactivate it instead. */
export async function deleteVehicleCatalogEntry(kind: VehicleCatalogKind, id: string): Promise<void> {
    await client.delete(`/vehicle-catalog/${kind}/${id}`);
}

/**
 * Select options for a catalog-backed field. Only active entries are offered for a new choice.
 * The current code stays selectable even when it has been deactivated since - the API accepts
 * keeping it - and is labelled by `inactiveLabel`; a code the catalog does not know at all is
 * shown as-is.
 */
export function catalogSelectOptions(
    entries: readonly VehicleCatalogEntry[],
    current: string | null | undefined,
    inactiveLabel: (name: string) => string,
): { title: string; value: string }[] {
    const currentKey = current?.toLowerCase() ?? null;
    const options = entries
        .filter((entry) => entry.isActive || entry.code.toLowerCase() === currentKey)
        .map((entry) => ({ title: entry.isActive ? entry.name : inactiveLabel(entry.name), value: entry.code }));
    if (current && !entries.some((entry) => entry.code.toLowerCase() === currentKey)) {
        options.push({ title: current, value: current });
    }
    return options;
}

/** Display name for a stored code: the entry's name, the code itself if unknown, '' if none. */
export function catalogName(entries: readonly VehicleCatalogEntry[], code: string | null | undefined): string {
    if (!code) {
        return '';
    }
    const wanted = code.toLowerCase();
    return entries.find((entry) => entry.code.toLowerCase() === wanted)?.name ?? code;
}
