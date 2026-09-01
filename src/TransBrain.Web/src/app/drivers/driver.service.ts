import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Driver {
    id: string;
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    status: string;
    externalUserId: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface DriverWriteRequest {
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    externalUserId: string | null;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class DriverService {
    private readonly http = inject(HttpClient);

    /**
     * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY driver to
     * choose from - the tour form - has to ask for more, or a recently added one simply is not
     * in the list and cannot be chosen. Capped at 100 by the API; a fleet larger than that
     * needs a searchable picker, not a bigger page.
     * @param status Filters server-side (DriverEndpoints.cs). The home page asks with pageSize 1
     * and reads only totalCount, so a driver count costs one row over the wire.
     */
    list(pageSize?: number, status?: string | null): Observable<PagedResult<Driver>> {
        let params = new HttpParams();
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        // An omitted status must not become the string "null" in the query string - the API
        // rejects an unknown status with a 400 rather than ignoring it.
        if (status) {
            params = params.set('status', status);
        }
        return this.http.get<PagedResult<Driver>>('/api/drivers', { params });
    }

    getById(id: string): Observable<Driver> {
        return this.http.get<Driver>(`/api/drivers/${id}`);
    }

    create(request: DriverWriteRequest): Observable<Driver> {
        return this.http.post<Driver>('/api/drivers', request);
    }

    update(id: string, request: DriverWriteRequest): Observable<Driver> {
        return this.http.put<Driver>(`/api/drivers/${id}`, request);
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/drivers/${id}`);
    }
}
