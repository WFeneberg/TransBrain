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

export async function listVehicles(): Promise<PagedResult<Vehicle>> {
    const response = await client.get<PagedResult<Vehicle>>('/vehicles');
    return response.data;
}
