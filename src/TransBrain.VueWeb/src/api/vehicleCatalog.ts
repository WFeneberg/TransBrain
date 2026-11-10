import axios from 'axios';
import { userManager } from '../auth/userManager';

/** The maintainable selection lists behind the vehicle form - see VehicleCatalog.vue. */
export type VehicleCatalogKind = 'VehicleClass' | 'CarrierGroup';

export const VEHICLE_CATALOG_KINDS: readonly VehicleCatalogKind[] = ['VehicleClass', 'CarrierGroup'];

export interface VehicleCatalogEntry {
    id: string;
    kind: VehicleCatalogKind;
    /** Stored on the vehicle; unique per kind (case-insensitive) and immutable after create. */
    code: string;
    /** User data, shown as-is in every language. */
    name: string;
    /** Inactive entries stay valid on vehicles that already use them, but are not offered for new selection. */
    isActive: boolean;
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

/** Display name for a stored code: the entry's name, the code itself if unknown, '' if none. */
export function catalogName(entries: readonly VehicleCatalogEntry[], code: string | null | undefined): string {
    if (!code) {
        return '';
    }
    const wanted = code.toLowerCase();
    return entries.find((entry) => entry.code.toLowerCase() === wanted)?.name ?? code;
}
