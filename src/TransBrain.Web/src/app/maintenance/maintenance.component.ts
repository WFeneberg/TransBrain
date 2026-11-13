import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { saveBlob } from '../invoices/invoice.service';
import {
    BACKUP_FILE_FORMAT,
    MaintenanceService,
    RestoreResult,
    TenantBackup,
} from './maintenance.service';

/** What the component shows about a chosen backup file before the admin commits to it. */
interface PendingRestore {
    backup: TenantBackup;
    fileName: string;
    createdAt: Date;
    tableCount: number;
}

/**
 * The Wartung (maintenance) page: download a backup of the tenant's data, or replace the
 * tenant's data with a previously downloaded backup. Reached only by admins - nav link, route
 * guard and the API's Maintenance policy all check the same capability.
 */
@Component({
    selector: 'app-maintenance',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatButtonModule, MatCardModule, MatCheckboxModule, MatIconModule, MatProgressBarModule],
    template: `
        <div class="page">
            <header>
                <h1>{{ i18n.t('maintenance.title') }}</h1>
                <p class="subtitle">{{ i18n.t('maintenance.subtitle') }}</p>
            </header>

            <mat-card appearance="outlined">
                <mat-card-header>
                    <mat-card-title>{{ i18n.t('maintenance.backup.title') }}</mat-card-title>
                </mat-card-header>
                <mat-card-content>
                    <p>{{ i18n.t('maintenance.backup.description') }}</p>
                    @if (backupError()) {
                        <p class="error-banner" data-testid="maintenance-backup-error">{{ backupError() }}</p>
                    }
                </mat-card-content>
                <mat-card-actions>
                    <button
                        mat-flat-button
                        data-testid="maintenance-download"
                        [disabled]="downloading()"
                        (click)="download()"
                    >
                        <mat-icon>download</mat-icon>
                        {{ i18n.t('maintenance.backup.download') }}
                    </button>
                </mat-card-actions>
                @if (downloading()) {
                    <mat-progress-bar mode="indeterminate" />
                }
            </mat-card>

            <mat-card appearance="outlined">
                <mat-card-header>
                    <mat-card-title>{{ i18n.t('maintenance.restore.title') }}</mat-card-title>
                </mat-card-header>
                <mat-card-content>
                    <p>{{ i18n.t('maintenance.restore.description') }}</p>

                    <input
                        #fileInput
                        type="file"
                        accept=".json,application/json"
                        hidden
                        data-testid="maintenance-file-input"
                        (change)="onFileSelected(fileInput.files)"
                    />
                    <button mat-stroked-button data-testid="maintenance-choose-file" (click)="fileInput.click()">
                        <mat-icon>folder_open</mat-icon>
                        {{ i18n.t('maintenance.restore.selectFile') }}
                    </button>

                    @if (pending(); as file) {
                        <div class="file-info" data-testid="maintenance-file-info">
                            <p>
                                <strong>{{ file.fileName }}</strong>
                            </p>
                            <p>
                                {{
                                    i18n.t('maintenance.restore.fileInfo', {
                                        createdAt: file.createdAt.toLocaleString(i18n.locale()),
                                        tables: file.tableCount,
                                    })
                                }}
                            </p>
                        </div>

                        <div class="warning" data-testid="maintenance-warning">
                            <mat-icon>warning</mat-icon>
                            <span>{{ i18n.t('maintenance.restore.warning') }}</span>
                        </div>

                        <mat-checkbox data-testid="maintenance-confirm" [checked]="confirmed()" (change)="confirmed.set($event.checked)">
                            {{ i18n.t('maintenance.restore.confirm') }}
                        </mat-checkbox>
                    }

                    @if (restoreError()) {
                        <p class="error-banner" data-testid="maintenance-restore-error">{{ restoreError() }}</p>
                    }

                    @if (result(); as restored) {
                        <div class="result" data-testid="maintenance-result">
                            <p class="success">
                                {{ i18n.t('maintenance.restore.success', { rows: restored.totalRows }) }}
                            </p>
                            <table>
                                <thead>
                                    <tr>
                                        <th>{{ i18n.t('maintenance.restore.table') }}</th>
                                        <th class="rows">{{ i18n.t('maintenance.restore.rows') }}</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (entry of restoredTables(restored); track entry.table) {
                                        <tr>
                                            <td>{{ entry.table }}</td>
                                            <td class="rows">{{ entry.rows }}</td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                    }
                </mat-card-content>
                <mat-card-actions>
                    <button
                        mat-flat-button
                        class="danger"
                        data-testid="maintenance-restore"
                        [disabled]="!pending() || !confirmed() || restoring()"
                        (click)="restore()"
                    >
                        <mat-icon>settings_backup_restore</mat-icon>
                        {{ i18n.t('maintenance.restore.start') }}
                    </button>
                </mat-card-actions>
                @if (restoring()) {
                    <mat-progress-bar mode="indeterminate" />
                }
            </mat-card>
        </div>
    `,
    styles: `
        .page {
            display: flex;
            flex-direction: column;
            gap: 16px;
            max-width: 720px;
            margin: 0 auto;
        }

        .subtitle {
            color: var(--mat-sys-on-surface-variant);
            margin: 4px 0 0;
        }

        mat-card-actions {
            padding: 8px 16px 16px;
        }

        .file-info {
            margin-top: 12px;

            p {
                margin: 0 0 4px;
            }
        }

        .warning {
            display: flex;
            align-items: center;
            gap: 8px;
            margin: 12px 0;
            padding: 12px;
            border-radius: 8px;
            background: var(--mat-sys-error-container);
            color: var(--mat-sys-on-error-container);
        }

        .error-banner {
            color: var(--mat-sys-error);
            margin-top: 12px;
        }

        .result {
            margin-top: 16px;

            .success {
                font-weight: 500;
            }

            table {
                border-collapse: collapse;
                width: 100%;
            }

            th,
            td {
                text-align: left;
                padding: 4px 8px;
                border-bottom: 1px solid var(--mat-sys-outline-variant);
            }

            .rows {
                text-align: right;
            }
        }

        button.danger:not(:disabled) {
            background: var(--mat-sys-error);
            color: var(--mat-sys-on-error);
        }
    `,
})
export class MaintenanceComponent {
    protected readonly i18n = inject(I18nService);
    private readonly maintenance = inject(MaintenanceService);

    protected readonly downloading = signal(false);
    protected readonly backupError = signal<string | null>(null);
    protected readonly pending = signal<PendingRestore | null>(null);
    protected readonly confirmed = signal(false);
    protected readonly restoring = signal(false);
    protected readonly restoreError = signal<string | null>(null);
    protected readonly result = signal<RestoreResult | null>(null);

    protected download(): void {
        this.downloading.set(true);
        this.backupError.set(null);
        this.maintenance.downloadBackup().subscribe({
            next: ({ blob, fileName }) => {
                saveBlob(blob, fileName);
                this.downloading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.backupError.set(this.problemMessage(error, 'maintenance.backup.error'));
                this.downloading.set(false);
            },
        });
    }

    protected async onFileSelected(files: FileList | null): Promise<void> {
        this.restoreError.set(null);
        this.result.set(null);
        this.pending.set(null);
        this.confirmed.set(false);

        const file = files?.item(0);
        if (!file) {
            return;
        }

        try {
            const backup = JSON.parse(await file.text()) as TenantBackup;
            if (backup.fileFormat !== BACKUP_FILE_FORMAT || typeof backup.tables !== 'object') {
                this.restoreError.set(this.i18n.t('maintenance.restore.invalidFile'));
                return;
            }
            this.pending.set({
                backup,
                fileName: file.name,
                createdAt: new Date(backup.createdAt),
                tableCount: Object.keys(backup.tables).length,
            });
        } catch {
            this.restoreError.set(this.i18n.t('maintenance.restore.invalidFile'));
        }
    }

    protected restore(): void {
        const file = this.pending();
        if (!file) {
            return;
        }
        this.restoring.set(true);
        this.restoreError.set(null);
        this.result.set(null);
        this.maintenance.restore(file.backup).subscribe({
            next: (result) => {
                this.result.set(result);
                this.pending.set(null);
                this.confirmed.set(false);
                this.restoring.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.restoreError.set(this.problemMessage(error, 'maintenance.restore.error'));
                this.restoring.set(false);
            },
        });
    }

    protected restoredTables(result: RestoreResult): { table: string; rows: number }[] {
        return Object.entries(result.tables)
            .filter(([, rows]) => rows > 0)
            .map(([table, rows]) => ({ table, rows }))
            .sort((a, b) => a.table.localeCompare(b.table));
    }

    /** Prefers the API's problem details - its messages name the exact reason - over a generic text. */
    private problemMessage(error: HttpErrorResponse, fallbackKey: TranslationKey): string {
        const problem = error.error as
            | { detail?: string; title?: string; errors?: Record<string, string[]> }
            | null
            | undefined;
        if (problem?.errors) {
            const details = Object.values(problem.errors).flat();
            if (details.length > 0) {
                return details.join(' ');
            }
        }
        return problem?.detail ?? this.i18n.t(fallbackKey);
    }
}
