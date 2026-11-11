import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { Invoice, InvoiceService, invoiceStatusTone, saveBlob } from './invoice.service';

const LINE_COLUMNS = ['position', 'orderNumber', 'description', 'netAmount'];

@Component({
    selector: 'app-invoice-detail',
    standalone: true,
    imports: [
        DatePipe,
        DecimalPipe,
        MatButtonModule,
        MatCardModule,
        MatChipsModule,
        MatIconModule,
        MatTableModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            @if (errorMessage(); as message) {
                <p class="error-banner" data-testid="invoice-detail-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            @if (invoice(); as inv) {
                <header class="page-header">
                    <div>
                        <div class="title-row">
                            <h1 data-testid="invoice-detail-number">
                                {{ i18n.t('invoices.detailTitle', { number: inv.invoiceNumber }) }}
                            </h1>
                            <mat-chip-set>
                                <mat-chip
                                    class="chip"
                                    [attr.data-tone]="statusTone(inv.status)"
                                    data-testid="invoice-detail-status"
                                    >{{ inv.status }}</mat-chip
                                >
                            </mat-chip-set>
                        </div>
                        <p class="subtitle" data-testid="invoice-detail-customer">{{ inv.customerName }}</p>
                    </div>
                    <div class="header-actions">
                        @if (inv.status === 'Issued' || inv.status === 'Paid') {
                            <button
                                mat-stroked-button
                                data-testid="invoice-detail-pdf"
                                (click)="downloadPdf(inv)"
                            >
                                <mat-icon>print</mat-icon>
                                {{ i18n.t('invoices.downloadPdf') }}
                            </button>
                            <button
                                mat-stroked-button
                                data-testid="invoice-detail-xrechnung"
                                (click)="downloadXRechnung(inv)"
                            >
                                <mat-icon>receipt_long</mat-icon>
                                {{ i18n.t('invoices.downloadXRechnung') }}
                            </button>
                        }
                        @if (session.can('dispatch.write')) {
                            @if (inv.status === 'Draft') {
                                <button
                                    mat-flat-button
                                    data-testid="invoice-detail-issue"
                                    (click)="issue(inv)"
                                >
                                    <mat-icon>send</mat-icon>
                                    {{ i18n.t('invoices.issue') }}
                                </button>
                            }
                            @if (inv.status === 'Issued') {
                                <button
                                    mat-flat-button
                                    data-testid="invoice-detail-pay"
                                    (click)="markPaid(inv)"
                                >
                                    <mat-icon>paid</mat-icon>
                                    {{ i18n.t('invoices.pay') }}
                                </button>
                            }
                            @if (inv.status === 'Draft' || inv.status === 'Issued') {
                                <button
                                    mat-stroked-button
                                    class="delete"
                                    data-testid="invoice-detail-cancel"
                                    (click)="cancel(inv)"
                                >
                                    <mat-icon>block</mat-icon>
                                    {{ i18n.t('invoices.cancel') }}
                                </button>
                            }
                        }
                    </div>
                </header>

                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">business</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoices.section.customer')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <p class="strong">{{ inv.customerName }}</p>
                            <p>
                                {{ inv.billingAddress.street }}<br />
                                {{ inv.billingAddress.postalCode }} {{ inv.billingAddress.city
                                }}<br />
                                {{ inv.billingAddress.country }}
                            </p>
                            @if (inv.customerVatId) {
                                <p>{{ i18n.t('invoices.vatId') }}: {{ inv.customerVatId }}</p>
                            }
                            <p>
                                {{ i18n.t('invoices.paymentTerm') }}:
                                {{ i18n.t('invoices.paymentTermDays', { days: inv.paymentTermDays }) }}
                            </p>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">event</mat-icon>
                            <mat-card-title>{{ i18n.t('invoices.dates') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <p>
                                {{ i18n.t('invoices.createdAt') }}:
                                {{ inv.createdAt | date: 'short' }}
                            </p>
                            <p data-testid="invoice-detail-issuedAt">
                                {{ i18n.t('invoices.issuedAt') }}:
                                {{ inv.issuedAt ? (inv.issuedAt | date: 'short') : '—' }}
                            </p>
                            <p data-testid="invoice-detail-dueDate">
                                {{ i18n.t('invoices.dueDate') }}:
                                {{ inv.dueDate ? (inv.dueDate | date: 'shortDate') : '—' }}
                            </p>
                            <p data-testid="invoice-detail-paidAt">
                                {{ i18n.t('invoices.paidAt') }}:
                                {{ inv.paidAt ? (inv.paidAt | date: 'short') : '—' }}
                            </p>
                        </mat-card-content>
                    </mat-card>
                </div>

                <section class="table-card">
                    <div class="table-scroll">
                        <table mat-table [dataSource]="inv.lines">
                            <ng-container matColumnDef="position">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('invoices.position') }}
                                </th>
                                <td mat-cell *matCellDef="let line">{{ line.position }}</td>
                            </ng-container>
                            <ng-container matColumnDef="orderNumber">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('orders.number') }}
                                </th>
                                <td mat-cell *matCellDef="let line" class="strong">
                                    {{ line.orderNumber }}
                                </td>
                            </ng-container>
                            <ng-container matColumnDef="description">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('invoices.description') }}
                                </th>
                                <td mat-cell *matCellDef="let line">{{ line.description }}</td>
                            </ng-container>
                            <ng-container matColumnDef="netAmount">
                                <th mat-header-cell *matHeaderCellDef>
                                    {{ i18n.t('invoices.netAmount') }}
                                </th>
                                <td mat-cell *matCellDef="let line" data-testid="invoice-line-net">
                                    {{ line.netAmount | number: '1.2-2' }} €
                                </td>
                            </ng-container>
                            <tr mat-header-row *matHeaderRowDef="lineColumns"></tr>
                            <tr mat-row *matRowDef="let row; columns: lineColumns"></tr>
                        </table>
                    </div>
                    <div class="totals" data-testid="invoice-detail-totals">
                        <p>
                            {{ i18n.t('invoices.netTotal') }}:
                            <span class="strong">{{ inv.netTotal | number: '1.2-2' }} €</span>
                        </p>
                        <p>
                            {{ i18n.t('invoices.vatAmount') }} ({{ inv.vatRatePercent }} %):
                            <span class="strong">{{ inv.vatAmount | number: '1.2-2' }} €</span>
                        </p>
                        <p>
                            {{ i18n.t('invoices.grossTotal') }}:
                            <span class="strong">{{ inv.grossTotal | number: '1.2-2' }} €</span>
                        </p>
                    </div>
                </section>

                <div class="action-bar">
                    <a mat-button routerLink="/invoices" data-testid="invoice-detail-back">
                        <mat-icon>arrow_back</mat-icon>
                        {{ i18n.t('invoices.backToList') }}
                    </a>
                </div>
            }
        </div>
    `,
    styles: `
        .totals {
            display: flex;
            gap: 2rem;
            justify-content: flex-end;
            padding: 1rem 1.5rem;
        }
    `,
})
export class InvoiceDetailComponent {
    private readonly service = inject(InvoiceService);
    private readonly route = inject(ActivatedRoute);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly lineColumns = LINE_COLUMNS;
    protected readonly invoice = signal<Invoice | null>(null);
    protected readonly errorMessage = signal<string | null>(null);

    constructor() {
        // See TourFormComponent: session.ready guarantees the rehydrated session before the
        // first request on direct navigation (bookmark, reload, browser back).
        this.session.ready
            .pipe(
                switchMap(() => this.service.getById(this.route.snapshot.paramMap.get('id') ?? '')),
            )
            .subscribe({
                next: (invoice) => this.invoice.set(invoice),
                error: (error: HttpErrorResponse) =>
                    this.errorMessage.set(
                        this.describe(error, this.i18n.t('invoices.error.load')),
                    ),
            });
    }

    protected statusTone(status: string): string {
        return invoiceStatusTone(status);
    }

    protected issue(invoice: Invoice): void {
        this.apply(this.service.issue(invoice.id), 'invoices.error.issue');
    }

    protected markPaid(invoice: Invoice): void {
        this.apply(this.service.markPaid(invoice.id), 'invoices.error.pay');
    }

    protected downloadPdf(invoice: Invoice): void {
        this.download(this.service.downloadPdf(invoice.id), `${invoice.invoiceNumber}.pdf`);
    }

    protected downloadXRechnung(invoice: Invoice): void {
        this.download(
            this.service.downloadXRechnung(invoice.id),
            `${invoice.invoiceNumber}-xrechnung.xml`,
        );
    }

    private download(request: Observable<Blob>, fileName: string): void {
        this.errorMessage.set(null);
        request.subscribe({
            next: (blob) => saveBlob(blob, fileName),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(
                    this.describe(error, this.i18n.t('invoices.error.download')),
                ),
        });
    }

    protected cancel(invoice: Invoice): void {
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
                next: (updated) => this.invoice.set(updated),
                error: (error: HttpErrorResponse) =>
                    this.errorMessage.set(
                        this.describe(error, this.i18n.t('invoices.error.cancel')),
                    ),
            });
    }

    private apply(
        request: Observable<Invoice>,
        fallbackKey: 'invoices.error.issue' | 'invoices.error.pay',
    ): void {
        this.errorMessage.set(null);
        request.subscribe({
            next: (updated) => this.invoice.set(updated),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(this.describe(error, this.i18n.t(fallbackKey))),
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
