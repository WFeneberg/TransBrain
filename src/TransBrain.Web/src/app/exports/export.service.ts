import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { SystemTenantService } from '../system/system-tenant.service';

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

@Injectable({ providedIn: 'root' })
export class ExportService {
    private readonly http = inject(HttpClient);
    private readonly tenant = inject(SystemTenantService);

    /** The exportable master-data sets of the chosen tenant with their current row counts. */
    getTargets(): Observable<ExportTarget[]> {
        return this.http.get<ExportTarget[]>(`${this.tenant.apiBase()}/exports/targets`);
    }

    /** Downloads one data set in the chosen format; the server names the file. */
    download(entity: string, format: ExportFormat): Observable<ExportDownload> {
        return this.http
            .get(`${this.tenant.apiBase()}/exports/${encodeURIComponent(entity)}`, {
                params: { format },
                responseType: 'blob',
                observe: 'response',
            })
            .pipe(map((response) => ({ blob: response.body!, fileName: fileNameOf(response) })));
    }
}

function fileNameOf(response: HttpResponse<Blob>): string {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const match = /filename\*?=(?:UTF-8''|")?([^";\n]+)/i.exec(disposition);
    return match ? decodeURIComponent(match[1].replace(/"/g, '')) : 'transbrain-export';
}
