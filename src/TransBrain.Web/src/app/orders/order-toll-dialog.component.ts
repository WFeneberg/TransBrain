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
import { wholeNumberInRange } from '../vehicles/vehicle-validators';
import { OrderService, OrderToll } from './order.service';

export interface OrderTollDialogData {
    orderId: string;
    /** null opens the dialog in create mode. */
    toll: OrderToll | null;
    /** All TollCategory catalog entries, including inactive ones. */
    tollCategories: readonly VehicleCatalogEntry[];
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MAX_KILOMETERS = 100_000;
const MAX_FACTOR = 1_000;

/**
 * Saves the toll record itself and closes with the saved record, so a server rejection
 * (a field error such as an over-precise factor) is shown inside the still-open dialog.
 */
@Component({
    selector: 'app-order-toll-dialog',
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
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="toll-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit' : 'add_circle'
                }}</mat-icon>
                <span>{{
                    isEditMode ? i18n.t('orders.tolls.edit') : i18n.t('orders.tolls.new')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="toll-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <mat-form-field>
                    <mat-label>{{ i18n.t('orders.tolls.category') }}</mat-label>
                    <mat-select formControlName="tollCategory" data-testid="toll-category">
                        @for (option of categoryOptions; track option.code) {
                            <mat-option [value]="option.code">{{
                                optionLabel(option)
                            }}</mat-option>
                        }
                    </mat-select>
                    @if (form.controls.tollCategory.errors; as errors) {
                        <mat-error data-testid="toll-category-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <div class="pair">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('orders.tolls.emptyKm') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            min="0"
                            [max]="maxKilometers"
                            step="1"
                            formControlName="emptyKm"
                            data-testid="toll-emptyKm"
                        />
                        @if (form.controls.emptyKm.errors; as errors) {
                            <mat-error data-testid="toll-emptyKm-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('orders.tolls.loadedKm') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            min="0"
                            [max]="maxKilometers"
                            step="1"
                            formControlName="loadedKm"
                            data-testid="toll-loadedKm"
                        />
                        @if (form.controls.loadedKm.errors; as errors) {
                            <mat-error data-testid="toll-loadedKm-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <mat-form-field>
                    <mat-label>{{ i18n.t('orders.tolls.factorPerKm') }}</mat-label>
                    <input
                        matInput
                        type="number"
                        min="0"
                        [max]="maxFactor"
                        step="0.001"
                        formControlName="factorPerKm"
                        data-testid="toll-factorPerKm"
                    />
                    <mat-hint>{{ i18n.t('orders.tolls.factorHint') }}</mat-hint>
                    @if (form.controls.factorPerKm.errors; as errors) {
                        <mat-error data-testid="toll-factorPerKm-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="toll-cancel"
                    [disabled]="busy()"
                    (click)="cancel()"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button mat-flat-button type="submit" data-testid="toll-save" [disabled]="busy()">
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
export class OrderTollDialogComponent {
    private readonly data = inject<OrderTollDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef =
        inject<MatDialogRef<OrderTollDialogComponent, OrderToll>>(MatDialogRef);
    private readonly service = inject(OrderService);
    private readonly session = inject(SessionService);
    private readonly fb = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.toll !== null;
    protected readonly maxKilometers = MAX_KILOMETERS;
    protected readonly maxFactor = MAX_FACTOR;
    protected readonly busy = signal(false);
    protected readonly dialogError = signal<string | null>(null);
    // Active categories only, plus the one an edited record already has if it has been
    // deactivated since - the server accepts keeping it, but rejects any other inactive one.
    protected readonly categoryOptions = catalogOptions(
        this.data.tollCategories,
        this.data.toll?.tollCategory ?? null,
    );

    protected readonly form = this.fb.nonNullable.group({
        tollCategory: [this.initialCategory(), Validators.required],
        emptyKm: [
            this.data.toll?.emptyKm ?? 0,
            [Validators.required, wholeNumberInRange(0, MAX_KILOMETERS)],
        ],
        loadedKm: [
            this.data.toll?.loadedKm ?? 0,
            [Validators.required, wholeNumberInRange(0, MAX_KILOMETERS)],
        ],
        factorPerKm: [
            this.data.toll?.factorPerKm ?? 0,
            [Validators.required, Validators.min(0), Validators.max(MAX_FACTOR)],
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
        if (errors['range']) {
            return this.i18n.t(
                'common.fieldRange',
                errors['range'] as { min: number; max: number },
            );
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
            tollCategory: raw.tollCategory,
            emptyKm: Number(raw.emptyKm),
            loadedKm: Number(raw.loadedKm),
            factorPerKm: Number(raw.factorPerKm),
        };
        const { orderId, toll } = this.data;
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                toll
                    ? this.service.updateToll(orderId, toll.id, request)
                    : this.service.createToll(orderId, request),
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
    private initialCategory(): string {
        const code = this.data.toll?.tollCategory;
        if (!code) {
            return '';
        }
        return (
            this.categoryOptions.find((option) => option.code.toLowerCase() === code.toLowerCase())
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
            problem?.detail ?? problem?.title ?? this.i18n.t('orders.tolls.error.save');
        this.dialogError.set(`${message} (HTTP ${error.status})`);
    }
}
