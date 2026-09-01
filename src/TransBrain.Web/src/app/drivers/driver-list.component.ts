import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { Driver, DriverService } from './driver.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-driver-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1>Drivers</h1>
            @if (session.can('masterData.write')) {
                <a mat-raised-button routerLink="/drivers/new" data-testid="driver-add">Add driver</a>
            }
            @if (actionError(); as message) {
                <p data-testid="driver-action-error">{{ message }}</p>
            }
            @if (errorMessage(); as message) {
                <p data-testid="driver-list-error">{{ message }}</p>
            } @else {
                <table mat-table [dataSource]="drivers()">
                    <ng-container matColumnDef="lastName">
                        <th mat-header-cell *matHeaderCellDef>Last name</th>
                        <td mat-cell *matCellDef="let d" data-testid="driver-lastname">{{ d.lastName }}</td>
                    </ng-container>
                    <ng-container matColumnDef="firstName">
                        <th mat-header-cell *matHeaderCellDef>First name</th>
                        <td mat-cell *matCellDef="let d" data-testid="driver-firstname">{{ d.firstName }}</td>
                    </ng-container>
                    <ng-container matColumnDef="licenseClasses">
                        <th mat-header-cell *matHeaderCellDef>License classes</th>
                        <td mat-cell *matCellDef="let d">{{ d.licenseClasses.join(', ') }}</td>
                    </ng-container>
                    <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef>Status</th>
                        <td mat-cell *matCellDef="let d">{{ d.status }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let d">
                            <a mat-button [routerLink]="['/drivers', d.id]" data-testid="driver-edit">Edit</a>
                            <button mat-button data-testid="driver-delete" (click)="delete(d)">Delete</button>
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            <p>Please sign in to see the drivers.</p>
        }
    `,
})
export class DriverListComponent {
    private readonly service = inject(DriverService);
    protected readonly session = inject(SessionService);

    protected readonly columns = ['lastName', 'firstName', 'licenseClasses', 'status', 'actions'];
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed delete must not hide the table the way a failed
    // list load does (mirrors VehicleListComponent's actionError - see that file for why).
    protected readonly actionError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    // See VehicleListComponent.delete for why these actions are shown to every authenticated
    // user rather than hidden by role: this SPA has no role-decoding infrastructure yet, and a
    // non-admin gets a clear 403-driven message here instead of a hidden button.
    protected delete(driver: Driver): void {
        this.actionError.set(null);
        this.service.remove(driver.id).subscribe({
            next: () => this.refresh(),
            // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
            // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
            // describe()'s fallback text is therefore action-specific, not the list-load one.
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, 'The driver could not be deleted.')),
        });
    }

    private refresh(): void {
        this.service.list(LIST_PAGE_SIZE).subscribe({
            next: (page) => this.drivers.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(this.describe(error, 'The driver list could not be loaded.')),
        });
    }

    // See VehicleListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
