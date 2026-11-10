import { HttpErrorResponse } from '@angular/common/http';
import { Component, WritableSignal, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { DriverService } from '../drivers/driver.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { InspectionUrgency, formatDate, inspectionUrgency, makeAndModel } from './vehicle-format';
import {
    VehicleCatalogEntry,
    VehicleCatalogKind,
    VehicleCatalogService,
    catalogIndex,
    catalogName,
} from './vehicle-catalog.service';
import {
    VEHICLE_STATUSES,
    VEHICLE_TYPES,
    Vehicle,
    VehicleService,
    VehicleStatus,
    VehicleType,
} from './vehicle.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

const BASE_COLUMNS = [
    'licensePlate',
    'type',
    'makeModel',
    'vehicleClass',
    'payloadKg',
    'defaultDriver',
    'nextInspectionDue',
    'status',
];

interface VehicleFilter {
    search: string;
    status: VehicleStatus | '';
    type: VehicleType | '';
}

@Component({
    selector: 'app-vehicle-list',
    standalone: true,
    imports: [
        MatButtonModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatSelectModule,
        MatSortModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('nav.vehicles') }}</h1>
                        <p class="subtitle" data-testid="vehicle-count">
                            {{ i18n.t('vehicles.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    <div class="header-actions">
                        <a
                            mat-stroked-button
                            routerLink="/vehicle-catalog"
                            data-testid="vehicle-catalog-link"
                        >
                            <mat-icon>tune</mat-icon>
                            {{ i18n.t('vehicles.catalogLink') }}
                        </a>
                        @if (session.can('masterData.write')) {
                            <a mat-flat-button routerLink="/vehicles/new" data-testid="vehicle-add">
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('vehicles.add') }}
                            </a>
                        }
                    </div>
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="vehicle-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="vehicle-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="filter-bar">
                        <mat-form-field
                            appearance="outline"
                            subscriptSizing="dynamic"
                            class="search"
                        >
                            <mat-label>{{ i18n.t('vehicles.search') }}</mat-label>
                            <mat-icon matPrefix>search</mat-icon>
                            <input
                                matInput
                                data-testid="vehicle-search"
                                [value]="search()"
                                (input)="search.set($any($event.target).value)"
                                [placeholder]="i18n.t('vehicles.searchHint')"
                            />
                        </mat-form-field>
                        <mat-form-field appearance="outline" subscriptSizing="dynamic">
                            <mat-label>{{ i18n.t('common.status') }}</mat-label>
                            <mat-select
                                data-testid="vehicle-status-filter"
                                [value]="statusFilter()"
                                (valueChange)="statusFilter.set($event)"
                            >
                                <mat-option value="">{{ i18n.t('common.all') }}</mat-option>
                                @for (status of statuses; track status) {
                                    <mat-option [value]="status">{{
                                        i18n.t(statusKey(status))
                                    }}</mat-option>
                                }
                            </mat-select>
                        </mat-form-field>
                        <mat-form-field appearance="outline" subscriptSizing="dynamic">
                            <mat-label>{{ i18n.t('vehicles.type') }}</mat-label>
                            <mat-select
                                data-testid="vehicle-type-filter"
                                [value]="typeFilter()"
                                (valueChange)="typeFilter.set($event)"
                            >
                                <mat-option value="">{{ i18n.t('common.all') }}</mat-option>
                                @for (type of types; track type) {
                                    <mat-option [value]="type">{{
                                        i18n.t(typeKey(type))
                                    }}</mat-option>
                                }
                            </mat-select>
                        </mat-form-field>
                    </section>

                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="licensePlate">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.licensePlate') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">
                                        <span class="plate">
                                            <span data-testid="vehicle-plate">{{
                                                v.licensePlate
                                            }}</span>
                                            @if (v.isLicensePlateConfidential) {
                                                <mat-icon
                                                    class="lock"
                                                    data-testid="vehicle-confidential"
                                                    [matTooltip]="
                                                        i18n.t('vehicles.licensePlateConfidential')
                                                    "
                                                    [attr.aria-label]="
                                                        i18n.t('vehicles.licensePlateConfidential')
                                                    "
                                                    >lock</mat-icon
                                                >
                                            }
                                        </span>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="type">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.type') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">{{ typeLabel(v) }}</td>
                                </ng-container>
                                <ng-container matColumnDef="makeModel">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.makeModel') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">
                                        {{ makeAndModel(v) || '–' }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="vehicleClass">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.vehicleClass') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">{{ classLabel(v) }}</td>
                                </ng-container>
                                <ng-container matColumnDef="payloadKg">
                                    <th
                                        mat-header-cell
                                        *matHeaderCellDef
                                        mat-sort-header
                                        arrowPosition="before"
                                    >
                                        {{ i18n.t('vehicles.payloadKg') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v" class="numeric">
                                        {{ v.payloadKg }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="defaultDriver">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.defaultDriver') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">{{ driverName(v) }}</td>
                                </ng-container>
                                <ng-container matColumnDef="nextInspectionDue">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('vehicles.nextInspectionShort') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">
                                        @switch (urgency(v)) {
                                            @case ('overdue') {
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        data-tone="error"
                                                        [matTooltip]="
                                                            i18n.t('vehicles.inspectionOverdue')
                                                        "
                                                        >{{ date(v.nextInspectionDue) }}</mat-chip
                                                    >
                                                </mat-chip-set>
                                            }
                                            @case ('dueSoon') {
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        data-tone="warning"
                                                        [matTooltip]="
                                                            i18n.t('vehicles.inspectionDueSoon')
                                                        "
                                                        >{{ date(v.nextInspectionDue) }}</mat-chip
                                                    >
                                                </mat-chip-set>
                                            }
                                            @default {
                                                {{ date(v.nextInspectionDue) }}
                                            }
                                        }
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let v">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip status-chip"
                                                [attr.data-tone]="statusTone(v.status)"
                                                data-testid="vehicle-status-chip"
                                                >{{ i18n.t(statusKey(v.status)) }}</mat-chip
                                            >
                                        </mat-chip-set>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let v" class="actions-cell">
                                        <a
                                            mat-icon-button
                                            [routerLink]="['/vehicles', v.id]"
                                            data-testid="vehicle-edit"
                                            [matTooltip]="i18n.t('common.edit')"
                                            [attr.aria-label]="
                                                i18n.t('common.edit') + ' ' + v.licensePlate
                                            "
                                        >
                                            <mat-icon>edit</mat-icon>
                                        </a>
                                        <button
                                            mat-icon-button
                                            class="delete"
                                            data-testid="vehicle-delete"
                                            [matTooltip]="i18n.t('common.delete')"
                                            [attr.aria-label]="
                                                i18n.t('common.delete') + ' ' + v.licensePlate
                                            "
                                            (click)="delete(v)"
                                        >
                                            <mat-icon>delete</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr
                                    mat-row
                                    *matRowDef="let row; columns: columns()"
                                    [class.decommissioned]="row.status === 'Decommissioned'"
                                ></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">local_shipping</mat-icon>
                                            <span data-testid="vehicle-empty">{{
                                                i18n.t('vehicles.empty')
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
            <p>{{ i18n.t('vehicles.signIn') }}</p>
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

            .header-actions {
                display: flex;
                align-items: center;
                gap: 8px;
                flex-wrap: wrap;
            }

            .subtitle {
                margin: 4px 0 0;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-medium);
            }

            .error-banner {
                display: flex;
                align-items: center;
                gap: 8px;
                margin: 0;
                padding: 12px 16px;
                border-radius: var(--mat-sys-corner-medium);
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }

            .filter-bar {
                display: flex;
                flex-wrap: wrap;
                gap: 16px;
                padding: 16px;
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-large);
                background: var(--mat-sys-surface-container-low);
            }

            .filter-bar mat-form-field {
                flex: 1 1 200px;
            }

            .filter-bar .search {
                flex: 3 1 320px;
            }

            .table-card {
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-large);
                background: var(--mat-sys-surface);
                overflow: hidden;
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

            tr.decommissioned td {
                color: var(--mat-sys-on-surface-variant);
            }

            .plate {
                display: inline-flex;
                align-items: center;
                gap: 6px;
                font-weight: 600;
                white-space: nowrap;
            }

            .lock {
                font-size: 18px;
                width: 18px;
                height: 18px;
                color: var(--mat-sys-on-surface-variant);
            }

            .numeric {
                text-align: right;
                font-variant-numeric: tabular-nums;
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

            .chip[data-tone='success'] {
                --mat-chip-elevated-container-color: #d7f3dc;
                --mat-chip-label-text-color: #0f5223;
            }

            .chip[data-tone='warning'] {
                --mat-chip-elevated-container-color: #ffe4bc;
                --mat-chip-label-text-color: #6d3d00;
            }

            .chip[data-tone='error'] {
                --mat-chip-elevated-container-color: var(--mat-sys-error-container);
                --mat-chip-label-text-color: var(--mat-sys-on-error-container);
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
export class VehicleListComponent {
    private readonly service = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    private readonly catalogService = inject(VehicleCatalogService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly statuses = VEHICLE_STATUSES;
    protected readonly types = VEHICLE_TYPES;
    protected readonly makeAndModel = makeAndModel;

    protected readonly columns = computed(() =>
        this.session.can('masterData.write') ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
    );
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed delete must not hide the table the way a failed
    // list load does (the @else branch above only renders the table when errorMessage is
    // unset), so it gets its own signal and its own paragraph.
    protected readonly actionError = signal<string | null>(null);

    protected readonly search = signal('');
    protected readonly statusFilter = signal<VehicleStatus | ''>('');
    protected readonly typeFilter = signal<VehicleType | ''>('');
    // Only used to resolve default-driver names. A failed driver load leaves it empty, which
    // shows "–" in that column instead of taking the whole vehicle list down with it.
    private readonly driverNames = signal(new Map<string, string>());
    // Same reasoning for the catalogs: a failed load falls back to showing the stored code.
    private readonly vehicleClasses = signal(new Map<string, VehicleCatalogEntry>());
    private readonly carrierGroups = signal(new Map<string, VehicleCatalogEntry>());

    private readonly criteria = computed<VehicleFilter>(() => ({
        search: this.search().trim().toLowerCase(),
        status: this.statusFilter(),
        type: this.typeFilter(),
    }));
    protected readonly filteredCount = computed(() => {
        const criteria = this.criteria();
        return this.vehicles().filter((vehicle) => this.matches(vehicle, criteria)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Vehicle>([]);
    // The table sits inside an @if, so MatSort only exists once the list has rendered.
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (vehicle, filterJson) =>
            this.matches(vehicle, JSON.parse(filterJson) as VehicleFilter);
        this.dataSource.sortingDataAccessor = (vehicle, column) => this.sortValue(vehicle, column);

        effect(() => {
            this.dataSource.data = this.vehicles();
        });
        effect(() => {
            // Reading the catalog too re-runs the filter once carrier-group names arrive.
            this.carrierGroups();
            this.dataSource.filter = JSON.stringify(this.criteria());
        });
        effect(() => {
            this.dataSource.sort = this.sort() ?? null;
        });

        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
                this.loadDrivers();
                this.loadCatalog('VehicleClass', this.vehicleClasses);
                this.loadCatalog('CarrierGroup', this.carrierGroups);
            }
        });
    }

    protected statusKey(status: VehicleStatus) {
        return `vehicles.status.${status}` as const;
    }

    protected typeKey(type: VehicleType) {
        return `vehicles.typeOption.${type}` as const;
    }

    protected statusTone(status: VehicleStatus): string {
        switch (status) {
            case 'Available':
                return 'success';
            case 'InWorkshop':
                return 'warning';
            default:
                return 'neutral';
        }
    }

    protected typeLabel(vehicle: Vehicle): string {
        return this.i18n.t(this.typeKey(vehicle.type));
    }

    protected classLabel(vehicle: Vehicle): string {
        return catalogName(this.vehicleClasses(), vehicle.vehicleClass) ?? '–';
    }

    protected driverName(vehicle: Vehicle): string {
        return (vehicle.defaultDriverId && this.driverNames().get(vehicle.defaultDriverId)) || '–';
    }

    protected date(iso: string): string {
        return formatDate(iso, this.i18n.locale());
    }

    protected urgency(vehicle: Vehicle): InspectionUrgency {
        return inspectionUrgency(vehicle.nextInspectionDue);
    }

    protected delete(vehicle: Vehicle): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('vehicles.deleteTitle'),
            message: this.i18n.t('vehicles.deleteConfirm', { plate: vehicle.licensePlate }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'vehicle-delete-confirm',
            cancelTestId: 'vehicle-delete-cancel',
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
                switchMap(() => this.service.remove(vehicle.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
                // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
                // describe()'s fallback text is therefore action-specific, not the list-load one.
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(
                        this.describe(error, this.i18n.t('vehicles.error.delete')),
                    ),
            });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list(LIST_PAGE_SIZE).subscribe({
            next: (page) => {
                this.vehicles.set(page.items);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('vehicles.error.list')));
            },
        });
    }

    private loadDrivers(): void {
        this.driverService.list(LIST_PAGE_SIZE).subscribe({
            next: (page) =>
                this.driverNames.set(
                    new Map(
                        page.items.map((driver) => [
                            driver.id,
                            `${driver.lastName}, ${driver.firstName}`,
                        ]),
                    ),
                ),
            error: () => this.driverNames.set(new Map()),
        });
    }

    private loadCatalog(
        kind: VehicleCatalogKind,
        target: WritableSignal<Map<string, VehicleCatalogEntry>>,
    ): void {
        this.catalogService.list(kind).subscribe({
            next: (entries) => target.set(catalogIndex(entries)),
            error: () => target.set(new Map()),
        });
    }

    private matches(vehicle: Vehicle, criteria: VehicleFilter): boolean {
        if (criteria.status && vehicle.status !== criteria.status) {
            return false;
        }
        if (criteria.type && vehicle.type !== criteria.type) {
            return false;
        }
        if (!criteria.search) {
            return true;
        }
        return [
            vehicle.licensePlate,
            vehicle.make,
            vehicle.model,
            vehicle.chassisNumber,
            vehicle.carrierGroup,
            catalogName(this.carrierGroups(), vehicle.carrierGroup),
        ].some((value) => !!value && value.toLowerCase().includes(criteria.search));
    }

    private sortValue(vehicle: Vehicle, column: string): string | number {
        switch (column) {
            case 'licensePlate':
                return vehicle.licensePlate.toLowerCase();
            case 'type':
                return this.typeLabel(vehicle).toLowerCase();
            case 'makeModel':
                return makeAndModel(vehicle).toLowerCase();
            case 'vehicleClass':
                return vehicle.vehicleClass ? this.classLabel(vehicle).toLowerCase() : '';
            case 'payloadKg':
                return vehicle.payloadKg;
            case 'defaultDriver':
                return vehicle.defaultDriverId ? this.driverName(vehicle).toLowerCase() : '';
            case 'nextInspectionDue':
                return vehicle.nextInspectionDue;
            case 'status':
                return this.i18n.t(this.statusKey(vehicle.status)).toLowerCase();
            default:
                return '';
        }
    }

    // ProblemDetails' `errors` dictionary is now keyed by field name (Task 1 of the
    // 2026-08-29 master-data-completion phase fixed the previous error-code keying), but this
    // component has no form fields to bind those keys onto - it only ever needs the
    // free-text summary, which `title`/`detail` already provide.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
