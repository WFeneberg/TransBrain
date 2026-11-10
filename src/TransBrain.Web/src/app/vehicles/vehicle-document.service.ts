import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** A special document ("Sonderpapier") of a vehicle, e.g. an ADR certificate. */
export interface VehicleDocument {
    id: string;
    vehicleId: string;
    /** Code of a DocumentType entry of the vehicle catalog; the UI shows the entry's name. */
    documentType: string;
    /** "YYYY-MM-DD". */
    lastCheckedOn: string | null;
    /** "YYYY-MM-DD". */
    validUntil: string | null;
    remark: string | null;
    modifiedAt: string | null;
    modifiedBy: string | null;
}

/** Body shared by create and update - the ids come from the route, not the payload. */
export interface VehicleDocumentWriteRequest {
    documentType: string;
    lastCheckedOn: string | null;
    validUntil: string | null;
    remark: string | null;
}

@Injectable({ providedIn: 'root' })
export class VehicleDocumentService {
    private readonly http = inject(HttpClient);

    /** Ordered by validUntil ascending (empty last), then by type. */
    list(vehicleId: string): Observable<VehicleDocument[]> {
        return this.http.get<VehicleDocument[]>(`/api/vehicles/${vehicleId}/documents`);
    }

    /** Answers 409 when the vehicle already has a document of that type. */
    create(vehicleId: string, request: VehicleDocumentWriteRequest): Observable<VehicleDocument> {
        return this.http.post<VehicleDocument>(`/api/vehicles/${vehicleId}/documents`, request);
    }

    update(
        vehicleId: string,
        documentId: string,
        request: VehicleDocumentWriteRequest,
    ): Observable<VehicleDocument> {
        return this.http.put<VehicleDocument>(
            `/api/vehicles/${vehicleId}/documents/${documentId}`,
            request,
        );
    }

    remove(vehicleId: string, documentId: string): Observable<void> {
        return this.http.delete<void>(`/api/vehicles/${vehicleId}/documents/${documentId}`);
    }
}
