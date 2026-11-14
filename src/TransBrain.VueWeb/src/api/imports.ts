import axios from 'axios';
import { userManager } from '../auth/userManager';
import { systemApiBase } from './system';

/** Mirrors ImportFileTypes on the API; the server is the authority, this only informs the picker. */
export const IMPORT_FILE_EXTENSIONS = [
    '.csv',
    '.txt',
    '.json',
    '.xml',
    '.xlsx',
    '.mdb',
    '.accdb',
    '.sqlite',
    '.sqlite3',
    '.db',
] as const;

/** Mirrors ImportLimits.MaxFileBytes on the API (25 MB). */
export const IMPORT_MAX_FILE_MB = 25;

export const IMPORT_DATABASE_PROVIDERS = ['PostgreSql', 'SqlServer', 'MySql', 'Odbc'] as const;

export type ImportDatabaseProvider = (typeof IMPORT_DATABASE_PROVIDERS)[number];

export type ImportSessionStatus = 'Ready' | 'Completed' | 'CompletedWithErrors' | 'Failed';

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
    /** German display name, provided by the API. */
    label: string;
    fields: ImportTargetField[];
}

/** One analyzed source table, with up to five sample rows for the mapping UI. */
export interface ImportTable {
    name: string;
    columns: string[];
    rowCount: number;
    sampleRows: (string | null)[][];
}

/** An import session. `tables` is null in list views. */
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

/**
 * One edge of the mapping: which source column (and/or constant) fills a target field. When both
 * are set, the constant acts as the fallback for empty source cells.
 */
export interface ImportFieldMapping {
    targetField: string;
    sourceColumn: string | null;
    constantValue: string | null;
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
    /** Capped at 100 entries by the API. */
    errors: ImportRowError[];
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

export async function listImportTargets(): Promise<ImportTarget[]> {
    const response = await client.get<ImportTarget[]>(`${systemApiBase()}/imports/targets`);
    return response.data;
}

/** Uploads a source file; the 201 response already carries the analyzed tables. */
export async function createFileImportSession(file: File): Promise<ImportSession> {
    const form = new FormData();
    form.append('file', file);
    const response = await client.post<ImportSession>(`${systemApiBase()}/imports/files`, form);
    return response.data;
}

/** Connects to a live database once; the connection string is never stored by the API. */
export async function createDatabaseImportSession(
    provider: ImportDatabaseProvider,
    connectionString: string,
): Promise<ImportSession> {
    const response = await client.post<ImportSession>(`${systemApiBase()}/imports/databases`, { provider, connectionString });
    return response.data;
}

export async function listImportSessions(): Promise<ImportSession[]> {
    const response = await client.get<ImportSession[]>(`${systemApiBase()}/imports`);
    return response.data;
}

export async function getImportSession(id: string): Promise<ImportSession> {
    const response = await client.get<ImportSession>(`${systemApiBase()}/imports/${id}`);
    return response.data;
}

export async function previewImport(
    id: string,
    tableName: string,
    targetEntity: string,
    mappings: ImportFieldMapping[],
    maxRows = 20,
): Promise<ImportPreview> {
    const response = await client.post<ImportPreview>(`${systemApiBase()}/imports/${id}/preview`, {
        tableName,
        targetEntity,
        mappings,
        maxRows,
    });
    return response.data;
}

export async function executeImport(
    id: string,
    tableName: string,
    targetEntity: string,
    mappings: ImportFieldMapping[],
): Promise<ImportExecution> {
    const response = await client.post<ImportExecution>(`${systemApiBase()}/imports/${id}/execute`, {
        tableName,
        targetEntity,
        mappings,
    });
    return response.data;
}

export async function deleteImportSession(id: string): Promise<void> {
    await client.delete(`${systemApiBase()}/imports/${id}`);
}
