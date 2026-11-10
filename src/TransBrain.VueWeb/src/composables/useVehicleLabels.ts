import { en } from '../i18n/en';
import { useI18n, type MessageKey } from '../i18n';

const STATUS_COLORS: Record<string, string> = {
    Available: 'success',
    InWorkshop: 'warning',
    Decommissioned: 'grey',
};

/**
 * Localized labels and colours for the vehicle enums the API sends as plain strings. Vehicle
 * class and carrier group are not enums: their names come from the vehicle catalog.
 */
export function useVehicleLabels() {
    const { t, locale } = useI18n();

    // An enum value this build does not know yet is shown as-is rather than as a missing key.
    function label(prefix: string, value: string): string {
        const key = `${prefix}.${value}`;
        return key in en ? t(key as MessageKey) : value;
    }

    function typeLabel(type: string): string {
        return label('vehicles.type', type);
    }

    function statusLabel(status: string): string {
        return label('vehicles.status', status);
    }

    function statusColor(status: string): string {
        return STATUS_COLORS[status] ?? 'grey';
    }

    function dateTag(): string {
        return locale.value === 'de' ? 'de-DE' : 'en-US';
    }

    /** "YYYY-MM-DD" is a calendar date, so it is built in local time - `new Date(iso)` would be UTC. */
    function formatDate(iso: string): string {
        const [year, month, day] = iso.split('-').map(Number);
        return new Date(year, month - 1, day).toLocaleDateString(dateTag());
    }

    function formatDateTime(iso: string): string {
        return new Date(iso).toLocaleString(dateTag(), { dateStyle: 'short', timeStyle: 'short' });
    }

    return { typeLabel, statusLabel, statusColor, formatDate, formatDateTime };
}
