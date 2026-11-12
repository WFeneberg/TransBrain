import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Order } from '../orders/order.service';

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

export interface TourRouteStop {
    sequence: number;
    orderNumber: string;
    stopType: string;
    countryCode: string;
    place: string;
    latitude: number;
    longitude: number;
}

export interface TourRouteScheduleEntry {
    kind: string;
    start: string;
    end: string;
    countryCode: string | null;
}

export interface TourRouteViolation {
    severity: string;
    code: string;
    message: string;
    countryCode: string | null;
}

export interface TourRoute {
    tourId: string;
    departure: string;
    end: string;
    earliestNextStart: string | null;
    distanceMeters: number;
    drivingSeconds: number;
    stops: TourRouteStop[];
    geometry: { latitude: number; longitude: number }[];
    schedule: TourRouteScheduleEntry[];
    violations: TourRouteViolation[];
}

export interface FittingOrders {
    tourId: string;
    remainingWeightKg: number;
    remainingLoadMeters: number;
    orders: Order[];
}

export interface TourFilters {
    tourDate?: string | null;
    vehicleId?: string | null;
    driverId?: string | null;
    /**
     * The API defaults to 20 rows and tours come back sorted ascending, so without this the list
     * shows the twenty OLDEST tours and hides everything planned since - the same trap the
     * vehicle, driver and order clients already ask past. Capped at 100 by the API.
     */
    pageSize?: number | null;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

export const TOUR_STATUSES = ['Planned', 'InProgress', 'Completed'] as const;

/** The status chip colour, shared by the tour list, the tour detail and the home page. */
export function tourStatusTone(status: string): string {
    switch (status) {
        case 'Completed':
            return 'success';
        case 'InProgress':
            return 'warning';
        default:
            return 'neutral';
    }
}

@Injectable({ providedIn: 'root' })
export class TourService {
    private readonly http = inject(HttpClient);

    list(filters: TourFilters = {}): Observable<PagedResult<Tour>> {
        // Only the filters that are actually set are appended. An omitted one must not become
        // the string "null" in the query string - the API would reject that with a 400.
        let params = new HttpParams();
        if (filters.tourDate) {
            params = params.set('tourDate', filters.tourDate);
        }
        if (filters.vehicleId) {
            params = params.set('vehicleId', filters.vehicleId);
        }
        if (filters.driverId) {
            params = params.set('driverId', filters.driverId);
        }
        if (filters.pageSize) {
            params = params.set('pageSize', String(filters.pageSize));
        }
        return this.http.get<PagedResult<Tour>>('/api/tours', { params });
    }

    getById(id: string): Observable<Tour> {
        return this.http.get<Tour>(`/api/tours/${id}`);
    }

    create(request: TourWriteRequest): Observable<Tour> {
        return this.http.post<Tour>('/api/tours', request);
    }

    assignOrder(tourId: string, transportOrderId: string): Observable<Tour> {
        return this.http.post<Tour>(`/api/tours/${tourId}/orders`, { transportOrderId });
    }

    /**
     * Removing an order is a DELETE, unlike cancelling one: the stops really go away and the
     * order returns to Draft, so it can be planned onto another tour.
     */
    removeOrder(tourId: string, transportOrderId: string): Observable<Tour> {
        return this.http.delete<Tour>(`/api/tours/${tourId}/orders/${transportOrderId}`);
    }

    /** 409 when the tour cannot be routed, 404 when unknown, 503 when routing is not configured or unreachable. */
    getRoute(id: string): Observable<TourRoute> {
        return this.http.get<TourRoute>(`/api/tours/${id}/route`);
    }

    /** Draft orders that still fit into the tour's remaining capacity. Empty once the tour has started. */
    fittingOrders(id: string): Observable<FittingOrders> {
        return this.http.get<FittingOrders>(`/api/tours/${id}/fitting-orders`);
    }

    /** Reorders the stops to the shortest found route. 409 when not editable, 503 without geodata. */
    optimize(id: string): Observable<Tour> {
        return this.http.post<Tour>(`/api/tours/${id}/optimize`, null);
    }

    start(id: string): Observable<Tour> {
        return this.http.post<Tour>(`/api/tours/${id}/start`, null);
    }

    complete(id: string): Observable<Tour> {
        return this.http.post<Tour>(`/api/tours/${id}/complete`, null);
    }
}
