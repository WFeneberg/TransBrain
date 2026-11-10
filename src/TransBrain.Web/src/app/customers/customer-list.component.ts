import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { Customer, CustomerService } from './customer.service';

/**
 * The API's maximum page size. There are no pagination controls yet, so one request is all a
 * user sees. See DriverListComponent for the same stopgap.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-customer-list',
    standalone: true,
    imports: [
        MatButtonModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
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
                        <h1>{{ i18n.t('nav.customers') }}</h1>
                        <p class="subtitle" data-testid="customer-count">
                            {{ i18n.t('customers.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    <div class="header-actions">
                        <a
                            mat-stroked-button
                            routerLink="/customer-catalog"
                            data-testid="customer-catalog-link"
                        >
                            <mat-icon>tune</mat-icon>
                            {{ i18n.t('customers.catalogLink') }}
                        </a>
                        @if (session.can('masterData.write')) {
                            <a
                                mat-flat-button
                                routerLink="/customers/new"
                                data-testid="customer-add"
                            >
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('customers.add') }}
                            </a>
                        }
                    </div>
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="customer-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="customer-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="filter-bar">
                        <mat-form-field
                            appearance="outline"
                            subscriptSizing="dynamic"
                            class="search"
                        >
                            <mat-label>{{ i18n.t('customers.search') }}</mat-label>
                            <mat-icon matPrefix>search</mat-icon>
                            <input
                                matInput
                                data-testid="customer-search"
                                [value]="search()"
                                (input)="search.set($any($event.target).value)"
                                [placeholder]="i18n.t('customers.searchHint')"
                            />
                        </mat-form-field>
                    </section>

                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="name">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.name') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let c"
                                        class="strong"
                                        data-testid="customer-name"
                                    >
                                        {{ c.name }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="city">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.city') }}
                                    </th>
                                    <td mat-cell *matCellDef="let c">
                                        {{ c.postalCode }} {{ c.city }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="contactPerson">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('customers.contactPerson') }}
                                    </th>
                                    <td mat-cell *matCellDef="let c">
                                        {{ c.contactPerson || '–' }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="email">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('customers.email') }}
                                    </th>
                                    <td mat-cell *matCellDef="let c">{{ c.email || '–' }}</td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let c" class="actions-cell">
                                        <a
                                            mat-icon-button
                                            [routerLink]="['/customers', c.id]"
                                            data-testid="customer-edit"
                                            [matTooltip]="i18n.t('common.edit')"
                                            [attr.aria-label]="i18n.t('common.edit') + ' ' + c.name"
                                        >
                                            <mat-icon>edit</mat-icon>
                                        </a>
                                        <button
                                            mat-icon-button
                                            class="delete"
                                            data-testid="customer-delete"
                                            [matTooltip]="i18n.t('common.delete')"
                                            [attr.aria-label]="
                                                i18n.t('common.delete') + ' ' + c.name
                                            "
                                            (click)="delete(c)"
                                        >
                                            <mat-icon>delete</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns()"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">business</mat-icon>
                                            <span data-testid="customer-empty">{{
                                                i18n.t('customers.empty')
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
            <p>{{ i18n.t('customers.signIn') }}</p>
        }
    `,
})
export class CustomerListComponent {
    private readonly service = inject(CustomerService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly columns = computed(() => {
        const base = ['name', 'city', 'contactPerson', 'email'];
        return this.session.can('masterData.write') ? [...base, 'actions'] : base;
    });
    protected readonly customers = signal<Customer[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage so a failed delete does not hide the table (see DriverListComponent).
    protected readonly actionError = signal<string | null>(null);

    protected readonly search = signal('');
    private readonly criteria = computed(() => this.search().trim().toLowerCase());
    protected readonly filteredCount = computed(() => {
        const search = this.criteria();
        return this.customers().filter((customer) => this.matches(customer, search)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Customer>([]);
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (customer, search) => this.matches(customer, search);
        this.dataSource.sortingDataAccessor = (customer, column) =>
            this.sortValue(customer, column);

        effect(() => {
            this.dataSource.data = this.customers();
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

    protected delete(customer: Customer): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('customers.deleteTitle'),
            message: this.i18n.t('customers.deleteConfirm', { name: customer.name }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'customer-delete-confirm',
            cancelTestId: 'customer-delete-cancel',
            icon: 'delete',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.service.remove(customer.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(
                        this.describe(error, this.i18n.t('customers.error.delete')),
                    ),
            });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list(1, LIST_PAGE_SIZE).subscribe({
            next: (page) => {
                this.customers.set(page.items);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('customers.error.list')));
            },
        });
    }

    private matches(customer: Customer, search: string): boolean {
        if (!search) {
            return true;
        }
        return [
            customer.name,
            customer.postalCode,
            customer.city,
            customer.contactPerson,
            customer.email,
            customer.vatId,
        ].some((value) => !!value && value.toLowerCase().includes(search));
    }

    private sortValue(customer: Customer, column: string): string {
        switch (column) {
            case 'city':
                return `${customer.city} ${customer.postalCode}`.toLowerCase();
            case 'contactPerson':
                return (customer.contactPerson ?? '').toLowerCase();
            case 'email':
                return (customer.email ?? '').toLowerCase();
            default:
                return customer.name.toLowerCase();
        }
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
