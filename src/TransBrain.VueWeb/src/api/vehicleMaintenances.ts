import axios from 'axios';
import { userManager } from '../auth/userManager';

export const MAINTENANCE_KINDS = ['Service', 'Inspection', 'Repair', 'TireChange', 'Other'] as const;

export type VehicleMaintenanceKind = (typeof MAINTENANCE_KINDS)[number];

/** One workshop visit ("Wartung") of a vehicle. Dates are "YYYY-MM-DD". */
export interface VehicleMaintenance {
    id: string;
    vehicleId: string;
    kind: VehicleMaintenanceKind;
    performedOn: string;
    description: string;
    workshop: string | null;
    odometerKm: number | null;
    cost: number | null;
    nextDueOn: string | null;
    /** Read-only, set by the server on every create and update. */
    modifiedAt: string;
    modifiedBy: string | null;
}

/** Body shared by create and update - the ids come from the route, not the payload. */
export interface VehicleMaintenanceWriteRequest {
    kind: VehicleMaintenanceKind;
    performedOn: string;
    description: string;
    workshop: string | null;
    odometerKm: number | null;
    cost: number | null;
    nextDueOn: string | null;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Ordered by performedOn descending - the latest maintenance first. */
export async function listVehicleMaintenances(vehicleId: string): Promise<VehicleMaintenance[]> {
    const response = await client.get<VehicleMaintenance[]>(`/vehicles/${vehicleId}/maintenances`);
    return response.data;
}

export async function createVehicleMaintenance(
    vehicleId: string,
    request: VehicleMaintenanceWriteRequest,
): Promise<VehicleMaintenance> {
    const response = await client.post<VehicleMaintenance>(`/vehicles/${vehicleId}/maintenances`, request);
    return response.data;
}

export async function updateVehicleMaintenance(
    vehicleId: string,
    maintenanceId: string,
    request: VehicleMaintenanceWriteRequest,
): Promise<VehicleMaintenance> {
    const response = await client.put<VehicleMaintenance>(
        `/vehicles/${vehicleId}/maintenances/${maintenanceId}`,
        request,
    );
    return response.data;
}

export async function deleteVehicleMaintenance(vehicleId: string, maintenanceId: string): Promise<void> {
    await client.delete(`/vehicles/${vehicleId}/maintenances/${maintenanceId}`);
}
