import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

const CODE_CHARS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

function randomChars(length: number): string {
    return Array.from(
        { length },
        () => CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)],
    ).join('');
}

const FLAGS = ['requiresPalletCheck', 'excludeTollFromInvoice', 'reimburseToll'] as const;

function checkbox(page: Page, field: string) {
    return page.getByTestId(`customer-${field}`).getByRole('checkbox');
}

async function chooseOption(page: Page, testId: string, name: string): Promise<void> {
    await page.getByTestId(testId).click();
    await page.getByRole('option', { name, exact: true }).click();
}

async function openNewCustomerForm(page: Page): Promise<void> {
    await page.goto('/customers');
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
    await page.getByTestId('customer-add').click();
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
}

async function fillRequiredFields(page: Page, name: string): Promise<void> {
    await page.getByTestId('customer-name').fill(name);
    await page.getByTestId('customer-street').fill('Hauptstraße 1');
    await page.getByTestId('customer-postalCode').fill('6600');
    await page.getByTestId('customer-city').fill('Reutte');
    await page.getByTestId('customer-country').fill('AT');
}

async function openCustomerFromList(page: Page, name: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
    await page.getByTestId('customer-search').fill(name);
    const row = page.locator('tr').filter({ hasText: name });
    await expect(row).toBeVisible();
    await row.getByTestId('customer-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
    await expect(page.getByTestId('customer-name')).toHaveValue(name);
}

test('adminUser_recordsAdditionalDataWithACustomerClass_andKeepsItAfterReload', async ({
    page,
}) => {
    await signIn(page, 'admin');

    const code = `E2E${randomChars(6)}`;
    const className = `E2E Kundenklasse ${code}`;
    const customerName = `E2E Kunde ${randomChars(6)}`;

    // Create the customer class on the customer master data page.
    await page.goto('/customers');
    await page.getByTestId('customer-catalog-link').click();
    await expect(page.getByRole('heading', { name: 'Customer master data' })).toBeVisible();
    await expect(page.getByTestId('customer-catalog-tab-CustomerClass')).toBeVisible();
    await expect(page.getByTestId('vehicle-catalog-tab-VehicleClass')).toHaveCount(0);
    await page.getByTestId('catalog-add').click();
    await expect(page.getByTestId('catalog-dialog')).toBeVisible();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(className);
    await page.getByTestId('catalog-save').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
    const catalogRow = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(catalogRow.getByTestId('catalog-entry-name')).toHaveText(className);

    // Create a customer with additional data.
    await page.getByTestId('customer-catalog-back').click();
    await openNewCustomerForm(page);
    await fillRequiredFields(page, customerName);
    await expect(page.getByTestId('customer-additional')).toBeVisible();
    await page.getByTestId('customer-secondaryName').fill('Lager Vils');
    await page.getByTestId('customer-secondaryStreet').fill('Industriestraße 5');
    await page.getByTestId('customer-secondaryPostalCode').fill('6682');
    await page.getByTestId('customer-secondaryCity').fill('Vils');
    await page.getByTestId('customer-secondaryCountry').fill('at');
    await page.getByTestId('customer-foundedOn').fill('1985-04-01');
    await page.getByTestId('customer-workerCount').fill('42');
    await page.getByTestId('customer-salariedEmployeeCount').fill('7');
    await chooseOption(page, 'customer-customerClass', className);
    for (const flag of FLAGS) {
        await checkbox(page, flag).check();
    }
    await page.getByTestId('customer-comment').fill('Anlieferung nur vormittags');
    await page.getByTestId('customer-save').click();

    // Every value survived the round trip.
    await openCustomerFromList(page, customerName);
    await expect(page.getByTestId('customer-secondaryName')).toHaveValue('Lager Vils');
    await expect(page.getByTestId('customer-secondaryStreet')).toHaveValue('Industriestraße 5');
    await expect(page.getByTestId('customer-secondaryPostalCode')).toHaveValue('6682');
    await expect(page.getByTestId('customer-secondaryCity')).toHaveValue('Vils');
    await expect(page.getByTestId('customer-secondaryCountry')).toHaveValue('AT');
    await expect(page.getByTestId('customer-foundedOn')).toHaveValue('1985-04-01');
    await expect(page.getByTestId('customer-workerCount')).toHaveValue('42');
    await expect(page.getByTestId('customer-salariedEmployeeCount')).toHaveValue('7');
    await expect(page.getByTestId('customer-customerClass')).toContainText(className);
    for (const flag of FLAGS) {
        await expect(checkbox(page, flag)).toBeChecked();
    }
    await expect(page.getByTestId('customer-comment')).toHaveValue('Anlieferung nur vormittags');

    // Clear the class and one flag.
    await chooseOption(page, 'customer-customerClass', '–');
    await checkbox(page, 'reimburseToll').uncheck();
    await page.getByTestId('customer-save').click();

    await openCustomerFromList(page, customerName);
    await expect(page.getByTestId('customer-customerClass')).not.toContainText(className);
    await expect(checkbox(page, 'reimburseToll')).not.toBeChecked();
    await expect(checkbox(page, 'requiresPalletCheck')).toBeChecked();
    await page.getByTestId('customer-cancel').click();

    // Delete the customer, then the now unused class.
    await page.getByTestId('customer-search').fill(customerName);
    await page.locator('tr').filter({ hasText: customerName }).getByTestId('customer-delete').click();
    await page.getByTestId('customer-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: customerName })).toHaveCount(0);
    await page.getByTestId('customer-catalog-link').click();
    await expect(catalogRow.getByTestId('catalog-entry-usage')).toHaveText('0 customers');
    await catalogRow.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(page.getByTestId('catalog-row').filter({ hasText: code })).toHaveCount(0);
});

test('invalidAdditionalValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewCustomerForm(page);

    await fillRequiredFields(page, `E2E Kunde ${randomChars(6)}`);
    await page.getByTestId('customer-workerCount').fill('-1');
    await page.getByTestId('customer-foundedOn').fill('1700-01-01');
    await page.getByTestId('customer-save').click();

    await expect(page.getByTestId('customer-workerCount-error')).toBeVisible();
    await expect(page.getByTestId('customer-foundedOn-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
});
