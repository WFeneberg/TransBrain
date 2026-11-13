import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

async function openNewDriverForm(page: Page): Promise<void> {
    await page.goto('/drivers');
    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();
    await page.getByTestId('driver-add').click();
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
}

async function fillRequiredFields(page: Page, lastName: string): Promise<void> {
    await page.getByTestId('driver-firstName').fill('Sophie');
    await page.getByTestId('driver-lastName').fill(lastName);
    await page.getByTestId('driver-licenseClass-CE').click();
    await page.getByTestId('driver-licenseValidUntil').fill('2029-03-31');
}

async function openDriverFromList(page: Page, lastName: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: lastName });
    await expect(row).toBeVisible();
    await row.getByTestId('driver-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit driver' })).toBeVisible();
    await expect(page.getByTestId('driver-lastName')).toHaveValue(lastName);
}

test('adminUser_recordsAdditionalDriverData_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');

    const lastName = `E2EZUSATZ${Date.now().toString(36).toUpperCase()}`;

    // Create a driver with licence documents, clothing sizes and one row in every list.
    await openNewDriverForm(page);
    await fillRequiredFields(page, lastName);
    await page.getByTestId('driver-licenseNumber').fill('B072RRE2I55');
    await page.getByTestId('driver-issueDate').fill('2008-03-12');
    await page.getByTestId('driver-issuingAuthority').fill('BH Reutte');
    await page.getByTestId('driver-clothingSize').fill('52');
    await page.getByTestId('driver-shoeSize').fill('44');
    await page.getByTestId('driver-gloveSize').fill('10');

    await page.getByTestId('driver-relative-add').click();
    await page.getByTestId('driver-relative-lastName').fill('Huber');
    await page.getByTestId('driver-relative-firstName').fill('Anna');
    await page.getByTestId('driver-relative-relationship').fill('Ehefrau');
    await page.getByTestId('driver-relative-dateOfBirth').fill('1982-07-04');
    await page.getByTestId('driver-relative-phone').fill('+43 664 1234567');

    await page.getByTestId('driver-specialDocument-add').click();
    await page.getByTestId('driver-specialDocument-type').click();
    await page.getByRole('option', { name: 'TÜV - Begutachtung', exact: true }).click();
    await page.getByTestId('driver-specialDocument-lastInspectionDate').fill('2024-05-02');
    await page.getByTestId('driver-specialDocument-validUntil').fill('2029-05-02');
    await page.getByTestId('driver-specialDocument-comment').fill('Basiskurs + Tank');

    await page.getByTestId('driver-phoneNumber-add').click();
    const firstPhone = page.getByTestId('driver-phoneNumber-row').first();
    await firstPhone.getByTestId('driver-phoneNumber-type').click();
    await page.getByRole('option', { name: 'Privat', exact: true }).click();
    await firstPhone.getByTestId('driver-phoneNumber-number').fill('+43 5672 11111');
    await page.getByTestId('driver-phoneNumber-add').click();
    const secondPhone = page.getByTestId('driver-phoneNumber-row').nth(1);
    await secondPhone.getByTestId('driver-phoneNumber-type').click();
    await page.getByRole('option', { name: 'Auto-Telefon', exact: true }).click();
    await secondPhone.getByTestId('driver-phoneNumber-number').fill('+43 664 9876543');
    await secondPhone.getByTestId('driver-phoneNumber-comment').fill('dienstlich');

    await page.getByTestId('driver-save').click();

    // Every value survived the round trip, rows in their original order.
    await openDriverFromList(page, lastName);
    await expect(page.getByTestId('driver-licenseNumber')).toHaveValue('B072RRE2I55');
    await expect(page.getByTestId('driver-issueDate')).toHaveValue('2008-03-12');
    await expect(page.getByTestId('driver-issuingAuthority')).toHaveValue('BH Reutte');
    await expect(page.getByTestId('driver-clothingSize')).toHaveValue('52');
    await expect(page.getByTestId('driver-shoeSize')).toHaveValue('44');
    await expect(page.getByTestId('driver-gloveSize')).toHaveValue('10');
    await expect(page.getByTestId('driver-relative-row')).toHaveCount(1);
    await expect(page.getByTestId('driver-relative-lastName')).toHaveValue('Huber');
    await expect(page.getByTestId('driver-relative-dateOfBirth')).toHaveValue('1982-07-04');
    await expect(page.getByTestId('driver-specialDocument-row')).toHaveCount(1);
    await expect(page.getByTestId('driver-specialDocument-type')).toContainText('TÜV - Begutachtung');
    await expect(page.getByTestId('driver-specialDocument-validUntil')).toHaveValue('2029-05-02');
    await expect(page.getByTestId('driver-phoneNumber-row')).toHaveCount(2);
    await expect(
        page.getByTestId('driver-phoneNumber-row').first().getByTestId('driver-phoneNumber-number'),
    ).toHaveValue('+43 5672 11111');
    await expect(
        page.getByTestId('driver-phoneNumber-row').first().getByTestId('driver-phoneNumber-type'),
    ).toContainText('Privat');
    await expect(
        page.getByTestId('driver-phoneNumber-row').nth(1).getByTestId('driver-phoneNumber-type'),
    ).toContainText('Auto-Telefon');

    // Removing a row deletes it from the stored driver.
    await page
        .getByTestId('driver-phoneNumber-row')
        .nth(1)
        .getByTestId('driver-phoneNumber-remove')
        .click();
    await expect(page.getByTestId('driver-phoneNumber-row')).toHaveCount(1);
    await page.getByTestId('driver-save').click();
    await openDriverFromList(page, lastName);
    await expect(page.getByTestId('driver-phoneNumber-row')).toHaveCount(1);
    await expect(page.getByTestId('driver-phoneNumber-number')).toHaveValue('+43 5672 11111');
    await page.getByTestId('driver-cancel').click();

    // Delete the driver again (cleanup through the UI).
    const row = page.locator('tr').filter({ hasText: lastName });
    await row.getByTestId('driver-delete').click();
    await page.getByTestId('driver-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: lastName })).toHaveCount(0);
});

test('relativeWithoutLastName_showsTheRowError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewDriverForm(page);

    await fillRequiredFields(page, `E2EZUSATZ${Date.now().toString(36).toUpperCase()}`);
    await page.getByTestId('driver-relative-add').click();
    await page.getByTestId('driver-relative-firstName').fill('Anna');
    await page.getByTestId('driver-relative-dateOfBirth').fill('1800-01-01');
    await page.getByTestId('driver-save').click();

    // The invalid submit must not navigate away, and both errors render at their fields.
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
    await expect(page.getByTestId('driver-relative-lastName-error')).toBeVisible();
    await expect(page.getByTestId('driver-relative-dateOfBirth-error')).toHaveText(/1900/);
});
