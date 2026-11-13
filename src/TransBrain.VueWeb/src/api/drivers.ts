import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Driver {
    id: string;
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    status: string;
    externalUserId: string | null;
    socialInsurance: DriverSocialInsurance;
    documents: DriverDocuments;
    clothingSizes: DriverClothingSizes;
    relatives: DriverRelative[];
    specialDocuments: DriverSpecialDocument[];
    phoneNumbers: DriverPhoneNumber[];
}

/** The social insurance data ("Sozv-Daten") of a driver. Every value is optional. */
export interface DriverSocialInsurance {
    dateOfBirth: string | null;
    placeOfBirth: string | null;
    nationality: string | null;
    socialInsuranceNumber: string | null;
    entryDate: string | null;
    healthInsurance: string | null;
    bankName: string | null;
    bankCode: string | null;
    accountNumber: string | null;
    bic: string | null;
    iban: string | null;
    familyDoctor: string | null;
    familyDoctorPhone: string | null;
}

/** Driving licence document details. Every value is optional. */
export interface DriverDocuments {
    licenseNumber: string | null;
    issuingAuthority: string | null;
    issueDate: string | null;
}

/** Work clothing sizes - free text, the legacy app used pick lists. */
export interface DriverClothingSizes {
    clothingSize: string | null;
    shoeSize: string | null;
    gloveSize: string | null;
}

/** An emergency contact / relative of the driver. Only the last name is required. */
export interface DriverRelative {
    lastName: string;
    firstName: string | null;
    relationship: string | null;
    dateOfBirth: string | null;
    phone: string | null;
}

/** A special paper such as an ADR certificate. Only the name is required. */
export interface DriverSpecialDocument {
    /** Code of a DriverDocumentType entry of the driver catalog; the UI shows the entry's name. */
    type: string;
    lastInspectionDate: string | null;
    validUntil: string | null;
    comment: string | null;
}

/** An additional phone number of the driver. Only the number is required. */
export interface DriverPhoneNumber {
    number: string;
    type: string | null;
    comment: string | null;
}

/** Body shared by create and update - the API takes the id from the route on update, not the payload. */
export interface DriverWriteRequest {
    firstName: string;
    lastName: string;
    licenseClasses: string[];
    licenseValidUntil: string;
    externalUserId: string | null;
    socialInsurance: DriverSocialInsurance;
    documents: DriverDocuments;
    clothingSizes: DriverClothingSizes;
    relatives: DriverRelative[];
    specialDocuments: DriverSpecialDocument[];
    phoneNumbers: DriverPhoneNumber[];
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

/**
 * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY driver to choose
 * from - the tour form - has to ask for more, or a recently added one simply is not in the list
 * and cannot be chosen. Capped at 100 by the API.
 */
export async function listDrivers(pageSize?: number, status?: string | null): Promise<PagedResult<Driver>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (status) {
        params.status = status;
    }

    const response = await client.get<PagedResult<Driver>>('/drivers', { params });
    return response.data;
}

export async function getDriver(id: string): Promise<Driver> {
    const response = await client.get<Driver>(`/drivers/${id}`);
    return response.data;
}

export async function createDriver(request: DriverWriteRequest): Promise<Driver> {
    const response = await client.post<Driver>('/drivers', request);
    return response.data;
}

export async function updateDriver(id: string, request: DriverWriteRequest): Promise<Driver> {
    const response = await client.put<Driver>(`/drivers/${id}`, request);
    return response.data;
}

export async function deleteDriver(id: string): Promise<void> {
    await client.delete(`/drivers/${id}`);
}
