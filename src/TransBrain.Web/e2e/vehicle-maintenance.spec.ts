import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

const CODE_CHARS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

function randomChars(length: number): string {
    return Array.from(
        { length },
        () => CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)],
    ).join('');
}

// Plates carry a unique index, so each run brings its own - the database is not necessarily
// reset between local runs against one Aspire stack.
function uniquePlate(): string {
    return `E2E-WA${randomChars(4)}`;
}

async function openNewVehicleForm(page: Page): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-payloadKg').fill('18000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2029-03-31');
}

async function openVehicleFromList(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
}

async function deleteVehicleFromList(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await row.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
}

async function chooseOption(page: Page, testId: string, name: string): Promise<void> {
    await page.getByTestId(testId).click();
    await page.getByRole('option', { name, exact: true }).click();
}

test('adminUser_maintainsMaintenanceHistory_onAnExistingVehicle', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewVehicleForm(page);

    // A vehicle that does not exist yet cannot hold maintenance entries.
    await expect(page.getByTestId('vehicle-maintenances-hint')).toBeVisible();
    await expect(page.getByTestId('vehicle-maintenance-add')).toHaveCount(0);

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-save').click();

    await openVehicleFromList(page, plate);
    await expect(page.getByTestId('vehicle-maintenances-empty')).toBeVisible();

    // Record a repair with a far-away next due date.
    await page.getByTestId('vehicle-maintenance-add').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeVisible();
    await chooseOption(page, 'maintenance-kind', 'Repair');
    await page.getByTestId('maintenance-performedOn').fill('2025-05-10');
    await page.getByTestId('maintenance-description').fill('Bremsbeläge vorne erneuert');
    await page.getByTestId('maintenance-workshop').fill('Werkstatt Huber');
    await page.getByTestId('maintenance-odometerKm').fill('500');
    await page.getByTestId('maintenance-cost').fill('350.75');
    await page.getByTestId('maintenance-nextDueOn').fill('2032-06-30');
    await page.getByTestId('maintenance-save').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeHidden();

    const rows = page.getByTestId('vehicle-maintenance-row');
    await expect(rows).toHaveCount(1);
    const row = rows.first();
    await expect(row.getByTestId('vehicle-maintenance-kind')).toHaveText('Repair');
    await expect(row.getByTestId('vehicle-maintenance-description')).toHaveText(
        'Bremsbeläge vorne erneuert',
    );
    await expect(row.getByTestId('vehicle-maintenance-workshop')).toHaveText('Werkstatt Huber');
    await expect(row.getByTestId('vehicle-maintenance-odometerKm')).toContainText('500');
    await expect(row.getByTestId('vehicle-maintenance-cost')).toContainText('350.75');
    await expect(row.getByTestId('vehicle-maintenance-status')).toHaveText('Scheduled');
    await expect(row.getByTestId('vehicle-maintenance-modified')).toBeVisible();
    await expect(page.getByTestId('vehicle-maintenances-empty')).toHaveCount(0);

    // An overdue date flips the chip.
    await row.getByTestId('vehicle-maintenance-edit').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeVisible();
    await expect(page.getByTestId('maintenance-description')).toHaveValue(
        'Bremsbeläge vorne erneuert',
    );
    await page.getByTestId('maintenance-nextDueOn').fill('2025-05-20');
    await page.getByTestId('maintenance-save').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeHidden();
    await expect(row.getByTestId('vehicle-maintenance-status')).toHaveText('Overdue');

    // A next due date before the maintenance itself is rejected in the dialog.
    await page.getByTestId('vehicle-maintenance-add').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeVisible();
    await chooseOption(page, 'maintenance-kind', 'Inspection');
    await page.getByTestId('maintenance-performedOn').fill('2025-05-10');
    await page.getByTestId('maintenance-description').fill('Jahresinspektion');
    await page.getByTestId('maintenance-nextDueOn').fill('2025-05-09');
    await page.getByTestId('maintenance-save').click();
    await expect(page.getByTestId('maintenance-nextDueOn-error')).toBeVisible();
    await page.getByTestId('maintenance-cancel').click();
    await expect(page.getByTestId('maintenance-dialog')).toBeHidden();
    await expect(rows).toHaveCount(1);

    // Delete the entry after confirming.
    await row.getByTestId('vehicle-maintenance-delete').click();
    await page.getByTestId('vehicle-maintenance-delete-confirm').click();
    await expect(page.getByTestId('vehicle-maintenances-empty')).toBeVisible();
    await expect(rows).toHaveCount(0);

    await page.getByTestId('vehicle-cancel').click();
    await deleteVehicleFromList(page, plate);
});
