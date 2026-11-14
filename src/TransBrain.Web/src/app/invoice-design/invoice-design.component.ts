import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { CountrySelectComponent } from '../shared/country-select.component';
import { wholeNumberInRange } from '../vehicles/vehicle-validators';
import {
    ALLOWED_LOGO_TYPES,
    InvoiceDesign,
    InvoiceDesignPayload,
    InvoiceDesignService,
    MAX_LOGO_BYTES,
} from './invoice-design.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const PASCAL_SEGMENTS = /(^|\.)([A-Z])/g;

const MAX_TEXT_LENGTH = 1000;

@Component({
    selector: 'app-invoice-design',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatSelectModule,
        CountrySelectComponent,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div>
                    <h1>{{ i18n.t('invoiceDesign.title') }}</h1>
                    <p class="subtitle">{{ i18n.t('invoiceDesign.subtitle') }}</p>
                </div>
            </header>

            @if (loading()) {
                <mat-progress-bar mode="indeterminate" data-testid="design-loading" />
            }

            @if (!loading() && !isCustomized()) {
                <p class="hint defaults-hint" data-testid="design-defaults-hint">
                    <mat-icon aria-hidden="true">info</mat-icon>
                    {{ i18n.t('invoiceDesign.defaultsHint') }}
                </p>
            }

            @if (successMessage(); as message) {
                <p class="success-banner" data-testid="design-success">
                    <mat-icon aria-hidden="true">check_circle</mat-icon>{{ message }}
                </p>
            }

            @if (formError(); as message) {
                <p class="error-banner" data-testid="design-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">business</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoiceDesign.section.company')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            @for (field of companyFields; track field.name) {
                                @if (field.name === 'countryCode') {
                                    <app-country-select
                                        [control]="form.controls.countryCode"
                                        [label]="i18n.t('common.country')"
                                        testid="design-countryCode"
                                    />
                                } @else {
                                    <mat-form-field>
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'design-' + field.name"
                                        />
                                        @if (form.controls[field.name].errors; as errors) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'design-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">account_balance</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoiceDesign.section.bank')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            @for (field of bankFields; track field.name) {
                                <mat-form-field>
                                    <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                    <input
                                        matInput
                                        [formControlName]="field.name"
                                        [attr.data-testid]="'design-' + field.name"
                                    />
                                    @if (form.controls[field.name].errors; as errors) {
                                        <mat-error
                                            [attr.data-testid]="'design-' + field.name + '-error'"
                                            >{{ fieldErrorText(errors) }}</mat-error
                                        >
                                    }
                                </mat-form-field>
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined" class="full-width">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">palette</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoiceDesign.section.design')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <div class="fields three">
                                <div class="color-field">
                                    <label for="design-accent-color">{{
                                        i18n.t('invoiceDesign.accentColor')
                                    }}</label>
                                    <div class="color-input">
                                        <input
                                            id="design-accent-color"
                                            type="color"
                                            formControlName="accentColor"
                                            data-testid="design-accentColor"
                                        />
                                        <code>{{
                                            form.controls.accentColor.value.toUpperCase()
                                        }}</code>
                                    </div>
                                </div>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('invoiceDesign.fontSize') }}</mat-label>
                                    <input
                                        matInput
                                        type="number"
                                        min="8"
                                        max="14"
                                        step="1"
                                        formControlName="fontSizePoints"
                                        data-testid="design-fontSizePoints"
                                    />
                                    <mat-hint>{{ i18n.t('invoiceDesign.fontSizeHint') }}</mat-hint>
                                    @if (form.controls.fontSizePoints.errors; as errors) {
                                        <mat-error data-testid="design-fontSizePoints-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('invoiceDesign.logoPosition')
                                    }}</mat-label>
                                    <mat-select
                                        formControlName="logoPosition"
                                        data-testid="design-logoPosition"
                                    >
                                        <mat-option value="Left">{{
                                            i18n.t('invoiceDesign.logoPosition.Left')
                                        }}</mat-option>
                                        <mat-option value="Right">{{
                                            i18n.t('invoiceDesign.logoPosition.Right')
                                        }}</mat-option>
                                    </mat-select>
                                </mat-form-field>
                            </div>

                            <div class="logo-block">
                                <input
                                    #logoInput
                                    type="file"
                                    accept="image/png,image/jpeg"
                                    hidden
                                    data-testid="design-logo-input"
                                    (change)="onLogoSelected(logoInput)"
                                />
                                @if (logoDataUrl(); as url) {
                                    <img
                                        [src]="url"
                                        [alt]="i18n.t('invoiceDesign.logo.alt')"
                                        class="logo-preview"
                                        data-testid="design-logo-preview"
                                    />
                                }
                                <div class="logo-actions">
                                    <button
                                        mat-stroked-button
                                        type="button"
                                        data-testid="design-logo-choose"
                                        (click)="logoInput.click()"
                                    >
                                        <mat-icon>upload</mat-icon>
                                        {{ i18n.t('invoiceDesign.logo.choose') }}
                                    </button>
                                    @if (logoDataUrl()) {
                                        <button
                                            mat-stroked-button
                                            type="button"
                                            data-testid="design-logo-remove"
                                            (click)="removeLogo()"
                                        >
                                            <mat-icon>delete</mat-icon>
                                            {{ i18n.t('invoiceDesign.logo.remove') }}
                                        </button>
                                    }
                                </div>
                                <p class="hint">
                                    <mat-icon aria-hidden="true">image</mat-icon>
                                    {{ i18n.t('invoiceDesign.logo.hint') }}
                                </p>
                                @if (logoError(); as message) {
                                    <p class="error-banner" data-testid="design-logo-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                            </div>

                            <div class="fields">
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('invoiceDesign.headerText') }}</mat-label>
                                    <textarea
                                        matInput
                                        rows="3"
                                        [maxlength]="maxTextLength"
                                        formControlName="headerText"
                                        data-testid="design-headerText"
                                    ></textarea>
                                    <mat-hint>{{
                                        i18n.t('invoiceDesign.headerTextHint')
                                    }}</mat-hint>
                                    @if (form.controls.headerText.errors; as errors) {
                                        <mat-error data-testid="design-headerText-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('invoiceDesign.footerText') }}</mat-label>
                                    <textarea
                                        matInput
                                        rows="3"
                                        [maxlength]="maxTextLength"
                                        formControlName="footerText"
                                        data-testid="design-footerText"
                                    ></textarea>
                                    <mat-hint>{{
                                        i18n.t('invoiceDesign.footerTextHint')
                                    }}</mat-hint>
                                    @if (form.controls.footerText.errors; as errors) {
                                        <mat-error data-testid="design-footerText-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                            </div>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined" class="full-width">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">picture_as_pdf</mat-icon>
                            <mat-card-title>{{
                                i18n.t('invoiceDesign.previewTitle')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            @if (previewUrl(); as url) {
                                <iframe
                                    [src]="url"
                                    class="preview-frame"
                                    title="PDF"
                                    data-testid="design-preview-frame"
                                ></iframe>
                            } @else {
                                <p class="hint">
                                    <mat-icon aria-hidden="true">visibility</mat-icon>
                                    {{ i18n.t('invoiceDesign.previewEmpty') }}
                                </p>
                            }
                            @if (previewing()) {
                                <mat-progress-bar mode="indeterminate" />
                            }
                        </mat-card-content>
                    </mat-card>
                </div>

                <div class="action-bar">
                    @if (updatedAt(); as savedAt) {
                        <span class="audit" data-testid="design-updatedAt">
                            <mat-icon aria-hidden="true">history</mat-icon>
                            {{ i18n.t('invoiceDesign.updatedAt', { date: savedAt }) }}
                        </span>
                    }
                    <span class="action-spacer"></span>
                    <button
                        mat-stroked-button
                        type="button"
                        data-testid="design-preview"
                        [disabled]="busy()"
                        (click)="preview()"
                    >
                        <mat-icon>visibility</mat-icon>
                        {{ i18n.t('invoiceDesign.preview') }}
                    </button>
                    <button
                        mat-flat-button
                        type="submit"
                        data-testid="design-save"
                        [disabled]="busy()"
                    >
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: `
        .defaults-hint {
            padding: 12px 16px;
            border-radius: var(--mat-sys-corner-medium);
            background: var(--mat-sys-surface-container);
            font: var(--mat-sys-body-medium);
        }

        .success-banner {
            display: flex;
            align-items: center;
            gap: 8px;
            margin: 0;
            padding: 12px 16px;
            border-radius: var(--mat-sys-corner-medium);
            background: #d7f3dc;
            color: #0f5223;
        }

        .color-field {
            display: flex;
            flex-direction: column;
            gap: 8px;
            padding-top: 4px;

            label {
                font: var(--mat-sys-body-small);
                color: var(--mat-sys-on-surface-variant);
            }
        }

        .color-input {
            display: flex;
            align-items: center;
            gap: 12px;

            input[type='color'] {
                width: 56px;
                height: 40px;
                padding: 2px;
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-small);
                background: var(--mat-sys-surface);
                cursor: pointer;
            }
        }

        .logo-block {
            display: flex;
            flex-direction: column;
            gap: 8px;
            margin: 8px 0 16px;
        }

        .logo-preview {
            max-height: 72px;
            max-width: 240px;
            align-self: flex-start;
            border: 1px solid var(--mat-sys-outline-variant);
            border-radius: var(--mat-sys-corner-small);
            padding: 4px;
            background: #fff;
        }

        .logo-actions {
            display: flex;
            gap: 8px;
            flex-wrap: wrap;
        }

        .preview-frame {
            width: 100%;
            height: 640px;
            border: 1px solid var(--mat-sys-outline-variant);
            border-radius: var(--mat-sys-corner-medium);
            background: var(--mat-sys-surface-container-low);
        }

        .action-spacer {
            flex: 1;
        }
    `,
})
export class InvoiceDesignComponent {
    private readonly service = inject(InvoiceDesignService);
    private readonly fb = inject(FormBuilder);
    private readonly sanitizer = inject(DomSanitizer);
    private readonly destroyRef = inject(DestroyRef);
    // See CustomerFormComponent: a directly opened page must wait for the stored session.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly maxTextLength = MAX_TEXT_LENGTH;

    protected readonly companyFields = [
        { name: 'companyName', label: 'invoiceDesign.companyName' },
        { name: 'street', label: 'common.street' },
        { name: 'postalCode', label: 'common.postalCode' },
        { name: 'city', label: 'common.city' },
        { name: 'countryCode', label: 'common.country' },
        { name: 'vatId', label: 'customers.vatId' },
        { name: 'taxNumber', label: 'invoiceDesign.taxNumber' },
        { name: 'email', label: 'customers.email' },
        { name: 'phone', label: 'customers.phone' },
    ] as const;

    protected readonly bankFields = [
        { name: 'iban', label: 'invoiceDesign.iban' },
        { name: 'bic', label: 'invoiceDesign.bic' },
        { name: 'bankName', label: 'invoiceDesign.bankName' },
    ] as const;

    protected readonly loading = signal(true);
    protected readonly saving = signal(false);
    protected readonly previewing = signal(false);
    protected readonly busy = computed(() => this.loading() || this.saving() || this.previewing());

    protected readonly isCustomized = signal(true);
    protected readonly updatedAt = signal<string | null>(null);
    protected readonly formError = signal<string | null>(null);
    protected readonly successMessage = signal<string | null>(null);
    protected readonly logoError = signal<string | null>(null);

    // The logo lives outside the form group: it is picked through a file dialog, not typed.
    protected readonly logoBase64 = signal<string | null>(null);
    protected readonly logoContentType = signal<string | null>(null);
    protected readonly logoDataUrl = computed(() => {
        const base64 = this.logoBase64();
        const contentType = this.logoContentType();
        return base64 && contentType ? `data:${contentType};base64,${base64}` : null;
    });

    protected readonly previewUrl = signal<SafeResourceUrl | null>(null);
    private previewObjectUrl: string | null = null;

    protected readonly form = this.fb.nonNullable.group({
        companyName: ['', Validators.required],
        street: ['', Validators.required],
        postalCode: ['', Validators.required],
        city: ['', Validators.required],
        countryCode: ['', Validators.required],
        vatId: ['', Validators.required],
        taxNumber: [''],
        email: ['', [Validators.required, Validators.email]],
        phone: [''],
        iban: ['', Validators.required],
        bic: [''],
        bankName: [''],
        accentColor: ['#1F3864', Validators.required],
        fontSizePoints: [10, [Validators.required, wholeNumberInRange(8, 14)]],
        logoPosition: ['Right', Validators.required],
        headerText: ['', Validators.maxLength(MAX_TEXT_LENGTH)],
        footerText: ['', Validators.maxLength(MAX_TEXT_LENGTH)],
    });

    constructor() {
        this.session.ready.pipe(switchMap(() => this.service.getDesign())).subscribe({
            next: (design) => {
                this.applyDesign(design);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.formError.set(
                    this.describeFailure(error, this.i18n.t('invoiceDesign.error.load')),
                );
                this.loading.set(false);
            },
        });

        // Blob URLs stay alive until revoked; free the last one when the page goes away.
        this.destroyRef.onDestroy(() => this.revokePreview());
    }

    protected onLogoSelected(input: HTMLInputElement): void {
        const file = input.files?.item(0);
        // Clear the input so picking the same file again still fires a change event.
        input.value = '';
        if (!file) {
            return;
        }
        this.logoError.set(null);
        if (!(ALLOWED_LOGO_TYPES as readonly string[]).includes(file.type)) {
            this.logoError.set(this.i18n.t('invoiceDesign.logo.wrongType'));
            return;
        }
        if (file.size > MAX_LOGO_BYTES) {
            this.logoError.set(this.i18n.t('invoiceDesign.logo.tooLarge'));
            return;
        }
        const reader = new FileReader();
        reader.onload = () => {
            // readAsDataURL yields "data:image/png;base64,...."; the API wants the bare base64.
            const result = reader.result as string;
            this.logoBase64.set(result.slice(result.indexOf(',') + 1));
            this.logoContentType.set(file.type);
        };
        reader.onerror = () => this.logoError.set(this.i18n.t('invoiceDesign.logo.readFailed'));
        reader.readAsDataURL(file);
    }

    protected removeLogo(): void {
        this.logoBase64.set(null);
        this.logoContentType.set(null);
        this.logoError.set(null);
    }

    protected preview(): void {
        if (!this.validate()) {
            return;
        }
        this.previewing.set(true);
        this.service.previewDesign(this.payload()).subscribe({
            next: (blob) => {
                this.showPreview(blob);
                this.previewing.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.handleBlobError(error, 'invoiceDesign.error.preview');
                this.previewing.set(false);
            },
        });
    }

    protected save(): void {
        if (!this.validate()) {
            return;
        }
        this.saving.set(true);
        this.service.updateDesign(this.payload()).subscribe({
            next: (design) => {
                this.applyDesign(design);
                this.successMessage.set(this.i18n.t('invoiceDesign.saved'));
                this.saving.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.applyProblem(
                    error.error as ProblemDetailsBody | null,
                    error.status,
                    'invoiceDesign.error.save',
                );
                this.saving.set(false);
            },
        });
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

    private validate(): boolean {
        this.formError.set(null);
        this.successMessage.set(null);
        this.form.markAllAsTouched();
        return this.form.valid;
    }

    private payload(): InvoiceDesignPayload {
        const value = this.form.getRawValue();
        return {
            companyName: value.companyName.trim(),
            street: value.street.trim(),
            postalCode: value.postalCode.trim(),
            city: value.city.trim(),
            countryCode: value.countryCode.trim().toUpperCase(),
            vatId: value.vatId.trim(),
            taxNumber: this.optional(value.taxNumber),
            email: value.email.trim(),
            phone: this.optional(value.phone),
            iban: value.iban.trim(),
            bic: this.optional(value.bic),
            bankName: this.optional(value.bankName),
            // The native colour input emits lowercase hex; the domain stores uppercase.
            accentColor: value.accentColor.trim().toUpperCase(),
            fontSizePoints: Number(value.fontSizePoints),
            logoPosition: value.logoPosition,
            logoBase64: this.logoBase64(),
            logoContentType: this.logoContentType(),
            headerText: this.optional(value.headerText),
            footerText: this.optional(value.footerText),
        };
    }

    private optional(value: string): string | null {
        const trimmed = value.trim();
        return trimmed === '' ? null : trimmed;
    }

    private applyDesign(design: InvoiceDesign): void {
        this.form.patchValue({
            companyName: design.companyName,
            street: design.street,
            postalCode: design.postalCode,
            city: design.city,
            countryCode: design.countryCode,
            vatId: design.vatId,
            taxNumber: design.taxNumber ?? '',
            email: design.email,
            phone: design.phone ?? '',
            iban: design.iban,
            bic: design.bic ?? '',
            bankName: design.bankName ?? '',
            accentColor: design.accentColor,
            fontSizePoints: design.fontSizePoints,
            logoPosition: design.logoPosition,
            headerText: design.headerText ?? '',
            footerText: design.footerText ?? '',
        });
        this.logoBase64.set(design.logoBase64);
        this.logoContentType.set(design.logoContentType);
        this.isCustomized.set(design.isCustomized);
        this.updatedAt.set(
            design.updatedAt ? new Date(design.updatedAt).toLocaleString(this.i18n.locale()) : null,
        );
    }

    private showPreview(blob: Blob): void {
        this.revokePreview();
        this.previewObjectUrl = URL.createObjectURL(blob);
        // blob: URLs are blocked as iframe resources unless explicitly trusted; ours comes
        // straight from our own HttpClient response, never from user input.
        this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl));
    }

    private revokePreview(): void {
        if (this.previewObjectUrl) {
            URL.revokeObjectURL(this.previewObjectUrl);
            this.previewObjectUrl = null;
        }
        this.previewUrl.set(null);
    }

    /** A blob request's error body arrives as a Blob - read it before treating it as a problem. */
    private handleBlobError(error: HttpErrorResponse, fallbackKey: TranslationKey): void {
        if (error.error instanceof Blob) {
            void error.error.text().then((text) => {
                let problem: ProblemDetailsBody | null = null;
                try {
                    problem = JSON.parse(text) as ProblemDetailsBody;
                } catch {
                    // Not JSON: fall through to the generic message.
                }
                this.applyProblem(problem, error.status, fallbackKey);
            });
            return;
        }
        this.applyProblem(error.error as ProblemDetailsBody | null, error.status, fallbackKey);
    }

    // Field keys arrive PascalCase (.NET property names); lowercase the first letter of every
    // segment to find the control - same as CustomerFormComponent.
    private applyProblem(
        problem: ProblemDetailsBody | null,
        status: number,
        fallbackKey: TranslationKey,
    ): void {
        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const controlName = field.replace(
                    PASCAL_SEGMENTS,
                    (_, separator: string, letter: string) => separator + letter.toLowerCase(),
                );
                const control = this.form.get(controlName);
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                } else {
                    this.formError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }
        const sentence = problem?.detail ?? problem?.title ?? this.i18n.t(fallbackKey);
        this.formError.set(`${sentence} (HTTP ${status})`);
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
