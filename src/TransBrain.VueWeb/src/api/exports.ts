import axios from 'axios';
import { userManager } from '../auth/userManager';

/** Mirror of ExportTargetResponse in the API. */
export interface ExportTarget {
    entity: string;
    label: string;
    rowCount: number;
}

/** The file formats the export endpoint can produce. */
export type ExportFormat = 'Csv' | 'Json' | 'Xlsx';

export interface ExportDownload {
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

/** The exportable master-data sets with their current row counts. */
export async function getExportTargets(): Promise<ExportTarget[]> {
    const response = await client.get<ExportTarget[]>('/exports/targets');
    return response.data;
}

/** Downloads one data set in the chosen format; the server names the file. */
export async function downloadExport(entity: string, format: ExportFormat): Promise<ExportDownload> {
    const response = await client.get<Blob>(`/exports/${encodeURIComponent(entity)}`, {
        params: { format },
        responseType: 'blob',
    });
    const disposition = (response.headers['content-disposition'] as string | undefined) ?? '';
    const match = /filename\*?=(?:UTF-8''|")?([^";\n]+)/i.exec(disposition);
    const fileName = match ? decodeURIComponent(match[1].replace(/"/g, '')) : 'transbrain-export';
    return { blob: response.data, fileName };
}

/** Hands the export blob to the browser as a named file download. */
export function saveExport(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
}
