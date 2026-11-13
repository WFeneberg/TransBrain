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

/** One party's location; `null` in OrderLocations means its address could not be geocoded. */
export interface LocatedParty {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
}

export interface OrderLocations {
    consignor: LocatedParty | null;
    consignee: LocatedParty | null;
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

    /** 503 when routing/geocoding is not configured or unreachable. */
    getLocations(id: string): Observable<OrderLocations> {
        return this.http.get<OrderLocations>(`/api/orders/${id}/locations`);
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

    /** Oldest first. Toll records stay editable after dispatch: the km arrive after the trip. */
    listTolls(orderId: string): Observable<OrderToll[]> {
        return this.http.get<OrderToll[]>(`/api/orders/${orderId}/tolls`);
    }

    createToll(orderId: string, request: OrderTollWriteRequest): Observable<OrderToll> {
        return this.http.post<OrderToll>(`/api/orders/${orderId}/tolls`, request);
    }

    updateToll(
        orderId: string,
        tollId: string,
        request: OrderTollWriteRequest,
    ): Observable<OrderToll> {
        return this.http.put<OrderToll>(`/api/orders/${orderId}/tolls/${tollId}`, request);
    }

    removeToll(orderId: string, tollId: string): Observable<void> {
        return this.http.delete<void>(`/api/orders/${orderId}/tolls/${tollId}`);
    }

    /** Oldest first, like the tolls. */
    listCharges(orderId: string): Observable<OrderCharge[]> {
        return this.http.get<OrderCharge[]>(`/api/orders/${orderId}/charges`);
    }

    createCharge(orderId: string, request: OrderChargeWriteRequest): Observable<OrderCharge> {
        return this.http.post<OrderCharge>(`/api/orders/${orderId}/charges`, request);
    }

    updateCharge(
        orderId: string,
        chargeId: string,
        request: OrderChargeWriteRequest,
    ): Observable<OrderCharge> {
        return this.http.put<OrderCharge>(`/api/orders/${orderId}/charges/${chargeId}`, request);
    }

    removeCharge(orderId: string, chargeId: string): Observable<void> {
        return this.http.delete<void>(`/api/orders/${orderId}/charges/${chargeId}`);
    }
}
