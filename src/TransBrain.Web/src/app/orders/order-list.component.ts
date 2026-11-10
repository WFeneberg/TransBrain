import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { Order, OrderService } from './order.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

const BASE_COLUMNS = ['orderNumber', 'consignor', 'consignee', 'cargo', 'pickup', 'status'];

@Component({
    selector: 'app-order-list',
    standalone: true,
    imports: [
        DatePipe,
        MatButtonModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatSelectModule,
        MatSortModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('nav.orders') }}</h1>
                        <p class="subtitle" data-testid="order-count">
                            {{ i18n.t('orders.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    @if (session.can('dispatch.write')) {
                        <div class="header-actions">
                            <a mat-flat-button routerLink="/orders/new" data-testid="order-add">
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('orders.add') }}
                            </a>
                        </div>
                    }
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="order-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <section class="filter-bar">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
                        <mat-label>{{ i18n.t('orders.search') }}</mat-label>
                        <mat-icon matPrefix>search</mat-icon>
                        <input
                            matInput
                            data-testid="order-search"
                            [value]="search()"
                            (input)="search.set($any($event.target).value)"
                            [placeholder]="i18n.t('orders.searchHint')"
                        />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.status') }}</mat-label>
                        <mat-select
                            [value]="statusFilter()"
                            (valueChange)="filterByStatus($event)"
                            data-testid="order-status-filter"
                        >
                            <mat-option [value]="''">{{ i18n.t('common.all') }}</mat-option>
                            @for (status of statusOptions; track status) {
                                <mat-option [value]="status">{{ status }}</mat-option>
                            }
                        </mat-select>
                    </mat-form-field>
                </section>

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="order-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="orderNumber">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('orders.number') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let o"
                                        class="strong"
                                        data-testid="order-number"
                                    >{{ o.orderNumber }}</td>
                                </ng-container>
                                <ng-container matColumnDef="consignor">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('orders.consignor') }}
                                    </th>
                                    <td mat-cell *matCellDef="let o" data-testid="order-consignor">
                                        {{ o.consignor.name }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="consignee">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('orders.consignee') }}
                                    </th>
                                    <td mat-cell *matCellDef="let o" data-testid="order-consignee">
                                        {{ o.consignee.name }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="cargo">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('orders.cargo') }}
                                    </th>
                                    <td mat-cell *matCellDef="let o" data-testid="order-cargo">
                                        {{ o.cargoDescription }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="pickup">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('orders.pickup') }}
                                    </th>
                                    <td mat-cell *matCellDef="let o">
                                        {{ o.pickupFrom | date: 'short' }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let o">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="statusTone(o.status)"
                                                data-testid="order-status"
                                                >{{ o.status }}</mat-chip
                                            >
                                        </mat-chip-set>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let o" class="actions-cell">
                                        <a
                                            mat-icon-button
                                            [routerLink]="['/orders', o.id]"
                                            data-testid="order-edit"
                                            [matTooltip]="i18n.t('common.edit')"
                                            [attr.aria-label]="
                                                i18n.t('common.edit') + ' ' + o.orderNumber
                                            "
                                        >
                                            <mat-icon>edit</mat-icon>
                                        </a>
                                        <button
                                            mat-icon-button
                                            class="delete"
                                            data-testid="order-cancel"
                                            [matTooltip]="i18n.t('orders.cancel')"
                                            [attr.aria-label]="
                                                i18n.t('orders.cancel') + ' ' + o.orderNumber
                                            "
                                            (click)="cancel(o)"
                                        >
                                            <mat-icon>block</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns()"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">inventory_2</mat-icon>
                                            <span data-testid="order-empty">{{
                                                i18n.t('orders.empty')
                                            }}</span>
                                        }
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </section>
                }
            </div>
        } @else {
            <p>{{ i18n.t('orders.signIn') }}</p>
        }
    `,
})
export class OrderListComponent {
    private readonly service = inject(OrderService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    // Edit and cancel need dispatch.write - the same gate as the /orders/:id route guard and the
    // API's DispatchWrite policy, so a viewer is not offered actions that would only end in a 403.
    protected readonly columns = computed(() =>
        this.session.can('dispatch.write') ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
    );
    protected readonly statusOptions = ['Draft', 'Planned', 'InTransit', 'Delivered', 'Cancelled'];
    protected readonly orders = signal<Order[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed cancel must not hide the table the way a failed
    // list load does (mirrors DriverListComponent's actionError).
    protected readonly actionError = signal<string | null>(null);
    // Filters server-side, so the list is reloaded on change; the search filters client-side.
    protected readonly statusFilter = signal('');
    protected readonly search = signal('');

    private readonly criteria = computed(() => this.search().trim().toLowerCase());
    protected readonly filteredCount = computed(() => {
        const search = this.criteria();
        return this.orders().filter((order) => this.matches(order, search)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Order>([]);
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (order, search) => this.matches(order, search);
        this.dataSource.sortingDataAccessor = (order, column) => this.sortValue(order, column);

        effect(() => {
            this.dataSource.data = this.orders();
        });
        effect(() => {
            this.dataSource.filter = this.criteria();
        });
        effect(() => {
            this.dataSource.sort = this.sort() ?? null;
        });

        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    protected statusTone(status: string): string {
        switch (status) {
            case 'Delivered':
                return 'success';
            case 'InTransit':
                return 'warning';
            case 'Cancelled':
                return 'error';
            default:
                return 'neutral';
        }
    }

    protected filterByStatus(status: string): void {
        this.statusFilter.set(status);
        this.refresh();
    }

    // Shown for every order rather than hidden for the ones the domain will refuse: an order
    // already in transit answers 409 with a message explaining why, which is more useful than
    // a button that silently is not there.
    protected cancel(order: Order): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('orders.cancelTitle'),
            message: this.i18n.t('orders.cancelQuestion', { number: order.orderNumber }),
            confirmLabel: this.i18n.t('orders.cancelConfirm'),
            cancelLabel: this.i18n.t('orders.cancelKeep'),
            confirmTestId: 'order-cancel-confirm',
            cancelTestId: 'order-cancel-abort',
            icon: 'block',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.service.cancel(order.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                // A policy failure (e.g. a viewer's 403) is rejected by ASP.NET's authorization
                // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
                // describe()'s fallback text is therefore action-specific, not the list-load one.
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(this.describe(error, this.i18n.t('orders.error.cancel'))),
            });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list(this.statusFilter(), LIST_PAGE_SIZE).subscribe({
            next: (page) => {
                this.orders.set(page.items);
                this.errorMessage.set(null);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('orders.error.list')));
            },
        });
    }

    private matches(order: Order, search: string): boolean {
        if (!search) {
            return true;
        }
        return [
            order.orderNumber,
            order.consignor.name,
            order.consignor.city,
            order.consignee.name,
            order.consignee.city,
            order.cargoDescription,
        ].some((value) => !!value && value.toLowerCase().includes(search));
    }

    private sortValue(order: Order, column: string): string {
        switch (column) {
            case 'consignor':
                return order.consignor.name.toLowerCase();
            case 'consignee':
                return order.consignee.name.toLowerCase();
            case 'cargo':
                return order.cargoDescription.toLowerCase();
            case 'pickup':
                return order.pickupFrom;
            case 'status':
                return order.status.toLowerCase();
            default:
                return order.orderNumber.toLowerCase();
        }
    }

    // See DriverListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
