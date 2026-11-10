import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

// The plate column carries a unique index, so every run needs its own plate (at most 15 chars).
function uniquePlate(): string {
    const suffix = Math.random().toString(36).slice(2, 6).toUpperCase().padEnd(4, '0');
    return `E2E-EQ${suffix}`;
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor' }).click();
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

test('adminUser_recordsVehicleEquipment_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);

    const card = page.getByTestId('vehicle-equipment');
    await expect(card).toBeVisible();
    await expect(card.getByTestId('vehicle-marbleRackMaterial-none')).toBeChecked();
    await card.getByTestId('vehicle-hasPartitionWall').check();
    await card.getByTestId('vehicle-isDangerousGoodsApproved').check();
    await card.getByTestId('vehicle-hasLiftAxle').check();
    await card.getByTestId('vehicle-interiorLengthMm').fill('13600');
    await card.getByTestId('vehicle-interiorWidthMm').fill('2480');
    await card.getByTestId('vehicle-interiorHeightMm').fill('2700');
    await card.getByTestId('vehicle-tankVolumeLiters').fill('24000');
    await card.getByTestId('vehicle-lashingStrapCount').fill('20');
    await card.getByTestId('vehicle-palletCount').fill('10');
    await card.getByTestId('vehicle-marbleRackCount').fill('4');
    await card.getByTestId('vehicle-marbleRackMaterial-Aluminium').check();
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: plate })).toBeVisible();

    await openForEdit(page, plate);
    await expect(page.getByTestId('vehicle-hasPartitionWall')).toBeChecked();
    await expect(page.getByTestId('vehicle-isDangerousGoodsApproved')).toBeChecked();
    await expect(page.getByTestId('vehicle-hasLiftAxle')).toBeChecked();
    for (const unchecked of [
        'vehicle-hasPalletBasket',
        'vehicle-hasDangerousGoodsEquipment',
        'vehicle-hasDoubleDeck',
        'vehicle-hasDoubleDeckBeams',
        'vehicle-hasSmallLadder',
        'vehicle-hasLargeLadder',
    ]) {
        await expect(page.getByTestId(unchecked)).not.toBeChecked();
    }
    await expect(page.getByTestId('vehicle-interiorLengthMm')).toHaveValue('13600');
    await expect(page.getByTestId('vehicle-interiorWidthMm')).toHaveValue('2480');
    await expect(page.getByTestId('vehicle-interiorHeightMm')).toHaveValue('2700');
    await expect(page.getByTestId('vehicle-tankVolumeLiters')).toHaveValue('24000');
    await expect(page.getByTestId('vehicle-lashingStrapCount')).toHaveValue('20');
    await expect(page.getByTestId('vehicle-palletCount')).toHaveValue('10');
    await expect(page.getByTestId('vehicle-marbleRackCount')).toHaveValue('4');
    await expect(page.getByTestId('vehicle-marbleRackMaterial-Aluminium')).toBeChecked();

    await page.getByTestId('vehicle-hasLiftAxle').uncheck();
    await page.getByTestId('vehicle-marbleRackMaterial-Steel').check();
    await page.getByTestId('vehicle-lashingStrapCount').fill('24');
    await page.getByTestId('vehicle-save').click();

    await openForEdit(page, plate);
    await expect(page.getByTestId('vehicle-hasLiftAxle')).not.toBeChecked();
    await expect(page.getByTestId('vehicle-hasPartitionWall')).toBeChecked();
    await expect(page.getByTestId('vehicle-marbleRackMaterial-Steel')).toBeChecked();
    await expect(page.getByTestId('vehicle-marbleRackMaterial-Aluminium')).not.toBeChecked();
    await expect(page.getByTestId('vehicle-lashingStrapCount')).toHaveValue('24');
    await page.getByTestId('vehicle-cancel').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
});

test('invalidEquipmentValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();

    await fillRequiredFields(page, uniquePlate());
    await page.getByTestId('vehicle-tankVolumeLiters').fill('200000');
    await page.getByTestId('vehicle-lashingStrapCount').fill('-1');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByTestId('vehicle-tankVolumeLiters-error')).toBeVisible();
    await expect(page.getByTestId('vehicle-lashingStrapCount-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
});
