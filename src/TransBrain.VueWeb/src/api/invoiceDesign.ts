import axios from 'axios';
import { userManager } from '../auth/userManager';

export type InvoiceLogoPosition = 'Left' | 'Right';

/** The writable design, body of PUT and of the preview POST. */
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
    logoPosition: InvoiceLogoPosition;
    logoBase64: string | null;
    logoContentType: string | null;
    headerText: string | null;
    footerText: string | null;
}

export interface InvoiceDesignResponse extends InvoiceDesignPayload {
    /** False while the tenant still renders with the deployment defaults. */
    isCustomized: boolean;
    updatedAt: string | null;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

export async function getInvoiceDesign(): Promise<InvoiceDesignResponse> {
    const response = await client.get<InvoiceDesignResponse>('/invoice-design');
    return response.data;
}

export async function updateInvoiceDesign(payload: InvoiceDesignPayload): Promise<InvoiceDesignResponse> {
    const response = await client.put<InvoiceDesignResponse>('/invoice-design', payload);
    return response.data;
}

/**
 * Renders a sample invoice with the submitted - possibly unsaved - design. A POST because the
 * design travels in the body; the answer is the PDF itself, not JSON.
 */
export async function previewInvoiceDesign(payload: InvoiceDesignPayload): Promise<Blob> {
    const response = await client.post<Blob>('/invoice-design/preview', payload, { responseType: 'blob' });
    return response.data;
}
