import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { formatDateTime } from '../vehicles/vehicle-format';
import { ImportService, ImportSession } from './import.service';

const COLUMNS = ['source', 'status', 'target', 'counts', 'createdBy', 'createdAt', 'actions'];

/** All import sessions of the tenant, newest first, with the option to remove finished ones. */
@Component({
    selector: 'app-import-history',
    standalone: true,
    imports: [
        MatButtonModule,
        MatChipsModule,
        MatIconModule,
        MatProgressBarModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('imports.history.title') }}</h1>
                        <p class="subtitle" data-testid="import-history-count">
                            {{ i18n.t('imports.history.count', { count: sessions().length }) }}
                        </p>
                    </div>
                    <div class="header-actions">
                        <a mat-flat-button routerLink="/import" data-testid="import-history-new">
                            <mat-icon>upload_file</mat-icon>
                            {{ i18n.t('imports.history.newImport') }}
                        </a>
                    </div>
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="import-history-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="import-history-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="sessions()">
                                <ng-container matColumnDef="source">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('imports.history.source') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let s"
                                        class="strong"
                                        data-testid="import-history-source"
                                    >
                                        {{ s.sourceName }}
                                        <span class="source-type">({{ s.sourceType }})</span>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let s">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="statusTone(s.status)"
                                                data-testid="import-history-status"
                                                >{{ i18n.t(statusKey(s.status)) }}</mat-chip
                                            >
                                        </mat-chip-set>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="target">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('imports.history.target') }}
                                    </th>
                                    <td mat-cell *matCellDef="let s">
                                        {{ targetLabel(s) }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="counts">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('imports.history.importedFailed') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let s"
                                        class="numeric"
                                        data-testid="import-history-counts"
                                    >
                                        {{ s.importedCount }} / {{ s.failedCount }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="createdBy">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('imports.history.createdBy') }}
                                    </th>
                                    <td mat-cell *matCellDef="let s">{{ s.createdBy }}</td>
                                </ng-container>
                                <ng-container matColumnDef="createdAt">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('imports.history.createdAt') }}
                                    </th>
                                    <td mat-cell *matCellDef="let s">
                                        {{ formatTimestamp(s.createdAtUtc) }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let s" class="actions-cell">
                                        <button
                                            mat-icon-button
                                            class="delete"
                                            data-testid="import-history-delete"
                                            [matTooltip]="i18n.t('common.delete')"
                                            [attr.aria-label]="
                                                i18n.t('common.delete') + ' ' + s.sourceName
                                            "
                                            (click)="remove(s)"
                                        >
                                            <mat-icon>delete</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns.length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">upload_file</mat-icon>
                                            <span data-testid="import-history-empty">{{
                                                i18n.t('imports.history.empty')
                                            }}</span>
                                        }
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </section>
                }
            </div>
        } @else {
            <p>{{ i18n.t('imports.signIn') }}</p>
        }
    `,
    styles: [
        `
            .source-type {
                color: var(--mat-sys-on-surface-variant);
                font-weight: 400;
            }
        `,
    ],
})
export class ImportHistoryComponent {
    private readonly service = inject(ImportService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly columns = COLUMNS;
    protected readonly sessions = signal<ImportSession[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage so a failed delete does not hide the table (see CustomerListComponent).
    protected readonly actionError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    protected statusKey(status: string): TranslationKey {
        switch (status) {
            case 'Completed':
                return 'imports.status.completed';
            case 'CompletedWithErrors':
                return 'imports.status.completedWithErrors';
            case 'Failed':
                return 'imports.status.failed';
            default:
                return 'imports.status.ready';
        }
    }

    protected statusTone(status: string): string {
        switch (status) {
            case 'Completed':
                return 'success';
            case 'CompletedWithErrors':
                return 'warning';
            case 'Failed':
                return 'error';
            default:
                return 'neutral';
        }
    }

    protected targetLabel(item: ImportSession): string {
        if (!item.targetEntity) {
            return '–';
        }
        return item.tableName ? `${item.tableName} → ${item.targetEntity}` : item.targetEntity;
    }

    protected formatTimestamp(iso: string): string {
        return formatDateTime(iso, this.i18n.locale());
    }

    protected remove(item: ImportSession): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('imports.history.deleteTitle'),
            message: this.i18n.t('imports.history.deleteConfirm', {
                name: item.sourceName,
                date: this.formatTimestamp(item.createdAtUtc),
            }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'import-delete-confirm',
            cancelTestId: 'import-delete-cancel',
            icon: 'delete',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.service.remove(item.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(this.describe(error, this.i18n.t('imports.error.delete'))),
            });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list().subscribe({
            next: (sessions) => {
                this.sessions.set(sessions);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('imports.error.list')));
            },
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
