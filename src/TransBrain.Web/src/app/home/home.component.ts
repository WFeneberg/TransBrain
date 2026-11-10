import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Area, Capability } from '../auth/capabilities';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { DriverService } from '../drivers/driver.service';
import { Order, OrderService } from '../orders/order.service';
import { Tour, TourService, tourStatusTone } from '../tours/tour.service';
import { VehicleService } from '../vehicles/vehicle.service';

interface AreaTile {
    area: Area;
    icon: string;
    title: TranslationKey;
    description: TranslationKey;
    route: string;
    addLabel: TranslationKey;
    addRoute: string;
    addCapability: Capability;
}

const TILES: readonly AreaTile[] = [
    {
        area: 'vehicles',
        icon: 'local_shipping',
        title: 'nav.vehicles',
        description: 'home.tile.vehicles',
        route: '/vehicles',
        addLabel: 'home.tile.addVehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        icon: 'person',
        title: 'nav.drivers',
        description: 'home.tile.drivers',
        route: '/drivers',
        addLabel: 'home.tile.addDriver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        icon: 'assignment',
        title: 'nav.orders',
        description: 'home.tile.orders',
        route: '/orders',
        addLabel: 'home.tile.addOrder',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        icon: 'route',
        title: 'nav.tours',
        description: 'home.tile.tours',
        route: '/tours',
        addLabel: 'home.tile.addTour',
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
    imports: [
        MatButtonModule,
        MatCardModule,
        MatChipsModule,
        MatIconModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            @if (session.isAuthenticated()) {
                <header class="page-header">
                    <div>
                        <div class="title-row">
                            <h1 data-testid="home-greeting">
                                {{ i18n.t('home.welcome', { name: session.displayName() }) }}
                            </h1>
                            <mat-chip-set>
                                @for (role of session.roles(); track role) {
                                    <mat-chip
                                        class="chip"
                                        data-tone="neutral"
                                        data-testid="home-role-chip"
                                        >{{ role }}</mat-chip
                                    >
                                }
                            </mat-chip-set>
                        </div>
                        <p class="subtitle">{{ i18n.t('home.subtitle') }}</p>
                    </div>
                </header>

                <section class="kpis">
                    @if (session.areas().has('vehicles')) {
                        <mat-card appearance="outlined">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">local_shipping</mat-icon>
                                <mat-card-title>{{ i18n.t('nav.vehicles') }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                @if (vehicleError(); as message) {
                                    <p class="error-banner" data-testid="home-kpi-vehicles-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                } @else {
                                    <p class="kpi">
                                        <span>{{ i18n.t('home.available') }}</span>
                                        <strong data-testid="home-kpi-vehicles-available">{{
                                            vehiclesAvailable()
                                        }}</strong>
                                    </p>
                                    <p class="kpi">
                                        <span>{{ i18n.t('home.inWorkshop') }}</span>
                                        <strong data-testid="home-kpi-vehicles-workshop">{{
                                            vehiclesInWorkshop()
                                        }}</strong>
                                    </p>
                                }
                            </mat-card-content>
                        </mat-card>
                    }
                    @if (session.areas().has('drivers')) {
                        <mat-card appearance="outlined">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">person</mat-icon>
                                <mat-card-title>{{ i18n.t('nav.drivers') }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                @if (driverError(); as message) {
                                    <p class="error-banner" data-testid="home-kpi-drivers-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                } @else {
                                    <p class="kpi">
                                        <span>{{ i18n.t('home.available') }}</span>
                                        <strong data-testid="home-kpi-drivers-available">{{
                                            driversAvailable()
                                        }}</strong>
                                    </p>
                                }
                            </mat-card-content>
                        </mat-card>
                    }
                    @if (session.areas().has('orders')) {
                        <mat-card appearance="outlined">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">assignment</mat-icon>
                                <mat-card-title>{{ i18n.t('nav.orders') }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                @if (orderError(); as message) {
                                    <p class="error-banner" data-testid="home-kpi-orders-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                } @else {
                                    <p class="kpi">
                                        <span>{{ i18n.t('home.inDraft') }}</span>
                                        <strong data-testid="home-kpi-orders-draft">{{
                                            ordersInDraft()
                                        }}</strong>
                                    </p>
                                }
                            </mat-card-content>
                        </mat-card>
                    }
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">route</mat-icon>
                            <mat-card-title>{{ i18n.t('home.toursToday') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (tourError(); as message) {
                                <p class="error-banner" data-testid="home-kpi-tours-error">
                                    <mat-icon aria-hidden="true">error_outline</mat-icon
                                    >{{ message }}
                                </p>
                            } @else {
                                <p class="kpi">
                                    <span>{{ todayLabel() }}</span>
                                    <strong data-testid="home-kpi-tours-today">{{
                                        toursToday()
                                    }}</strong>
                                </p>
                            }
                        </mat-card-content>
                    </mat-card>
                </section>

                @if (session.can('dispatch.write')) {
                    <mat-card appearance="outlined" data-testid="home-draft-orders">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">pending_actions</mat-icon>
                            <mat-card-title>{{ i18n.t('home.draftOrders') }}</mat-card-title>
                            <span class="spacer"></span>
                            @if (!draftOrdersError()) {
                                <!-- No per-row "Plan" button: an order is put onto a tour by the
                                     picker on /tours/:id, and no endpoint creates a tour from an
                                     order. The honest action here is to start a tour and assign
                                     from there. -->
                                <a
                                    mat-flat-button
                                    routerLink="/tours/new"
                                    data-testid="home-plan-tour"
                                >
                                    <mat-icon>add</mat-icon>
                                    {{ i18n.t('home.planTour') }}
                                </a>
                            }
                        </mat-card-header>
                        <mat-card-content>
                            @if (draftOrdersError(); as message) {
                                <p class="error-banner" data-testid="home-draft-orders-error">
                                    <mat-icon aria-hidden="true">error_outline</mat-icon
                                    >{{ message }}
                                </p>
                            } @else {
                                <div class="table-card">
                                    <div class="table-scroll">
                                        <table mat-table [dataSource]="draftOrders()">
                                            <ng-container matColumnDef="orderNumber">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.order') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let o"
                                                    class="strong"
                                                    data-testid="home-draft-order-row"
                                                >
                                                    {{ o.orderNumber }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="route">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('home.route') }}
                                                </th>
                                                <td mat-cell *matCellDef="let o">
                                                    {{ o.consignor.city }} → {{ o.consignee.city }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="actions">
                                                <th
                                                    mat-header-cell
                                                    *matHeaderCellDef
                                                    class="actions-cell"
                                                >
                                                    <span class="cdk-visually-hidden">{{
                                                        i18n.t('common.actions')
                                                    }}</span>
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let o"
                                                    class="actions-cell"
                                                >
                                                    <a
                                                        mat-icon-button
                                                        [routerLink]="['/orders', o.id]"
                                                        data-testid="home-draft-order-open"
                                                        [matTooltip]="i18n.t('common.open')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.open') +
                                                            ' ' +
                                                            o.orderNumber
                                                        "
                                                    >
                                                        <mat-icon>open_in_new</mat-icon>
                                                    </a>
                                                </td>
                                            </ng-container>
                                            <tr mat-header-row *matHeaderRowDef="draftColumns"></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: draftColumns"
                                            ></tr>
                                            <tr class="mat-mdc-row" *matNoDataRow>
                                                <td
                                                    class="empty"
                                                    [attr.colspan]="draftColumns.length"
                                                >
                                                    <mat-icon aria-hidden="true">task_alt</mat-icon>
                                                    <span>{{
                                                        i18n.t('home.draftOrdersEmpty')
                                                    }}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </div>
                                </div>
                            }
                        </mat-card-content>
                    </mat-card>
                }

                @if (session.hasRole('fahrer')) {
                    <mat-card appearance="outlined" data-testid="home-my-tours">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">route</mat-icon>
                            <mat-card-title>{{ i18n.t('home.myTours') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (myToursError(); as message) {
                                <p class="error-banner" data-testid="home-my-tours-error">
                                    <mat-icon aria-hidden="true">error_outline</mat-icon
                                    >{{ message }}
                                </p>
                            }
                            <div class="table-card">
                                <div class="table-scroll">
                                    <table mat-table [dataSource]="myTours()">
                                        <ng-container matColumnDef="vehicle">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('common.vehicle') }}
                                            </th>
                                            <td mat-cell *matCellDef="let tour" class="strong">
                                                {{ tour.vehicleLicensePlate }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="status">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('common.status') }}
                                            </th>
                                            <td mat-cell *matCellDef="let tour">
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        [attr.data-tone]="statusTone(tour.status)"
                                                        >{{ tour.status }}</mat-chip
                                                    >
                                                </mat-chip-set>
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="actions">
                                            <th
                                                mat-header-cell
                                                *matHeaderCellDef
                                                class="actions-cell"
                                            >
                                                <span class="cdk-visually-hidden">{{
                                                    i18n.t('common.actions')
                                                }}</span>
                                            </th>
                                            <td
                                                mat-cell
                                                *matCellDef="let tour"
                                                class="actions-cell"
                                            >
                                                @if (tour.status === 'Planned') {
                                                    <button
                                                        mat-flat-button
                                                        data-testid="home-my-tour-start"
                                                        (click)="startTour(tour)"
                                                    >
                                                        <mat-icon>play_arrow</mat-icon>
                                                        {{ i18n.t('tours.start') }}
                                                    </button>
                                                }
                                                @if (tour.status === 'InProgress') {
                                                    <button
                                                        mat-flat-button
                                                        data-testid="home-my-tour-complete"
                                                        (click)="completeTour(tour)"
                                                    >
                                                        <mat-icon>flag</mat-icon>
                                                        {{ i18n.t('tours.complete') }}
                                                    </button>
                                                }
                                                <a
                                                    mat-icon-button
                                                    [routerLink]="['/tours', tour.id]"
                                                    [matTooltip]="i18n.t('common.open')"
                                                    [attr.aria-label]="
                                                        i18n.t('common.open') +
                                                        ' ' +
                                                        tour.vehicleLicensePlate
                                                    "
                                                >
                                                    <mat-icon>open_in_new</mat-icon>
                                                </a>
                                            </td>
                                        </ng-container>
                                        <tr mat-header-row *matHeaderRowDef="myTourColumns"></tr>
                                        <tr
                                            mat-row
                                            *matRowDef="let row; columns: myTourColumns"
                                            data-testid="home-my-tour-row"
                                        ></tr>
                                        <tr class="mat-mdc-row" *matNoDataRow>
                                            <td class="empty" [attr.colspan]="myTourColumns.length">
                                                <mat-icon aria-hidden="true"
                                                    >event_available</mat-icon
                                                >
                                                <span>{{ i18n.t('home.myToursEmpty') }}</span>
                                            </td>
                                        </tr>
                                    </table>
                                </div>
                            </div>
                        </mat-card-content>
                    </mat-card>
                }

                <section class="tiles">
                    @for (tile of tiles; track tile.area) {
                        @if (session.areas().has(tile.area)) {
                            <mat-card
                                appearance="outlined"
                                [attr.data-testid]="'home-tile-' + tile.area"
                            >
                                <mat-card-header>
                                    <mat-icon mat-card-avatar class="avatar">{{
                                        tile.icon
                                    }}</mat-icon>
                                    <mat-card-title>{{ i18n.t(tile.title) }}</mat-card-title>
                                </mat-card-header>
                                <mat-card-content class="tile-text">{{
                                    i18n.t(tile.description)
                                }}</mat-card-content>
                                <mat-card-actions align="end">
                                    <a mat-button [routerLink]="tile.route">
                                        <mat-icon>arrow_forward</mat-icon>
                                        {{ i18n.t('common.open') }}
                                    </a>
                                    @if (session.can(tile.addCapability)) {
                                        <a
                                            mat-flat-button
                                            [routerLink]="tile.addRoute"
                                            [attr.data-testid]="'home-tile-' + tile.area + '-add'"
                                        >
                                            <mat-icon>add</mat-icon>
                                            {{ i18n.t(tile.addLabel) }}
                                        </a>
                                    }
                                </mat-card-actions>
                            </mat-card>
                        }
                    }
                </section>
            } @else {
                <mat-card appearance="outlined" class="signed-out">
                    <mat-card-header>
                        <mat-icon mat-card-avatar class="avatar">lock</mat-icon>
                        <mat-card-title>TransBrain</mat-card-title>
                    </mat-card-header>
                    <mat-card-content>
                        @if (session.error(); as message) {
                            <p class="error-banner" data-testid="home-error">
                                <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                            </p>
                        }
                    </mat-card-content>
                    <mat-card-actions align="end">
                        <button mat-flat-button data-testid="login" (click)="session.login()">
                            <mat-icon>login</mat-icon>
                            {{ i18n.t('auth.signIn') }}
                        </button>
                    </mat-card-actions>
                </mat-card>
            }
        </div>
    `,
    styles: `
        .kpis,
        .tiles {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 16px;
        }

        .kpi {
            display: flex;
            align-items: baseline;
            justify-content: space-between;
            gap: 12px;
            margin: 4px 0;
            color: var(--mat-sys-on-surface-variant);
        }

        .kpi strong {
            color: var(--mat-sys-on-surface);
            font: var(--mat-sys-display-small);
            font-variant-numeric: tabular-nums;
        }

        .spacer {
            flex: 1;
        }

        mat-card-header a {
            align-self: center;
        }

        .tile-text {
            flex: 1;
            color: var(--mat-sys-on-surface-variant);
        }

        .tiles mat-card {
            display: flex;
            flex-direction: column;
        }

        .signed-out {
            max-width: 440px;
            margin: 48px auto 0;
            width: 100%;
        }
    `,
})
export class HomeComponent {
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);
    private readonly vehicles = inject(VehicleService);
    private readonly drivers = inject(DriverService);
    private readonly orders = inject(OrderService);
    private readonly tours = inject(TourService);

    protected readonly tiles = TILES;
    protected readonly draftColumns = ['orderNumber', 'route', 'actions'];
    protected readonly myTourColumns = ['vehicle', 'status', 'actions'];
    protected readonly statusTone = tourStatusTone;
    protected readonly todayLabel = computed(() =>
        new Date().toLocaleDateString(this.i18n.locale(), { dateStyle: 'full' }),
    );

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
                this.myToursError.set(this.describe(error, this.i18n.t('tours.error.start'))),
        });
    }

    protected completeTour(tour: Tour): void {
        this.myToursError.set(null);
        this.tours.complete(tour.id).subscribe({
            next: () => this.loadMyTours(),
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, this.i18n.t('tours.error.complete'))),
        });
    }

    private loadBlocks(): void {
        const areas = this.session.areas();

        if (areas.has('vehicles')) {
            this.vehicles.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.vehiclesAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(
                        this.describe(error, this.i18n.t('home.error.vehicleCounts')),
                    ),
            });
            this.vehicles.list(COUNT_ONLY, 'InWorkshop').subscribe({
                next: (page) => this.vehiclesInWorkshop.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(
                        this.describe(error, this.i18n.t('home.error.vehicleCounts')),
                    ),
            });
        }

        if (areas.has('drivers')) {
            this.drivers.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.driversAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.driverError.set(
                        this.describe(error, this.i18n.t('home.error.driverCounts')),
                    ),
            });
        }

        if (areas.has('orders')) {
            this.orders.list('Draft', COUNT_ONLY).subscribe({
                next: (page) => this.ordersInDraft.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.orderError.set(
                        this.describe(error, this.i18n.t('home.error.orderCounts')),
                    ),
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
                this.tourError.set(this.describe(error, this.i18n.t('home.error.toursToday'))),
        });

        if (this.session.can('dispatch.write')) {
            this.orders.list('Draft', DRAFT_PREVIEW_SIZE).subscribe({
                next: (page) => this.draftOrders.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.draftOrdersError.set(
                        this.describe(error, this.i18n.t('home.error.draftOrders')),
                    ),
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
                this.myToursError.set(this.describe(error, this.i18n.t('home.error.toursReload'))),
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
