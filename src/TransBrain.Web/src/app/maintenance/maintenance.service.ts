import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

/** Mirror of TenantBackupDocument in the API; the client treats the tables as opaque. */
export interface TenantBackup {
    fileFormat: string;
    formatVersion: number;
    schemaVersion: string;
    tenantId: string;
    createdAt: string;
    tables: Record<string, unknown>;
}

export const BACKUP_FILE_FORMAT = 'transbrain-tenant-backup';

export interface RestoreResult {
    schemaVersion: string;
    backupCreatedAt: string;
    totalRows: number;
    tables: Record<string, number>;
}

export interface BackupDownload {
    blob: Blob;
    fileName: string;
}

@Injectable({ providedIn: 'root' })
export class MaintenanceService {
    private readonly http = inject(HttpClient);

    /** Downloads the tenant backup; the server names the file, the client honors it. */
    downloadBackup(): Observable<BackupDownload> {
        return this.http
            .get('/api/maintenance/backup', { responseType: 'blob', observe: 'response' })
            .pipe(map((response) => ({ blob: response.body!, fileName: fileNameOf(response) })));
    }

    /** Uploads a previously downloaded backup; the server replaces the tenant's data with it. */
    restore(backup: TenantBackup): Observable<RestoreResult> {
        return this.http.post<RestoreResult>('/api/maintenance/restore', backup);
    }
}

function fileNameOf(response: HttpResponse<Blob>): string {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const match = /filename\*?=(?:UTF-8''|")?([^";\n]+)/i.exec(disposition);
    return match ? decodeURIComponent(match[1].replace(/"/g, '')) : 'transbrain-backup.json';
}
