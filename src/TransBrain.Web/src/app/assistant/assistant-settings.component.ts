import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
    AbstractControl,
    FormBuilder,
    ReactiveFormsModule,
    ValidationErrors,
    Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { merge } from 'rxjs';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import {
    AssistantSettings,
    AssistantSettingsPayload,
    AssistantSettingsService,
} from './assistant-settings.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

/** German-friendly labels for the provider ids the API knows; unknown ids show as-is. */
const PROVIDER_LABEL_KEYS: Record<string, TranslationKey> = {
    openrouter: 'assistant.settings.provider.openrouter',
    'openai-api': 'assistant.settings.provider.openai-api',
    anthropic: 'assistant.settings.provider.anthropic',
    gemini: 'assistant.settings.provider.gemini',
    deepseek: 'assistant.settings.provider.deepseek',
    xai: 'assistant.settings.provider.xai',
    custom: 'assistant.settings.provider.custom',
};

function noWhitespace(control: AbstractControl<string>): ValidationErrors | null {
    return /\s/.test(control.value) ? { whitespace: true } : null;
}

function absoluteHttpUrl(control: AbstractControl<string>): ValidationErrors | null {
    const value = control.value.trim();
    if (value === '') {
        return null;
    }
    try {
        const url = new URL(value);
        return url.protocol === 'http:' || url.protocol === 'https:' ? null : { url: true };
    } catch {
        return { url: true };
    }
}

/**
 * The KI-Assistent tab of the Wartung area: provider, model and API key of the installation's
 * shared agent container. Unlike its sibling tabs this page is NOT per-tenant - it needs no
 * tenant selection and acts on the one agent every user shares. The API key is write-only:
 * the server only ever says whether one is stored, and an empty field keeps it.
 */
@Component({
    selector: 'app-assistant-settings',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatSelectModule,
        MatSlideToggleModule,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page narrow">
            <header>
                <h1>{{ i18n.t('assistant.settings.title') }}</h1>
                <p class="subtitle">{{ i18n.t('assistant.settings.subtitle') }}</p>
            </header>

            @if (loading()) {
                <mat-progress-bar mode="indeterminate" data-testid="assistant-settings-loading" />
            }

            @if (loadError()) {
                <p class="error-banner" data-testid="assistant-settings-load-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>
                    {{ i18n.t('assistant.settings.error.load') }}
                </p>
            }

            @if (notProvisioned()) {
                <p class="error-banner" data-testid="assistant-settings-not-provisioned">
                    <mat-icon aria-hidden="true">warning</mat-icon>
                    {{ i18n.t('assistant.settings.notProvisioned') }}
                </p>
            }

            @if (!loading() && !loadError()) {
                <form [formGroup]="form" (ngSubmit)="save()">
                    <mat-card appearance="outlined">
                        <mat-card-content class="fields one">
                            <mat-slide-toggle
                                formControlName="enabled"
                                data-testid="assistant-settings-enabled"
                            >
                                {{ i18n.t('assistant.settings.enabled') }}
                            </mat-slide-toggle>
                            <p class="hint toggle-hint">
                                {{ i18n.t('assistant.settings.enabledHint') }}
                            </p>

                            <mat-form-field>
                                <mat-label>{{ i18n.t('assistant.settings.provider') }}</mat-label>
                                <mat-select
                                    formControlName="provider"
                                    data-testid="assistant-settings-provider"
                                >
                                    @for (provider of providers(); track provider) {
                                        <mat-option [value]="provider">
                                            {{ providerLabel(provider) }}
                                        </mat-option>
                                    }
                                </mat-select>
                            </mat-form-field>

                            <mat-form-field>
                                <mat-label>{{ i18n.t('assistant.settings.model') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="model"
                                    data-testid="assistant-settings-model"
                                />
                                <mat-hint>{{ i18n.t('assistant.settings.modelHint') }}</mat-hint>
                                @if (form.controls.model.errors; as errors) {
                                    <mat-error data-testid="assistant-settings-model-error">
                                        {{
                                            errors['whitespace']
                                                ? i18n.t('assistant.settings.modelNoWhitespace')
                                                : i18n.t('common.fieldRequired')
                                        }}
                                    </mat-error>
                                }
                            </mat-form-field>

                            @if (form.controls.provider.value === 'custom') {
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('assistant.settings.baseUrl') }}</mat-label>
                                    <input
                                        matInput
                                        formControlName="customBaseUrl"
                                        placeholder="https://"
                                        data-testid="assistant-settings-base-url"
                                    />
                                    <mat-hint>{{ i18n.t('assistant.settings.baseUrlHint') }}</mat-hint>
                                    @if (form.controls.customBaseUrl.errors; as errors) {
                                        <mat-error data-testid="assistant-settings-base-url-error">
                                            {{
                                                errors['required']
                                                    ? i18n.t('common.fieldRequired')
                                                    : i18n.t('assistant.settings.baseUrlInvalid')
                                            }}
                                        </mat-error>
                                    }
                                </mat-form-field>
                            }

                            <mat-slide-toggle
                                formControlName="autonomousActionsEnabled"
                                data-testid="assistant-settings-autonomy"
                            >
                                {{ i18n.t('assistant.settings.autonomy') }}
                            </mat-slide-toggle>
                            <p class="hint toggle-hint">
                                {{ i18n.t('assistant.settings.autonomyHint') }}
                            </p>

                            <mat-form-field>
                                <mat-label>{{ i18n.t('assistant.settings.apiKey') }}</mat-label>
                                <input
                                    matInput
                                    type="password"
                                    autocomplete="off"
                                    formControlName="apiKey"
                                    data-testid="assistant-settings-api-key"
                                />
                                <mat-hint>
                                    {{
                                        hasApiKey()
                                            ? i18n.t('assistant.settings.apiKeyStored')
                                            : i18n.t('assistant.settings.apiKeyHint')
                                    }}
                                </mat-hint>
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    @if (formError(); as message) {
                        <p class="error-banner" data-testid="assistant-settings-error">
                            <mat-icon aria-hidden="true">error_outline</mat-icon>
                            {{ message }}
                        </p>
                    }

                    <div class="action-bar">
                        <button
                            mat-flat-button
                            type="submit"
                            data-testid="assistant-settings-save"
                            [disabled]="saving()"
                        >
                            {{ i18n.t('assistant.settings.save') }}
                        </button>
                    </div>
                    @if (saving()) {
                        <mat-progress-bar mode="indeterminate" />
                    }
                </form>
            }
        </div>
    `,
    styles: `
        .page.narrow {
            max-width: 720px;
        }

        .fields.one {
            row-gap: 12px;
        }

        .toggle-hint {
            margin: 0 0 8px;
        }
    `,
})
export class AssistantSettingsComponent {
    protected readonly i18n = inject(I18nService);
    private readonly service = inject(AssistantSettingsService);
    private readonly snackBar = inject(MatSnackBar);
    private readonly formBuilder = inject(FormBuilder);

    protected readonly loading = signal(true);
    protected readonly loadError = signal(false);
    protected readonly saving = signal(false);
    protected readonly notProvisioned = signal(false);
    protected readonly hasApiKey = signal(false);
    protected readonly providers = signal<string[]>([]);
    protected readonly formError = signal<string | null>(null);

    protected readonly form = this.formBuilder.nonNullable.group({
        enabled: [false],
        provider: ['openrouter'],
        model: ['', [noWhitespace]],
        customBaseUrl: ['', [absoluteHttpUrl]],
        autonomousActionsEnabled: [false],
        apiKey: [''],
    });

    constructor() {
        // The same conditional rules the server enforces: a model is required only while the
        // assistant is enabled, a base URL only for the custom provider.
        merge(this.form.controls.enabled.valueChanges, this.form.controls.provider.valueChanges)
            .pipe(takeUntilDestroyed())
            .subscribe(() => this.applyConditionalValidators());

        this.service.getSettings().subscribe({
            next: (settings) => {
                this.applySettings(settings);
                this.loading.set(false);
            },
            error: () => {
                this.loadError.set(true);
                this.loading.set(false);
            },
        });
    }

    protected providerLabel(provider: string): string {
        const key = PROVIDER_LABEL_KEYS[provider];
        return key === undefined ? provider : this.i18n.t(key);
    }

    protected save(): void {
        this.formError.set(null);
        this.applyConditionalValidators();
        this.form.markAllAsTouched();
        if (this.form.invalid) {
            return;
        }

        this.saving.set(true);
        this.service.updateSettings(this.payload()).subscribe({
            next: (settings) => {
                this.applySettings(settings);
                this.form.controls.apiKey.setValue('');
                this.saving.set(false);
                this.snackBar.open(this.i18n.t('assistant.settings.saved'), undefined, {
                    duration: 4000,
                });
            },
            error: (error: HttpErrorResponse) => {
                this.applyProblem(error);
                this.saving.set(false);
            },
        });
    }

    private payload(): AssistantSettingsPayload {
        const value = this.form.getRawValue();
        return {
            enabled: value.enabled,
            provider: value.provider,
            model: value.model.trim(),
            customBaseUrl: value.provider === 'custom' ? value.customBaseUrl.trim() : null,
            autonomousActionsEnabled: value.autonomousActionsEnabled,
            // Null keeps the key already stored in the agent's volume.
            apiKey: value.apiKey.trim() === '' ? null : value.apiKey,
        };
    }

    private applySettings(settings: AssistantSettings): void {
        this.notProvisioned.set(!settings.provisioned);
        this.hasApiKey.set(settings.hasApiKey);
        this.providers.set(settings.knownProviders);
        this.form.patchValue({
            enabled: settings.enabled,
            provider: settings.provider,
            model: settings.model,
            customBaseUrl: settings.customBaseUrl ?? '',
            autonomousActionsEnabled: settings.autonomousActionsEnabled,
        });
        this.applyConditionalValidators();
    }

    /**
     * 503 "Assistant.NotProvisioned" turns into the same warning banner the GET announces;
     * validation problems show the server's own messages, which name the exact rule.
     */
    private applyProblem(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null | undefined;
        if (problem?.title === 'Assistant.NotProvisioned') {
            this.notProvisioned.set(true);
            return;
        }
        const details = problem?.errors === undefined ? [] : Object.values(problem.errors).flat();
        if (details.length > 0) {
            this.formError.set(details.join(' '));
            return;
        }
        this.formError.set(problem?.detail ?? this.i18n.t('assistant.settings.error.save'));
    }

    private applyConditionalValidators(): void {
        const model = this.form.controls.model;
        const baseUrl = this.form.controls.customBaseUrl;
        model.setValidators(
            this.form.controls.enabled.value ? [Validators.required, noWhitespace] : [noWhitespace],
        );
        baseUrl.setValidators(
            this.form.controls.provider.value === 'custom'
                ? [Validators.required, absoluteHttpUrl]
                : [absoluteHttpUrl],
        );
        model.updateValueAndValidity({ emitEvent: false });
        baseUrl.updateValueAndValidity({ emitEvent: false });
    }
}
