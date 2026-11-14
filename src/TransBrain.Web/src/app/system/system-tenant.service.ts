import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

/** Mirror of TenantSummary in the API: one tenant owning data, with headline row counts. */
export interface TenantSummary {
    tenantId: string;
    customers: number;
    drivers: number;
    vehicles: number;
    orders: number;
    tours: number;
    invoices: number;
    totalRows: number;
}

const STORAGE_KEY = 'transbrain.system.tenant';

/**
 * Which tenant (Mandant) the system administrator is currently working ON. System admins carry
 * no tenant of their own, so every maintenance call needs this choice first; it is kept in
 * sessionStorage so a reload inside one session does not lose it, but a fresh sign-in starts
 * neutral. The maintenance, import and export services build their URLs from this selection.
 */
@Injectable({ providedIn: 'root' })
export class SystemTenantService {
    private readonly http = inject(HttpClient);

    private readonly selected = signal<string | null>(sessionStorage.getItem(STORAGE_KEY));

    readonly selectedTenantId = this.selected.asReadonly();

    readonly hasSelection = computed(() => this.selected() !== null);

    /** Every tenant that owns rows, for the tenant picker. */
    getTenants(): Observable<TenantSummary[]> {
        return this.http.get<TenantSummary[]>('/api/system/tenants');
    }

    select(tenantId: string | null): void {
        if (tenantId === null) {
            sessionStorage.removeItem(STORAGE_KEY);
        } else {
            sessionStorage.setItem(STORAGE_KEY, tenantId);
        }
        this.selected.set(tenantId);
    }

    /** `/api/system/tenants/{id}` for the chosen tenant; the shell guarantees a choice exists. */
    apiBase(): string {
        const tenantId = this.selected();
        if (tenantId === null) {
            throw new Error('No tenant selected for system maintenance.');
        }
        return `/api/system/tenants/${encodeURIComponent(tenantId)}`;
    }
}
