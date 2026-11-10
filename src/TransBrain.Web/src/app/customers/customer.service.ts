import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Customer {
    id: string;
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
    vatId: string | null;
    paymentTermDays: number;
    additional: CustomerAdditionalData;
}

/**
 * The additional data of a customer ("Zusatz"). Every value is optional; the counts default to 0
 * and the flags to false. `customerClass` is a code of the 'CustomerClass' catalog.
 */
export interface CustomerAdditionalData {
    secondaryName: string | null;
    secondaryStreet: string | null;
    secondaryPostalCode: string | null;
    secondaryCity: string | null;
    /** ISO 3166-1 alpha-2 code. */
    secondaryCountry: string | null;
    /** ISO date (yyyy-MM-dd). */
    foundedOn: string | null;
    workerCount: number;
    salariedEmployeeCount: number;
    requiresPalletCheck: boolean;
    excludeTollFromInvoice: boolean;
    reimburseToll: boolean;
    customerClass: string | null;
    comment: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface CustomerWriteRequest {
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
    vatId: string | null;
    paymentTermDays: number;
    /** Replaces the stored additional data as a whole; leaving it out clears it. */
    additional: CustomerAdditionalData;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class CustomerService {
    private readonly http = inject(HttpClient);

    list(page = 1, pageSize = 100): Observable<PagedResult<Customer>> {
        const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
        return this.http.get<PagedResult<Customer>>('/api/customers', { params });
    }

    getById(id: string): Observable<Customer> {
        return this.http.get<Customer>(`/api/customers/${id}`);
    }

    create(request: CustomerWriteRequest): Observable<Customer> {
        return this.http.post<Customer>('/api/customers', request);
    }

    update(id: string, request: CustomerWriteRequest): Observable<Customer> {
        return this.http.put<Customer>(`/api/customers/${id}`, request);
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/customers/${id}`);
    }
}
