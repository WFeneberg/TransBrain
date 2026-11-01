import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface CustomerLocation {
    id: string;
    customerId: string;
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
}

/** Body shared by create and update - the ids come from the route, not the payload. */
export interface CustomerLocationWriteRequest {
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
}

/** A delivery location with its customer's name, for pickers across all customers. */
export interface CustomerLocationOption extends CustomerLocation {
    customerName: string;
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

/** Every delivery location of every customer; the order form picks from this list. */
export async function listAllCustomerLocations(pageSize = 100): Promise<PagedResult<CustomerLocationOption>> {
    const response = await client.get<PagedResult<CustomerLocationOption>>('/customer-locations', {
        params: { pageSize },
    });
    return response.data;
}

export async function listCustomerLocations(customerId: string): Promise<CustomerLocation[]> {
    const response = await client.get<CustomerLocation[]>(`/customers/${customerId}/locations`);
    return response.data;
}

export async function getCustomerLocation(customerId: string, locationId: string): Promise<CustomerLocation | undefined> {
    const locations = await listCustomerLocations(customerId);
    return locations.find((l) => l.id === locationId);
}

export async function createCustomerLocation(
    customerId: string,
    request: CustomerLocationWriteRequest,
): Promise<CustomerLocation> {
    const response = await client.post<CustomerLocation>(`/customers/${customerId}/locations`, request);
    return response.data;
}

export async function updateCustomerLocation(
    customerId: string,
    locationId: string,
    request: CustomerLocationWriteRequest,
): Promise<CustomerLocation> {
    const response = await client.put<CustomerLocation>(`/customers/${customerId}/locations/${locationId}`, request);
    return response.data;
}

export interface CustomerLocationPosition {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
}

export async function getCustomerLocationPosition(
    customerId: string,
    locationId: string,
): Promise<CustomerLocationPosition> {
    const response = await client.get<CustomerLocationPosition>(`/customers/${customerId}/locations/${locationId}/position`);
    return response.data;
}

export async function deleteCustomerLocation(customerId: string, locationId: string): Promise<void> {
    await client.delete(`/customers/${customerId}/locations/${locationId}`);
}
