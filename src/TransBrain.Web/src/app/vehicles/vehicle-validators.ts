import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Optional amount: empty is fine, a filled-in value must lie within [min, max] with at most two decimals. */
export function amountInRange(min: number, max: number) {
    return (control: AbstractControl<number | null>): ValidationErrors | null => {
        const value = control.value;
        if (value === null || (value as unknown) === '') {
            return null;
        }
        const number = Number(value);
        const cents = number * 100;
        return Number.isFinite(number) &&
            number >= min &&
            number <= max &&
            Math.abs(cents - Math.round(cents)) < 1e-6
            ? null
            : { amountRange: { min, max } };
    };
}

/** Optional "YYYY-MM-DD" date: empty is fine, a filled-in date must not lie before `year`. */
export function dateFromYear(year: number) {
    return (control: AbstractControl<string | null>): ValidationErrors | null => {
        const value = control.value;
        if (!value) {
            return null;
        }
        return Number(value.slice(0, 4)) >= year ? null : { minYear: { year } };
    };
}
