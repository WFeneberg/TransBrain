import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

const CODE_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

// Plates carry a unique index, so every run brings its own.
function randomSuffix(length: number): string {
    return Array.from({ length }, () => CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)]).join('');
}

function uniquePlate(): string {
    return `E2E-WA${randomSuffix(6)}`;
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor', exact: true }).click();
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
}

async function openForEdit(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
}

async function deleteVehicle(page: Page, plate: string): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
}

test('adminUser_maintainsMaintenanceHistory_onAnExistingVehicle', async ({ page }) => {
    await signIn(page, 'admin');

    // A vehicle that does not exist yet cannot hold maintenance entries.
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-maintenances-hint')).toBeVisible();
    await expect(page.getByTestId('vehicle-maintenance-add')).toHaveCount(0);

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    await openForEdit(page, plate);
    await expect(page.getByTestId('vehicle-maintenances-empty')).toBeVisible();

    // Record a repair with a far-away next due date.
    await page.getByTestId('vehicle-maintenance-add').click();
    const dialog = page.getByTestId('maintenance-dialog');
    await expect(dialog).toBeVisible();
    await chooseOption(page, 'maintenance-kind', 'Repair');
    await page.getByTestId('maintenance-performedOn').fill('2025-05-10');
    await page.getByTestId('maintenance-description').fill('Bremsbeläge vorne erneuert');
    await page.getByTestId('maintenance-workshop').fill('Werkstatt Huber');
    await page.getByTestId('maintenance-odometerKm').fill('500');
    await page.getByTestId('maintenance-cost').fill('350.75');
    await page.getByTestId('maintenance-nextDueOn').fill('2032-06-30');
    await page.getByTestId('maintenance-save').click();
    await expect(dialog).toBeHidden();

    const rows = page.getByTestId('vehicle-maintenance-row');
    await expect(rows).toHaveCount(1);
    const row = rows.first();
    await expect(row.getByTestId('vehicle-maintenance-kind')).toHaveText('Repair');
    await expect(row.getByTestId('vehicle-maintenance-description')).toHaveText('Bremsbeläge vorne erneuert');
    await expect(row.getByTestId('vehicle-maintenance-workshop')).toHaveText('Werkstatt Huber');
    await expect(row.getByTestId('vehicle-maintenance-odometerKm')).toContainText('500');
    await expect(row.getByTestId('vehicle-maintenance-cost')).toContainText('350.75');
    await expect(row.getByTestId('vehicle-maintenance-status')).toHaveText('Scheduled');
    await expect(row.getByTestId('vehicle-maintenance-modified')).toBeVisible();
    await expect(page.getByTestId('vehicle-maintenances-empty')).toHaveCount(0);

    // An overdue date flips the chip.
    await row.getByTestId('vehicle-maintenance-edit').click();
    await expect(dialog).toBeVisible();
    await expect(page.getByTestId('maintenance-description')).toHaveValue('Bremsbeläge vorne erneuert');
    await page.getByTestId('maintenance-nextDueOn').fill('2025-05-20');
    await page.getByTestId('maintenance-save').click();
    await expect(dialog).toBeHidden();
    await expect(row.getByTestId('vehicle-maintenance-status')).toHaveText('Overdue');

    // A next due date before the maintenance itself is rejected in the dialog.
    await page.getByTestId('vehicle-maintenance-add').click();
    await expect(dialog).toBeVisible();
    await chooseOption(page, 'maintenance-kind', 'Inspection');
    await page.getByTestId('maintenance-performedOn').fill('2025-05-10');
    await page.getByTestId('maintenance-description').fill('Jahresinspektion');
    await page.getByTestId('maintenance-nextDueOn').fill('2025-05-09');
    await page.getByTestId('maintenance-save').click();
    await expect(page.getByTestId('maintenance-nextDueOn-error')).toBeVisible();
    await page.getByTestId('maintenance-cancel').click();
    await expect(dialog).toBeHidden();
    await expect(rows).toHaveCount(1);

    // Delete the entry after confirming.
    await row.getByTestId('vehicle-maintenance-delete').click();
    await page.getByTestId('vehicle-maintenance-delete-confirm').click();
    await expect(page.getByTestId('vehicle-maintenances-empty')).toBeVisible();
    await expect(rows).toHaveCount(0);

    await deleteVehicle(page, plate);
});
