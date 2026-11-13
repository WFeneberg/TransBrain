import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

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

/** A special paper such as a dangerous-goods certificate. The type is a DriverDocumentType catalog code. */
export interface DriverSpecialDocument {
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

@Injectable({ providedIn: 'root' })
export class DriverService {
    private readonly http = inject(HttpClient);

    /**
     * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY driver to
     * choose from - the tour form - has to ask for more, or a recently added one simply is not
     * in the list and cannot be chosen. Capped at 100 by the API; a fleet larger than that
     * needs a searchable picker, not a bigger page.
     * @param status Filters server-side (DriverEndpoints.cs). The home page asks with pageSize 1
     * and reads only totalCount, so a driver count costs one row over the wire.
     */
    list(pageSize?: number, status?: string | null): Observable<PagedResult<Driver>> {
        let params = new HttpParams();
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        // An omitted status must not become the string "null" in the query string - the API
        // rejects an unknown status with a 400 rather than ignoring it.
        if (status) {
            params = params.set('status', status);
        }
        return this.http.get<PagedResult<Driver>>('/api/drivers', { params });
    }

    getById(id: string): Observable<Driver> {
        return this.http.get<Driver>(`/api/drivers/${id}`);
    }

    create(request: DriverWriteRequest): Observable<Driver> {
        return this.http.post<Driver>('/api/drivers', request);
    }

    update(id: string, request: DriverWriteRequest): Observable<Driver> {
        return this.http.put<Driver>(`/api/drivers/${id}`, request);
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/drivers/${id}`);
    }
}
