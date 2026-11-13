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

test('adminUser_recordsSocialInsuranceData_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');

    const lastName = `E2ESOZV${Date.now().toString(36).toUpperCase()}`;

    // Create a driver with the full Sozv-Daten card filled in.
    await openNewDriverForm(page);
    await fillRequiredFields(page, lastName);
    await expect(page.getByTestId('driver-socialInsurance')).toBeVisible();
    await page.getByTestId('driver-dateOfBirth').fill('1990-05-17');
    await page.getByTestId('driver-placeOfBirth').fill('Reutte');
    await page.getByTestId('driver-nationality').fill('Österreich');
    await page.getByTestId('driver-socialInsuranceNumber').fill('1234 170590');
    await page.getByTestId('driver-entryDate').fill('2020-01-01');
    await page.getByTestId('driver-healthInsurance').fill('ÖGK Tirol\nMitgliedsnummer 998877');
    await page.getByTestId('driver-bankName').fill('Raiffeisenbank Reutte');
    await page.getByTestId('driver-bankCode').fill('36342');
    await page.getByTestId('driver-accountNumber').fill('234573201');
    // Lowercase on purpose - BIC and IBAN must come back uppercased from the API.
    await page.getByTestId('driver-bic').fill('rzooat2l');
    await page.getByTestId('driver-iban').fill('at611904300234573201');
    await page.getByTestId('driver-familyDoctor').fill('Dr. Anna Huber\nObermarkt 12, 6600 Reutte');
    await page.getByTestId('driver-familyDoctorPhone').fill('+43 5672 12345');
    await page.getByTestId('driver-save').click();

    // Every value survived the round trip, BIC and IBAN uppercased.
    await openDriverFromList(page, lastName);
    await expect(page.getByTestId('driver-dateOfBirth')).toHaveValue('1990-05-17');
    await expect(page.getByTestId('driver-placeOfBirth')).toHaveValue('Reutte');
    await expect(page.getByTestId('driver-nationality')).toHaveValue('Österreich');
    await expect(page.getByTestId('driver-socialInsuranceNumber')).toHaveValue('1234 170590');
    await expect(page.getByTestId('driver-entryDate')).toHaveValue('2020-01-01');
    await expect(page.getByTestId('driver-healthInsurance')).toHaveValue(
        'ÖGK Tirol\nMitgliedsnummer 998877',
    );
    await expect(page.getByTestId('driver-bankName')).toHaveValue('Raiffeisenbank Reutte');
    await expect(page.getByTestId('driver-bankCode')).toHaveValue('36342');
    await expect(page.getByTestId('driver-accountNumber')).toHaveValue('234573201');
    await expect(page.getByTestId('driver-bic')).toHaveValue('RZOOAT2L');
    await expect(page.getByTestId('driver-iban')).toHaveValue('AT611904300234573201');
    await expect(page.getByTestId('driver-familyDoctor')).toHaveValue(
        'Dr. Anna Huber\nObermarkt 12, 6600 Reutte',
    );
    await expect(page.getByTestId('driver-familyDoctorPhone')).toHaveValue('+43 5672 12345');

    // Clearing a field removes the stored value.
    await page.getByTestId('driver-iban').fill('');
    await page.getByTestId('driver-save').click();
    await openDriverFromList(page, lastName);
    await expect(page.getByTestId('driver-iban')).toHaveValue('');
    await expect(page.getByTestId('driver-bic')).toHaveValue('RZOOAT2L');
    await page.getByTestId('driver-cancel').click();

    // Delete the driver again (cleanup through the UI).
    const row = page.locator('tr').filter({ hasText: lastName });
    await row.getByTestId('driver-delete').click();
    await page.getByTestId('driver-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: lastName })).toHaveCount(0);
});

test('tooEarlyDateOfBirth_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewDriverForm(page);

    await fillRequiredFields(page, `E2ESOZV${Date.now().toString(36).toUpperCase()}`);
    await page.getByTestId('driver-dateOfBirth').fill('1800-01-01');
    await page.getByTestId('driver-save').click();

    // The invalid submit must not navigate away, and the error is rendered at the field.
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
    await expect(page.getByTestId('driver-dateOfBirth-error')).toBeVisible();
    await expect(page.getByTestId('driver-dateOfBirth-error')).toHaveText(/1900/);
});
