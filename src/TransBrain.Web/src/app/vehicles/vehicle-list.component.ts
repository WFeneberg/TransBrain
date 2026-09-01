import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { Vehicle, VehicleService } from './vehicle.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-vehicle-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1>Vehicles</h1>
            @if (session.can('masterData.write')) {
                <a mat-raised-button routerLink="/vehicles/new" data-testid="vehicle-add">Add vehicle</a>
            }
            @if (actionError(); as message) {
                <p data-testid="vehicle-action-error">{{ message }}</p>
            }
            @if (errorMessage(); as message) {
                <p data-testid="vehicle-list-error">{{ message }}</p>
            } @else {
                <table mat-table [dataSource]="vehicles()">
                    <ng-container matColumnDef="licensePlate">
                        <th mat-header-cell *matHeaderCellDef>License plate</th>
                        <td mat-cell *matCellDef="let v" data-testid="vehicle-plate">{{ v.licensePlate }}</td>
                    </ng-container>
                    <ng-container matColumnDef="type">
                        <th mat-header-cell *matHeaderCellDef>Type</th>
                        <td mat-cell *matCellDef="let v">{{ v.type }}</td>
                    </ng-container>
                    <ng-container matColumnDef="payloadKg">
                        <th mat-header-cell *matHeaderCellDef>Payload (kg)</th>
                        <td mat-cell *matCellDef="let v">{{ v.payloadKg }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let v">
                            @if (session.can('masterData.write')) {
                                <a mat-button [routerLink]="['/vehicles', v.id]" data-testid="vehicle-edit">Edit</a>
                                <button mat-button data-testid="vehicle-delete" (click)="delete(v)">Delete</button>
                            }
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            <p>Please sign in to see the vehicles.</p>
        }
    `,
})
export class VehicleListComponent {
    private readonly service = inject(VehicleService);
    protected readonly session = inject(SessionService);

    protected readonly columns = ['licensePlate', 'type', 'payloadKg', 'actions'];
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed delete must not hide the table the way a failed
    // list load does (the @else branch above only renders the table when errorMessage is
    // unset), so it gets its own signal and its own paragraph.
    protected readonly actionError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    protected delete(vehicle: Vehicle): void {
        this.actionError.set(null);
        this.service.remove(vehicle.id).subscribe({
            next: () => this.refresh(),
            // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
            // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
            // describe()'s fallback text is therefore action-specific, not the list-load one.
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, 'The vehicle could not be deleted.')),
        });
    }

    private refresh(): void {
        this.service.list(LIST_PAGE_SIZE).subscribe({
            next: (page) => this.vehicles.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(this.describe(error, 'The vehicle list could not be loaded.')),
        });
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
