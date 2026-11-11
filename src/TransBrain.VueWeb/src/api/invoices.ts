import axios from 'axios';
import { userManager } from '../auth/userManager';
import type { AddressPayload, Order, PagedResult } from './orders';

export interface InvoiceLine {
    position: number;
    transportOrderId: string;
    orderNumber: string;
    description: string;
    netAmount: number;
}

export interface Invoice {
    id: string;
    invoiceNumber: string;
    customerId: string;
    customerName: string;
    billingAddress: AddressPayload;
    customerVatId: string | null;
    paymentTermDays: number;
    vatRatePercent: number;
    status: string;
    createdAt: string;
    issuedAt: string | null;
    dueDate: string | null;
    paidAt: string | null;
    netTotal: number;
    vatAmount: number;
    grossTotal: number;
    lines: InvoiceLine[];
}

export interface InvoiceLinePayload {
    orderId: string;
    netAmount: number;
    description: string | null;
}

export interface CreateInvoiceRequest {
    customerId: string;
    vatRatePercent: number;
    lines: InvoiceLinePayload[];
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

export async function listInvoices(
    status?: string | null,
    pageSize?: number,
    page?: number,
): Promise<PagedResult<Invoice>> {
    // An omitted status must not become the string "null" in the query string - the API
    // rejects an unknown status with a 400 rather than ignoring it (same as orders).
    const params: Record<string, string> = {};
    if (status) {
        params.status = status;
    }
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (page) {
        params.page = String(page);
    }

    const response = await client.get<PagedResult<Invoice>>('/invoices', { params });
    return response.data;
}

export async function getInvoice(id: string): Promise<Invoice> {
    const response = await client.get<Invoice>(`/invoices/${id}`);
    return response.data;
}

/** Delivered orders that are not yet on a non-cancelled invoice. */
export async function listBillableOrders(): Promise<Order[]> {
    const response = await client.get<Order[]>('/invoices/billable-orders');
    return response.data;
}

export async function createInvoice(request: CreateInvoiceRequest): Promise<Invoice> {
    const response = await client.post<Invoice>('/invoices', request);
    return response.data;
}

// All three are state transitions, not updates - POSTs to sub-resources returning the
// invoice's new state, mirroring the order cancel endpoint.
export async function issueInvoice(id: string): Promise<Invoice> {
    const response = await client.post<Invoice>(`/invoices/${id}/issue`);
    return response.data;
}

export async function markInvoicePaid(id: string): Promise<Invoice> {
    const response = await client.post<Invoice>(`/invoices/${id}/pay`);
    return response.data;
}

export async function cancelInvoice(id: string): Promise<Invoice> {
    const response = await client.post<Invoice>(`/invoices/${id}/cancel`);
    return response.data;
}

// Document downloads: the shared client's interceptor adds the token, so plain blob GETs suffice.
export async function downloadInvoicePdf(id: string): Promise<Blob> {
    const response = await client.get<Blob>(`/invoices/${id}/pdf`, { responseType: 'blob' });
    return response.data;
}

export async function downloadInvoiceXRechnung(id: string): Promise<Blob> {
    const response = await client.get<Blob>(`/invoices/${id}/xrechnung`, { responseType: 'blob' });
    return response.data;
}

/** Hands a downloaded document blob to the browser as a named file download. */
export function saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
}
