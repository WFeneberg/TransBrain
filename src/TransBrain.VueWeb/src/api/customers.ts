import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Customer {
    id: string;
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
    vatId: string | null;
    paymentTermDays: number;
    additional: CustomerAdditionalData;
}

/**
 * The additional data of a customer ("Zusatz"). Every value is optional; the counts default to 0
 * and the flags to false. `customerClass` is a code of the 'CustomerClass' catalog.
 */
export interface CustomerAdditionalData {
    secondaryName: string | null;
    secondaryStreet: string | null;
    secondaryPostalCode: string | null;
    secondaryCity: string | null;
    /** ISO 3166-1 alpha-2 code. */
    secondaryCountry: string | null;
    /** ISO date (yyyy-MM-dd). */
    foundedOn: string | null;
    workerCount: number;
    salariedEmployeeCount: number;
    requiresPalletCheck: boolean;
    excludeTollFromInvoice: boolean;
    reimburseToll: boolean;
    customerClass: string | null;
    comment: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface CustomerWriteRequest {
    name: string;
    street: string;
    postalCode: string;
    city: string;
    country: string;
    contactPerson: string | null;
    email: string | null;
    phone: string | null;
    vatId: string | null;
    paymentTermDays: number;
    /** Replaces the stored additional data as a whole; leaving it out clears it. */
    additional: CustomerAdditionalData;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

export async function listCustomers(page = 1, pageSize = 100): Promise<PagedResult<Customer>> {
    const response = await client.get<PagedResult<Customer>>('/customers', { params: { page, pageSize } });
    return response.data;
}

export async function getCustomer(id: string): Promise<Customer> {
    const response = await client.get<Customer>(`/customers/${id}`);
    return response.data;
}

export async function createCustomer(request: CustomerWriteRequest): Promise<Customer> {
    const response = await client.post<Customer>('/customers', request);
    return response.data;
}

export async function updateCustomer(id: string, request: CustomerWriteRequest): Promise<Customer> {
    const response = await client.put<Customer>(`/customers/${id}`, request);
    return response.data;
}

export async function deleteCustomer(id: string): Promise<void> {
    await client.delete(`/customers/${id}`);
}
