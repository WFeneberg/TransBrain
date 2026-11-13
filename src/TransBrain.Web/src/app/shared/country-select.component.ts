import { Component, computed, inject, input } from '@angular/core';
import { FormControl, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { I18nService } from '../i18n/i18n.service';
import { COUNTRY_CODES } from './countries';

interface CountryOption {
    code: string;
    name: string;
}

/**
 * Address country dropdown: ISO 3166-1 alpha-2 codes shown with their flag (flag-icons)
 * and the country name localised through Intl.DisplayNames. The bound control keeps
 * storing the plain two-letter code.
 */
@Component({
    selector: 'app-country-select',
    standalone: true,
    imports: [MatFormFieldModule, MatSelectModule, ReactiveFormsModule],
    template: `
        <mat-form-field>
            <mat-label>{{ label() }}</mat-label>
            <mat-select
                [formControl]="control()"
                [attr.data-testid]="testid()"
                (closed)="control().markAsTouched()"
            >
                <mat-select-trigger>
                    @if (control().value; as code) {
                        <span class="fi fi-{{ code.toLowerCase() }} flag" aria-hidden="true"></span
                        >{{ countryName(code) }}
                    }
                </mat-select-trigger>
                @if (allowEmpty()) {
                    <mat-option value="">–</mat-option>
                }
                @if (storedUnknownCode(); as code) {
                    <mat-option [value]="code">
                        <span class="fi fi-{{ code.toLowerCase() }} flag" aria-hidden="true"></span
                        >{{ countryName(code) }}
                    </mat-option>
                }
                @for (option of options(); track option.code) {
                    <mat-option [value]="option.code">
                        <span
                            class="fi fi-{{ option.code.toLowerCase() }} flag"
                            aria-hidden="true"
                        ></span
                        >{{ option.name }}
                    </mat-option>
                }
            </mat-select>
            @if (control().errors; as errors) {
                <mat-error [attr.data-testid]="testid() + '-error'">{{
                    errorText(errors)
                }}</mat-error>
            }
        </mat-form-field>
    `,
    styles: [
        `
            :host {
                display: block;
            }

            mat-form-field {
                width: 100%;
            }

            .flag {
                margin-right: 8px;
                border-radius: 2px;
            }
        `,
    ],
})
export class CountrySelectComponent {
    protected readonly i18n = inject(I18nService);

    readonly control = input.required<FormControl<string | null>>();
    readonly label = input.required<string>();
    readonly testid = input.required<string>();
    readonly allowEmpty = input(false);

    private readonly displayNames = computed(
        () => new Intl.DisplayNames([this.i18n.locale()], { type: 'region' }),
    );

    protected readonly options = computed<CountryOption[]>(() => {
        const collator = new Intl.Collator(this.i18n.locale());
        return COUNTRY_CODES.map((code) => ({ code, name: this.countryName(code) })).sort((a, b) =>
            collator.compare(a.name, b.name),
        );
    });

    protected countryName(code: string): string {
        try {
            return this.displayNames().of(code.toUpperCase()) ?? code;
        } catch {
            return code;
        }
    }

    /** A stored value outside the offered list still needs an option, or it could not stay selected. */
    protected storedUnknownCode(): string | null {
        const code = this.control().value?.toUpperCase();
        return code && !COUNTRY_CODES.includes(code) ? code : null;
    }

    protected errorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        return this.i18n.t('common.fieldRequired');
    }
}
