import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface TourStop {
    sequence: number;
    transportOrderId: string;
    orderNumber: string;
    stopType: string;
}

export interface Tour {
    id: string;
    tourDate: string;
    vehicleId: string;
    vehicleLicensePlate: string;
    driverId: string;
    driverName: string;
    status: string;
    totalWeightKg: number;
    totalLoadMeters: number;
    vehiclePayloadKg: number;
    vehicleLoadMeters: number;
    stops: TourStop[];
}

export interface TourWriteRequest {
    tourDate: string;
    vehicleId: string;
    driverId: string;
}

export interface TourFilters {
    tourDate?: string | null;
    vehicleId?: string | null;
    driverId?: string | null;
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

export async function listTours(filters: TourFilters = {}): Promise<PagedResult<Tour>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (filters.tourDate) {
        params.tourDate = filters.tourDate;
    }
    if (filters.vehicleId) {
        params.vehicleId = filters.vehicleId;
    }
    if (filters.driverId) {
        params.driverId = filters.driverId;
    }

    const response = await client.get<PagedResult<Tour>>('/tours', { params });
    return response.data;
}

export async function getTour(id: string): Promise<Tour> {
    const response = await client.get<Tour>(`/tours/${id}`);
    return response.data;
}

export async function createTour(request: TourWriteRequest): Promise<Tour> {
    const response = await client.post<Tour>('/tours', request);
    return response.data;
}

export async function assignOrderToTour(tourId: string, transportOrderId: string): Promise<Tour> {
    const response = await client.post<Tour>(`/tours/${tourId}/orders`, { transportOrderId });
    return response.data;
}

/**
 * Removing an order is a DELETE, unlike cancelling one: the stops really go away and the order
 * returns to Draft, so it can be planned onto another tour.
 */
export async function removeOrderFromTour(tourId: string, transportOrderId: string): Promise<Tour> {
    const response = await client.delete<Tour>(`/tours/${tourId}/orders/${transportOrderId}`);
    return response.data;
}

export async function startTour(id: string): Promise<Tour> {
    const response = await client.post<Tour>(`/tours/${id}/start`);
    return response.data;
}

export async function completeTour(id: string): Promise<Tour> {
    const response = await client.post<Tour>(`/tours/${id}/complete`);
    return response.data;
}
