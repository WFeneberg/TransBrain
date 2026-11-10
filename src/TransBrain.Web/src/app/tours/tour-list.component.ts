import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { Driver, DriverService } from '../drivers/driver.service';
import { formatDate } from '../vehicles/vehicle-format';
import { Vehicle, VehicleService } from '../vehicles/vehicle.service';
import { TOUR_STATUSES, Tour, TourService, tourStatusTone } from './tour.service';

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

interface TourFilter {
    search: string;
    status: string;
}

@Component({
    selector: 'app-tour-list',
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
    styleUrl: '../shared/page-layout.scss',
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('nav.tours') }}</h1>
                        <p class="subtitle" data-testid="tour-count">
                            {{ i18n.t('tours.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    @if (session.can('dispatch.write')) {
                        <div class="header-actions">
                            <a mat-flat-button routerLink="/tours/new" data-testid="tour-add">
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('tours.add') }}
                            </a>
                        </div>
                    }
                </header>

                <section class="filter-bar">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
                        <mat-label>{{ i18n.t('tours.search') }}</mat-label>
                        <mat-icon matPrefix>search</mat-icon>
                        <input
                            matInput
                            data-testid="tour-search"
                            [value]="search()"
                            (input)="search.set($any($event.target).value)"
                            [placeholder]="i18n.t('tours.searchHint')"
                        />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.date') }}</mat-label>
                        <input
                            matInput
                            type="date"
                            [value]="dateFilter()"
                            (change)="filterByDate($any($event.target).value)"
                            data-testid="tour-date-filter"
                        />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.vehicle') }}</mat-label>
                        <mat-select
                            [value]="vehicleFilter()"
                            (valueChange)="filterByVehicle($event)"
                            data-testid="tour-vehicle-filter"
                        >
                            <mat-option [value]="''">{{ i18n.t('common.all') }}</mat-option>
                            @for (vehicle of vehicles(); track vehicle.id) {
                                <mat-option [value]="vehicle.id">{{
                                    vehicle.licensePlate
                                }}</mat-option>
                            }
                        </mat-select>
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.driver') }}</mat-label>
                        <mat-select
                            [value]="driverFilter()"
                            (valueChange)="filterByDriver($event)"
                            data-testid="tour-driver-filter"
                        >
                            <mat-option [value]="''">{{ i18n.t('common.all') }}</mat-option>
                            @for (driver of drivers(); track driver.id) {
                                <mat-option [value]="driver.id"
                                    >{{ driver.lastName }}, {{ driver.firstName }}</mat-option
                                >
                            }
                        </mat-select>
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.status') }}</mat-label>
                        <mat-select
                            data-testid="tour-status-filter"
                            [value]="statusFilter()"
                            (valueChange)="statusFilter.set($event)"
                        >
                            <mat-option value="">{{ i18n.t('common.all') }}</mat-option>
                            @for (status of statuses; track status) {
                                <mat-option [value]="status">{{ status }}</mat-option>
                            }
                        </mat-select>
                    </mat-form-field>
                </section>

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="tour-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="tourDate">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.date') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let t"
                                        class="strong"
                                        data-testid="tour-date"
                                    >
                                        {{ date(t.tourDate) }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="vehicle">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.vehicle') }}
                                    </th>
                                    <td mat-cell *matCellDef="let t" data-testid="tour-vehicle">
                                        {{ t.vehicleLicensePlate }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="driver">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.driver') }}
                                    </th>
                                    <td mat-cell *matCellDef="let t" data-testid="tour-driver">
                                        {{ t.driverName }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="stops">
                                    <th
                                        mat-header-cell
                                        *matHeaderCellDef
                                        mat-sort-header
                                        arrowPosition="before"
                                    >
                                        {{ i18n.t('tours.stops') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let t"
                                        class="numeric"
                                        data-testid="tour-stops"
                                    >
                                        {{ t.stops.length }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let t">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="statusTone(t.status)"
                                                data-testid="tour-status"
                                                >{{ t.status }}</mat-chip
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
                                    <td mat-cell *matCellDef="let t" class="actions-cell">
                                        <a
                                            mat-icon-button
                                            [routerLink]="['/tours', t.id]"
                                            data-testid="tour-open"
                                            [matTooltip]="i18n.t('common.open')"
                                            [attr.aria-label]="
                                                i18n.t('common.open') + ' ' + t.vehicleLicensePlate
                                            "
                                        >
                                            <mat-icon>open_in_new</mat-icon>
                                        </a>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns.length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">route</mat-icon>
                                            <span data-testid="tour-empty">{{
                                                i18n.t('tours.empty')
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
            <p>{{ i18n.t('tours.signIn') }}</p>
        }
    `,
})
export class TourListComponent {
    private readonly service = inject(TourService);
    private readonly vehicleService = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly columns = ['tourDate', 'vehicle', 'driver', 'stops', 'status', 'actions'];
    protected readonly statuses = TOUR_STATUSES;
    protected readonly statusTone = tourStatusTone;
    protected readonly tours = signal<Tour[]>([]);
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Date, vehicle and driver filter server-side; search and status filter the loaded page.
    protected readonly dateFilter = signal('');
    protected readonly vehicleFilter = signal('');
    protected readonly driverFilter = signal('');
    protected readonly search = signal('');
    protected readonly statusFilter = signal('');

    private readonly criteria = computed<TourFilter>(() => ({
        search: this.search().trim().toLowerCase(),
        status: this.statusFilter(),
    }));
    protected readonly filteredCount = computed(() => {
        const criteria = this.criteria();
        return this.tours().filter((tour) => this.matches(tour, criteria)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Tour>([]);
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (tour, filterJson) =>
            this.matches(tour, JSON.parse(filterJson) as TourFilter);
        this.dataSource.sortingDataAccessor = (tour, column) => this.sortValue(tour, column);

        effect(() => {
            this.dataSource.data = this.tours();
        });
        effect(() => {
            this.dataSource.filter = JSON.stringify(this.criteria());
        });
        effect(() => {
            this.dataSource.sort = this.sort() ?? null;
        });

        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
                this.loadFilterOptions();
            }
        });
    }

    protected date(iso: string): string {
        return formatDate(iso, this.i18n.locale());
    }

    protected filterByDate(value: string): void {
        this.dateFilter.set(value);
        this.refresh();
    }

    protected filterByVehicle(value: string): void {
        this.vehicleFilter.set(value);
        this.refresh();
    }

    protected filterByDriver(value: string): void {
        this.driverFilter.set(value);
        this.refresh();
    }

    private loadFilterOptions(): void {
        // A failed option load leaves the filters empty but must not blank the table - the
        // tours themselves loaded fine, and an unusable filter is better than no list.
        this.vehicleService
            .list(PICKER_PAGE_SIZE)
            .subscribe({ next: (page) => this.vehicles.set(page.items) });
        this.driverService
            .list(PICKER_PAGE_SIZE)
            .subscribe({ next: (page) => this.drivers.set(page.items) });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service
            .list({
                tourDate: this.dateFilter(),
                vehicleId: this.vehicleFilter(),
                driverId: this.driverFilter(),
                pageSize: LIST_PAGE_SIZE,
            })
            .subscribe({
                next: (page) => {
                    this.tours.set(page.items);
                    this.errorMessage.set(null);
                    this.loading.set(false);
                },
                error: (error: HttpErrorResponse) => {
                    this.loading.set(false);
                    this.errorMessage.set(this.describe(error, this.i18n.t('tours.error.list')));
                },
            });
    }

    private matches(tour: Tour, criteria: TourFilter): boolean {
        if (criteria.status && tour.status !== criteria.status) {
            return false;
        }
        if (!criteria.search) {
            return true;
        }
        return [
            tour.vehicleLicensePlate,
            tour.driverName,
            tour.tourDate,
            this.date(tour.tourDate),
            ...tour.stops.map((stop) => stop.orderNumber),
        ].some((value) => !!value && value.toLowerCase().includes(criteria.search));
    }

    private sortValue(tour: Tour, column: string): string | number {
        switch (column) {
            case 'vehicle':
                return tour.vehicleLicensePlate.toLowerCase();
            case 'driver':
                return tour.driverName.toLowerCase();
            case 'stops':
                return tour.stops.length;
            case 'status':
                return tour.status.toLowerCase();
            default:
                return tour.tourDate;
        }
    }

    // See DriverListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
