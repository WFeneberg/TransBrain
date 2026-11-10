import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const VEHICLE_TYPES = ['Tractor', 'RigidTruck', 'Van'] as const;
export type VehicleType = (typeof VEHICLE_TYPES)[number];

export const VEHICLE_STATUSES = ['Available', 'InWorkshop', 'Decommissioned'] as const;
export type VehicleStatus = (typeof VEHICLE_STATUSES)[number];

export const MARBLE_RACK_MATERIALS = ['Steel', 'Aluminium'] as const;
export type MarbleRackMaterial = (typeof MARBLE_RACK_MATERIALS)[number];

/** Optional equipment of a vehicle; PUT replaces it as a whole, so it is always sent complete. */
export interface VehicleEquipment {
    hasPartitionWall: boolean;
    hasPalletBasket: boolean;
    hasDangerousGoodsEquipment: boolean;
    isDangerousGoodsApproved: boolean;
    hasLiftAxle: boolean;
    hasDoubleDeck: boolean;
    hasDoubleDeckBeams: boolean;
    hasSmallLadder: boolean;
    hasLargeLadder: boolean;
    tankVolumeLiters: number | null;
    interiorLengthMm: number | null;
    interiorWidthMm: number | null;
    interiorHeightMm: number | null;
    supportPoleCount: number | null;
    loadBarCount: number | null;
    lashingStrapCount: number | null;
    palletCount: number | null;
    chainTensionerCount: number | null;
    marbleRackCount: number | null;
    marbleRackMaterial: MarbleRackMaterial | null;
}

export const EMPTY_VEHICLE_EQUIPMENT: Readonly<VehicleEquipment> = {
    hasPartitionWall: false,
    hasPalletBasket: false,
    hasDangerousGoodsEquipment: false,
    isDangerousGoodsApproved: false,
    hasLiftAxle: false,
    hasDoubleDeck: false,
    hasDoubleDeckBeams: false,
    hasSmallLadder: false,
    hasLargeLadder: false,
    tankVolumeLiters: null,
    interiorLengthMm: null,
    interiorWidthMm: null,
    interiorHeightMm: null,
    supportPoleCount: null,
    loadBarCount: null,
    lashingStrapCount: null,
    palletCount: null,
    chainTensionerCount: null,
    marbleRackCount: null,
    marbleRackMaterial: null,
};

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface VehicleWriteRequest {
    licensePlate: string;
    isLicensePlateConfidential: boolean;
    type: VehicleType;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    make: string | null;
    model: string | null;
    /** Month precision, always sent as "YYYY-MM-01". */
    manufacturedOn: string | null;
    powerKw: number | null;
    /** Code of a VehicleClass entry of the vehicle catalog (see vehicle-catalog.service.ts). */
    vehicleClass: string | null;
    /** Code of a CarrierGroup entry of the vehicle catalog. */
    carrierGroup: string | null;
    defaultDriverId: string | null;
    chassisNumber: string | null;
    engineNumber: string | null;
    lengthMm: number | null;
    widthMm: number | null;
    heightMm: number | null;
    curbWeightKg: number | null;
    grossWeightKg: number | null;
    maxFifthWheelLoadKg: number | null;
    notes: string | null;
    /** Omitted or null on write means "no equipment"; responses always carry it. */
    equipment: VehicleEquipment | null;
}

export interface Vehicle extends VehicleWriteRequest {
    id: string;
    status: VehicleStatus;
    /** Read-only audit fields, set by the server on every write and status change. */
    modifiedAt: string | null;
    modifiedBy: string | null;
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

    /**
     * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY vehicle to
     * choose from - the tour form - has to ask for more, or a recently added one simply is not
     * in the list and cannot be chosen. Capped at 100 by the API; a fleet larger than that
     * needs a searchable picker, not a bigger page.
     * @param status Filters server-side (VehicleEndpoints.cs). The home page asks with pageSize 1
     * and reads only totalCount, so a fleet count costs one row over the wire.
     */
    list(pageSize?: number, status?: string | null): Observable<PagedResult<Vehicle>> {
        let params = new HttpParams();
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        // An omitted status must not become the string "null" in the query string - the API
        // rejects an unknown status with a 400 rather than ignoring it.
        if (status) {
            params = params.set('status', status);
        }
        return this.http.get<PagedResult<Vehicle>>('/api/vehicles', { params });
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

    /** Available <-> InWorkshop, either -> Decommissioned; leaving Decommissioned answers 409. */
    changeStatus(id: string, status: VehicleStatus): Observable<Vehicle> {
        return this.http.post<Vehicle>(`/api/vehicles/${id}/status`, { status });
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/vehicles/${id}`);
    }
}
