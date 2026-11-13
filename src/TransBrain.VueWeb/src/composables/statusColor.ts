/**
 * Chip colours for the driver, order, tour and import-session statuses, in the scheme of the
 * vehicle statuses (see useVehicleLabels.ts). The statuses are shown as the API sends them; the
 * e2e specs assert those raw values.
 */
const STATUS_COLORS: Record<string, string> = {
    Available: 'success',
    Absent: 'warning',
    Inactive: 'grey',
    Draft: 'grey',
    Planned: 'info',
    InTransit: 'warning',
    InProgress: 'warning',
    Delivered: 'success',
    Completed: 'success',
    Cancelled: 'error',
    Issued: 'info',
    Paid: 'success',
    Ready: 'info',
    CompletedWithErrors: 'warning',
    Failed: 'error',
};

export const DRIVER_STATUSES = ['Available', 'Absent', 'Inactive'] as const;
export const ORDER_STATUSES = ['Draft', 'Planned', 'InTransit', 'Delivered', 'Cancelled'] as const;
export const TOUR_STATUSES = ['Planned', 'InProgress', 'Completed'] as const;
export const INVOICE_STATUSES = ['Draft', 'Issued', 'Paid', 'Cancelled'] as const;

export function statusColor(status: string): string {
    return STATUS_COLORS[status] ?? 'grey';
}

/** "YYYY-MM-DD" is a calendar date, so it is built in local time - `new Date(iso)` would be UTC. */
export function formatCalendarDate(iso: string, locale: string): string {
    const [year, month, day] = iso.split('-').map(Number);
    return new Date(year, month - 1, day).toLocaleDateString(locale === 'de' ? 'de-DE' : 'en-US');
}
