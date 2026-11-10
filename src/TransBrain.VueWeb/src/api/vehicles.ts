import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Vehicle {
    id: string;
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    status: VehicleStatus;
    isLicensePlateConfidential: boolean;
    make: string | null;
    model: string | null;
    /** "YYYY-MM-01" - the API stores the construction date with month precision. */
    manufacturedOn: string | null;
    powerKw: number | null;
    /** Code of a VehicleClass catalog entry, see api/vehicleCatalog.ts. */
    vehicleClass: string | null;
    /** Code of a CarrierGroup catalog entry, see api/vehicleCatalog.ts. */
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
    /** Free text kept verbatim - line breaks and leading spaces matter. */
    dataSheet: string | null;
    /** Never null in a response; all flags false and all values null when nothing is recorded. */
    equipment: VehicleEquipment;
    /** Never null in a response; all values null when nothing is recorded. */
    commercial: VehicleCommercial;
    /** Read-only, set by the server on every create, update and status change. */
    modifiedAt: string | null;
    modifiedBy: string | null;
}

export type VehicleStatus = 'Available' | 'InWorkshop' | 'Decommissioned';

export const VEHICLE_STATUSES: readonly VehicleStatus[] = ['Available', 'InWorkshop', 'Decommissioned'];
export const VEHICLE_TYPES = ['Tractor', 'RigidTruck', 'Van'] as const;

export type MarbleRackMaterial = 'Steel' | 'Aluminium';

export const MARBLE_RACK_MATERIALS: readonly MarbleRackMaterial[] = ['Steel', 'Aluminium'];

/** Equipment of the legacy fleet screen "Seite2". Numbers are whole numbers, null when not recorded. */
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

export type VehicleEquipmentFlag = {
    [K in keyof VehicleEquipment]: VehicleEquipment[K] extends boolean ? K : never;
}[keyof VehicleEquipment];

export type VehicleEquipmentNumber = {
    [K in keyof VehicleEquipment]: VehicleEquipment[K] extends number | null ? K : never;
}[keyof VehicleEquipment];

/** Commercial data and insurance of the legacy fleet screen "Seite3". Dates are "YYYY-MM-DD". */
export interface VehicleCommercial {
    /** EUR, at most two decimals. */
    purchasePrice: number | null;
    purchasedOn: string | null;
    purchaseOdometerKm: number | null;
    firstRegisteredOn: string | null;
    usefulLifeYears: number | null;
    usefulLifeKm: number | null;
    costCenter: string | null;
    /** Code of an InsuranceCompany catalog entry, see api/vehicleCatalog.ts. */
    vehicleInsurer: string | null;
    vehicleInsurancePolicyNumbers: string | null;
    /** Code of an InsuranceCompany catalog entry, see api/vehicleCatalog.ts. */
    transportInsurer: string | null;
    transportInsurancePolicyNumbers: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface VehicleWriteRequest {
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    isLicensePlateConfidential: boolean;
    make: string | null;
    model: string | null;
    manufacturedOn: string | null;
    powerKw: number | null;
    vehicleClass: string | null;
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
    dataSheet: string | null;
    /** PUT replaces the whole equipment, so the complete object is always sent. */
    equipment: VehicleEquipment;
    /** PUT replaces the whole commercial data, so the complete object is always sent. */
    commercial: VehicleCommercial;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/**
 * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY vehicle to choose
 * from - the tour form - has to ask for more, or a recently added one simply is not in the list
 * and cannot be chosen. Capped at 100 by the API.
 * @param status Filters server-side (VehicleEndpoints.cs). The home page asks with pageSize 1 and
 * reads only totalCount, so a fleet count costs one row over the wire.
 */
export async function listVehicles(pageSize?: number, status?: string | null): Promise<PagedResult<Vehicle>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (status) {
        params.status = status;
    }

    const response = await client.get<PagedResult<Vehicle>>('/vehicles', { params });
    return response.data;
}

export async function getVehicle(id: string): Promise<Vehicle> {
    const response = await client.get<Vehicle>(`/vehicles/${id}`);
    return response.data;
}

export async function createVehicle(request: VehicleWriteRequest): Promise<Vehicle> {
    const response = await client.post<Vehicle>('/vehicles', request);
    return response.data;
}

export async function updateVehicle(id: string, request: VehicleWriteRequest): Promise<Vehicle> {
    const response = await client.put<Vehicle>(`/vehicles/${id}`, request);
    return response.data;
}

/** Available <-> InWorkshop, either -> Decommissioned; a decommissioned vehicle answers 409. */
export async function changeVehicleStatus(id: string, status: VehicleStatus): Promise<Vehicle> {
    const response = await client.post<Vehicle>(`/vehicles/${id}/status`, { status });
    return response.data;
}

export async function deleteVehicle(id: string): Promise<void> {
    await client.delete(`/vehicles/${id}`);
}
