import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BehaviorSubject, EMPTY, catchError, filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { formatDateTime } from '../vehicles/vehicle-format';
import { GeodataDialogComponent, GeodataDialogData } from './geodata-dialog.component';
import { GeodataEntry, GeodataService, GeodataSource } from './geodata.service';

const BASE_COLUMNS = ['address', 'coordinates', 'source', 'usage'];

type SourceFilter = 'all' | GeodataSource;

const SOURCE_LABELS: Record<GeodataSource, TranslationKey> = {
    manual: 'geodata.source.manual',
    geocoded: 'geodata.source.geocoded',
    unresolved: 'geodata.source.unresolved',
};

const SOURCE_TONES: Record<GeodataSource, string> = {
    manual: 'primary',
    geocoded: 'success',
    unresolved: 'warn',
};

/**
 * Maintenance page for the coordinates behind every known address (orders, customer locations,
 * manual overrides). Admins fix wrong or missing geodata here; maps and tour planning follow.
 */
@Component({
    selector: 'app-geodata-list',
    standalone: true,
    imports: [
        MatButtonModule,
        MatButtonToggleModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatTableModule,
        MatTooltipModule,
    ],
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('geodata.title') }}</h1>
                        <p class="subtitle">{{ i18n.t('geodata.subtitle') }}</p>
                    </div>
                    @if (session.can('masterData.write')) {
                        <button
                            mat-flat-button
                            type="button"
                            data-testid="geodata-add"
                            (click)="openDialog(null)"
                        >
                            <mat-icon>add_location_alt</mat-icon>
                            {{ i18n.t('geodata.add') }}
                        </button>
                    }
                </header>

                <section class="table-card">
                    <div class="toolbar">
                        <mat-form-field class="filter" subscriptSizing="dynamic">
                            <mat-icon matPrefix>search</mat-icon>
                            <mat-label>{{ i18n.t('geodata.filter') }}</mat-label>
                            <input
                                matInput
                                data-testid="geodata-filter"
                                [value]="filterText()"
                                (input)="filterText.set($any($event.target).value)"
                            />
                        </mat-form-field>
                        <mat-button-toggle-group
                            class="source-filter"
                            hideSingleSelectionIndicator
                            [value]="sourceFilter()"
                            (change)="sourceFilter.set($event.value)"
                        >
                            <mat-button-toggle value="all" data-testid="geodata-filter-all">
                                {{ i18n.t('common.all') }}
                            </mat-button-toggle>
                            <mat-button-toggle value="manual" data-testid="geodata-filter-manual">
                                {{ i18n.t('geodata.source.manual') }}
                            </mat-button-toggle>
                            <mat-button-toggle value="geocoded" data-testid="geodata-filter-geocoded">
                                {{ i18n.t('geodata.source.geocoded') }}
                            </mat-button-toggle>
                            <mat-button-toggle
                                value="unresolved"
                                data-testid="geodata-filter-unresolved"
                            >
                                {{ i18n.t('geodata.source.unresolved') }}
                            </mat-button-toggle>
                        </mat-button-toggle-group>
                        <p class="count" data-testid="geodata-count">
                            {{ i18n.t('geodata.count', { count: filtered().length }) }}
                        </p>
                    </div>

                    @if (errorMessage(); as message) {
                        <p class="error-banner" data-testid="geodata-error">
                            <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                        </p>
                    }

                    @if (loading()) {
                        <mat-progress-bar mode="indeterminate" />
                    }

                    <div class="table-scroll">
                        <table mat-table [dataSource]="filtered()">
                            <ng-container matColumnDef="address">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('geodata.address') }}
                                </th>
                                <td mat-cell *matCellDef="let e">
                                    <div class="street" data-testid="geodata-entry-street">
                                        {{ e.street }}
                                    </div>
                                    <div class="place">
                                        {{ e.postalCode }} {{ e.city }}, {{ e.country }}
                                    </div>
                                </td>
                            </ng-container>

                            <ng-container matColumnDef="coordinates">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('geodata.coordinates') }}
                                </th>
                                <td mat-cell *matCellDef="let e">
                                    @if (e.latitude !== null) {
                                        <code class="coords" data-testid="geodata-entry-coords"
                                            >{{ format(e.latitude) }}, {{ format(e.longitude) }}</code
                                        >
                                    } @else {
                                        <span class="missing" data-testid="geodata-entry-coords">—</span>
                                    }
                                </td>
                            </ng-container>

                            <ng-container matColumnDef="source">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('common.status') }}
                                </th>
                                <td mat-cell *matCellDef="let e">
                                    <mat-chip-set>
                                        <mat-chip
                                            class="chip"
                                            [attr.data-tone]="tone(e.source)"
                                            data-testid="geodata-entry-source"
                                            >{{ i18n.t(label(e.source)) }}</mat-chip
                                        >
                                    </mat-chip-set>
                                </td>
                            </ng-container>

                            <ng-container matColumnDef="usage">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('geodata.usedBy') }}
                                </th>
                                <td mat-cell *matCellDef="let e" class="usage" [class.unused]="!e.inUse">
                                    <span data-testid="geodata-entry-usage">
                                        @if (e.inUse) {
                                            {{
                                                i18n.t('geodata.usage', {
                                                    orders: e.orderCount,
                                                    locations: e.locationCount
                                                })
                                            }}
                                        } @else {
                                            {{ i18n.t('geodata.notInUse') }}
                                        }
                                    </span>
                                </td>
                            </ng-container>

                            <ng-container matColumnDef="actions">
                                <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                    <span class="cdk-visually-hidden">{{
                                        i18n.t('common.actions')
                                    }}</span>
                                </th>
                                <td mat-cell *matCellDef="let e" class="actions-cell">
                                    @if (e.source !== 'manual') {
                                        <button
                                            mat-icon-button
                                            type="button"
                                            data-testid="geodata-resolve"
                                            [disabled]="resolvingKey() === key(e)"
                                            [matTooltip]="i18n.t('geodata.resolve')"
                                            [attr.aria-label]="
                                                i18n.t('geodata.resolve') + ' ' + e.street
                                            "
                                            (click)="resolve(e)"
                                        >
                                            <mat-icon>travel_explore</mat-icon>
                                        </button>
                                    }
                                    <button
                                        mat-icon-button
                                        type="button"
                                        data-testid="geodata-edit"
                                        [matTooltip]="i18n.t('geodata.setCoordinates')"
                                        [attr.aria-label]="
                                            i18n.t('geodata.setCoordinates') + ' ' + e.street
                                        "
                                        (click)="openDialog(e)"
                                    >
                                        <mat-icon>edit_location_alt</mat-icon>
                                    </button>
                                    @if (e.overrideId) {
                                        <button
                                            mat-icon-button
                                            type="button"
                                            class="delete"
                                            data-testid="geodata-delete"
                                            [matTooltip]="i18n.t('geodata.deleteOverride')"
                                            [attr.aria-label]="
                                                i18n.t('geodata.deleteOverride') + ' ' + e.street
                                            "
                                            (click)="deleteOverride(e)"
                                        >
                                            <mat-icon>wrong_location</mat-icon>
                                        </button>
                                    }
                                </td>
                            </ng-container>

                            <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                            <tr
                                mat-row
                                *matRowDef="let row; columns: columns()"
                                data-testid="geodata-row"
                            ></tr>
                            <tr class="mat-mdc-row" *matNoDataRow>
                                <td class="empty" [attr.colspan]="columns().length">
                                    @if (!loading() && !loadFailed()) {
                                        <mat-icon aria-hidden="true">location_off</mat-icon>
                                        <span data-testid="geodata-empty">{{
                                            i18n.t('geodata.empty')
                                        }}</span>
                                    }
                                </td>
                            </tr>
                        </table>
                    </div>
                </section>
            </div>
        } @else {
            <p>{{ i18n.t('geodata.signIn') }}</p>
        }
    `,
    styles: [
        `
            .page {
                max-width: 1200px;
                margin: 0 auto;
                display: flex;
                flex-direction: column;
                gap: 16px;
            }

            .page-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 16px;
                flex-wrap: wrap;
            }

            h1 {
                font: var(--mat-sys-headline-medium);
                margin: 0;
            }

            .subtitle {
                margin: 4px 0 0;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-medium);
            }

            .table-card {
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-large);
                background: var(--mat-sys-surface);
                overflow: hidden;
            }

            .toolbar {
                display: flex;
                align-items: center;
                gap: 16px;
                flex-wrap: wrap;
                padding: 12px 16px;
            }

            .filter {
                flex: 1 1 240px;
                max-width: 360px;
            }

            .count {
                margin: 0 0 0 auto;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-medium);
                white-space: nowrap;
            }

            .error-banner {
                display: flex;
                align-items: center;
                gap: 8px;
                margin: 0 16px 12px;
                padding: 12px 16px;
                border-radius: var(--mat-sys-corner-medium);
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }

            .table-scroll {
                overflow-x: auto;
            }

            table {
                width: 100%;
                --mat-table-background-color: transparent;
            }

            th.mat-mdc-header-cell {
                background: var(--mat-sys-surface-container);
                font: var(--mat-sys-title-small);
                white-space: nowrap;
            }

            tr.mat-mdc-row:hover {
                background: var(--mat-sys-surface-container-lowest);
            }

            .street {
                font-weight: 500;
            }

            .place {
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .coords {
                padding: 2px 8px;
                border-radius: var(--mat-sys-corner-small);
                background: var(--mat-sys-surface-container-high);
                font-family: 'Roboto Mono', ui-monospace, monospace;
                font-size: 0.875em;
                white-space: nowrap;
            }

            .missing {
                color: var(--mat-sys-on-surface-variant);
            }

            .usage {
                white-space: nowrap;
                font-variant-numeric: tabular-nums;
            }

            .usage.unused {
                color: var(--mat-sys-on-surface-variant);
                font-style: italic;
            }

            .actions-cell {
                text-align: right;
                white-space: nowrap;
                width: 1%;
            }

            .delete {
                color: var(--mat-sys-error);
            }

            .chip {
                --mat-chip-container-height: 26px;
                --mat-chip-outline-width: 0;
                --mat-chip-label-text-weight: 500;
            }

            .chip[data-tone='primary'] {
                --mat-chip-elevated-container-color: var(--mat-sys-primary-container);
                --mat-chip-label-text-color: var(--mat-sys-on-primary-container);
            }

            .chip[data-tone='success'] {
                --mat-chip-elevated-container-color: #d7f3dc;
                --mat-chip-label-text-color: #0f5223;
            }

            .chip[data-tone='warn'] {
                --mat-chip-elevated-container-color: var(--mat-sys-error-container);
                --mat-chip-label-text-color: var(--mat-sys-on-error-container);
            }

            .empty {
                padding: 48px 16px;
                text-align: center;
                color: var(--mat-sys-on-surface-variant);
            }

            .empty mat-icon {
                display: block;
                margin: 0 auto 8px;
                font-size: 40px;
                width: 40px;
                height: 40px;
                opacity: 0.6;
            }
        `,
    ],
})
export class GeodataListComponent {
    private readonly service = inject(GeodataService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly entries = signal<GeodataEntry[]>([]);
    protected readonly loading = signal(false);
    protected readonly loadFailed = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    protected readonly filterText = signal('');
    protected readonly sourceFilter = signal<SourceFilter>('all');
    /** The address key of the row whose resolve request is in flight. */
    protected readonly resolvingKey = signal<string | null>(null);

    protected readonly columns = computed(() =>
        this.session.can('masterData.write') ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
    );

    protected readonly filtered = computed(() => {
        const text = this.filterText().trim().toLowerCase();
        const source = this.sourceFilter();
        return this.entries().filter((entry) => {
            if (source !== 'all' && entry.source !== source) {
                return false;
            }
            if (text.length === 0) {
                return true;
            }
            return [entry.street, entry.postalCode, entry.city, entry.country]
                .join(' ')
                .toLowerCase()
                .includes(text);
        });
    });

    private readonly reload$ = new BehaviorSubject<void>(undefined);

    constructor() {
        this.session.ready
            .pipe(
                filter((isAuthenticated) => isAuthenticated),
                switchMap(() => this.reload$),
                switchMap(() => {
                    this.loading.set(true);
                    this.loadFailed.set(false);
                    return this.service.list().pipe(
                        catchError((error: HttpErrorResponse) => {
                            this.loading.set(false);
                            this.loadFailed.set(true);
                            this.errorMessage.set(
                                this.describe(error, this.i18n.t('geodata.error.list')),
                            );
                            return EMPTY;
                        }),
                    );
                }),
                takeUntilDestroyed(),
            )
            .subscribe((entries) => {
                this.entries.set(entries);
                this.loading.set(false);
            });
    }

    protected label(source: GeodataSource): TranslationKey {
        return SOURCE_LABELS[source];
    }

    protected tone(source: GeodataSource): string {
        return SOURCE_TONES[source];
    }

    protected key(entry: GeodataEntry): string {
        return `${entry.country}|${entry.postalCode}|${entry.city}|${entry.street}`.toLowerCase();
    }

    protected format(value: number): string {
        return value.toFixed(5);
    }

    protected formatUpdated(iso: string | null): string {
        return iso ? formatDateTime(iso, this.i18n.locale()) : '';
    }

    protected openDialog(entry: GeodataEntry | null): void {
        this.errorMessage.set(null);
        this.dialog
            .open<GeodataDialogComponent, GeodataDialogData>(GeodataDialogComponent, {
                data: { entry },
                width: '520px',
                autoFocus: 'first-tabbable',
            })
            .afterClosed()
            .pipe(filter((saved) => !!saved))
            .subscribe(() => this.reload$.next());
    }

    protected deleteOverride(entry: GeodataEntry): void {
        if (!entry.overrideId) {
            return;
        }
        this.errorMessage.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('geodata.deleteTitle'),
            message: this.i18n.t('geodata.deleteConfirm', { street: entry.street, city: entry.city }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'geodata-delete-confirm',
            cancelTestId: 'geodata-delete-cancel',
            icon: 'wrong_location',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.service.deleteOverride(entry.overrideId!)),
            )
            .subscribe({
                next: () => this.reload$.next(),
                error: (error: HttpErrorResponse) =>
                    this.errorMessage.set(this.describe(error, this.i18n.t('geodata.error.delete'))),
            });
    }

    /** Asks the server to resolve the address now - a geocoder success lands in the cache. */
    protected resolve(entry: GeodataEntry): void {
        this.errorMessage.set(null);
        this.resolvingKey.set(this.key(entry));
        this.service
            .resolve({
                street: entry.street,
                postalCode: entry.postalCode,
                city: entry.city,
                country: entry.country,
            })
            .subscribe({
                next: () => {
                    this.resolvingKey.set(null);
                    this.reload$.next();
                },
                error: (error: HttpErrorResponse) => {
                    this.resolvingKey.set(null);
                    this.errorMessage.set(
                        this.describe(
                            error,
                            this.i18n.t(
                                error.status === 503 || error.status === 404
                                    ? 'geodata.error.resolveFailed'
                                    : 'geodata.error.resolve',
                            ),
                        ),
                    );
                },
            });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
