import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

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

@Injectable({ providedIn: 'root' })
export class OrderService {
    private readonly http = inject(HttpClient);

    /**
     * @param pageSize Orders come back sorted by order number ASCENDING, so the default page of
     * 20 is the twenty OLDEST matches. A caller that needs to choose among current orders — the
     * tour detail's assignable-order picker — must ask for more. The API caps this at 100; a
     * haulier with more than 100 open drafts will need a searchable picker, not a bigger page.
     */
    list(status?: string | null, pageSize?: number, page?: number): Observable<PagedResult<Order>> {
        // An omitted `status` must not become the string "null"/"undefined" in the query
        // string: the API rejects an unknown status with a 400 rather than ignoring it.
        let params = new HttpParams();
        if (status) {
            params = params.set('status', status);
        }
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        if (page) {
            params = params.set('page', String(page));
        }
        return this.http.get<PagedResult<Order>>('/api/orders', { params });
    }

    getById(id: string): Observable<Order> {
        return this.http.get<Order>(`/api/orders/${id}`);
    }

    create(request: OrderWriteRequest): Observable<Order> {
        return this.http.post<Order>('/api/orders', request);
    }

    update(id: string, request: OrderWriteRequest): Observable<Order> {
        return this.http.put<Order>(`/api/orders/${id}`, request);
    }

    /**
     * Cancelling is a state transition, not a deletion - hence a POST to a sub-resource
     * rather than a DELETE, and hence a response body carrying the order's new state.
     */
    cancel(id: string): Observable<Order> {
        return this.http.post<Order>(`/api/orders/${id}/cancel`, null);
    }
}
