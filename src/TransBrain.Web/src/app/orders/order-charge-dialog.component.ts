import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import {
    VehicleCatalogEntry,
    VehicleCatalogOption,
    catalogOptions,
} from '../vehicles/vehicle-catalog.service';
import { OrderCharge, OrderService } from './order.service';

export interface OrderChargeDialogData {
    orderId: string;
    /** null opens the dialog in create mode. */
    charge: OrderCharge | null;
    /** All Unit catalog entries, including inactive ones. */
    units: readonly VehicleCatalogEntry[];
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const DESCRIPTION_MAX_LENGTH = 200;
const MAX_QUANTITY = 1_000_000;
const MAX_UNIT_PRICE = 1_000_000;

/**
 * Saves the charge itself and closes with the saved charge, so a server rejection (a field
 * error such as an over-precise quantity) is shown inside the still-open dialog.
 */
@Component({
    selector: 'app-order-charge-dialog',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatDialogModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatSelectModule,
    ],
    template: `
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="charge-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit' : 'add_circle'
                }}</mat-icon>
                <span>{{
                    isEditMode ? i18n.t('orders.charges.edit') : i18n.t('orders.charges.new')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="charge-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <mat-form-field>
                    <mat-label>{{ i18n.t('orders.charges.description') }}</mat-label>
                    <input
                        matInput
                        formControlName="description"
                        [maxlength]="descriptionMaxLength"
                        data-testid="charge-description"
                    />
                    @if (form.controls.description.errors; as errors) {
                        <mat-error data-testid="charge-description-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <div class="pair">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('orders.charges.quantity') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            min="0"
                            [max]="maxQuantity"
                            step="0.01"
                            formControlName="quantity"
                            data-testid="charge-quantity"
                        />
                        @if (form.controls.quantity.errors; as errors) {
                            <mat-error data-testid="charge-quantity-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('orders.charges.unit') }}</mat-label>
                        <mat-select formControlName="unit" data-testid="charge-unit">
                            <mat-option value="">{{ i18n.t('orders.charges.noUnit') }}</mat-option>
                            @for (option of unitOptions; track option.code) {
                                <mat-option [value]="option.code">{{
                                    optionLabel(option)
                                }}</mat-option>
                            }
                        </mat-select>
                        @if (form.controls.unit.errors; as errors) {
                            <mat-error data-testid="charge-unit-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <mat-form-field>
                    <mat-label>{{ i18n.t('orders.charges.unitPrice') }}</mat-label>
                    <input
                        matInput
                        type="number"
                        min="0"
                        [max]="maxUnitPrice"
                        step="0.01"
                        formControlName="unitPrice"
                        data-testid="charge-unitPrice"
                    />
                    <span matTextSuffix>€</span>
                    @if (form.controls.unitPrice.errors; as errors) {
                        <mat-error data-testid="charge-unitPrice-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="charge-cancel"
                    [disabled]="busy()"
                    (click)="cancel()"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button mat-flat-button type="submit" data-testid="charge-save" [disabled]="busy()">
                    <mat-icon>save</mat-icon>
                    {{ i18n.t('common.save') }}
                </button>
            </mat-dialog-actions>
        </form>
    `,
    styles: [
        `
            .title {
                display: flex;
                align-items: center;
                gap: 12px;
            }

            .title-icon {
                color: var(--mat-sys-primary);
            }

            .content {
                display: flex;
                flex-direction: column;
                gap: 8px;
            }

            .pair {
                display: grid;
                grid-template-columns: repeat(2, minmax(0, 1fr));
                column-gap: 16px;
            }

            mat-form-field {
                width: 100%;
            }

            .error-banner {
                display: flex;
                align-items: center;
                gap: 8px;
                margin: 0 0 8px;
                padding: 12px 16px;
                border-radius: var(--mat-sys-corner-medium);
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }

            @media (max-width: 599px) {
                .pair {
                    grid-template-columns: minmax(0, 1fr);
                }
            }
        `,
    ],
})
export class OrderChargeDialogComponent {
    private readonly data = inject<OrderChargeDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef =
        inject<MatDialogRef<OrderChargeDialogComponent, OrderCharge>>(MatDialogRef);
    private readonly service = inject(OrderService);
    private readonly session = inject(SessionService);
    private readonly fb = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.charge !== null;
    protected readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;
    protected readonly maxQuantity = MAX_QUANTITY;
    protected readonly maxUnitPrice = MAX_UNIT_PRICE;
    protected readonly busy = signal(false);
    protected readonly dialogError = signal<string | null>(null);
    // Active units only, plus the one an edited charge already has if it has been deactivated
    // since - the server accepts keeping it, but rejects choosing any other inactive one.
    protected readonly unitOptions = catalogOptions(
        this.data.units,
        this.data.charge?.unit ?? null,
    );

    protected readonly form = this.fb.nonNullable.group({
        description: [
            this.data.charge?.description ?? '',
            [Validators.required, Validators.maxLength(DESCRIPTION_MAX_LENGTH)],
        ],
        quantity: [
            this.data.charge?.quantity ?? 1,
            [Validators.required, Validators.min(0.01), Validators.max(MAX_QUANTITY)],
        ],
        unit: [this.initialUnit()],
        unitPrice: [
            this.data.charge?.unitPrice ?? 0,
            [Validators.required, Validators.min(0), Validators.max(MAX_UNIT_PRICE)],
        ],
    });

    protected optionLabel(option: VehicleCatalogOption): string {
        return option.isActive
            ? option.name
            : this.i18n.t('vehicles.catalogInactive', { name: option.name });
    }

    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['min']) {
            return this.i18n.t('common.fieldPositive');
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected cancel(): void {
        this.dialogRef.close();
    }

    protected save(): void {
        this.dialogError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const raw = this.form.getRawValue();
        const request = {
            description: raw.description.trim(),
            quantity: Number(raw.quantity),
            unit: raw.unit || null,
            unitPrice: Number(raw.unitPrice),
        };
        const { orderId, charge } = this.data;
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                charge
                    ? this.service.updateCharge(orderId, charge.id, request)
                    : this.service.createCharge(orderId, request),
            ),
        );

        this.busy.set(true);
        this.dialogRef.disableClose = true;
        save$.subscribe({
            next: (saved) => this.dialogRef.close(saved),
            error: (error: HttpErrorResponse) => {
                this.busy.set(false);
                this.dialogRef.disableClose = false;
                this.applyServerErrors(error);
            },
        });
    }

    // The option list matches codes exactly, while the stored code may differ in case from the
    // catalog's canonical one - select the option that carries the same code case-insensitively.
    private initialUnit(): string {
        const code = this.data.charge?.unit;
        if (!code) {
            return '';
        }
        return (
            this.unitOptions.find((option) => option.code.toLowerCase() === code.toLowerCase())
                ?.code ?? code
        );
    }

    // ValidationProblem keys are the .NET property names (PascalCase), the controls camelCase.
    // Anything without a matching control goes to the dialog-level banner.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const control = this.form.get(field.charAt(0).toLowerCase() + field.slice(1));
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                    control.markAsTouched();
                } else {
                    this.dialogError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }

        const message =
            problem?.detail ?? problem?.title ?? this.i18n.t('orders.charges.error.save');
        this.dialogError.set(`${message} (HTTP ${error.status})`);
    }
}
