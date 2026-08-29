import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface AddressPayload {
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
}

export interface Order {
    id: string;
    orderNumber: string;
    consignor: AddressPayload;
    consignee: AddressPayload;
    cargoDescription: string;
    cargoWeightKg: number;
    cargoLoadMeters: number;
    pickupFrom: string;
    pickupTo: string;
    deliveryFrom: string;
    deliveryTo: string;
    status: string;
    createdAt: string;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface OrderWriteRequest {
    consignor: AddressPayload;
    consignee: AddressPayload;
    cargoDescription: string;
    cargoWeightKg: number;
    cargoLoadMeters: number;
    pickupFrom: string;
    pickupTo: string;
    deliveryFrom: string;
    deliveryTo: string;
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

export async function listOrders(status?: string | null): Promise<PagedResult<Order>> {
    // An omitted status must not become the string "null"/"undefined" in the query string:
    // the API rejects an unknown status with a 400 rather than ignoring it.
    const response = await client.get<PagedResult<Order>>('/orders', {
        params: status ? { status } : undefined,
    });
    return response.data;
}

export async function getOrder(id: string): Promise<Order> {
    const response = await client.get<Order>(`/orders/${id}`);
    return response.data;
}

export async function createOrder(request: OrderWriteRequest): Promise<Order> {
    const response = await client.post<Order>('/orders', request);
    return response.data;
}

export async function updateOrder(id: string, request: OrderWriteRequest): Promise<Order> {
    const response = await client.put<Order>(`/orders/${id}`, request);
    return response.data;
}

/**
 * Cancelling is a state transition, not a deletion - hence a POST to a sub-resource rather
 * than a DELETE, and hence a response body carrying the order's new state.
 */
export async function cancelOrder(id: string): Promise<Order> {
    const response = await client.post<Order>(`/orders/${id}/cancel`);
    return response.data;
}
