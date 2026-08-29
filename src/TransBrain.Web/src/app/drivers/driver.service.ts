import { HttpClient } from '@angular/common/http';
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

    list(): Observable<PagedResult<Driver>> {
        return this.http.get<PagedResult<Driver>>('/api/drivers');
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
