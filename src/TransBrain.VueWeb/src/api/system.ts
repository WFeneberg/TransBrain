import axios from 'axios';
import { userManager } from '../auth/userManager';

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

/**
 * Which tenant (Mandant) the system administrator is currently working ON. System admins carry
 * no tenant of their own, so every maintenance call needs this choice first; sessionStorage is
 * the single source of truth so the api modules read it at call time, while the Pinia store in
 * stores/systemTenant.ts wraps it reactively for the shell.
 */
export const SYSTEM_TENANT_STORAGE_KEY = 'transbrain.system.tenant';

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Every tenant that owns rows, for the tenant picker. */
export async function getTenants(): Promise<TenantSummary[]> {
    const response = await client.get<TenantSummary[]>('/system/tenants');
    return response.data;
}

/** `/system/tenants/{id}` for the chosen tenant; the shell guarantees a choice exists. */
export function systemApiBase(): string {
    const tenantId = sessionStorage.getItem(SYSTEM_TENANT_STORAGE_KEY);
    if (tenantId === null) {
        throw new Error('No tenant selected for system maintenance.');
    }
    return `/system/tenants/${encodeURIComponent(tenantId)}`;
}
