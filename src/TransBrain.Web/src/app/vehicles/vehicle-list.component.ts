import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Vehicle, VehicleService } from './vehicle.service';

@Component({
    selector: 'app-vehicle-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule],
    template: `
        @if (isAuthenticated()) {
            <h1>Vehicles</h1>
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
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            @if (errorMessage(); as message) {
                <p data-testid="vehicle-list-error">{{ message }}</p>
            }
            <button mat-raised-button data-testid="login" (click)="login()">Sign in</button>
        }
    `,
})
export class VehicleListComponent {
    private readonly service = inject(VehicleService);
    private readonly oidc = inject(OidcSecurityService);

    protected readonly columns = ['licensePlate', 'type', 'payloadKg'];
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly isAuthenticated = signal(false);
    protected readonly errorMessage = signal<string | null>(null);

    constructor() {
        this.oidc.checkAuth().subscribe({
            next: ({ isAuthenticated }) => {
                this.isAuthenticated.set(isAuthenticated);
                if (isAuthenticated) {
                    this.service.list().subscribe({
                        next: (page) => this.vehicles.set(page.items),
                        error: (error: HttpErrorResponse) => this.errorMessage.set(this.describe(error)),
                    });
                }
            },
            // Without this, a checkAuth failure (e.g. Keycloak unreachable) escaped unhandled and
            // the user was bounced to the Sign in button with no explanation of why. Same scope as
            // the existing HTTP error handling above: set the message, no retry, no toast.
            error: () => this.errorMessage.set('Could not verify your sign-in status. Please try signing in again.'),
        });
    }

    protected login(): void {
        this.oidc.authorize();
    }

    // ProblemDetails' `errors` dictionary is keyed by error code (e.g. "Vehicle.PayloadKgNotPositive")
    // rather than by field name - a known API defect awaiting its own fix - so it is deliberately
    // not read here. `title`/`detail` are stable enough to surface directly.
    private describe(error: HttpErrorResponse): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? 'The vehicle list could not be loaded.';
        return `${sentence} (HTTP ${error.status})`;
    }
}
