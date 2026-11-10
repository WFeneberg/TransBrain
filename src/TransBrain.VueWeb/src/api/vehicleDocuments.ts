import axios from 'axios';
import { userManager } from '../auth/userManager';

/** A special document ("Sonderpapier") of a vehicle. Dates are "YYYY-MM-DD". */
export interface VehicleDocument {
    id: string;
    vehicleId: string;
    /** Code of a DocumentType catalog entry, see api/vehicleCatalog.ts; one per type and vehicle. */
    documentType: string;
    lastCheckedOn: string | null;
    validUntil: string | null;
    remark: string | null;
    /** Read-only, set by the server on every create and update. */
    modifiedAt: string;
    modifiedBy: string | null;
}

/** Body shared by create and update - the ids come from the route, not the payload. */
export interface VehicleDocumentWriteRequest {
    documentType: string;
    lastCheckedOn: string | null;
    validUntil: string | null;
    remark: string | null;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Ordered by validUntil (documents without one last), then by type. */
export async function listVehicleDocuments(vehicleId: string): Promise<VehicleDocument[]> {
    const response = await client.get<VehicleDocument[]>(`/vehicles/${vehicleId}/documents`);
    return response.data;
}

/** Answers 409 when the vehicle already has a document of that type. */
export async function createVehicleDocument(
    vehicleId: string,
    request: VehicleDocumentWriteRequest,
): Promise<VehicleDocument> {
    const response = await client.post<VehicleDocument>(`/vehicles/${vehicleId}/documents`, request);
    return response.data;
}

export async function updateVehicleDocument(
    vehicleId: string,
    documentId: string,
    request: VehicleDocumentWriteRequest,
): Promise<VehicleDocument> {
    const response = await client.put<VehicleDocument>(`/vehicles/${vehicleId}/documents/${documentId}`, request);
    return response.data;
}

export async function deleteVehicleDocument(vehicleId: string, documentId: string): Promise<void> {
    await client.delete(`/vehicles/${vehicleId}/documents/${documentId}`);
}
