import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

const UNCHECKED_FLAGS = [
    'hasPalletBasket',
    'hasDangerousGoodsEquipment',
    'hasDoubleDeck',
    'hasDoubleDeckBeams',
    'hasSmallLadder',
    'hasLargeLadder',
];

function uniquePlate(): string {
    const suffix = Math.random().toString(36).slice(2, 6).toUpperCase().padEnd(4, '0');
    return `E2E-EQ${suffix}`;
}

function checkbox(page: Page, field: string) {
    return page.getByTestId(`vehicle-${field}`).getByRole('checkbox');
}

function radio(page: Page, option: string) {
    return page.getByTestId(`vehicle-marbleRackMaterial-${option}`).getByRole('radio');
}

async function openNewVehicleForm(page: Page): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
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
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
}

test('adminUser_recordsVehicleEquipment_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewVehicleForm(page);

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);

    await expect(page.getByTestId('vehicle-equipment')).toBeVisible();
    await expect(radio(page, 'none')).toBeChecked();
    await checkbox(page, 'hasPartitionWall').check();
    await checkbox(page, 'isDangerousGoodsApproved').check();
    await checkbox(page, 'hasLiftAxle').check();
    await page.getByTestId('vehicle-interiorLengthMm').fill('13600');
    await page.getByTestId('vehicle-interiorWidthMm').fill('2480');
    await page.getByTestId('vehicle-interiorHeightMm').fill('2700');
    await page.getByTestId('vehicle-tankVolumeLiters').fill('24000');
    await page.getByTestId('vehicle-lashingStrapCount').fill('20');
    await page.getByTestId('vehicle-palletCount').fill('10');
    await page.getByTestId('vehicle-marbleRackCount').fill('4');
    await radio(page, 'Aluminium').check();
    await page.getByTestId('vehicle-save').click();

    await openVehicleFromList(page, plate);
    await expect(checkbox(page, 'hasPartitionWall')).toBeChecked();
    await expect(checkbox(page, 'isDangerousGoodsApproved')).toBeChecked();
    await expect(checkbox(page, 'hasLiftAxle')).toBeChecked();
    for (const field of UNCHECKED_FLAGS) {
        await expect(checkbox(page, field)).not.toBeChecked();
    }
    await expect(page.getByTestId('vehicle-interiorLengthMm')).toHaveValue('13600');
    await expect(page.getByTestId('vehicle-interiorWidthMm')).toHaveValue('2480');
    await expect(page.getByTestId('vehicle-interiorHeightMm')).toHaveValue('2700');
    await expect(page.getByTestId('vehicle-tankVolumeLiters')).toHaveValue('24000');
    await expect(page.getByTestId('vehicle-lashingStrapCount')).toHaveValue('20');
    await expect(page.getByTestId('vehicle-palletCount')).toHaveValue('10');
    await expect(page.getByTestId('vehicle-marbleRackCount')).toHaveValue('4');
    await expect(page.getByTestId('vehicle-supportPoleCount')).toHaveValue('');
    await expect(radio(page, 'Aluminium')).toBeChecked();

    await checkbox(page, 'hasLiftAxle').uncheck();
    await radio(page, 'Steel').check();
    await page.getByTestId('vehicle-lashingStrapCount').fill('24');
    await page.getByTestId('vehicle-save').click();

    await openVehicleFromList(page, plate);
    await expect(checkbox(page, 'hasLiftAxle')).not.toBeChecked();
    await expect(checkbox(page, 'hasPartitionWall')).toBeChecked();
    await expect(radio(page, 'Steel')).toBeChecked();
    await expect(radio(page, 'Aluminium')).not.toBeChecked();
    await expect(page.getByTestId('vehicle-lashingStrapCount')).toHaveValue('24');

    await page.getByTestId('vehicle-cancel').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await row.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
});

test('invalidEquipmentValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewVehicleForm(page);

    await fillRequiredFields(page, uniquePlate());
    await page.getByTestId('vehicle-tankVolumeLiters').fill('200000');
    await page.getByTestId('vehicle-lashingStrapCount').fill('-1');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByTestId('vehicle-tankVolumeLiters-error')).toBeVisible();
    await expect(page.getByTestId('vehicle-lashingStrapCount-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
});
