import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Area, Capability } from '../auth/capabilities';
import { SessionService } from '../auth/session.service';
import { DriverService } from '../drivers/driver.service';
import { Order, OrderService } from '../orders/order.service';
import { Tour, TourService } from '../tours/tour.service';
import { VehicleService } from '../vehicles/vehicle.service';

interface AreaTile {
    area: Area;
    title: string;
    description: string;
    route: string;
    addLabel: string;
    addRoute: string;
    addCapability: Capability;
}

const TILES: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'Vehicles',
        description: 'Fleet master data: plates, payload, inspections.',
        route: '/vehicles',
        addLabel: 'Add vehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'Drivers',
        description: 'Driver master data: licences, availability.',
        route: '/drivers',
        addLabel: 'Add driver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'Orders',
        description: 'Transport orders from pickup to delivery.',
        route: '/orders',
        addLabel: 'New order',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'Tours',
        description: 'Plan orders onto vehicles and follow execution.',
        route: '/tours',
        addLabel: 'Plan tour',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

/** One row is enough when only totalCount is read. */
const COUNT_ONLY = 1;

/** Five rows fit the block; the rest is one click away on /orders. */
const DRAFT_PREVIEW_SIZE = 5;

function today(): string {
    // The API takes a DateOnly, so an ISO date without a time part. Local date, not UTC:
    // toISOString() would roll over a day early for anyone east of Greenwich after their evening.
    const now = new Date();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
}

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [MatButtonModule, MatCardModule, MatChipsModule, MatTableModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1 data-testid="home-greeting">Welcome, {{ session.displayName() }}</h1>
            <mat-chip-set>
                @for (role of session.roles(); track role) {
                    <mat-chip data-testid="home-role-chip">{{ role }}</mat-chip>
                }
            </mat-chip-set>

            <section class="kpis">
                @if (session.areas().has('vehicles')) {
                    <mat-card>
                        <mat-card-title>Vehicles</mat-card-title>
                        <mat-card-content>
                            @if (vehicleError(); as message) {
                                <p data-testid="home-kpi-vehicles-error">{{ message }}</p>
                            } @else {
                                <p>
                                    Available:
                                    <strong data-testid="home-kpi-vehicles-available">{{ vehiclesAvailable() }}</strong>
                                </p>
                                <p>
                                    In workshop:
                                    <strong data-testid="home-kpi-vehicles-workshop">{{ vehiclesInWorkshop() }}</strong>
                                </p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                @if (session.areas().has('drivers')) {
                    <mat-card>
                        <mat-card-title>Drivers</mat-card-title>
                        <mat-card-content>
                            @if (driverError(); as message) {
                                <p data-testid="home-kpi-drivers-error">{{ message }}</p>
                            } @else {
                                <p>
                                    Available:
                                    <strong data-testid="home-kpi-drivers-available">{{ driversAvailable() }}</strong>
                                </p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                @if (session.areas().has('orders')) {
                    <mat-card>
                        <mat-card-title>Orders</mat-card-title>
                        <mat-card-content>
                            @if (orderError(); as message) {
                                <p data-testid="home-kpi-orders-error">{{ message }}</p>
                            } @else {
                                <p>
                                    In draft:
                                    <strong data-testid="home-kpi-orders-draft">{{ ordersInDraft() }}</strong>
                                </p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                <mat-card>
                    <mat-card-title>Tours today</mat-card-title>
                    <mat-card-content>
                        @if (tourError(); as message) {
                            <p data-testid="home-kpi-tours-error">{{ message }}</p>
                        } @else {
                            <p><strong data-testid="home-kpi-tours-today">{{ toursToday() }}</strong></p>
                        }
                    </mat-card-content>
                </mat-card>
            </section>

            @if (session.can('dispatch.write')) {
                <section data-testid="home-draft-orders">
                    <h2>Orders awaiting a tour</h2>
                    @if (draftOrdersError(); as message) {
                        <p data-testid="home-draft-orders-error">{{ message }}</p>
                    } @else {
                        <table mat-table [dataSource]="draftOrders()">
                            <ng-container matColumnDef="orderNumber">
                                <th mat-header-cell *matHeaderCellDef>Order</th>
                                <td mat-cell *matCellDef="let o" data-testid="home-draft-order-row">
                                    {{ o.orderNumber }}
                                </td>
                            </ng-container>
                            <ng-container matColumnDef="route">
                                <th mat-header-cell *matHeaderCellDef>Route</th>
                                <td mat-cell *matCellDef="let o">{{ o.consignor.city }} → {{ o.consignee.city }}</td>
                            </ng-container>
                            <ng-container matColumnDef="actions">
                                <th mat-header-cell *matHeaderCellDef>Actions</th>
                                <td mat-cell *matCellDef="let o">
                                    <a mat-button [routerLink]="['/orders', o.id]" data-testid="home-draft-order-open">
                                        Open
                                    </a>
                                </td>
                            </ng-container>
                            <tr mat-header-row *matHeaderRowDef="draftColumns"></tr>
                            <tr mat-row *matRowDef="let row; columns: draftColumns"></tr>
                        </table>
                        <!-- No per-row "Plan" button: an order is put onto a tour by the picker on
                             /tours/:id, and no endpoint creates a tour from an order. The honest
                             action here is to start a tour and assign from there. -->
                        <a mat-raised-button routerLink="/tours/new" data-testid="home-plan-tour">Plan a tour</a>
                    }
                </section>
            }

            @if (session.hasRole('fahrer')) {
                <section data-testid="home-my-tours">
                    <h2>My tours today</h2>
                    @if (myToursError(); as message) {
                        <p data-testid="home-my-tours-error">{{ message }}</p>
                    }
                    @for (tour of myTours(); track tour.id) {
                        <mat-card data-testid="home-my-tour-row">
                            <mat-card-title>{{ tour.vehicleLicensePlate }} — {{ tour.status }}</mat-card-title>
                            <mat-card-actions>
                                <a mat-button [routerLink]="['/tours', tour.id]">Open</a>
                                @if (tour.status === 'Planned') {
                                    <button mat-raised-button data-testid="home-my-tour-start" (click)="startTour(tour)">
                                        Start tour
                                    </button>
                                }
                                @if (tour.status === 'InProgress') {
                                    <button
                                        mat-raised-button
                                        data-testid="home-my-tour-complete"
                                        (click)="completeTour(tour)"
                                    >
                                        Complete tour
                                    </button>
                                }
                            </mat-card-actions>
                        </mat-card>
                    }
                </section>
            }

            <section class="tiles">
                @for (tile of tiles; track tile.area) {
                    @if (session.areas().has(tile.area)) {
                        <mat-card [attr.data-testid]="'home-tile-' + tile.area">
                            <mat-card-title>{{ tile.title }}</mat-card-title>
                            <mat-card-content>{{ tile.description }}</mat-card-content>
                            <mat-card-actions>
                                <a mat-button [routerLink]="tile.route">Open</a>
                                @if (session.can(tile.addCapability)) {
                                    <a
                                        mat-raised-button
                                        [routerLink]="tile.addRoute"
                                        [attr.data-testid]="'home-tile-' + tile.area + '-add'"
                                        >{{ tile.addLabel }}</a
                                    >
                                }
                            </mat-card-actions>
                        </mat-card>
                    }
                }
            </section>
        } @else {
            @if (session.error(); as message) {
                <p data-testid="home-error">{{ message }}</p>
            }
            <button mat-raised-button data-testid="login" (click)="session.login()">Sign in</button>
        }
    `,
    styles: `
        .kpis,
        .tiles {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
        }

        .kpis {
            margin-bottom: 1.5rem;
        }

        .kpis mat-card,
        .tiles mat-card {
            flex: 1 1 16rem;
        }
    `,
})
export class HomeComponent {
    protected readonly session = inject(SessionService);
    private readonly vehicles = inject(VehicleService);
    private readonly drivers = inject(DriverService);
    private readonly orders = inject(OrderService);
    private readonly tours = inject(TourService);

    protected readonly tiles = TILES;
    protected readonly draftColumns = ['orderNumber', 'route', 'actions'];

    protected readonly vehiclesAvailable = signal(0);
    protected readonly vehiclesInWorkshop = signal(0);
    protected readonly driversAvailable = signal(0);
    protected readonly ordersInDraft = signal(0);
    protected readonly toursToday = signal(0);
    protected readonly draftOrders = signal<Order[]>([]);
    protected readonly myTours = signal<Tour[]>([]);

    // One error signal per block, not one for the page: a failing vehicle count must not blank
    // the work list next to it. Same separation the lists already make between errorMessage and
    // actionError.
    protected readonly vehicleError = signal<string | null>(null);
    protected readonly driverError = signal<string | null>(null);
    protected readonly orderError = signal<string | null>(null);
    protected readonly tourError = signal<string | null>(null);
    protected readonly draftOrdersError = signal<string | null>(null);
    protected readonly myToursError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.loadBlocks();
            }
        });
    }

    protected startTour(tour: Tour): void {
        this.myToursError.set(null);
        this.tours.start(tour.id).subscribe({
            next: () => this.loadMyTours(),
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, 'The tour could not be started.')),
        });
    }

    protected completeTour(tour: Tour): void {
        this.myToursError.set(null);
        this.tours.complete(tour.id).subscribe({
            next: () => this.loadMyTours(),
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, 'The tour could not be completed.')),
        });
    }

    private loadBlocks(): void {
        const areas = this.session.areas();

        if (areas.has('vehicles')) {
            this.vehicles.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.vehiclesAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(this.describe(error, 'The vehicle counts could not be loaded.')),
            });
            this.vehicles.list(COUNT_ONLY, 'InWorkshop').subscribe({
                next: (page) => this.vehiclesInWorkshop.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(this.describe(error, 'The vehicle counts could not be loaded.')),
            });
        }

        if (areas.has('drivers')) {
            this.drivers.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.driversAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.driverError.set(this.describe(error, 'The driver counts could not be loaded.')),
            });
        }

        if (areas.has('orders')) {
            this.orders.list('Draft', COUNT_ONLY).subscribe({
                next: (page) => this.ordersInDraft.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.orderError.set(this.describe(error, 'The order counts could not be loaded.')),
            });
        }

        // Every role sees this one - for a fahrer the API narrows it to their own tours, see
        // ListToursQueryHandler. That is also why the frontend never needs to know its own
        // driverId.
        this.tours.list({ tourDate: today() }).subscribe({
            next: (page) => {
                this.toursToday.set(page.totalCount);
                if (this.session.hasRole('fahrer')) {
                    this.myTours.set(page.items);
                }
            },
            error: (error: HttpErrorResponse) =>
                this.tourError.set(this.describe(error, "Today's tours could not be loaded.")),
        });

        if (this.session.can('dispatch.write')) {
            this.orders.list('Draft', DRAFT_PREVIEW_SIZE).subscribe({
                next: (page) => this.draftOrders.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.draftOrdersError.set(this.describe(error, 'The draft orders could not be loaded.')),
            });
        }
    }

    private loadMyTours(): void {
        this.tours.list({ tourDate: today() }).subscribe({
            next: (page) => {
                this.myTours.set(page.items);
                this.toursToday.set(page.totalCount);
            },
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, "Today's tours could not be reloaded.")),
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
