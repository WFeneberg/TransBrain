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
    consigneeLocationId: string | null;
    /** Special remarks for dispatch ("Besonderes"). */
    notes: string | null;
    /** Special remarks printed on the order confirmation. */
    confirmationNotes: string | null;
    requiresPartitionWall: boolean;
    requiresDoubleDeck: boolean;
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
    consigneeLocationId: string | null;
    notes: string | null;
    confirmationNotes: string | null;
    requiresPartitionWall: boolean;
    requiresDoubleDeck: boolean;
}

/** One toll record ("Mautdaten") of an order; the amounts are computed by the API. */
export interface OrderToll {
    id: string;
    orderId: string;
    /** Code of a TollCategory entry of the catalog; the UI shows the entry's name. */
    tollCategory: string;
    emptyKm: number;
    loadedKm: number;
    factorPerKm: number;
    emptyAmount: number;
    loadedAmount: number;
    modifiedAt: string;
    modifiedBy: string | null;
}

/** Body shared by toll create and update - the ids come from the route. */
export interface OrderTollWriteRequest {
    tollCategory: string;
    emptyKm: number;
    loadedKm: number;
    factorPerKm: number;
}

/** One additional charge position ("Zusatzposition") of an order; the total is computed by the API. */
export interface OrderCharge {
    id: string;
    orderId: string;
    description: string;
    quantity: number;
    /** Code of a Unit entry of the catalog, or null for a bare quantity. */
    unit: string | null;
    unitPrice: number;
    total: number;
    modifiedAt: string;
    modifiedBy: string | null;
}

/** Body shared by charge create and update - the ids come from the route. */
export interface OrderChargeWriteRequest {
    description: string;
    quantity: number;
    unit: string | null;
    unitPrice: number;
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
 * @param pageSize Orders come back sorted by order number ASCENDING, so the default page of 20
 * is the twenty OLDEST matches. A caller choosing among current orders - TourDetail's
 * assignable-order picker - must ask for more. The API caps this at 100.
 */
export async function listOrders(
    status?: string | null,
    pageSize?: number,
    page?: number,
): Promise<PagedResult<Order>> {
    // An omitted status must not become the string "null"/"undefined" in the query string:
    // the API rejects an unknown status with a 400 rather than ignoring it.
    const params: Record<string, string> = {};
    if (status) {
        params.status = status;
    }
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (page) {
        params.page = String(page);
    }

    const response = await client.get<PagedResult<Order>>('/orders', { params });
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

export interface OrderLocation {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
}

/** A party is null when its address could not be located - the rest of the order still renders. */
export interface OrderLocations {
    consignor: OrderLocation | null;
    consignee: OrderLocation | null;
}

/** Fails with 503 when the routing/geocoding service is not configured or unreachable. */
export async function getOrderLocations(id: string): Promise<OrderLocations> {
    const response = await client.get<OrderLocations>(`/orders/${id}/locations`);
    return response.data;
}

/** Oldest first. Toll records stay editable after dispatch: the km arrive after the trip. */
export async function listOrderTolls(orderId: string): Promise<OrderToll[]> {
    const response = await client.get<OrderToll[]>(`/orders/${orderId}/tolls`);
    return response.data;
}

export async function createOrderToll(
    orderId: string,
    request: OrderTollWriteRequest,
): Promise<OrderToll> {
    const response = await client.post<OrderToll>(`/orders/${orderId}/tolls`, request);
    return response.data;
}

export async function updateOrderToll(
    orderId: string,
    tollId: string,
    request: OrderTollWriteRequest,
): Promise<OrderToll> {
    const response = await client.put<OrderToll>(`/orders/${orderId}/tolls/${tollId}`, request);
    return response.data;
}

export async function removeOrderToll(orderId: string, tollId: string): Promise<void> {
    await client.delete(`/orders/${orderId}/tolls/${tollId}`);
}

/** Oldest first, like the tolls. */
export async function listOrderCharges(orderId: string): Promise<OrderCharge[]> {
    const response = await client.get<OrderCharge[]>(`/orders/${orderId}/charges`);
    return response.data;
}

export async function createOrderCharge(
    orderId: string,
    request: OrderChargeWriteRequest,
): Promise<OrderCharge> {
    const response = await client.post<OrderCharge>(`/orders/${orderId}/charges`, request);
    return response.data;
}

export async function updateOrderCharge(
    orderId: string,
    chargeId: string,
    request: OrderChargeWriteRequest,
): Promise<OrderCharge> {
    const response = await client.put<OrderCharge>(
        `/orders/${orderId}/charges/${chargeId}`,
        request,
    );
    return response.data;
}

export async function removeOrderCharge(orderId: string, chargeId: string): Promise<void> {
    await client.delete(`/orders/${orderId}/charges/${chargeId}`);
}
