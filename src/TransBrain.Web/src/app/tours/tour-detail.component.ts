import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { LocationMapComponent, MapMarker } from '../shared/location-map.component';
import { Observable, of, Subscription, switchMap } from 'rxjs';
import { Order, OrderService } from '../orders/order.service';
import { Tour, TourRoute, TourService, tourStatusTone } from './tour.service';

@Component({
    selector: 'app-tour-detail',
    standalone: true,
    imports: [
        DatePipe,
        LocationMapComponent,
        MatButtonModule,
        MatCardModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatProgressBarModule,
        MatSelectModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ i18n.t('tours.detail') }}</h1>
                    @if (tour(); as t) {
                        <!-- tour-detail-* rather than tour-*: the list uses tour-status/tour-date/etc
                             on its cells, and a page-wide locator that matched both screens would be
                             ambiguous the moment a test navigated between them. -->
                        <mat-chip-set>
                            <mat-chip
                                class="chip"
                                [attr.data-tone]="statusTone(t.status)"
                                data-testid="tour-detail-status"
                                >{{ t.status }}</mat-chip
                            >
                        </mat-chip-set>
                    }
                </div>
                <div class="header-actions">
                    @if (tour(); as t) {
                        <!-- tourStatus.write, not dispatch.write: a fahrer may run their own tour
                             without being allowed to change what is on it. -->
                        @if (session.can('tourStatus.write')) {
                            @if (t.status === 'Planned') {
                                <button mat-flat-button data-testid="tour-start" (click)="start()">
                                    <mat-icon>play_arrow</mat-icon>
                                    {{ i18n.t('tours.start') }}
                                </button>
                            }
                            @if (t.status === 'InProgress') {
                                <button
                                    mat-flat-button
                                    data-testid="tour-complete"
                                    (click)="complete()"
                                >
                                    <mat-icon>flag</mat-icon>
                                    {{ i18n.t('tours.complete') }}
                                </button>
                            }
                        }
                    }
                    <a mat-stroked-button routerLink="/tours" data-testid="tour-back">
                        <mat-icon>arrow_back</mat-icon>
                        {{ i18n.t('tours.backToTours') }}
                    </a>
                </div>
            </header>

            @if (loadError(); as message) {
                <p class="error-banner" data-testid="tour-detail-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            @if (tour(); as t) {
                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="tour-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">route</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.section.tour') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <dl class="facts">
                                <div>
                                    <dt>{{ i18n.t('common.date') }}</dt>
                                    <dd data-testid="tour-detail-date">{{ t.tourDate }}</dd>
                                </div>
                                <div>
                                    <dt>{{ i18n.t('common.vehicle') }}</dt>
                                    <dd data-testid="tour-detail-vehicle">
                                        {{ t.vehicleLicensePlate }}
                                    </dd>
                                </div>
                                <div>
                                    <dt>{{ i18n.t('common.driver') }}</dt>
                                    <dd data-testid="tour-detail-driver">{{ t.driverName }}</dd>
                                </div>
                            </dl>
                        </mat-card-content>
                    </mat-card>

                    <!-- The headroom a dispatcher needs before choosing the next order. The API
                         already sends both numbers, so showing them costs nothing extra. -->
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">scale</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.capacity') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="capacity">
                            <p data-testid="tour-capacity-weight">
                                {{ t.totalWeightKg }} / {{ t.vehiclePayloadKg }} kg
                            </p>
                            <mat-progress-bar
                                mode="determinate"
                                [value]="percent(t.totalWeightKg, t.vehiclePayloadKg)"
                            />
                            <p data-testid="tour-capacity-meters">
                                {{ t.totalLoadMeters }} / {{ t.vehicleLoadMeters }}
                                {{ i18n.t('tours.loadMetersUnit') }}
                            </p>
                            <mat-progress-bar
                                mode="determinate"
                                [value]="percent(t.totalLoadMeters, t.vehicleLoadMeters)"
                            />
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined" class="full-width">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">format_list_numbered</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.stops') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (session.can('dispatch.write')) {
                                <!-- Only Draft orders are offered: any other status is refused by
                                     the domain, and offering a choice the server will reject is
                                     worse than not offering it. -->
                                <div class="assign">
                                    <mat-form-field subscriptSizing="dynamic">
                                        <mat-label>{{ i18n.t('tours.assignTitle') }}</mat-label>
                                        <mat-select
                                            [value]="selectedOrderId()"
                                            (valueChange)="selectedOrderId.set($event)"
                                            data-testid="tour-assign-select"
                                        >
                                            @for (order of assignableOrders(); track order.id) {
                                                <mat-option [value]="order.id">
                                                    {{ order.orderNumber }} —
                                                    {{ order.consignor.name }} ({{
                                                        order.cargoWeightKg
                                                    }}
                                                    kg)
                                                </mat-option>
                                            }
                                        </mat-select>
                                    </mat-form-field>
                                    <button
                                        mat-flat-button
                                        data-testid="tour-assign"
                                        (click)="assign()"
                                    >
                                        <mat-icon>playlist_add</mat-icon>
                                        {{ i18n.t('tours.assign') }}
                                    </button>
                                </div>
                            }
                            <div class="table-card">
                                <div class="table-scroll">
                                    <table mat-table [dataSource]="t.stops">
                                        <ng-container matColumnDef="sequence">
                                            <th mat-header-cell *matHeaderCellDef>#</th>
                                            <td
                                                mat-cell
                                                *matCellDef="let s"
                                                data-testid="tour-stop-sequence"
                                            >
                                                {{ s.sequence }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="orderNumber">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('common.order') }}
                                            </th>
                                            <td
                                                mat-cell
                                                *matCellDef="let s"
                                                class="strong"
                                                data-testid="tour-stop-order"
                                            >
                                                {{ s.orderNumber }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="stopType">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('common.type') }}
                                            </th>
                                            <td mat-cell *matCellDef="let s">
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        [attr.data-tone]="
                                                            s.stopType === 'Pickup'
                                                                ? 'warning'
                                                                : 'success'
                                                        "
                                                        data-testid="tour-stop-type"
                                                        >{{ s.stopType }}</mat-chip
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
                                            <td mat-cell *matCellDef="let s" class="actions-cell">
                                                @if (
                                                    s.stopType === 'Pickup' &&
                                                    session.can('dispatch.write')
                                                ) {
                                                    <button
                                                        mat-icon-button
                                                        class="delete"
                                                        data-testid="tour-remove"
                                                        [matTooltip]="i18n.t('tours.remove')"
                                                        [attr.aria-label]="
                                                            i18n.t('tours.remove') +
                                                            ' ' +
                                                            s.orderNumber
                                                        "
                                                        (click)="removeOrder(s.transportOrderId)"
                                                    >
                                                        <mat-icon>remove_circle_outline</mat-icon>
                                                    </button>
                                                }
                                            </td>
                                        </ng-container>
                                        <tr mat-header-row *matHeaderRowDef="stopColumns"></tr>
                                        <tr mat-row *matRowDef="let row; columns: stopColumns"></tr>
                                        <tr class="mat-mdc-row" *matNoDataRow>
                                            <td class="empty" [attr.colspan]="stopColumns.length">
                                                <mat-icon aria-hidden="true">inventory_2</mat-icon>
                                                <span data-testid="tour-stops-empty">{{
                                                    i18n.t('tours.stopsEmpty')
                                                }}</span>
                                            </td>
                                        </tr>
                                    </table>
                                </div>
                            </div>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined" class="full-width" data-testid="tour-route">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">map</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.route.title') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (routeError(); as message) {
                                <p class="hint" data-testid="tour-route-error">
                                    <mat-icon aria-hidden="true">info</mat-icon>{{ message }}
                                </p>
                            }
                            @if (tourRoute(); as r) {
                                <p class="route-summary">
                                    {{ i18n.t('tours.route.distance') }}:
                                    <span data-testid="tour-route-distance"
                                        >{{ formatKm(r.distanceMeters) }} km</span
                                    >
                                    &middot; {{ i18n.t('tours.route.drivingTime') }}:
                                    <span data-testid="tour-route-duration"
                                        >{{ formatDuration(r.drivingSeconds) }} h</span
                                    >
                                </p>
                                @if (r.earliestNextStart; as next) {
                                    <p class="hint" data-testid="tour-route-next-start">
                                        <mat-icon aria-hidden="true">schedule</mat-icon>
                                        {{ i18n.t('tours.route.earliestNextStart') }}:
                                        {{ next | date: 'short' }}
                                    </p>
                                }
                                <app-location-map [markers]="stopMarkers()" [route]="r.geometry" />

                                <h3 class="sub-title">{{ i18n.t('tours.route.schedule') }}</h3>
                                <div class="table-card">
                                    <div class="table-scroll">
                                        <table
                                            mat-table
                                            [dataSource]="r.schedule"
                                            data-testid="tour-route-schedule"
                                        >
                                            <ng-container matColumnDef="kind">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.type') }}
                                                </th>
                                                <td mat-cell *matCellDef="let entry">
                                                    {{ enumText('tours.route.kind', entry.kind) }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="start">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.from') }}
                                                </th>
                                                <td mat-cell *matCellDef="let entry">
                                                    {{ entry.start | date: 'short' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="end">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.to') }}
                                                </th>
                                                <td mat-cell *matCellDef="let entry">
                                                    {{ entry.end | date: 'short' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="country">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.country') }}
                                                </th>
                                                <td mat-cell *matCellDef="let entry">
                                                    {{ entry.countryCode ?? '' }}
                                                </td>
                                            </ng-container>
                                            <tr
                                                mat-header-row
                                                *matHeaderRowDef="scheduleColumns"
                                            ></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: scheduleColumns"
                                            ></tr>
                                        </table>
                                    </div>
                                </div>

                                <h3 class="sub-title">{{ i18n.t('tours.route.violations') }}</h3>
                                @if (r.violations.length) {
                                    <ul class="violations" data-testid="tour-route-violations">
                                        @for (violation of r.violations; track $index) {
                                            <li
                                                [class.violation]="
                                                    violation.severity === 'Violation'
                                                "
                                                [class.warning]="violation.severity === 'Warning'"
                                            >
                                                <mat-icon aria-hidden="true">{{
                                                    violation.severity === 'Violation'
                                                        ? 'error'
                                                        : 'warning'
                                                }}</mat-icon>
                                                <span>
                                                    <strong
                                                        >{{
                                                            enumText(
                                                                'tours.route.severity',
                                                                violation.severity
                                                            )
                                                        }}:</strong
                                                    >
                                                    {{ violation.message }}
                                                </span>
                                            </li>
                                        }
                                    </ul>
                                } @else {
                                    <p class="hint" data-testid="tour-route-no-violations">
                                        <mat-icon aria-hidden="true">check_circle</mat-icon>
                                        {{ i18n.t('tours.route.noViolations') }}
                                    </p>
                                }
                            }
                        </mat-card-content>
                    </mat-card>
                </div>
            }
        </div>
    `,
    styles: [
        `
            .facts {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
                gap: 16px;
                margin: 0;
            }

            .facts dt {
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .facts dd {
                margin: 2px 0 0;
                font: var(--mat-sys-title-medium);
            }

            .capacity p {
                margin: 0 0 6px;
                font-variant-numeric: tabular-nums;
            }

            .capacity mat-progress-bar {
                margin-bottom: 16px;
            }

            .assign {
                display: flex;
                flex-wrap: wrap;
                align-items: center;
                gap: 16px;
                margin-bottom: 16px;
            }

            .assign mat-form-field {
                flex: 1 1 320px;
            }

            .route-summary {
                margin: 0 0 8px;
            }

            .sub-title {
                margin: 24px 0 8px;
                font: var(--mat-sys-title-small);
            }

            .violations {
                margin: 0;
                padding: 0;
                list-style: none;
            }

            .violations li {
                display: flex;
                align-items: flex-start;
                gap: 8px;
                padding: 4px 0;
            }

            .violation {
                color: var(--mat-sys-error);
            }

            .warning {
                color: #6d3d00;
            }
        `,
    ],
})
export class TourDetailComponent {
    private readonly service = inject(TourService);
    private readonly orderService = inject(OrderService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out; a directly opened /tours/{id} would
    // otherwise send every request without a bearer token. The one checkAuth() behind it runs in
    // the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly tourId = this.route.snapshot.paramMap.get('id')!;
    protected readonly stopColumns = ['sequence', 'orderNumber', 'stopType', 'actions'];
    protected readonly scheduleColumns = ['kind', 'start', 'end', 'country'];
    protected readonly statusTone = tourStatusTone;
    protected readonly tour = signal<Tour | null>(null);
    protected readonly draftOrders = signal<Order[]>([]);
    protected readonly selectedOrderId = signal('');
    protected readonly loadError = signal<string | null>(null);
    // Separate from loadError: a refused assignment must not blank the page the way a failed
    // load does. This is where a 409 from the capacity or status rules reaches the user.
    protected readonly actionError = signal<string | null>(null);
    protected readonly tourRoute = signal<TourRoute | null>(null);
    protected readonly routeError = signal<string | null>(null);
    protected readonly stopMarkers = computed<MapMarker[]>(() =>
        (this.tourRoute()?.stops ?? []).map((stop) => ({
            latitude: stop.latitude,
            longitude: stop.longitude,
            label: String(stop.sequence),
            title: `${this.enumText('tours.route.stopType', stop.stopType)} ${stop.orderNumber}, ${stop.place}`,
        })),
    );
    private routeSub?: Subscription;

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
            this.actionError.set(this.i18n.t('tours.error.chooseOrder'));
            return;
        }

        this.actionError.set(null);
        this.run(this.service.assignOrder(this.tourId, orderId), this.i18n.t('tours.error.assign'));
    }

    protected removeOrder(transportOrderId: string): void {
        this.actionError.set(null);
        this.run(
            this.service.removeOrder(this.tourId, transportOrderId),
            this.i18n.t('tours.error.remove'),
        );
    }

    protected start(): void {
        this.actionError.set(null);
        this.run(this.service.start(this.tourId), this.i18n.t('tours.error.start'));
    }

    protected complete(): void {
        this.actionError.set(null);
        this.run(this.service.complete(this.tourId), this.i18n.t('tours.error.complete'));
    }

    protected percent(value: number, max: number): number {
        return max > 0 ? Math.min(100, (value / max) * 100) : 0;
    }

    protected formatKm(meters: number): string {
        return new Intl.NumberFormat(this.i18n.locale(), { maximumFractionDigits: 1 }).format(
            meters / 1000,
        );
    }

    protected formatDuration(seconds: number): string {
        const minutes = Math.round(seconds / 60);
        return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}`;
    }

    protected enumText(prefix: string, value: string): string {
        return this.i18n.t(`${prefix}.${value}` as TranslationKey) ?? value;
    }

    private run(action: Observable<Tour>, fallback: string): void {
        this.session.ready.pipe(switchMap(() => action)).subscribe({
            next: (tour) => {
                this.tour.set(tour);
                this.selectedOrderId.set('');
                this.loadDraftOrders();
                this.loadRoute();
            },
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, fallback)),
        });
    }

    private refresh(): void {
        this.session.ready.pipe(switchMap(() => this.service.getById(this.tourId))).subscribe({
            next: (tour) => {
                this.tour.set(tour);
                this.loadDraftOrders();
                this.loadRoute();
            },
            // The load path gets its own wording: "could not be assigned" on a failed getById
            // would be plainly wrong.
            error: (error: HttpErrorResponse) =>
                this.loadError.set(this.describe(error, this.i18n.t('tours.error.load'))),
        });
    }

    // Stops change with every assign or remove, so the route is refetched after each one. A
    // refetch supersedes one still in flight, so a slow earlier answer cannot overwrite a newer.
    private loadRoute(): void {
        this.routeSub?.unsubscribe();
        this.routeSub = this.session.ready
            .pipe(switchMap(() => this.service.getRoute(this.tourId)))
            .subscribe({
                next: (route) => {
                    this.tourRoute.set(route);
                    this.routeError.set(null);
                },
                error: (error: HttpErrorResponse) => {
                    this.tourRoute.set(null);
                    const detail: string = error.error?.detail ?? '';
                    this.routeError.set(
                        error.status === 503
                            ? this.i18n.t('tours.route.error.unavailable')
                            : error.error?.title === 'Geocoding.NoMatch'
                              ? this.i18n.t('tours.route.error.addressNotFound', {
                                    // The server quotes the address it could not place.
                                    address: /'(.+)'/.exec(detail)?.[1] ?? detail,
                                })
                              : this.describe(error, this.i18n.t('tours.route.error.load')),
                    );
                },
            });
    }

    // Orders come back sorted by order number ASCENDING, so page 1 is the OLDEST drafts - which
    // in a running installation are the ones nobody has planned for weeks, not the ones a
    // dispatcher is working on today. Fetch the last page instead: one extra request only when
    // there are more drafts than fit on a page, and never more than the API's 100-row cap.
    // Beyond that a searchable picker is needed; a bigger page will not save it.
    private loadDraftOrders(): void {
        const pageSize = 100;

        this.session.ready
            .pipe(
                switchMap(() => this.orderService.list('Draft', pageSize)),
                switchMap((first) => {
                    const lastPage = Math.max(1, Math.ceil(first.totalCount / pageSize));
                    return lastPage === 1
                        ? of(first)
                        : this.orderService.list('Draft', pageSize, lastPage);
                }),
            )
            .subscribe({
                next: (page) => this.draftOrders.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(
                        this.describe(error, this.i18n.t('tours.error.assignable')),
                    ),
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
