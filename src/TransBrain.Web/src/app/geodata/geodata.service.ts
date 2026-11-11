import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Where a row's coordinates come from; mirror of GeodataSources in the API. */
export const GEODATA_SOURCES = {
    manual: 'manual',
    geocoded: 'geocoded',
    unresolved: 'unresolved',
} as const;

export type GeodataSource = (typeof GEODATA_SOURCES)[keyof typeof GEODATA_SOURCES];

/** One address the system knows, merged from orders, customer locations and manual overrides. */
export interface GeodataEntry {
    street: string;
    postalCode: string;
    city: string;
    country: string;
    latitude: number | null;
    longitude: number | null;
    source: GeodataSource;
    /** Set when a manual override exists; needed to delete it. */
    overrideId: string | null;
    orderCount: number;
    locationCount: number;
    /** false for an override whose address no longer appears in any order or location. */
    inUse: boolean;
    updatedAt: string | null;
}

export interface GeodataOverrideRequest {
    street: string;
    postalCode: string;
    city: string;
    country: string;
    latitude: number;
    longitude: number;
}

export interface GeodataOverride {
    id: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    latitude: number;
    longitude: number;
    updatedAt: string;
}

export interface GeodataResolveRequest {
    street: string;
    postalCode: string;
    city: string;
    country: string;
}

export interface GeodataPosition {
    latitude: number;
    longitude: number;
    source: GeodataSource;
}

@Injectable({ providedIn: 'root' })
export class GeodataService {
    private readonly http = inject(HttpClient);

    /** Every known address with its current resolution state, including orphaned overrides. */
    list(): Observable<GeodataEntry[]> {
        return this.http.get<GeodataEntry[]>('/api/geodata');
    }

    /** Creates or updates the manual override for the address (identified case-insensitively). */
    setOverride(request: GeodataOverrideRequest): Observable<GeodataOverride> {
        return this.http.put<GeodataOverride>('/api/geodata/overrides', request);
    }

    /** The address falls back to the geocoder (or unresolved) afterwards. */
    deleteOverride(id: string): Observable<void> {
        return this.http.delete<void>(`/api/geodata/overrides/${id}`);
    }

    /** Resolves now (override, cache or live geocoder); a geocoder success fills the cache. */
    resolve(request: GeodataResolveRequest): Observable<GeodataPosition> {
        return this.http.post<GeodataPosition>('/api/geodata/resolve', request);
    }
}
