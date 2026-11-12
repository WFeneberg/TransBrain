import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { forkJoin, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { Customer, CustomerService } from '../customers/customer.service';
import { I18nService } from '../i18n/i18n.service';
import { Order } from '../orders/order.service';
import { InvoiceLinePayload, InvoiceService } from './invoice.service';

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

/** One billable order as shown in the picker table, with its editable line fields. */
interface LineDraft {
    order: Order;
    selected: boolean;
    netAmount: string;
    description: string;
}

const LINE_COLUMNS = ['select', 'orderNumber', 'route', 'cargo', 'netAmount', 'description'];

@Component({
    selector: 'app-invoice-form',
    standalone: true,
    imports: [
        DecimalPipe,
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatSelectModule,
        MatTableModule,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ i18n.t('invoices.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="invoice-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">business</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoices.section.customer')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('invoices.customer') }}</mat-label>
                                <mat-select
                                    formControlName="customerId"
                                    data-testid="invoice-customerId"
                                >
                                    @for (customer of customers(); track customer.id) {
                                        <mat-option [value]="customer.id">{{
                                            customer.name
                                        }}</mat-option>
                                    }
                                </mat-select>
                                @if (form.controls.customerId.errors) {
                                    <mat-error data-testid="invoice-customerId-error">{{
                                        i18n.t('common.fieldRequired')
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('invoices.vatRate') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    step="0.1"
                                    min="0"
                                    max="100"
                                    formControlName="vatRatePercent"
                                    data-testid="invoice-vatRate"
                                />
                                @if (form.controls.vatRatePercent.errors) {
                                    <mat-error data-testid="invoice-vatRate-error">{{
                                        i18n.t('common.fieldInvalid')
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">receipt_long</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoices.section.positions')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (lines().length === 0) {
                                <p data-testid="invoice-no-billable">
                                    {{ i18n.t('invoices.noBillable') }}
                                </p>
                            } @else {
                                <div class="table-scroll">
                                    <table mat-table [dataSource]="lines()">
                                        <ng-container matColumnDef="select">
                                            <th mat-header-cell *matHeaderCellDef></th>
                                            <td mat-cell *matCellDef="let line">
                                                <mat-checkbox
                                                    [checked]="line.selected"
                                                    (change)="
                                                        toggle(line.order.id, $event.checked)
                                                    "
                                                    data-testid="invoice-line-select"
                                                />
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="orderNumber">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('orders.number') }}
                                            </th>
                                            <td mat-cell *matCellDef="let line" class="strong">
                                                {{ line.order.orderNumber }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="route">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('invoices.route') }}
                                            </th>
                                            <td mat-cell *matCellDef="let line">
                                                {{ line.order.consignor.city }} →
                                                {{ line.order.consignee.city }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="cargo">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('orders.cargo') }}
                                            </th>
                                            <td mat-cell *matCellDef="let line">
                                                {{ line.order.cargoDescription }}
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="netAmount">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('invoices.netAmount') }}
                                            </th>
                                            <td mat-cell *matCellDef="let line">
                                                <mat-form-field
                                                    appearance="outline"
                                                    subscriptSizing="dynamic"
                                                    class="dense"
                                                >
                                                    <input
                                                        matInput
                                                        type="number"
                                                        step="0.01"
                                                        min="0.01"
                                                        [value]="line.netAmount"
                                                        [disabled]="!line.selected"
                                                        (input)="
                                                            setAmount(
                                                                line.order.id,
                                                                $any($event.target).value
                                                            )
                                                        "
                                                        data-testid="invoice-line-amount"
                                                    />
                                                </mat-form-field>
                                            </td>
                                        </ng-container>
                                        <ng-container matColumnDef="description">
                                            <th mat-header-cell *matHeaderCellDef>
                                                {{ i18n.t('invoices.descriptionOptional') }}
                                            </th>
                                            <td mat-cell *matCellDef="let line">
                                                <mat-form-field
                                                    appearance="outline"
                                                    subscriptSizing="dynamic"
                                                    class="dense"
                                                >
                                                    <input
                                                        matInput
                                                        [value]="line.description"
                                                        [disabled]="!line.selected"
                                                        (input)="
                                                            setDescription(
                                                                line.order.id,
                                                                $any($event.target).value
                                                            )
                                                        "
                                                        data-testid="invoice-line-description"
                                                    />
                                                </mat-form-field>
                                            </td>
                                        </ng-container>
                                        <tr mat-header-row *matHeaderRowDef="lineColumns"></tr>
                                        <tr
                                            mat-row
                                            *matRowDef="let row; columns: lineColumns"
                                        ></tr>
                                    </table>
                                </div>
                                <p class="subtitle" data-testid="invoice-net-preview">
                                    {{ i18n.t('invoices.netTotal') }}:
                                    {{ selectedNetTotal() | number: '1.2-2' }} €
                                </p>
                            }
                        </mat-card-content>
                    </mat-card>
                </div>

                <div class="action-bar">
                    <button
                        mat-button
                        type="button"
                        data-testid="invoice-form-cancel"
                        (click)="cancel()"
                    >
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="invoice-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
})
export class InvoiceFormComponent {
    private readonly service = inject(InvoiceService);
    private readonly customerService = inject(CustomerService);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    // See TourFormComponent: session.ready guarantees the rehydrated session before the first
    // request on direct navigation (bookmark, reload, browser back).
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly lineColumns = LINE_COLUMNS;
    protected readonly customers = signal<Customer[]>([]);
    protected readonly lines = signal<LineDraft[]>([]);
    protected readonly formError = signal<string | null>(null);

    protected readonly selectedNetTotal = computed(() =>
        this.lines()
            .filter((line) => line.selected)
            .reduce((sum, line) => sum + (Number(line.netAmount) || 0), 0),
    );

    protected readonly form = this.fb.nonNullable.group({
        customerId: ['', Validators.required],
        vatRatePercent: [19, [Validators.required, Validators.min(0), Validators.max(100)]],
    });

    constructor() {
        this.session.ready
            .pipe(
                switchMap(() =>
                    forkJoin({
                        customers: this.customerService.list(1, PICKER_PAGE_SIZE),
                        billable: this.service.billableOrders(),
                    }),
                ),
            )
            .subscribe({
                next: ({ customers, billable }) => {
                    this.customers.set(customers.items);
                    this.lines.set(
                        billable.map((order) => ({
                            order,
                            selected: false,
                            netAmount: '',
                            description: '',
                        })),
                    );
                },
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        this.describeFailure(error, this.i18n.t('invoices.error.loadBillable')),
                    ),
            });
    }

    protected toggle(orderId: string, selected: boolean): void {
        this.patchLine(orderId, { selected });
    }

    protected setAmount(orderId: string, netAmount: string): void {
        this.patchLine(orderId, { netAmount });
    }

    protected setDescription(orderId: string, description: string): void {
        this.patchLine(orderId, { description });
    }

    protected save(): void {
        this.formError.set(null);
        this.form.markAllAsTouched();
        if (this.form.invalid) {
            return;
        }

        const selected = this.lines().filter((line) => line.selected);
        if (selected.length === 0) {
            this.formError.set(this.i18n.t('invoices.selectAtLeastOne'));
            return;
        }
        const invalidAmount = selected.find((line) => !(Number(line.netAmount) > 0));
        if (invalidAmount) {
            this.formError.set(
                this.i18n.t('invoices.amountMissing', {
                    number: invalidAmount.order.orderNumber,
                }),
            );
            return;
        }

        const value = this.form.getRawValue();
        const lines: InvoiceLinePayload[] = selected.map((line) => ({
            orderId: line.order.id,
            netAmount: Number(line.netAmount),
            description: line.description.trim() === '' ? null : line.description.trim(),
        }));

        this.service
            .create({
                customerId: value.customerId,
                vatRatePercent: Number(value.vatRatePercent),
                lines,
            })
            .subscribe({
                next: (invoice) => void this.router.navigate(['/invoices', invoice.id]),
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        this.describeFailure(error, this.i18n.t('invoices.error.save')),
                    ),
            });
    }

    protected cancel(): void {
        void this.router.navigate(['/invoices']);
    }

    private patchLine(orderId: string, patch: Partial<LineDraft>): void {
        this.lines.update((lines) =>
            lines.map((line) => (line.order.id === orderId ? { ...line, ...patch } : line)),
        );
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const validationMessage = problem?.errors
            ? Object.values(problem.errors).flat()[0]
            : undefined;
        const sentence = validationMessage ?? problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
