import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, of, shareReplay, switchMap } from 'rxjs';
import { Order, OrderService } from '../orders/order.service';
import { Tour, TourService } from './tour.service';

@Component({
    selector: 'app-tour-detail',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, MatFormFieldModule, MatSelectModule, RouterLink],
    template: `
        <h1>Tour</h1>
        @if (loadError(); as message) {
            <p data-testid="tour-detail-error">{{ message }}</p>
        }
        @if (tour(); as t) {
            <!-- tour-detail-* rather than tour-*: the list uses tour-status/tour-date/etc on its
                 cells, and a page-wide locator that matched both screens would be ambiguous the
                 moment a test navigated between them. -->
            <dl>
                <dt>Date</dt>
                <dd data-testid="tour-detail-date">{{ t.tourDate }}</dd>
                <dt>Vehicle</dt>
                <dd data-testid="tour-detail-vehicle">{{ t.vehicleLicensePlate }}</dd>
                <dt>Driver</dt>
                <dd data-testid="tour-detail-driver">{{ t.driverName }}</dd>
                <dt>Status</dt>
                <dd data-testid="tour-detail-status">{{ t.status }}</dd>
            </dl>

            <!-- The headroom a dispatcher needs before choosing the next order. The API already
                 sends both numbers, so showing them costs nothing extra. -->
            <section>
                <h2>Capacity</h2>
                <p data-testid="tour-capacity-weight">{{ t.totalWeightKg }} / {{ t.vehiclePayloadKg }} kg</p>
                <progress [value]="t.totalWeightKg" [max]="t.vehiclePayloadKg"></progress>
                <p data-testid="tour-capacity-meters">
                    {{ t.totalLoadMeters }} / {{ t.vehicleLoadMeters }} load meters
                </p>
                <progress [value]="t.totalLoadMeters" [max]="t.vehicleLoadMeters"></progress>
            </section>

            @if (actionError(); as message) {
                <p data-testid="tour-action-error">{{ message }}</p>
            }

            <section>
                <h2>Stops</h2>
                <table mat-table [dataSource]="t.stops">
                    <ng-container matColumnDef="sequence">
                        <th mat-header-cell *matHeaderCellDef>#</th>
                        <td mat-cell *matCellDef="let s" data-testid="tour-stop-sequence">{{ s.sequence }}</td>
                    </ng-container>
                    <ng-container matColumnDef="orderNumber">
                        <th mat-header-cell *matHeaderCellDef>Order</th>
                        <td mat-cell *matCellDef="let s" data-testid="tour-stop-order">{{ s.orderNumber }}</td>
                    </ng-container>
                    <ng-container matColumnDef="stopType">
                        <th mat-header-cell *matHeaderCellDef>Type</th>
                        <td mat-cell *matCellDef="let s" data-testid="tour-stop-type">{{ s.stopType }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let s">
                            @if (s.stopType === 'Pickup') {
                                <button
                                    mat-button
                                    data-testid="tour-remove"
                                    (click)="removeOrder(s.transportOrderId)"
                                >
                                    Remove
                                </button>
                            }
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="stopColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: stopColumns"></tr>
                </table>
            </section>

            <section>
                <h2>Assign an order</h2>
                <!-- Only Draft orders are offered: any other status is refused by the domain,
                     and offering a choice the server will reject is worse than not offering it. -->
                <mat-form-field>
                    <mat-label>Order</mat-label>
                    <mat-select
                        [value]="selectedOrderId()"
                        (valueChange)="selectedOrderId.set($event)"
                        data-testid="tour-assign-select"
                    >
                        @for (order of assignableOrders(); track order.id) {
                            <mat-option [value]="order.id">
                                {{ order.orderNumber }} — {{ order.consignor.name }}
                                ({{ order.cargoWeightKg }} kg)
                            </mat-option>
                        }
                    </mat-select>
                </mat-form-field>
                <button mat-raised-button data-testid="tour-assign" (click)="assign()">Assign</button>
            </section>

            <section>
                @if (t.status === 'Planned') {
                    <button mat-raised-button data-testid="tour-start" (click)="start()">Start tour</button>
                }
                @if (t.status === 'InProgress') {
                    <button mat-raised-button data-testid="tour-complete" (click)="complete()">
                        Complete tour
                    </button>
                }
            </section>

            <a mat-button routerLink="/tours" data-testid="tour-back">Back to tours</a>
        }
    `,
})
export class TourDetailComponent {
    private readonly service = inject(TourService);
    private readonly orderService = inject(OrderService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly oidc = inject(OidcSecurityService);

    // See TourFormComponent.session - a directly opened /tours/{id} would otherwise send every
    // request without a bearer token.
    private readonly session = this.oidc.checkAuth().pipe(shareReplay(1));

    protected readonly tourId = this.route.snapshot.paramMap.get('id')!;
    protected readonly stopColumns = ['sequence', 'orderNumber', 'stopType', 'actions'];
    protected readonly tour = signal<Tour | null>(null);
    protected readonly draftOrders = signal<Order[]>([]);
    protected readonly selectedOrderId = signal('');
    protected readonly loadError = signal<string | null>(null);
    // Separate from loadError: a refused assignment must not blank the page the way a failed
    // load does. This is where a 409 from the capacity or status rules reaches the user.
    protected readonly actionError = signal<string | null>(null);

    // Orders already on this tour are no longer Draft, but the picker's list is only refreshed
    // alongside the tour, so filter defensively rather than offering a stale choice.
    protected readonly assignableOrders = computed(() => {
        const assigned = new Set(this.tour()?.stops.map((stop) => stop.transportOrderId) ?? []);
        return this.draftOrders().filter((order) => !assigned.has(order.id));
    });

    constructor() {
        this.refresh();
    }

    protected assign(): void {
        const orderId = this.selectedOrderId();
        if (!orderId) {
            this.actionError.set('Choose an order to assign first.');
            return;
        }

        this.actionError.set(null);
        this.run(this.service.assignOrder(this.tourId, orderId), 'The order could not be assigned.');
    }

    protected removeOrder(transportOrderId: string): void {
        this.actionError.set(null);
        this.run(this.service.removeOrder(this.tourId, transportOrderId), 'The order could not be removed.');
    }

    protected start(): void {
        this.actionError.set(null);
        this.run(this.service.start(this.tourId), 'The tour could not be started.');
    }

    protected complete(): void {
        this.actionError.set(null);
        this.run(this.service.complete(this.tourId), 'The tour could not be completed.');
    }

    private run(action: Observable<Tour>, fallback: string): void {
        this.session.pipe(switchMap(() => action)).subscribe({
            next: (tour) => {
                this.tour.set(tour);
                this.selectedOrderId.set('');
                this.loadDraftOrders();
            },
            error: (error: HttpErrorResponse) => this.actionError.set(this.describe(error, fallback)),
        });
    }

    private refresh(): void {
        this.session.pipe(switchMap(() => this.service.getById(this.tourId))).subscribe({
            next: (tour) => {
                this.tour.set(tour);
                this.loadDraftOrders();
            },
            // The load path gets its own wording: "could not be assigned" on a failed getById
            // would be plainly wrong.
            error: (error: HttpErrorResponse) =>
                this.loadError.set(this.describe(error, 'The tour could not be loaded.')),
        });
    }

    // Orders come back sorted by order number ASCENDING, so page 1 is the OLDEST drafts - which
    // in a running installation are the ones nobody has planned for weeks, not the ones a
    // dispatcher is working on today. Fetch the last page instead: one extra request only when
    // there are more drafts than fit on a page, and never more than the API's 100-row cap.
    // Beyond that a searchable picker is needed; a bigger page will not save it.
    private loadDraftOrders(): void {
        const pageSize = 100;

        this.session
            .pipe(
                switchMap(() => this.orderService.list('Draft', pageSize)),
                switchMap((first) => {
                    const lastPage = Math.max(1, Math.ceil(first.totalCount / pageSize));
                    return lastPage === 1 ? of(first) : this.orderService.list('Draft', pageSize, lastPage);
                }),
            )
            .subscribe({
                next: (page) => this.draftOrders.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(this.describe(error, 'The assignable orders could not be loaded.')),
            });
    }

    // A 403 from the authorization middleware carries no ProblemDetails body at all, while a
    // 403 from TourAccess does - hence a per-call-site fallback rather than one shared string.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
