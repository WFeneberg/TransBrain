import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Vehicle {
    id: string;
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    status: string;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface VehicleWriteRequest {
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/**
 * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY vehicle to choose
 * from - the tour form - has to ask for more, or a recently added one simply is not in the list
 * and cannot be chosen. Capped at 100 by the API.
 * @param status Filters server-side (VehicleEndpoints.cs). The home page asks with pageSize 1 and
 * reads only totalCount, so a fleet count costs one row over the wire.
 */
export async function listVehicles(pageSize?: number, status?: string | null): Promise<PagedResult<Vehicle>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (status) {
        params.status = status;
    }

    const response = await client.get<PagedResult<Vehicle>>('/vehicles', { params });
    return response.data;
}

export async function getVehicle(id: string): Promise<Vehicle> {
    const response = await client.get<Vehicle>(`/vehicles/${id}`);
    return response.data;
}

export async function createVehicle(request: VehicleWriteRequest): Promise<Vehicle> {
    const response = await client.post<Vehicle>('/vehicles', request);
    return response.data;
}

export async function updateVehicle(id: string, request: VehicleWriteRequest): Promise<Vehicle> {
    const response = await client.put<Vehicle>(`/vehicles/${id}`, request);
    return response.data;
}

export async function deleteVehicle(id: string): Promise<void> {
    await client.delete(`/vehicles/${id}`);
}
