import { DatePipe, DecimalPipe } from '@angular/common';
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
import { Observable, filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { Invoice, InvoiceService, invoiceStatusTone } from './invoice.service';

/** The API's maximum page size - same stopgap as the other lists until real paging exists. */
const LIST_PAGE_SIZE = 100;

const BASE_COLUMNS = ['invoiceNumber', 'customer', 'netTotal', 'grossTotal', 'dueDate', 'status'];

@Component({
    selector: 'app-invoice-list',
    standalone: true,
    imports: [
        DatePipe,
        DecimalPipe,
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
                        <h1>{{ i18n.t('nav.invoices') }}</h1>
                        <p class="subtitle" data-testid="invoice-count">
                            {{ i18n.t('invoices.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    @if (session.can('dispatch.write')) {
                        <div class="header-actions">
                            <a mat-flat-button routerLink="/invoices/new" data-testid="invoice-add">
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('invoices.add') }}
                            </a>
                        </div>
                    }
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="invoice-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <section class="filter-bar">
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
                        <mat-label>{{ i18n.t('invoices.search') }}</mat-label>
                        <mat-icon matPrefix>search</mat-icon>
                        <input
                            matInput
                            data-testid="invoice-search"
                            [value]="search()"
                            (input)="search.set($any($event.target).value)"
                            [placeholder]="i18n.t('invoices.searchHint')"
                        />
                    </mat-form-field>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic">
                        <mat-label>{{ i18n.t('common.status') }}</mat-label>
                        <mat-select
                            [value]="statusFilter()"
                            (valueChange)="filterByStatus($event)"
                            data-testid="invoice-status-filter"
                        >
                            <mat-option [value]="''">{{ i18n.t('common.all') }}</mat-option>
                            @for (status of statusOptions; track status) {
                                <mat-option [value]="status">{{ status }}</mat-option>
                            }
                        </mat-select>
                    </mat-form-field>
                </section>

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="invoice-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="invoiceNumber">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('invoices.number') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv" class="strong">
                                        <a
                                            [routerLink]="['/invoices', inv.id]"
                                            data-testid="invoice-number"
                                            >{{ inv.invoiceNumber }}</a
                                        >
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="customer">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('invoices.customer') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv" data-testid="invoice-customer">
                                        {{ inv.customerName }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="netTotal">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('invoices.netTotal') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv" data-testid="invoice-net">
                                        {{ inv.netTotal | number: '1.2-2' }} €
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="grossTotal">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('invoices.grossTotal') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv" data-testid="invoice-gross">
                                        {{ inv.grossTotal | number: '1.2-2' }} €
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="dueDate">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('invoices.dueDate') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv">
                                        {{ inv.dueDate ? (inv.dueDate | date: 'shortDate') : '—' }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let inv">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="statusTone(inv.status)"
                                                data-testid="invoice-status"
                                                >{{ inv.status }}</mat-chip
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
                                    <td mat-cell *matCellDef="let inv" class="actions-cell">
                                        @if (inv.status === 'Draft') {
                                            <button
                                                mat-icon-button
                                                data-testid="invoice-issue"
                                                [matTooltip]="i18n.t('invoices.issue')"
                                                [attr.aria-label]="
                                                    i18n.t('invoices.issue') + ' ' + inv.invoiceNumber
                                                "
                                                (click)="issue(inv)"
                                            >
                                                <mat-icon>send</mat-icon>
                                            </button>
                                        }
                                        @if (inv.status === 'Issued') {
                                            <button
                                                mat-icon-button
                                                data-testid="invoice-pay"
                                                [matTooltip]="i18n.t('invoices.pay')"
                                                [attr.aria-label]="
                                                    i18n.t('invoices.pay') + ' ' + inv.invoiceNumber
                                                "
                                                (click)="markPaid(inv)"
                                            >
                                                <mat-icon>paid</mat-icon>
                                            </button>
                                        }
                                        @if (inv.status === 'Draft' || inv.status === 'Issued') {
                                            <button
                                                mat-icon-button
                                                class="delete"
                                                data-testid="invoice-cancel"
                                                [matTooltip]="i18n.t('invoices.cancel')"
                                                [attr.aria-label]="
                                                    i18n.t('invoices.cancel') + ' ' + inv.invoiceNumber
                                                "
                                                (click)="cancel(inv)"
                                            >
                                                <mat-icon>block</mat-icon>
                                            </button>
                                        }
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns()"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">receipt_long</mat-icon>
                                            <span data-testid="invoice-empty">{{
                                                i18n.t('invoices.empty')
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
            <p>{{ i18n.t('invoices.signIn') }}</p>
        }
    `,
})
export class InvoiceListComponent {
    private readonly service = inject(InvoiceService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    // Issue/pay/cancel need dispatch.write - the same gate as the API's DispatchWrite policy,
    // so a viewer is not offered actions that would only end in a 403.
    protected readonly columns = computed(() =>
        this.session.can('dispatch.write') ? [...BASE_COLUMNS, 'actions'] : BASE_COLUMNS,
    );
    protected readonly statusOptions = ['Draft', 'Issued', 'Paid', 'Cancelled'];
    protected readonly invoices = signal<Invoice[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    protected readonly actionError = signal<string | null>(null);
    protected readonly statusFilter = signal('');
    protected readonly search = signal('');

    private readonly criteria = computed(() => this.search().trim().toLowerCase());
    protected readonly filteredCount = computed(() => {
        const search = this.criteria();
        return this.invoices().filter((invoice) => this.matches(invoice, search)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Invoice>([]);
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (invoice, search) => this.matches(invoice, search);
        this.dataSource.sortingDataAccessor = (invoice, column) => this.sortValue(invoice, column);

        effect(() => {
            this.dataSource.data = this.invoices();
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
        return invoiceStatusTone(status);
    }

    protected filterByStatus(status: string): void {
        this.statusFilter.set(status);
        this.refresh();
    }

    protected issue(invoice: Invoice): void {
        this.transition(this.service.issue(invoice.id), 'invoices.error.issue');
    }

    protected markPaid(invoice: Invoice): void {
        this.transition(this.service.markPaid(invoice.id), 'invoices.error.pay');
    }

    // Cancelling frees the billed orders again, which is worth a second look - hence the
    // confirm dialog while issue/pay act immediately.
    protected cancel(invoice: Invoice): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('invoices.cancelTitle'),
            message: this.i18n.t('invoices.cancelQuestion', { number: invoice.invoiceNumber }),
            confirmLabel: this.i18n.t('invoices.cancelConfirm'),
            cancelLabel: this.i18n.t('invoices.cancelKeep'),
            confirmTestId: 'invoice-cancel-confirm',
            cancelTestId: 'invoice-cancel-abort',
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
                switchMap(() => this.service.cancel(invoice.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(
                        this.describe(error, this.i18n.t('invoices.error.cancel')),
                    ),
            });
    }

    private transition(
        request: Observable<Invoice>,
        fallbackKey: 'invoices.error.issue' | 'invoices.error.pay',
    ): void {
        this.actionError.set(null);
        request.subscribe({
            next: () => this.refresh(),
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, this.i18n.t(fallbackKey))),
        });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list(this.statusFilter(), LIST_PAGE_SIZE).subscribe({
            next: (page) => {
                this.invoices.set(page.items);
                this.errorMessage.set(null);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('invoices.error.list')));
            },
        });
    }

    private matches(invoice: Invoice, search: string): boolean {
        if (!search) {
            return true;
        }
        return [invoice.invoiceNumber, invoice.customerName, invoice.status].some(
            (value) => !!value && value.toLowerCase().includes(search),
        );
    }

    private sortValue(invoice: Invoice, column: string): string | number {
        switch (column) {
            case 'customer':
                return invoice.customerName.toLowerCase();
            case 'netTotal':
                return invoice.netTotal;
            case 'grossTotal':
                return invoice.grossTotal;
            case 'dueDate':
                return invoice.dueDate ?? '';
            case 'status':
                return invoice.status.toLowerCase();
            default:
                return invoice.invoiceNumber.toLowerCase();
        }
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
