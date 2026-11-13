import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

const CODE_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

// Catalog codes carry a unique index per kind, so every run brings its own.
function randomSuffix(length: number): string {
    return Array.from({ length }, () => CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)]).join('');
}

const FLAGS = ['requiresPalletCheck', 'excludeTollFromInvoice', 'reimburseToll'] as const;

async function openNewCustomerForm(page: Page): Promise<void> {
    await page.goto('/customers/new');
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
}

async function fillRequiredFields(page: Page, name: string): Promise<void> {
    await page.getByTestId('customer-name').fill(name);
    await page.getByTestId('customer-street').fill('Hauptstraße 1');
    await page.getByTestId('customer-postalCode').fill('6600');
    await page.getByTestId('customer-city').fill('Reutte');
    await chooseOption(page, 'customer-country', 'Austria');
}

async function openForEdit(page: Page, name: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Customers' })).toBeVisible();
    await page.locator('tr').filter({ hasText: name }).getByTestId('customer-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
    await expect(page.getByTestId('customer-name')).toHaveValue(name);
}

async function openCustomerClasses(page: Page): Promise<void> {
    await page.goto('/customers');
    await page.getByTestId('customer-catalog-link').click();
    await expect(page.getByRole('heading', { name: 'Customer master data' })).toBeVisible();
}

test('adminUser_recordsAdditionalDataWithACustomerClass_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');

    const code = `E2E${randomSuffix(6)}`;
    const className = `E2E Kundenklasse ${code}`;
    const customerName = `E2E Kunde ${randomSuffix(6)}`;

    await openCustomerClasses(page);
    await expect(page.getByTestId('customer-catalog-tab-CustomerClass')).toBeVisible();
    await expect(page.getByTestId('vehicle-catalog-tab-VehicleClass')).toHaveCount(0);
    const dialog = page.getByTestId('catalog-dialog');
    await page.getByTestId('catalog-add').click();
    await expect(dialog).toBeVisible();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(className);
    await page.getByTestId('catalog-save').click();
    await expect(dialog).toBeHidden();
    const entryRow = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(entryRow).toBeVisible();

    await openNewCustomerForm(page);
    await fillRequiredFields(page, customerName);
    const additional = page.getByTestId('customer-additional');
    await expect(additional).toBeVisible();
    await additional.getByTestId('customer-secondaryName').fill('Lager Vils');
    await additional.getByTestId('customer-secondaryStreet').fill('Industriestraße 5');
    await additional.getByTestId('customer-secondaryPostalCode').fill('6682');
    await additional.getByTestId('customer-secondaryCity').fill('Vils');
    await chooseOption(page, 'customer-secondaryCountry', 'Austria');
    await additional.getByTestId('customer-foundedOn').fill('1985-04-01');
    await additional.getByTestId('customer-workerCount').fill('42');
    await additional.getByTestId('customer-salariedEmployeeCount').fill('7');
    await chooseOption(page, 'customer-customerClass', className);
    for (const flag of FLAGS) {
        await page.getByTestId(`customer-${flag}`).check();
    }
    await additional.getByTestId('customer-comment').fill('Anlieferung nur vormittags');
    await page.getByTestId('customer-save').click();

    await expect(page.getByRole('heading', { name: 'Customers' })).toBeVisible();
    await openForEdit(page, customerName);
    await expect(page.getByTestId('customer-secondaryName')).toHaveValue('Lager Vils');
    await expect(page.getByTestId('customer-secondaryStreet')).toHaveValue('Industriestraße 5');
    await expect(page.getByTestId('customer-secondaryPostalCode')).toHaveValue('6682');
    await expect(page.getByTestId('customer-secondaryCity')).toHaveValue('Vils');
    await expect(page.getByTestId('customer-secondaryCountry')).toContainText('Austria');
    await expect(page.getByTestId('customer-foundedOn')).toHaveValue('1985-04-01');
    await expect(page.getByTestId('customer-workerCount')).toHaveValue('42');
    await expect(page.getByTestId('customer-salariedEmployeeCount')).toHaveValue('7');
    await expect(page.getByTestId('customer-customerClass')).toContainText(className);
    for (const flag of FLAGS) {
        await expect(page.getByTestId(`customer-${flag}`)).toBeChecked();
    }
    await expect(page.getByTestId('customer-comment')).toHaveValue('Anlieferung nur vormittags');

    await chooseOption(page, 'customer-customerClass', '–');
    await page.getByTestId('customer-reimburseToll').uncheck();
    await page.getByTestId('customer-save').click();

    await openForEdit(page, customerName);
    await expect(page.getByTestId('customer-customerClass')).not.toContainText(className);
    await expect(page.getByTestId('customer-reimburseToll')).not.toBeChecked();
    await expect(page.getByTestId('customer-requiresPalletCheck')).toBeChecked();
    await page.getByTestId('customer-cancel').click();

    await expect(page.getByRole('heading', { name: 'Customers' })).toBeVisible();
    await page.locator('tr').filter({ hasText: customerName }).getByTestId('customer-delete').click();
    await page.getByTestId('customer-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: customerName })).toHaveCount(0);

    await openCustomerClasses(page);
    await expect(entryRow.getByTestId('catalog-entry-usage')).toHaveText('0 customers');
    await entryRow.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(entryRow).toHaveCount(0);
});

test('invalidAdditionalValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewCustomerForm(page);

    await fillRequiredFields(page, `E2E Kunde ${randomSuffix(6)}`);
    await page.getByTestId('customer-workerCount').fill('-1');
    await page.getByTestId('customer-foundedOn').fill('1700-01-01');
    await page.getByTestId('customer-save').click();

    await expect(page.getByTestId('customer-workerCount-error')).toBeVisible();
    await expect(page.getByTestId('customer-foundedOn-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
});
