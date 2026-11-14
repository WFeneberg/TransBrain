import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { I18nService } from '../i18n/i18n.service';
import { saveBlob } from '../invoices/invoice.service';
import { ExportFormat, ExportService, ExportTarget } from './export.service';

/**
 * The Export tab of the Wartung area: pick a master-data set (Kunden, Fahrer, Fahrzeuge) and a
 * file format, then download it. The files are the mirror image of the import - CSV and Excel
 * carry the import wizard's German column labels, JSON its field names - so every export can be
 * played back through the Import tab without manual column mapping.
 */
@Component({
    selector: 'app-export-page',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatProgressBarModule,
        MatSelectModule,
    ],
    template: `
        <div class="page">
            <header>
                <h1>{{ i18n.t('export.title') }}</h1>
                <p class="subtitle">{{ i18n.t('export.subtitle') }}</p>
            </header>

            <mat-card appearance="outlined">
                <mat-card-content>
                    <p>{{ i18n.t('export.description') }}</p>

                    <div class="controls">
                        <mat-form-field appearance="outline">
                            <mat-label>{{ i18n.t('export.target') }}</mat-label>
                            <mat-select
                                data-testid="export-target-select"
                                [value]="target()"
                                (valueChange)="target.set($event)"
                            >
                                @for (option of targets(); track option.entity) {
                                    <mat-option [value]="option.entity">
                                        {{ option.label }}
                                        ({{ i18n.t('export.rows', { rows: option.rowCount }) }})
                                    </mat-option>
                                }
                            </mat-select>
                        </mat-form-field>

                        <mat-form-field appearance="outline">
                            <mat-label>{{ i18n.t('export.format') }}</mat-label>
                            <mat-select
                                data-testid="export-format-select"
                                [value]="format()"
                                (valueChange)="format.set($event)"
                            >
                                <mat-option value="Csv">CSV</mat-option>
                                <mat-option value="Xlsx">Excel (XLSX)</mat-option>
                                <mat-option value="Json">JSON</mat-option>
                            </mat-select>
                        </mat-form-field>
                    </div>

                    <p class="hint">{{ i18n.t('export.hint') }}</p>

                    @if (error()) {
                        <p class="error-banner" data-testid="export-error">{{ error() }}</p>
                    }
                </mat-card-content>
                <mat-card-actions>
                    <button
                        mat-flat-button
                        data-testid="export-download"
                        [disabled]="downloading() || !target()"
                        (click)="download()"
                    >
                        <mat-icon>download</mat-icon>
                        {{ i18n.t('export.download') }}
                    </button>
                </mat-card-actions>
                @if (downloading()) {
                    <mat-progress-bar mode="indeterminate" />
                }
            </mat-card>
        </div>
    `,
    styles: `
        .page {
            display: flex;
            flex-direction: column;
            gap: 24px;
        }

        header h1 {
            margin: 0 0 4px;
        }

        .subtitle {
            margin: 0;
            color: var(--mat-sys-on-surface-variant);
        }

        .controls {
            display: flex;
            flex-wrap: wrap;
            gap: 16px;
            margin-top: 16px;
        }

        mat-form-field {
            min-width: 260px;
        }

        .hint {
            color: var(--mat-sys-on-surface-variant);
            font-size: 0.9rem;
        }

        .error-banner {
            color: var(--mat-sys-error);
        }

        mat-card-actions {
            padding: 0 16px 16px;
        }
    `,
})
export class ExportPageComponent {
    protected readonly i18n = inject(I18nService);
    private readonly exports = inject(ExportService);

    protected readonly targets = signal<ExportTarget[]>([]);
    protected readonly target = signal<string | null>(null);
    protected readonly format = signal<ExportFormat>('Csv');
    protected readonly downloading = signal(false);
    protected readonly error = signal<string | null>(null);

    constructor() {
        this.exports.getTargets().subscribe({
            next: (targets) => {
                this.targets.set(targets);
                this.target.set(targets[0]?.entity ?? null);
            },
            error: () => this.error.set(this.i18n.t('export.error')),
        });
    }

    protected download(): void {
        const entity = this.target();
        if (!entity) {
            return;
        }
        this.downloading.set(true);
        this.error.set(null);
        this.exports.download(entity, this.format()).subscribe({
            next: ({ blob, fileName }) => {
                saveBlob(blob, fileName);
                this.downloading.set(false);
            },
            error: () => {
                this.error.set(this.i18n.t('export.error'));
                this.downloading.set(false);
            },
        });
    }
}
