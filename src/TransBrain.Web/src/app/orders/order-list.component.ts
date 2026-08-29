import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Order, OrderService } from './order.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-order-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, MatFormFieldModule, MatSelectModule, RouterLink, DatePipe],
    template: `
        @if (isAuthenticated()) {
            <h1>Orders</h1>
            <a mat-raised-button routerLink="/orders/new" data-testid="order-add">Add order</a>
            <mat-form-field>
                <mat-label>Status</mat-label>
                <mat-select
                    [value]="statusFilter()"
                    (valueChange)="filterByStatus($event)"
                    data-testid="order-status-filter"
                >
                    <mat-option [value]="''">All</mat-option>
                    @for (status of statusOptions; track status) {
                        <mat-option [value]="status">{{ status }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>
            @if (actionError(); as message) {
                <p data-testid="order-action-error">{{ message }}</p>
            }
            @if (errorMessage(); as message) {
                <p data-testid="order-list-error">{{ message }}</p>
            } @else {
                <table mat-table [dataSource]="orders()">
                    <ng-container matColumnDef="orderNumber">
                        <th mat-header-cell *matHeaderCellDef>Order number</th>
                        <td mat-cell *matCellDef="let o" data-testid="order-number">{{ o.orderNumber }}</td>
                    </ng-container>
                    <ng-container matColumnDef="consignor">
                        <th mat-header-cell *matHeaderCellDef>Consignor</th>
                        <td mat-cell *matCellDef="let o" data-testid="order-consignor">{{ o.consignor.name }}</td>
                    </ng-container>
                    <ng-container matColumnDef="consignee">
                        <th mat-header-cell *matHeaderCellDef>Consignee</th>
                        <td mat-cell *matCellDef="let o" data-testid="order-consignee">{{ o.consignee.name }}</td>
                    </ng-container>
                    <ng-container matColumnDef="cargo">
                        <th mat-header-cell *matHeaderCellDef>Cargo</th>
                        <td mat-cell *matCellDef="let o" data-testid="order-cargo">{{ o.cargoDescription }}</td>
                    </ng-container>
                    <ng-container matColumnDef="pickup">
                        <th mat-header-cell *matHeaderCellDef>Pickup</th>
                        <td mat-cell *matCellDef="let o">{{ o.pickupFrom | date: 'short' }}</td>
                    </ng-container>
                    <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef>Status</th>
                        <td mat-cell *matCellDef="let o" data-testid="order-status">{{ o.status }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let o">
                            <a mat-button [routerLink]="['/orders', o.id]" data-testid="order-edit">Edit</a>
                            @if (pendingCancelId() === o.id) {
                                <!-- Inline confirmation rather than window.confirm(): a native
                                     dialog blocks until a Playwright dialog handler answers it,
                                     and an in-DOM confirmation is what the e2e spec can assert. -->
                                <button mat-button data-testid="order-cancel-confirm" (click)="cancel(o)">
                                    Confirm cancel
                                </button>
                                <button mat-button data-testid="order-cancel-abort" (click)="abortCancel()">
                                    Keep order
                                </button>
                            } @else {
                                <button mat-button data-testid="order-cancel" (click)="askToCancel(o)">
                                    Cancel order
                                </button>
                            }
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            @if (errorMessage(); as message) {
                <p data-testid="order-list-error">{{ message }}</p>
            }
            <button mat-raised-button data-testid="login" (click)="login()">Sign in</button>
        }
    `,
})
export class OrderListComponent {
    private readonly service = inject(OrderService);
    private readonly oidc = inject(OidcSecurityService);

    protected readonly columns = [
        'orderNumber',
        'consignor',
        'consignee',
        'cargo',
        'pickup',
        'status',
        'actions',
    ];
    protected readonly statusOptions = ['Draft', 'Planned', 'InTransit', 'Delivered', 'Cancelled'];
    protected readonly orders = signal<Order[]>([]);
    protected readonly isAuthenticated = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed cancel must not hide the table the way a failed
    // list load does (mirrors DriverListComponent's actionError).
    protected readonly actionError = signal<string | null>(null);
    protected readonly statusFilter = signal('');
    protected readonly pendingCancelId = signal<string | null>(null);

    constructor() {
        this.oidc.checkAuth().subscribe({
            next: ({ isAuthenticated }) => {
                this.isAuthenticated.set(isAuthenticated);
                if (isAuthenticated) {
                    this.refresh();
                }
            },
            error: () => this.errorMessage.set('Could not verify your sign-in status. Please try signing in again.'),
        });
    }

    protected login(): void {
        this.oidc.authorize();
    }

    protected filterByStatus(status: string): void {
        this.statusFilter.set(status);
        this.pendingCancelId.set(null);
        this.refresh();
    }

    protected askToCancel(order: Order): void {
        this.actionError.set(null);
        this.pendingCancelId.set(order.id);
    }

    protected abortCancel(): void {
        this.pendingCancelId.set(null);
    }

    // Shown for every order rather than hidden for the ones the domain will refuse: an order
    // already in transit answers 409 with a message explaining why, which is more useful than
    // a button that silently is not there. Same reasoning as the master-data delete actions,
    // which are shown to every authenticated user rather than hidden by role.
    protected cancel(order: Order): void {
        this.actionError.set(null);
        this.pendingCancelId.set(null);
        this.service.cancel(order.id).subscribe({
            next: () => this.refresh(),
            // A policy failure (e.g. a viewer's 403) is rejected by ASP.NET's authorization
            // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
            // describe()'s fallback text is therefore action-specific, not the list-load one.
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, 'The order could not be cancelled.')),
        });
    }

    private refresh(): void {
        this.service.list(this.statusFilter(), LIST_PAGE_SIZE).subscribe({
            next: (page) => this.orders.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(this.describe(error, 'The order list could not be loaded.')),
        });
    }

    // See DriverListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
