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
