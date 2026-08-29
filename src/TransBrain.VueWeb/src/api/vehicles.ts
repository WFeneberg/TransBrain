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
 */
export async function listVehicles(pageSize?: number): Promise<PagedResult<Vehicle>> {
    const response = await client.get<PagedResult<Vehicle>>('/vehicles', {
        params: pageSize ? { pageSize: String(pageSize) } : undefined,
    });
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
