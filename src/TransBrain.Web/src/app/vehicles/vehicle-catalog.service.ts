import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** The kinds behind the vehicle form, shown on '/vehicle-catalog'. */
export const VEHICLE_CATALOG_KINDS = [
    'VehicleClass',
    'CarrierGroup',
    'InsuranceCompany',
    'DocumentType',
] as const;

/**
 * The kinds behind the customer form, shown on '/customer-catalog'. Despite the name the API keeps
 * the customer classes in the same catalog.
 */
export const CUSTOMER_CATALOG_KINDS = ['CustomerClass'] as const;

/**
 * The kinds behind the driver form, shown on '/driver-catalog'. Like the customer classes they
 * live in the shared catalog of the API.
 */
export const DRIVER_CATALOG_KINDS = ['PhoneType', 'DriverDocumentType'] as const;

/**
 * The kinds behind the order form (toll categories and units of the additional charges), shown
 * on '/order-catalog'. They live in the shared catalog of the API as well.
 */
export const ORDER_CATALOG_KINDS = ['TollCategory', 'Unit'] as const;

export type VehicleCatalogKind =
    | (typeof VEHICLE_CATALOG_KINDS)[number]
    | (typeof CUSTOMER_CATALOG_KINDS)[number]
    | (typeof DRIVER_CATALOG_KINDS)[number]
    | (typeof ORDER_CATALOG_KINDS)[number];

export interface VehicleCatalogEntry {
    id: string;
    kind: VehicleCatalogKind;
    /** Stored on the vehicle; unique per kind (case-insensitive) and immutable after create. */
    code: string;
    /** User data, shown as-is in every language. */
    name: string;
    /** Inactive entries stay valid where already used, but are not offered for new selection. */
    isActive: boolean;
    /** Number of vehicles - for 'CustomerClass' customers, for the driver kinds drivers - using the entry. */
    usageCount: number;
}

export interface VehicleCatalogCreateRequest {
    code: string;
    name: string;
    isActive: boolean;
}

export interface VehicleCatalogUpdateRequest {
    name: string;
    isActive: boolean;
}

/** One selectable option of a vehicle-form select built from a catalog. */
export interface VehicleCatalogOption {
    code: string;
    name: string;
    isActive: boolean;
}

/**
 * The options a select offers: every active entry, plus the vehicle's current code when that
 * entry is inactive (or no longer in the catalog at all), so an existing value is never lost.
 */
export function catalogOptions(
    entries: readonly VehicleCatalogEntry[],
    currentCode: string | null,
): VehicleCatalogOption[] {
    const options: VehicleCatalogOption[] = entries
        .filter((entry) => entry.isActive || sameCode(entry.code, currentCode))
        .map(({ code, name, isActive }) => ({ code, name, isActive }));
    if (currentCode && !options.some((option) => sameCode(option.code, currentCode))) {
        options.push({ code: currentCode, name: currentCode, isActive: false });
    }
    return options;
}

/** Display name for a stored code: the catalog name, falling back to the code itself. */
export function catalogName(
    entries: ReadonlyMap<string, VehicleCatalogEntry>,
    code: string | null,
): string | null {
    if (!code) {
        return null;
    }
    return entries.get(code.toLowerCase())?.name ?? code;
}

/** Indexes entries by lower-cased code - codes are unique per kind case-insensitively. */
export function catalogIndex(
    entries: readonly VehicleCatalogEntry[],
): Map<string, VehicleCatalogEntry> {
    return new Map(entries.map((entry) => [entry.code.toLowerCase(), entry]));
}

function sameCode(a: string, b: string | null): boolean {
    return b !== null && a.toLowerCase() === b.toLowerCase();
}

@Injectable({ providedIn: 'root' })
export class VehicleCatalogService {
    private readonly http = inject(HttpClient);

    /** All entries of a kind, including inactive ones, ordered by name. */
    list(kind: VehicleCatalogKind): Observable<VehicleCatalogEntry[]> {
        return this.http.get<VehicleCatalogEntry[]>(`/api/vehicle-catalog/${kind}`);
    }

    create(
        kind: VehicleCatalogKind,
        request: VehicleCatalogCreateRequest,
    ): Observable<VehicleCatalogEntry> {
        return this.http.post<VehicleCatalogEntry>(`/api/vehicle-catalog/${kind}`, request);
    }

    update(
        kind: VehicleCatalogKind,
        id: string,
        request: VehicleCatalogUpdateRequest,
    ): Observable<VehicleCatalogEntry> {
        return this.http.put<VehicleCatalogEntry>(`/api/vehicle-catalog/${kind}/${id}`, request);
    }

    /** Answers 409 while any vehicle (or customer, or driver) still uses the entry. */
    remove(kind: VehicleCatalogKind, id: string): Observable<void> {
        return this.http.delete<void>(`/api/vehicle-catalog/${kind}/${id}`);
    }
}
