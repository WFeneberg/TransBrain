import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Vehicle {
    id: string;
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    status: string;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface VehicleWriteRequest {
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class VehicleService {
    private readonly http = inject(HttpClient);

    list(): Observable<PagedResult<Vehicle>> {
        return this.http.get<PagedResult<Vehicle>>('/api/vehicles');
    }

    getById(id: string): Observable<Vehicle> {
        return this.http.get<Vehicle>(`/api/vehicles/${id}`);
    }

    create(request: VehicleWriteRequest): Observable<Vehicle> {
        return this.http.post<Vehicle>('/api/vehicles', request);
    }

    update(id: string, request: VehicleWriteRequest): Observable<Vehicle> {
        return this.http.put<Vehicle>(`/api/vehicles/${id}`, request);
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/vehicles/${id}`);
    }
}
