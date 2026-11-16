import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
    AbstractControl,
    FormBuilder,
    ReactiveFormsModule,
    ValidationErrors,
    Validators,
} from '@angular/forms';
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
    MAINTENANCE_KINDS,
    VehicleMaintenance,
    VehicleMaintenanceKind,
    VehicleMaintenanceService,
} from './vehicle-maintenance.service';
import { amountInRange, dateFromYear, wholeNumberInRange } from './vehicle-validators';

export interface VehicleMaintenanceDialogData {
    vehicleId: string;
    /** null opens the dialog in create mode. */
    maintenance: VehicleMaintenance | null;
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MIN_YEAR = 1900;
const DESCRIPTION_MAX_LENGTH = 1000;
const WORKSHOP_MAX_LENGTH = 200;
const MAX_ODOMETER_KM = 9999999;
const MAX_COST = 10000000;

/** "YYYY-MM-DD" strings compare chronologically as plain strings. */
function notBeforePerformedOn(control: AbstractControl<string>): ValidationErrors | null {
    const performedOn = control.parent?.get('performedOn')?.value as string | undefined;
    return control.value && performedOn && control.value < performedOn
        ? { beforePerformedOn: true }
        : null;
}

/**
 * Saves the maintenance entry itself and closes with the saved entry, so a server rejection
 * (a field error) is shown inside the still-open dialog.
 */
@Component({
    selector: 'app-vehicle-maintenance-dialog',
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
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="maintenance-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit' : 'add_circle'
                }}</mat-icon>
                <span>{{
                    isEditMode
                        ? i18n.t('vehicles.maintenance.edit')
                        : i18n.t('vehicles.maintenance.new')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="maintenance-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <div class="pair">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.maintenance.kind') }}</mat-label>
                        <mat-select formControlName="kind" data-testid="maintenance-kind">
                            @for (kind of kinds; track kind) {
                                <mat-option [value]="kind">{{
                                    i18n.t(kindKey(kind))
                                }}</mat-option>
                            }
                        </mat-select>
                        @if (form.controls.kind.errors; as errors) {
                            <mat-error data-testid="maintenance-kind-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.maintenance.performedOn') }}</mat-label>
                        <input
                            matInput
                            type="date"
                            formControlName="performedOn"
                            data-testid="maintenance-performedOn"
                        />
                        @if (form.controls.performedOn.errors; as errors) {
                            <mat-error data-testid="maintenance-performedOn-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicles.maintenance.description') }}</mat-label>
                    <textarea
                        matInput
                        rows="3"
                        formControlName="description"
                        [maxlength]="descriptionMaxLength"
                        data-testid="maintenance-description"
                    ></textarea>
                    <mat-hint align="end"
                        >{{ form.controls.description.value.length }} /
                        {{ descriptionMaxLength }}</mat-hint
                    >
                    @if (form.controls.description.errors; as errors) {
                        <mat-error data-testid="maintenance-description-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicles.maintenance.workshop') }}</mat-label>
                    <input
                        matInput
                        formControlName="workshop"
                        [maxlength]="workshopMaxLength"
                        data-testid="maintenance-workshop"
                    />
                </mat-form-field>

                <div class="pair">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.maintenance.odometerKm') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            min="0"
                            formControlName="odometerKm"
                            data-testid="maintenance-odometerKm"
                        />
                        <span matTextSuffix>km</span>
                        @if (form.controls.odometerKm.errors; as errors) {
                            <mat-error data-testid="maintenance-odometerKm-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.maintenance.cost') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            min="0"
                            step="0.01"
                            formControlName="cost"
                            data-testid="maintenance-cost"
                        />
                        <span matTextSuffix>€</span>
                        @if (form.controls.cost.errors; as errors) {
                            <mat-error data-testid="maintenance-cost-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicles.maintenance.nextDueOn') }}</mat-label>
                    <input
                        matInput
                        type="date"
                        formControlName="nextDueOn"
                        data-testid="maintenance-nextDueOn"
                    />
                    @if (form.controls.nextDueOn.errors; as errors) {
                        <mat-error data-testid="maintenance-nextDueOn-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="maintenance-cancel"
                    [disabled]="busy()"
                    (click)="cancel()"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button
                    mat-flat-button
                    type="submit"
                    data-testid="maintenance-save"
                    [disabled]="busy()"
                >
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
export class VehicleMaintenanceDialogComponent {
    private readonly data = inject<VehicleMaintenanceDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef =
        inject<MatDialogRef<VehicleMaintenanceDialogComponent, VehicleMaintenance>>(MatDialogRef);
    private readonly service = inject(VehicleMaintenanceService);
    private readonly session = inject(SessionService);
    private readonly fb = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.maintenance !== null;
    protected readonly kinds = MAINTENANCE_KINDS;
    protected readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;
    protected readonly workshopMaxLength = WORKSHOP_MAX_LENGTH;
    protected readonly busy = signal(false);
    protected readonly dialogError = signal<string | null>(null);

    protected readonly form = this.fb.nonNullable.group({
        kind: [this.data.maintenance?.kind ?? ('Service' as VehicleMaintenanceKind), Validators.required],
        performedOn: [
            this.data.maintenance?.performedOn ?? '',
            [Validators.required, dateFromYear(MIN_YEAR)],
        ],
        description: [
            this.data.maintenance?.description ?? '',
            [Validators.required, Validators.maxLength(DESCRIPTION_MAX_LENGTH)],
        ],
        workshop: [
            this.data.maintenance?.workshop ?? '',
            Validators.maxLength(WORKSHOP_MAX_LENGTH),
        ],
        odometerKm: this.fb.control<number | null>(
            this.data.maintenance?.odometerKm ?? null,
            wholeNumberInRange(0, MAX_ODOMETER_KM),
        ),
        cost: this.fb.control<number | null>(
            this.data.maintenance?.cost ?? null,
            amountInRange(0, MAX_COST),
        ),
        nextDueOn: [
            this.data.maintenance?.nextDueOn ?? '',
            [dateFromYear(MIN_YEAR), notBeforePerformedOn],
        ],
    });

    constructor() {
        this.form.controls.performedOn.valueChanges
            .pipe(takeUntilDestroyed())
            .subscribe(() => this.form.controls.nextDueOn.updateValueAndValidity());
    }

    protected kindKey(kind: VehicleMaintenanceKind) {
        return `vehicles.maintenance.kindOption.${kind}` as const;
    }

    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['minYear']) {
            return this.i18n.t('common.fieldMinYear', errors['minYear'] as { year: number });
        }
        if (errors['range']) {
            return this.i18n.t(
                'common.fieldRange',
                errors['range'] as { min: number; max: number },
            );
        }
        if (errors['amountRange']) {
            return this.i18n.t(
                'common.fieldAmountRange',
                errors['amountRange'] as { min: number; max: number },
            );
        }
        if (errors['beforePerformedOn']) {
            return this.i18n.t('vehicles.maintenance.nextDueBeforePerformed');
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
            kind: raw.kind,
            performedOn: raw.performedOn,
            description: raw.description.trim(),
            workshop: raw.workshop.trim() === '' ? null : raw.workshop.trim(),
            odometerKm: raw.odometerKm === null ? null : Number(raw.odometerKm),
            cost: raw.cost === null ? null : Number(raw.cost),
            nextDueOn: raw.nextDueOn || null,
        };
        const { vehicleId, maintenance } = this.data;
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                maintenance
                    ? this.service.update(vehicleId, maintenance.id, request)
                    : this.service.create(vehicleId, request),
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
            problem?.detail ?? problem?.title ?? this.i18n.t('vehicles.maintenance.error.save');
        this.dialogError.set(`${message} (HTTP ${error.status})`);
    }
}
