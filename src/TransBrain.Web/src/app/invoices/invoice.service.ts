import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AddressPayload, Order, PagedResult } from '../orders/order.service';

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

@Injectable({ providedIn: 'root' })
export class InvoiceService {
    private readonly http = inject(HttpClient);

    list(status?: string | null, pageSize?: number, page?: number): Observable<PagedResult<Invoice>> {
        // An omitted status must not become the string "null" in the query string - the API
        // rejects an unknown status with a 400 rather than ignoring it (same as orders).
        let params = new HttpParams();
        if (status) {
            params = params.set('status', status);
        }
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        if (page) {
            params = params.set('page', String(page));
        }
        return this.http.get<PagedResult<Invoice>>('/api/invoices', { params });
    }

    getById(id: string): Observable<Invoice> {
        return this.http.get<Invoice>(`/api/invoices/${id}`);
    }

    /** Delivered orders that are not yet on a non-cancelled invoice. */
    billableOrders(): Observable<Order[]> {
        return this.http.get<Order[]>('/api/invoices/billable-orders');
    }

    create(request: CreateInvoiceRequest): Observable<Invoice> {
        return this.http.post<Invoice>('/api/invoices', request);
    }

    // All three are state transitions, not updates - POSTs to sub-resources returning the
    // invoice's new state, mirroring the order cancel endpoint.
    issue(id: string): Observable<Invoice> {
        return this.http.post<Invoice>(`/api/invoices/${id}/issue`, null);
    }

    markPaid(id: string): Observable<Invoice> {
        return this.http.post<Invoice>(`/api/invoices/${id}/pay`, null);
    }

    cancel(id: string): Observable<Invoice> {
        return this.http.post<Invoice>(`/api/invoices/${id}/cancel`, null);
    }
}

export function invoiceStatusTone(status: string): string {
    switch (status) {
        case 'Paid':
            return 'success';
        case 'Issued':
            return 'warning';
        case 'Cancelled':
            return 'error';
        default:
            return 'neutral';
    }
}
