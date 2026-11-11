import axios from 'axios';
import { userManager } from '../auth/userManager';

/** Where a row's coordinates come from; mirror of GeodataSources in the API. */
export type GeodataSource = 'manual' | 'geocoded' | 'unresolved';

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

/** One self-hosted map service: reachable or not, and the OSM data date it reports. */
export interface MapServiceStatus {
    available: boolean;
    osmDataUpdatedAt: string | null;
}

/** State of the map material: geocoder (Nominatim) and route planner (OpenRouteService). */
export interface MapDataStatus {
    geocoder: MapServiceStatus;
    routing: MapServiceStatus;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Every known address with its current resolution state, including orphaned overrides. */
export async function listGeodata(): Promise<GeodataEntry[]> {
    const response = await client.get<GeodataEntry[]>('/geodata');
    return response.data;
}

/** Creates or updates the manual override for the address (identified case-insensitively). */
export async function setGeodataOverride(request: GeodataOverrideRequest): Promise<GeodataOverride> {
    const response = await client.put<GeodataOverride>('/geodata/overrides', request);
    return response.data;
}

/** The address falls back to the geocoder (or unresolved) afterwards. */
export async function deleteGeodataOverride(id: string): Promise<void> {
    await client.delete(`/geodata/overrides/${id}`);
}

/** Resolves now (override, cache or live geocoder); a geocoder success fills the cache. */
export async function resolveGeodata(request: GeodataResolveRequest): Promise<GeodataPosition> {
    const response = await client.post<GeodataPosition>('/geodata/resolve', request);
    return response.data;
}

/** Whether the self-hosted map services are up and how old their map data is. */
export async function getMapDataStatus(): Promise<MapDataStatus> {
    const response = await client.get<MapDataStatus>('/geodata/map-status');
    return response.data;
}
