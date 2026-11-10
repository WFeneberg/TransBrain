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
    VehicleCatalogEntry,
    VehicleCatalogOption,
    catalogOptions,
} from './vehicle-catalog.service';
import { VehicleDocument, VehicleDocumentService } from './vehicle-document.service';
import { dateFromYear } from './vehicle-validators';

export interface VehicleDocumentDialogData {
    vehicleId: string;
    /** null opens the dialog in create mode. */
    document: VehicleDocument | null;
    /** All DocumentType catalog entries, including inactive ones. */
    documentTypes: readonly VehicleCatalogEntry[];
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MIN_YEAR = 1900;
const REMARK_MAX_LENGTH = 500;

/** "YYYY-MM-DD" strings compare chronologically as plain strings. */
function notBeforeLastChecked(control: AbstractControl<string>): ValidationErrors | null {
    const lastCheckedOn = control.parent?.get('lastCheckedOn')?.value as string | undefined;
    return control.value && lastCheckedOn && control.value < lastCheckedOn
        ? { beforeLastChecked: true }
        : null;
}

/**
 * Saves the document itself and closes with the saved document, so a server rejection (the
 * 409 on a duplicate type, a field error) is shown inside the still-open dialog.
 */
@Component({
    selector: 'app-vehicle-document-dialog',
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
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="document-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit' : 'add_circle'
                }}</mat-icon>
                <span>{{
                    isEditMode
                        ? i18n.t('vehicles.documents.edit')
                        : i18n.t('vehicles.documents.new')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="document-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicles.documents.type') }}</mat-label>
                    <mat-select formControlName="documentType" data-testid="document-type">
                        @for (option of typeOptions; track option.code) {
                            <mat-option [value]="option.code">{{
                                optionLabel(option)
                            }}</mat-option>
                        }
                    </mat-select>
                    @if (form.controls.documentType.errors; as errors) {
                        <mat-error data-testid="document-type-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <div class="dates">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.documents.lastCheckedOn') }}</mat-label>
                        <input
                            matInput
                            type="date"
                            formControlName="lastCheckedOn"
                            data-testid="document-lastCheckedOn"
                        />
                        @if (form.controls.lastCheckedOn.errors; as errors) {
                            <mat-error data-testid="document-lastCheckedOn-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('vehicles.documents.validUntil') }}</mat-label>
                        <input
                            matInput
                            type="date"
                            formControlName="validUntil"
                            data-testid="document-validUntil"
                        />
                        @if (form.controls.validUntil.errors; as errors) {
                            <mat-error data-testid="document-validUntil-error">{{
                                fieldErrorText(errors)
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicles.documents.remark') }}</mat-label>
                    <textarea
                        matInput
                        rows="3"
                        formControlName="remark"
                        [maxlength]="remarkMaxLength"
                        data-testid="document-remark"
                    ></textarea>
                    <mat-hint align="end"
                        >{{ form.controls.remark.value.length }} / {{ remarkMaxLength }}</mat-hint
                    >
                    @if (form.controls.remark.errors; as errors) {
                        <mat-error data-testid="document-remark-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="document-cancel"
                    [disabled]="busy()"
                    (click)="cancel()"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button
                    mat-flat-button
                    type="submit"
                    data-testid="document-save"
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

            .dates {
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
                .dates {
                    grid-template-columns: minmax(0, 1fr);
                }
            }
        `,
    ],
})
export class VehicleDocumentDialogComponent {
    private readonly data = inject<VehicleDocumentDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef =
        inject<MatDialogRef<VehicleDocumentDialogComponent, VehicleDocument>>(MatDialogRef);
    private readonly service = inject(VehicleDocumentService);
    private readonly session = inject(SessionService);
    private readonly fb = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.document !== null;
    protected readonly remarkMaxLength = REMARK_MAX_LENGTH;
    protected readonly busy = signal(false);
    protected readonly dialogError = signal<string | null>(null);
    // Active types only, plus the one an edited document already has if it has been deactivated
    // since - the server accepts keeping it, but rejects choosing any other inactive type.
    protected readonly typeOptions = catalogOptions(
        this.data.documentTypes,
        this.data.document?.documentType ?? null,
    );

    protected readonly form = this.fb.nonNullable.group({
        documentType: [this.initialType(), Validators.required],
        lastCheckedOn: [this.data.document?.lastCheckedOn ?? '', dateFromYear(MIN_YEAR)],
        validUntil: [
            this.data.document?.validUntil ?? '',
            [dateFromYear(MIN_YEAR), notBeforeLastChecked],
        ],
        remark: [this.data.document?.remark ?? '', Validators.maxLength(REMARK_MAX_LENGTH)],
    });

    constructor() {
        this.form.controls.lastCheckedOn.valueChanges
            .pipe(takeUntilDestroyed())
            .subscribe(() => this.form.controls.validUntil.updateValueAndValidity());
    }

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
        if (errors['minYear']) {
            return this.i18n.t('common.fieldMinYear', errors['minYear'] as { year: number });
        }
        if (errors['beforeLastChecked']) {
            return this.i18n.t('vehicles.documents.validUntilBeforeLastChecked');
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
            documentType: raw.documentType,
            lastCheckedOn: raw.lastCheckedOn || null,
            validUntil: raw.validUntil || null,
            remark: raw.remark.trim() === '' ? null : raw.remark.trim(),
        };
        const { vehicleId, document } = this.data;
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                document
                    ? this.service.update(vehicleId, document.id, request)
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

    // The option list matches codes exactly, while the stored code may differ in case from the
    // catalog's canonical one - select the option that carries the same code case-insensitively.
    private initialType(): string {
        const code = this.data.document?.documentType;
        if (!code) {
            return '';
        }
        return (
            this.typeOptions.find((option) => option.code.toLowerCase() === code.toLowerCase())
                ?.code ?? code
        );
    }

    // ValidationProblem keys are the .NET property names (PascalCase), the controls camelCase.
    // Anything without a matching control - and every non-field failure such as the 409 on a
    // duplicate type - goes to the dialog-level banner.
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
            problem?.detail ?? problem?.title ?? this.i18n.t('vehicles.documents.error.save');
        this.dialogError.set(`${message} (HTTP ${error.status})`);
    }
}
