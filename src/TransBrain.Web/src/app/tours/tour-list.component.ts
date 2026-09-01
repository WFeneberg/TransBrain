import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { Driver, DriverService } from '../drivers/driver.service';
import { Vehicle, VehicleService } from '../vehicles/vehicle.service';
import { Tour, TourService } from './tour.service';

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-tour-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1>Tours</h1>
            @if (session.can('dispatch.write')) {
                <a mat-raised-button routerLink="/tours/new" data-testid="tour-add">Add tour</a>
            }
            <mat-form-field>
                <mat-label>Date</mat-label>
                <input
                    matInput
                    type="date"
                    [value]="dateFilter()"
                    (change)="filterByDate($any($event.target).value)"
                    data-testid="tour-date-filter"
                />
            </mat-form-field>
            <mat-form-field>
                <mat-label>Vehicle</mat-label>
                <mat-select
                    [value]="vehicleFilter()"
                    (valueChange)="filterByVehicle($event)"
                    data-testid="tour-vehicle-filter"
                >
                    <mat-option [value]="''">All</mat-option>
                    @for (vehicle of vehicles(); track vehicle.id) {
                        <mat-option [value]="vehicle.id">{{ vehicle.licensePlate }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>
            <mat-form-field>
                <mat-label>Driver</mat-label>
                <mat-select
                    [value]="driverFilter()"
                    (valueChange)="filterByDriver($event)"
                    data-testid="tour-driver-filter"
                >
                    <mat-option [value]="''">All</mat-option>
                    @for (driver of drivers(); track driver.id) {
                        <mat-option [value]="driver.id">{{ driver.lastName }}, {{ driver.firstName }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>
            @if (errorMessage(); as message) {
                <p data-testid="tour-list-error">{{ message }}</p>
            } @else {
                <table mat-table [dataSource]="tours()">
                    <ng-container matColumnDef="tourDate">
                        <th mat-header-cell *matHeaderCellDef>Date</th>
                        <td mat-cell *matCellDef="let t" data-testid="tour-date">{{ t.tourDate }}</td>
                    </ng-container>
                    <ng-container matColumnDef="vehicle">
                        <th mat-header-cell *matHeaderCellDef>Vehicle</th>
                        <td mat-cell *matCellDef="let t" data-testid="tour-vehicle">
                            {{ t.vehicleLicensePlate }}
                        </td>
                    </ng-container>
                    <ng-container matColumnDef="driver">
                        <th mat-header-cell *matHeaderCellDef>Driver</th>
                        <td mat-cell *matCellDef="let t" data-testid="tour-driver">{{ t.driverName }}</td>
                    </ng-container>
                    <ng-container matColumnDef="stops">
                        <th mat-header-cell *matHeaderCellDef>Stops</th>
                        <td mat-cell *matCellDef="let t" data-testid="tour-stops">{{ t.stops.length }}</td>
                    </ng-container>
                    <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef>Status</th>
                        <td mat-cell *matCellDef="let t" data-testid="tour-status">{{ t.status }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let t">
                            <a mat-button [routerLink]="['/tours', t.id]" data-testid="tour-open">Open</a>
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            <p>Please sign in to see the tours.</p>
        }
    `,
})
export class TourListComponent {
    private readonly service = inject(TourService);
    private readonly vehicleService = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    protected readonly session = inject(SessionService);

    protected readonly columns = ['tourDate', 'vehicle', 'driver', 'stops', 'status', 'actions'];
    protected readonly tours = signal<Tour[]>([]);
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly errorMessage = signal<string | null>(null);
    protected readonly dateFilter = signal('');
    protected readonly vehicleFilter = signal('');
    protected readonly driverFilter = signal('');

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
                this.loadFilterOptions();
            }
        });
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
        this.vehicleService.list(PICKER_PAGE_SIZE).subscribe({ next: (page) => this.vehicles.set(page.items) });
        this.driverService.list(PICKER_PAGE_SIZE).subscribe({ next: (page) => this.drivers.set(page.items) });
    }

    private refresh(): void {
        this.service
            .list({
                tourDate: this.dateFilter(),
                vehicleId: this.vehicleFilter(),
                driverId: this.driverFilter(),
                pageSize: LIST_PAGE_SIZE,
            })
            .subscribe({
                next: (page) => this.tours.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.errorMessage.set(this.describe(error, 'The tour list could not be loaded.')),
            });
    }

    // See DriverListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
