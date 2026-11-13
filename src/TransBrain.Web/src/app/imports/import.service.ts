import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** The target entities the API offers for imports; mirrors GET /api/imports/targets. */
export type ImportFieldKind = 'Text' | 'Integer' | 'Decimal' | 'Date' | 'Boolean' | 'TextList';

export interface ImportTargetField {
    name: string;
    label: string;
    kind: ImportFieldKind;
    required: boolean;
    description: string | null;
    allowedValues: string[] | null;
}

export interface ImportTarget {
    entity: string;
    label: string;
    fields: ImportTargetField[];
}

/** One analyzed source table with up to five sample rows for the mapping UI. */
export interface ImportTable {
    name: string;
    columns: string[];
    rowCount: number;
    sampleRows: (string | null)[][];
}

export type ImportSessionStatus = 'Ready' | 'Completed' | 'CompletedWithErrors' | 'Failed';

/** An import session. `tables` is null in list views (GET /api/imports). */
export interface ImportSession {
    id: string;
    sourceType: string;
    sourceName: string;
    status: ImportSessionStatus;
    createdBy: string;
    createdAtUtc: string;
    completedAtUtc: string | null;
    targetEntity: string | null;
    tableName: string | null;
    importedCount: number;
    failedCount: number;
    tables: ImportTable[] | null;
}

export type ImportDatabaseProvider = 'PostgreSql' | 'SqlServer' | 'MySql' | 'Odbc';

/** A column-to-field assignment; `constantValue` fills the field when the cell is empty. */
export interface ImportFieldMapping {
    targetField: string;
    sourceColumn: string | null;
    constantValue: string | null;
}

export interface ImportMappingRequest {
    tableName: string;
    targetEntity: string;
    mappings: ImportFieldMapping[];
}

export interface ImportPreviewRow {
    rowNumber: number;
    values: Record<string, string | null>;
    errors: string[];
}

export interface ImportPreview {
    totalRows: number;
    validRows: number;
    invalidRows: number;
    rows: ImportPreviewRow[];
}

export interface ImportRowError {
    rowNumber: number;
    message: string;
}

export interface ImportExecution {
    sessionId: string;
    status: ImportSessionStatus;
    totalRows: number;
    importedCount: number;
    failedCount: number;
    errors: ImportRowError[];
}

/** The file extensions POST /api/imports/files accepts. */
export const IMPORT_FILE_EXTENSIONS = [
    '.csv',
    '.json',
    '.xml',
    '.xlsx',
    '.mdb',
    '.accdb',
    '.sqlite',
    '.db',
] as const;

/** The API rejects larger uploads with 413; checked client-side to save the round trip. */
export const IMPORT_MAX_FILE_BYTES = 25 * 1024 * 1024;

@Injectable({ providedIn: 'root' })
export class ImportService {
    private readonly http = inject(HttpClient);

    targets(): Observable<ImportTarget[]> {
        return this.http.get<ImportTarget[]>('/api/imports/targets');
    }

    uploadFile(file: File): Observable<ImportSession> {
        const form = new FormData();
        form.append('file', file, file.name);
        return this.http.post<ImportSession>('/api/imports/files', form);
    }

    connectDatabase(
        provider: ImportDatabaseProvider,
        connectionString: string,
    ): Observable<ImportSession> {
        return this.http.post<ImportSession>('/api/imports/databases', {
            provider,
            connectionString,
        });
    }

    list(): Observable<ImportSession[]> {
        return this.http.get<ImportSession[]>('/api/imports');
    }

    getById(id: string): Observable<ImportSession> {
        return this.http.get<ImportSession>(`/api/imports/${id}`);
    }

    preview(id: string, request: ImportMappingRequest, maxRows: number): Observable<ImportPreview> {
        return this.http.post<ImportPreview>(`/api/imports/${id}/preview`, {
            ...request,
            maxRows,
        });
    }

    execute(id: string, request: ImportMappingRequest): Observable<ImportExecution> {
        return this.http.post<ImportExecution>(`/api/imports/${id}/execute`, request);
    }

    remove(id: string): Observable<void> {
        return this.http.delete<void>(`/api/imports/${id}`);
    }
}
