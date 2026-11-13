import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { I18nService } from '../i18n/i18n.service';
import { CountrySelectComponent } from '../shared/country-select.component';
import { GeodataEntry, GeodataOverride, GeodataService } from './geodata.service';

export interface GeodataDialogData {
    /** null opens the dialog for a new address; otherwise the address is fixed. */
    entry: GeodataEntry | null;
}

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

/**
 * Sets the manual coordinates for one address. Saves itself and closes with the saved override,
 * so a server rejection is shown inside the still-open dialog instead of losing the input.
 */
@Component({
    selector: 'app-geodata-dialog',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatDialogModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        CountrySelectComponent,
    ],
    template: `
        <form [formGroup]="form" (ngSubmit)="save()" data-testid="geodata-dialog">
            <h2 mat-dialog-title class="title">
                <mat-icon class="title-icon" aria-hidden="true">{{
                    isEditMode ? 'edit_location_alt' : 'add_location_alt'
                }}</mat-icon>
                <span>{{
                    isEditMode ? i18n.t('geodata.editTitle') : i18n.t('geodata.newTitle')
                }}</span>
            </h2>
            <mat-dialog-content class="content">
                @if (dialogError(); as message) {
                    <p class="error-banner" data-testid="geodata-dialog-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                <mat-form-field>
                    <mat-label>{{ i18n.t('common.street') }}</mat-label>
                    <input
                        matInput
                        formControlName="street"
                        maxlength="200"
                        autocomplete="off"
                        data-testid="geodata-street"
                        [readonly]="isEditMode"
                    />
                </mat-form-field>

                <div class="row">
                    <mat-form-field class="postal">
                        <mat-label>{{ i18n.t('common.postalCode') }}</mat-label>
                        <input
                            matInput
                            formControlName="postalCode"
                            maxlength="20"
                            autocomplete="off"
                            data-testid="geodata-postal"
                            [readonly]="isEditMode"
                        />
                    </mat-form-field>
                    <mat-form-field class="city">
                        <mat-label>{{ i18n.t('common.city') }}</mat-label>
                        <input
                            matInput
                            formControlName="city"
                            maxlength="200"
                            autocomplete="off"
                            data-testid="geodata-city"
                            [readonly]="isEditMode"
                        />
                    </mat-form-field>
                </div>

                <app-country-select
                    class="country"
                    [control]="form.controls.country"
                    [label]="i18n.t('common.country')"
                    testid="geodata-country"
                />

                <div class="row">
                    <mat-form-field>
                        <mat-label>{{ i18n.t('geodata.latitude') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            step="any"
                            formControlName="latitude"
                            autocomplete="off"
                            data-testid="geodata-latitude"
                        />
                        @if (form.controls.latitude.errors) {
                            <mat-error data-testid="geodata-latitude-error">{{
                                i18n.t('geodata.latitudeRange')
                            }}</mat-error>
                        }
                    </mat-form-field>
                    <mat-form-field>
                        <mat-label>{{ i18n.t('geodata.longitude') }}</mat-label>
                        <input
                            matInput
                            type="number"
                            step="any"
                            formControlName="longitude"
                            autocomplete="off"
                            data-testid="geodata-longitude"
                        />
                        @if (form.controls.longitude.errors) {
                            <mat-error data-testid="geodata-longitude-error">{{
                                i18n.t('geodata.longitudeRange')
                            }}</mat-error>
                        }
                    </mat-form-field>
                </div>

                <p class="hint">{{ i18n.t('geodata.coordinatesHint') }}</p>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button
                    mat-button
                    type="button"
                    data-testid="geodata-cancel"
                    [mat-dialog-close]="undefined"
                >
                    {{ i18n.t('common.cancel') }}
                </button>
                <button
                    mat-flat-button
                    type="submit"
                    data-testid="geodata-save"
                    [disabled]="saving()"
                >
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
                gap: 8px;
            }

            .title-icon {
                color: var(--mat-sys-primary);
            }

            .content {
                display: flex;
                flex-direction: column;
                padding-top: 8px;
            }

            mat-form-field {
                width: 100%;
            }

            .row {
                display: flex;
                gap: 12px;
            }

            .row > * {
                flex: 1;
            }

            .postal {
                flex: 0 0 120px;
            }

            .country {
                width: 240px;
            }

            .hint {
                margin: 0;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .error-banner {
                display: flex;
                align-items: center;
                gap: 8px;
                margin: 0 0 12px;
                padding: 12px 16px;
                border-radius: var(--mat-sys-corner-medium);
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }
        `,
    ],
})
export class GeodataDialogComponent {
    private readonly service = inject(GeodataService);
    private readonly dialogRef =
        inject<MatDialogRef<GeodataDialogComponent, GeodataOverride>>(MatDialogRef);
    private readonly data = inject<GeodataDialogData>(MAT_DIALOG_DATA);
    private readonly formBuilder = inject(FormBuilder);
    protected readonly i18n = inject(I18nService);

    protected readonly isEditMode = this.data.entry !== null;
    protected readonly saving = signal(false);
    protected readonly dialogError = signal<string | null>(null);

    protected readonly form = this.formBuilder.nonNullable.group({
        street: [this.data.entry?.street ?? '', [Validators.required, Validators.maxLength(200)]],
        postalCode: [
            this.data.entry?.postalCode ?? '',
            [Validators.required, Validators.maxLength(20)],
        ],
        city: [this.data.entry?.city ?? '', [Validators.required, Validators.maxLength(200)]],
        country: [this.data.entry?.country ?? '', Validators.required],
        latitude: [
            this.data.entry?.latitude ?? (null as number | null),
            [Validators.required, Validators.min(-90), Validators.max(90)],
        ],
        longitude: [
            this.data.entry?.longitude ?? (null as number | null),
            [Validators.required, Validators.min(-180), Validators.max(180)],
        ],
    });

    constructor() {
        if (this.isEditMode) {
            // The address identifies the override and must stay fixed; getRawValue() still submits it.
            this.form.controls.country.disable();
        }
    }

    protected save(): void {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }
        const value = this.form.getRawValue();
        this.saving.set(true);
        this.dialogError.set(null);
        this.service
            .setOverride({
                street: value.street.trim(),
                postalCode: value.postalCode.trim(),
                city: value.city.trim(),
                country: value.country.trim(),
                latitude: value.latitude!,
                longitude: value.longitude!,
            })
            .subscribe({
                next: (saved) => this.dialogRef.close(saved),
                error: (error: HttpErrorResponse) => {
                    this.saving.set(false);
                    this.dialogError.set(this.describe(error));
                },
            });
    }

    private describe(error: HttpErrorResponse): string {
        const problem = error.error as ProblemDetailsBody | null;
        const fieldErrors = problem?.errors ? Object.values(problem.errors).flat() : [];
        const sentence =
            fieldErrors[0] ??
            problem?.detail ??
            problem?.title ??
            this.i18n.t('geodata.error.save');
        return `${sentence} (HTTP ${error.status})`;
    }
}
