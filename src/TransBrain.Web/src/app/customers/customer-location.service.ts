import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from './customer.service';

/** A contact person at a delivery location; only the name is required. */
export interface CustomerLocationContact {
    name: string;
    phone: string | null;
    email: string | null;
}

export interface CustomerLocation {
    id: string;
    customerId: string;
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    companyName: string | null;
    vatId: string | null;
    contacts: CustomerLocationContact[];
}

/** A delivery location together with its customer's name, for pickers across all customers. */
export interface CustomerLocationOption extends CustomerLocation {
    customerName: string;
}

/** Body shared by create and update - the ids come from the route, not the payload. contacts replaces the whole list. */
export interface CustomerLocationWriteRequest {
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    companyName: string | null;
    vatId: string | null;
    contacts: CustomerLocationContact[];
}

/** A delivery location resolved to coordinates, for drawing it on the map. */
export interface CustomerLocationPosition {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
}

@Injectable({ providedIn: 'root' })
export class CustomerLocationService {
    private readonly http = inject(HttpClient);

    /** Every delivery location of every customer; the order form picks from this list. */
    listAll(pageSize = 100): Observable<PagedResult<CustomerLocationOption>> {
        return this.http.get<PagedResult<CustomerLocationOption>>('/api/customer-locations', {
            params: { pageSize: String(pageSize) },
        });
    }

    list(customerId: string): Observable<CustomerLocation[]> {
        return this.http.get<CustomerLocation[]>(`/api/customers/${customerId}/locations`);
    }

    create(customerId: string, request: CustomerLocationWriteRequest): Observable<CustomerLocation> {
        return this.http.post<CustomerLocation>(`/api/customers/${customerId}/locations`, request);
    }

    update(customerId: string, locationId: string, request: CustomerLocationWriteRequest): Observable<CustomerLocation> {
        return this.http.put<CustomerLocation>(`/api/customers/${customerId}/locations/${locationId}`, request);
    }

    remove(customerId: string, locationId: string): Observable<void> {
        return this.http.delete<void>(`/api/customers/${customerId}/locations/${locationId}`);
    }

    position(customerId: string, locationId: string): Observable<CustomerLocationPosition> {
        return this.http.get<CustomerLocationPosition>(`/api/customers/${customerId}/locations/${locationId}/position`);
    }

    getById(customerId: string, locationId: string): Observable<CustomerLocation | undefined> {
        return new Observable((subscriber) => {
            this.list(customerId).subscribe({
                next: (locations) => {
                    subscriber.next(locations.find((l) => l.id === locationId));
                    subscriber.complete();
                },
                error: (error) => subscriber.error(error),
            });
        });
    }
}
