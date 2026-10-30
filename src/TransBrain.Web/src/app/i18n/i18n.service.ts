import { Injectable, signal } from '@angular/core';
import { de } from './de';
import { en } from './en';

export type Locale = 'de' | 'en';
export type TranslationKey = keyof typeof en;

const STORAGE_KEY = 'transbrain.locale';
const DICTIONARIES: Record<Locale, Record<TranslationKey, string>> = { de, en };

@Injectable({ providedIn: 'root' })
export class I18nService {
    readonly locale = signal<Locale>(this.stored());

    setLocale(locale: Locale): void {
        this.locale.set(locale);
        try {
            localStorage.setItem(STORAGE_KEY, locale);
        } catch {
            // Storage blocked (private window, disabled cookies): the choice lasts until reload.
        }
    }

    t(key: TranslationKey, params?: Record<string, string | number>): string {
        const text = DICTIONARIES[this.locale()][key];
        return params ? text.replace(/\{(\w+)\}/g, (match, name: string) => String(params[name] ?? match)) : text;
    }

    private stored(): Locale {
        try {
            return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'de';
        } catch {
            return 'de';
        }
    }
}
