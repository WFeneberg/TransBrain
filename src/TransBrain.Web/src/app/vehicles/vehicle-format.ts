import { Locale } from '../i18n/i18n.service';
import { Vehicle } from './vehicle.service';

export type InspectionUrgency = 'overdue' | 'dueSoon' | 'ok';

const DUE_SOON_DAYS = 30;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

function localeTag(locale: Locale): string {
    return locale === 'de' ? 'de-AT' : 'en-GB';
}

/** Parses "YYYY-MM-DD" as a local calendar date - `new Date(iso)` would read it as UTC midnight. */
function parseDate(iso: string): Date | null {
    const [year, month, day] = iso.slice(0, 10).split('-').map(Number);
    return year && month && day ? new Date(year, month - 1, day) : null;
}

export function formatDate(iso: string | null | undefined, locale: Locale): string {
    const date = iso ? parseDate(iso) : null;
    return date
        ? new Intl.DateTimeFormat(localeTag(locale), { dateStyle: 'medium' }).format(date)
        : '–';
}

export function formatDateTime(iso: string | null | undefined, locale: Locale): string {
    const date = iso ? new Date(iso) : null;
    return date && !isNaN(date.getTime())
        ? new Intl.DateTimeFormat(localeTag(locale), {
              dateStyle: 'medium',
              timeStyle: 'short',
          }).format(date)
        : '–';
}

export function inspectionUrgency(
    nextInspectionDue: string,
    today = new Date(),
): InspectionUrgency {
    const due = parseDate(nextInspectionDue);
    if (!due) {
        return 'ok';
    }
    const midnight = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const days = Math.round((due.getTime() - midnight.getTime()) / MS_PER_DAY);
    if (days < 0) {
        return 'overdue';
    }
    return days <= DUE_SOON_DAYS ? 'dueSoon' : 'ok';
}

export function makeAndModel(vehicle: Pick<Vehicle, 'make' | 'model'>): string {
    return [vehicle.make, vehicle.model].filter((part) => !!part).join(' ');
}
