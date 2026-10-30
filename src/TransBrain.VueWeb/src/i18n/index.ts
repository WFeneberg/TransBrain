import { ref } from 'vue';
import { de } from './de';
import { en, type MessageKey } from './en';

export type Locale = 'de' | 'en';

const STORAGE_KEY = 'transbrain.locale';
const MESSAGES: Record<Locale, Record<MessageKey, string>> = { de, en };

function storedLocale(): Locale {
    try {
        return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'de';
    } catch {
        return 'de';
    }
}

const locale = ref<Locale>(storedLocale());

export function useI18n() {
    function setLocale(next: Locale): void {
        locale.value = next;
        try {
            localStorage.setItem(STORAGE_KEY, next);
        } catch {
            // Storage blocked: the choice lasts until the page is reloaded.
        }
    }

    function t(key: MessageKey, params?: Record<string, string | number>): string {
        const template = MESSAGES[locale.value][key];
        return params ? template.replace(/\{(\w+)\}/g, (match, name: string) => String(params[name] ?? match)) : template;
    }

    return { locale, setLocale, t };
}

export type { MessageKey };
