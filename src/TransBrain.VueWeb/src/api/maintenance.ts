import axios from 'axios';
import { userManager } from '../auth/userManager';
import { systemApiBase } from './system';

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

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Downloads the chosen tenant's backup; the server names the file, the client honors it. */
export async function downloadBackup(): Promise<BackupDownload> {
    const response = await client.get<Blob>(`${systemApiBase()}/backup`, { responseType: 'blob' });
    const disposition = (response.headers['content-disposition'] as string | undefined) ?? '';
    const match = /filename\*?=(?:UTF-8''|")?([^";\n]+)/i.exec(disposition);
    const fileName = match
        ? decodeURIComponent(match[1].replace(/"/g, ''))
        : 'transbrain-backup.json';
    return { blob: response.data, fileName };
}

/** Uploads a previously downloaded backup; the server replaces the chosen tenant's data. */
export async function restoreBackup(backup: TenantBackup): Promise<RestoreResult> {
    const response = await client.post<RestoreResult>(`${systemApiBase()}/restore`, backup);
    return response.data;
}

/** Hands the backup blob to the browser as a named file download. */
export function saveBackup(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
}
