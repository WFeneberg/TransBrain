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

/**
 * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY driver to choose
 * from - the tour form - has to ask for more, or a recently added one simply is not in the list
 * and cannot be chosen. Capped at 100 by the API.
 */
export async function listDrivers(pageSize?: number, status?: string | null): Promise<PagedResult<Driver>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (status) {
        params.status = status;
    }

    const response = await client.get<PagedResult<Driver>>('/drivers', { params });
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
