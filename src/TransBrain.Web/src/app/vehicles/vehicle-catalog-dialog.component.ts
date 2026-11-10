import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import {
    VehicleCatalogEntry,
    VehicleCatalogKind,
    VehicleCatalogService,
} from './vehicle-catalog.service';

export interface VehicleCatalogDialogData {
    kind: VehicleCatalogKind;
    /** null opens the dialog in create mode. */
    entry: VehicleCatalogEntry | null;
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const CODE_PATTERN = /^[A-Za-z0-9_-]+$/;

/**
 * Saves the entry itself and closes with the saved entry, so a server rejection (a duplicate
 * code, a field error) is shown inside the still-open dialog instead of losing the input.
 */
@Component({
    selector: 'app-vehicle-catalog-dialog',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatDialogModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
    ],
    template: `
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="catalog-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit' : 'add_circle'
                }}</mat-icon>
                <span>{{
                    isEditMode ? i18n.t('vehicleCatalog.edit') : i18n.t('vehicleCatalog.new')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                <p class="kind">{{ i18n.t(kindKey) }}</p>

                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="catalog-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicleCatalog.code') }}</mat-label>
                    <input
                        matInput
                        formControlName="code"
                        maxlength="20"
                        autocomplete="off"
                        data-testid="catalog-code"
                        [readonly]="isEditMode"
                    />
                    <mat-hint>{{ i18n.t('vehicleCatalog.codeHint') }}</mat-hint>
                    @if (form.controls.code.errors; as errors) {
                        <mat-error data-testid="catalog-code-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <mat-form-field>
                    <mat-label>{{ i18n.t('vehicleCatalog.name') }}</mat-label>
                    <input
                        matInput
                        formControlName="name"
                        maxlength="100"
                        autocomplete="off"
                        data-testid="catalog-name"
                    />
                    @if (form.controls.name.errors; as errors) {
                        <mat-error data-testid="catalog-name-error">{{
                            fieldErrorText(errors)
                        }}</mat-error>
                    }
                </mat-form-field>

                <mat-checkbox formControlName="isActive" data-testid="catalog-active">
                    {{ i18n.t('vehicleCatalog.active') }}
                </mat-checkbox>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="catalog-cancel"
                    [disabled]="busy()"
                    (click)="cancel()"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button
                    mat-flat-button
                    type="submit"
                    data-testid="catalog-save"
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

            .kind {
                margin: 0 0 8px;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-medium);
            }

            mat-form-field {
                width: 100%;
            }

            input[readonly] {
                color: var(--mat-sys-on-surface-variant);
                cursor: default;
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
        `,
    ],
})
export class VehicleCatalogDialogComponent {
    private readonly data = inject<VehicleCatalogDialogData>(MAT_DIALOG_DATA);
    private readonly dialogRef =
        inject<MatDialogRef<VehicleCatalogDialogComponent, VehicleCatalogEntry>>(MatDialogRef);
    private readonly service = inject(VehicleCatalogService);
    private readonly session = inject(SessionService);
    private readonly fb = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.entry !== null;
    protected readonly kindKey = `vehicleCatalog.kind.${this.data.kind}` as const;
    protected readonly busy = signal(false);
    protected readonly dialogError = signal<string | null>(null);

    // The code is immutable after create: in edit mode the field is read-only and its validators
    // are dropped, so a legacy code that would not pass today's pattern cannot block a rename.
    protected readonly form = this.fb.nonNullable.group({
        code: [
            this.data.entry?.code ?? '',
            this.isEditMode
                ? []
                : [Validators.required, Validators.maxLength(20), Validators.pattern(CODE_PATTERN)],
        ],
        name: [this.data.entry?.name ?? '', [Validators.required, Validators.maxLength(100)]],
        isActive: [this.data.entry?.isActive ?? true],
    });

    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['pattern']) {
            return this.i18n.t('vehicleCatalog.codePattern');
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
        const { kind, entry } = this.data;
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                entry
                    ? this.service.update(kind, entry.id, {
                          name: raw.name.trim(),
                          isActive: raw.isActive,
                      })
                    : this.service.create(kind, {
                          code: raw.code.trim(),
                          name: raw.name.trim(),
                          isActive: raw.isActive,
                      }),
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

    // Same mapping as the vehicle form: the ValidationProblem keys are the .NET property names
    // (PascalCase "Code" / "Name"), the controls use camelCase. Anything without a matching
    // control - and every non-field failure such as the 409 on a duplicate code - goes to the
    // dialog-level banner.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const controlName = field.charAt(0).toLowerCase() + field.slice(1);
                const control = this.form.get(controlName);
                const message = messages.join(' ');
                if (control && !(this.isEditMode && controlName === 'code')) {
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
            problem?.detail ?? problem?.title ?? this.i18n.t('vehicleCatalog.error.save');
        this.dialogError.set(`${message} (HTTP ${error.status})`);
    }
}
