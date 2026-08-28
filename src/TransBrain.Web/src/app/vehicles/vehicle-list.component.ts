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
        } @else {
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

    constructor() {
        this.oidc.checkAuth().subscribe(({ isAuthenticated }) => {
            this.isAuthenticated.set(isAuthenticated);
            if (isAuthenticated) {
                this.service.list().subscribe((page) => this.vehicles.set(page.items));
            }
        });
    }

    protected login(): void {
        this.oidc.authorize();
    }
}
