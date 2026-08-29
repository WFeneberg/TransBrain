import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Driver {
    id: string;
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    status: string;
    externalUserId: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface DriverWriteRequest {
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    externalUserId: string | null;
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

export async function listDrivers(): Promise<PagedResult<Driver>> {
    const response = await client.get<PagedResult<Driver>>('/drivers');
    return response.data;
}

export async function getDriver(id: string): Promise<Driver> {
    const response = await client.get<Driver>(`/drivers/${id}`);
    return response.data;
}

export async function createDriver(request: DriverWriteRequest): Promise<Driver> {
    const response = await client.post<Driver>('/drivers', request);
    return response.data;
}

export async function updateDriver(id: string, request: DriverWriteRequest): Promise<Driver> {
    const response = await client.put<Driver>(`/drivers/${id}`, request);
    return response.data;
}

export async function deleteDriver(id: string): Promise<void> {
    await client.delete(`/drivers/${id}`);
}
