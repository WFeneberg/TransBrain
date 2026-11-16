import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const MAINTENANCE_KINDS = ['Service', 'Inspection', 'Repair', 'TireChange', 'Other'] as const;

export type VehicleMaintenanceKind = (typeof MAINTENANCE_KINDS)[number];

/** One workshop visit ("Wartung") of a vehicle; the entries form its maintenance history. */
export interface VehicleMaintenance {
    id: string;
    vehicleId: string;
    kind: VehicleMaintenanceKind;
    /** "YYYY-MM-DD". */
    performedOn: string;
    description: string;
    workshop: string | null;
    odometerKm: number | null;
    cost: number | null;
    /** "YYYY-MM-DD". */
    nextDueOn: string | null;
    modifiedAt: string | null;
    modifiedBy: string | null;
}

/** Body shared by create and update - the ids come from the route, not the payload. */
export interface VehicleMaintenanceWriteRequest {
    kind: VehicleMaintenanceKind;
    performedOn: string;
    description: string;
    workshop: string | null;
    odometerKm: number | null;
    cost: number | null;
    nextDueOn: string | null;
}

@Injectable({ providedIn: 'root' })
export class VehicleMaintenanceService {
    private readonly http = inject(HttpClient);

    /** Ordered by performedOn descending - the latest maintenance first. */
    list(vehicleId: string): Observable<VehicleMaintenance[]> {
        return this.http.get<VehicleMaintenance[]>(`/api/vehicles/${vehicleId}/maintenances`);
    }

    create(
        vehicleId: string,
        request: VehicleMaintenanceWriteRequest,
    ): Observable<VehicleMaintenance> {
        return this.http.post<VehicleMaintenance>(
            `/api/vehicles/${vehicleId}/maintenances`,
            request,
        );
    }

    update(
        vehicleId: string,
        maintenanceId: string,
        request: VehicleMaintenanceWriteRequest,
    ): Observable<VehicleMaintenance> {
        return this.http.put<VehicleMaintenance>(
            `/api/vehicles/${vehicleId}/maintenances/${maintenanceId}`,
            request,
        );
    }

    remove(vehicleId: string, maintenanceId: string): Observable<void> {
        return this.http.delete<void>(`/api/vehicles/${vehicleId}/maintenances/${maintenanceId}`);
    }
}
