import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
    BehaviorSubject,
    EMPTY,
    catchError,
    combineLatest,
    distinctUntilChanged,
    filter,
    map,
    switchMap,
    tap,
} from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';
import {
    VehicleCatalogDialogComponent,
    VehicleCatalogDialogData,
} from './vehicle-catalog-dialog.component';
import {
    VEHICLE_CATALOG_KINDS,
    VehicleCatalogEntry,
    VehicleCatalogKind,
    VehicleCatalogService,
} from './vehicle-catalog.service';

const BASE_COLUMNS = ['code', 'name', 'status', 'usage'];

const KIND_ICONS: Record<VehicleCatalogKind, string> = {
    VehicleClass: 'category',
    CarrierGroup: 'groups',
};

function toKind(value: string | null): VehicleCatalogKind {
    const match = VEHICLE_CATALOG_KINDS.find(
        (kind) => kind.toLowerCase() === value?.toLowerCase(),
    );
    return match ?? 'VehicleClass';
}

@Component({
    selector: 'app-vehicle-catalog',
    standalone: true,
    imports: [
        MatButtonModule,
        MatChipsModule,
        MatIconModule,
        MatProgressBarModule,
        MatTableModule,
        MatTabsModule,
        MatTooltipModule,
        RouterLink,
    ],
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <a
                    mat-button
                    class="back"
                    routerLink="/vehicles"
                    data-testid="vehicle-catalog-back"
                >
                    <mat-icon>arrow_back</mat-icon>
                    {{ i18n.t('vehicleCatalog.back') }}
                </a>

                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('vehicleCatalog.title') }}</h1>
                        <p class="subtitle">{{ i18n.t('vehicleCatalog.subtitle') }}</p>
                    </div>
                </header>

                <section class="table-card">
                    <nav mat-tab-nav-bar [tabPanel]="tabPanel" class="tabs">
                        @for (k of kinds; track k) {
                            <a
                                mat-tab-link
                                [routerLink]="[]"
                                [queryParams]="{ kind: k }"
                                [active]="kind() === k"
                                [attr.data-testid]="'vehicle-catalog-tab-' + k"
                            >
                                <mat-icon class="tab-icon" aria-hidden="true">{{
                                    kindIcon(k)
                                }}</mat-icon>
                                {{ i18n.t(kindKey(k)) }}
                            </a>
                        }
                    </nav>
                    <mat-tab-nav-panel #tabPanel>
                        <div class="toolbar">
                            <p class="count" data-testid="catalog-count">
                                {{ i18n.t('vehicleCatalog.count', { count: entries().length }) }}
                            </p>
                            @if (session.can('masterData.write')) {
                                <button
                                    mat-flat-button
                                    type="button"
                                    data-testid="catalog-add"
                                    (click)="openDialog(null)"
                                >
                                    <mat-icon>add</mat-icon>
                                    {{ i18n.t('vehicleCatalog.add') }}
                                </button>
                            }
                        </div>

                        @if (errorMessage(); as message) {
                            <p class="error-banner" data-testid="catalog-error">
                                <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                            </p>
                        }

                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="entries()">
                                <ng-container matColumnDef="code">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('vehicleCatalog.code') }}
                                    </th>
                                    <td mat-cell *matCellDef="let e">
                                        <code class="code" data-testid="catalog-entry-code">{{
                                            e.code
                                        }}</code>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="name">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('vehicleCatalog.name') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let e"
                                        class="name"
                                        data-testid="catalog-entry-name"
                                    >
                                        {{ e.name }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let e">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="e.isActive ? 'success' : 'neutral'"
                                                data-testid="catalog-entry-status"
                                                >{{
                                                    e.isActive
                                                        ? i18n.t('vehicleCatalog.active')
                                                        : i18n.t('vehicleCatalog.inactive')
                                                }}</mat-chip
                                            >
                                        </mat-chip-set>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="usage">
                                    <th mat-header-cell *matHeaderCellDef>
                                        {{ i18n.t('vehicleCatalog.usedBy') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let e"
                                        class="usage"
                                        [class.unused]="e.usageCount === 0"
                                        data-testid="catalog-entry-usage"
                                    >
                                        {{ i18n.t('vehicleCatalog.usage', { count: e.usageCount }) }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let e" class="actions-cell">
                                        <button
                                            mat-icon-button
                                            type="button"
                                            data-testid="catalog-edit"
                                            [matTooltip]="i18n.t('common.edit')"
                                            [attr.aria-label]="i18n.t('common.edit') + ' ' + e.name"
                                            (click)="openDialog(e)"
                                        >
                                            <mat-icon>edit</mat-icon>
                                        </button>
                                        <!-- A disabled button swallows pointer events, so the
                                             in-use tooltip sits on a wrapper around it. -->
                                        <span
                                            class="delete-wrapper"
                                            [matTooltip]="
                                                e.usageCount > 0
                                                    ? i18n.t('vehicleCatalog.inUse')
                                                    : i18n.t('common.delete')
                                            "
                                        >
                                            <button
                                                mat-icon-button
                                                type="button"
                                                class="delete"
                                                data-testid="catalog-delete"
                                                [disabled]="e.usageCount > 0"
                                                [attr.aria-label]="
                                                    (e.usageCount > 0
                                                        ? i18n.t('vehicleCatalog.inUse')
                                                        : i18n.t('common.delete')) +
                                                    ' ' +
                                                    e.name
                                                "
                                                (click)="delete(e)"
                                            >
                                                <mat-icon>delete</mat-icon>
                                            </button>
                                        </span>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr
                                    mat-row
                                    *matRowDef="let row; columns: columns()"
                                    data-testid="catalog-row"
                                    [class.inactive]="!row.isActive"
                                ></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading() && !loadFailed()) {
                                            <mat-icon aria-hidden="true">{{
                                                kindIcon(kind())
                                            }}</mat-icon>
                                            <span data-testid="catalog-empty">{{
                                                i18n.t('vehicleCatalog.empty')
                                            }}</span>
                                        }
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </mat-tab-nav-panel>
                </section>
            </div>
        } @else {
            <p>{{ i18n.t('vehicleCatalog.signIn') }}</p>
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

            .back {
                align-self: flex-start;
                margin-left: -12px;
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

            .tabs {
                background: var(--mat-sys-surface-container-low);
                border-bottom: 1px solid var(--mat-sys-outline-variant);
            }

            .tab-icon {
                margin-right: 8px;
            }

            .toolbar {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 16px;
                flex-wrap: wrap;
                padding: 12px 16px;
            }

            .count {
                margin: 0;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-medium);
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

            tr.inactive td.name {
                color: var(--mat-sys-on-surface-variant);
            }

            .code {
                padding: 2px 8px;
                border-radius: var(--mat-sys-corner-small);
                background: var(--mat-sys-surface-container-high);
                font-family: 'Roboto Mono', ui-monospace, monospace;
                font-size: 0.875em;
                white-space: nowrap;
            }

            .name {
                font-weight: 500;
            }

            .usage {
                white-space: nowrap;
                font-variant-numeric: tabular-nums;
            }

            .usage.unused {
                color: var(--mat-sys-on-surface-variant);
            }

            .actions-cell {
                text-align: right;
                white-space: nowrap;
                width: 1%;
            }

            .delete-wrapper {
                display: inline-block;
            }

            .delete:not(:disabled) {
                color: var(--mat-sys-error);
            }

            .chip {
                --mat-chip-container-height: 26px;
                --mat-chip-outline-width: 0;
                --mat-chip-label-text-weight: 500;
            }

            .chip[data-tone='success'] {
                --mat-chip-elevated-container-color: #d7f3dc;
                --mat-chip-label-text-color: #0f5223;
            }

            .chip[data-tone='neutral'] {
                --mat-chip-elevated-container-color: var(--mat-sys-surface-container-highest);
                --mat-chip-label-text-color: var(--mat-sys-on-surface-variant);
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
export class VehicleCatalogComponent {
    private readonly service = inject(VehicleCatalogService);
    private readonly route = inject(ActivatedRoute);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly kinds = VEHICLE_CATALOG_KINDS;
    protected readonly kind = toSignal(
        this.route.queryParamMap.pipe(map((params) => toKind(params.get('kind')))),
        { initialValue: toKind(this.route.snapshot.queryParamMap.get('kind')) },
    );
    protected readonly columns = computed(() =>
        this.session.can('masterData.write') ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
    );
    protected readonly entries = signal<VehicleCatalogEntry[]>([]);
    protected readonly loading = signal(false);
    protected readonly loadFailed = signal(false);
    // One banner for both a failed load and a failed delete - the table stays rendered either way.
    protected readonly errorMessage = signal<string | null>(null);

    private readonly reload$ = new BehaviorSubject<void>(undefined);

    constructor() {
        const kind$ = this.route.queryParamMap.pipe(
            map((params) => toKind(params.get('kind'))),
            distinctUntilChanged(),
            // A tab switch must not show the previous tab's rows while the new ones load.
            tap(() => {
                this.entries.set([]);
                this.errorMessage.set(null);
            }),
        );

        this.session.ready
            .pipe(
                filter((isAuthenticated) => isAuthenticated),
                switchMap(() => combineLatest([kind$, this.reload$])),
                switchMap(([kind]) => {
                    this.loading.set(true);
                    this.loadFailed.set(false);
                    return this.service.list(kind).pipe(
                        catchError((error: HttpErrorResponse) => {
                            this.loading.set(false);
                            this.loadFailed.set(true);
                            this.errorMessage.set(
                                this.describe(error, this.i18n.t('vehicleCatalog.error.list')),
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

    protected kindKey(kind: VehicleCatalogKind) {
        return `vehicleCatalog.kind.${kind}` as const;
    }

    protected kindIcon(kind: VehicleCatalogKind): string {
        return KIND_ICONS[kind];
    }

    protected openDialog(entry: VehicleCatalogEntry | null): void {
        this.errorMessage.set(null);
        this.dialog
            .open<VehicleCatalogDialogComponent, VehicleCatalogDialogData, VehicleCatalogEntry>(
                VehicleCatalogDialogComponent,
                { data: { kind: this.kind(), entry }, width: '480px', autoFocus: 'first-tabbable' },
            )
            .afterClosed()
            .pipe(filter((saved) => !!saved))
            .subscribe(() => this.reload$.next());
    }

    protected delete(entry: VehicleCatalogEntry): void {
        this.errorMessage.set(null);
        const kind = this.kind();
        const data: ConfirmDialogData = {
            title: this.i18n.t('vehicleCatalog.deleteTitle'),
            message: this.i18n.t('vehicleCatalog.deleteConfirm', { name: entry.name }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'catalog-delete-confirm',
            cancelTestId: 'catalog-delete-cancel',
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
                switchMap(() => this.service.remove(kind, entry.id)),
            )
            .subscribe({
                next: () => this.reload$.next(),
                error: (error: HttpErrorResponse) =>
                    this.errorMessage.set(
                        this.describe(error, this.i18n.t('vehicleCatalog.error.delete')),
                    ),
            });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
