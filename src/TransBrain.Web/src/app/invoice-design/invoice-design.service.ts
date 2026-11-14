import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** The writable part of a tenant's invoice design - what PUT saves and the preview renders. */
export interface InvoiceDesignPayload {
    companyName: string;
    street: string;
    postalCode: string;
    city: string;
    countryCode: string;
    vatId: string;
    taxNumber: string | null;
    email: string;
    phone: string | null;
    iban: string;
    bic: string | null;
    bankName: string | null;
    accentColor: string;
    fontSizePoints: number;
    logoPosition: string;
    logoBase64: string | null;
    logoContentType: string | null;
    headerText: string | null;
    footerText: string | null;
}

export interface InvoiceDesign extends InvoiceDesignPayload {
    /** False while the tenant still runs on the deployment defaults - nothing saved yet. */
    isCustomized: boolean;
    updatedAt: string | null;
}

/** Mirrors the domain's logo limits: PNG or JPEG, at most 1 MiB of raw image data. */
export const MAX_LOGO_BYTES = 1024 * 1024;
export const ALLOWED_LOGO_TYPES = ['image/png', 'image/jpeg'] as const;

@Injectable({ providedIn: 'root' })
export class InvoiceDesignService {
    private readonly http = inject(HttpClient);

    // A singleton per-tenant resource, hence no ids in the routes (see InvoiceDesignEndpoints).
    getDesign(): Observable<InvoiceDesign> {
        return this.http.get<InvoiceDesign>('/api/invoice-design');
    }

    updateDesign(payload: InvoiceDesignPayload): Observable<InvoiceDesign> {
        return this.http.put<InvoiceDesign>('/api/invoice-design', payload);
    }

    /** Renders a sample invoice PDF with the submitted - possibly unsaved - design. */
    previewDesign(payload: InvoiceDesignPayload): Observable<Blob> {
        return this.http.post('/api/invoice-design/preview', payload, { responseType: 'blob' });
    }
}
