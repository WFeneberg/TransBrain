import { expect, test, type Browser, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

function randomSuffix(length: number): string {
    return Array.from({ length }, () => ALPHABET[Math.floor(Math.random() * ALPHABET.length)]).join('');
}

async function asAdmin(browser: Browser, work: (page: Page) => Promise<void>): Promise<void> {
    const context = await browser.newContext();
    const page = await context.newPage();
    try {
        await signIn(page, 'admin');
        await work(page);
    } finally {
        await context.close();
    }
}

async function createCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers');
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
    await page.getByTestId('customer-add').click();
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
    await page.getByTestId('customer-name').fill(name);
    await page.getByTestId('customer-street').fill('Hauptstraße 1');
    await page.getByTestId('customer-postalCode').fill('86899');
    await page.getByTestId('customer-city').fill('Landsberg');
    await chooseOption(page, 'customer-country', 'Germany');
    await page.getByTestId('customer-save').click();
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
}

async function openCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers');
    await page.getByTestId('customer-search').fill(name);
    const row = page.locator('tr').filter({ hasText: name });
    await expect(row).toBeVisible();
    await row.getByTestId('customer-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
}

async function deleteCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers');
    await page.getByTestId('customer-search').fill(name);
    await page.locator('tr').filter({ hasText: name }).getByTestId('customer-delete').click();
    await page.getByTestId('customer-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: name })).toHaveCount(0);
}

// A delivery location with its own company name and VAT ID becomes selectable as the
// invoice recipient; one without stays invisible to billing.
test('deliveryLocationWithOwnCompanyName_isOfferedAsInvoiceRecipient', async ({ page, browser }) => {
    const tag = randomSuffix(5);
    const customerName = `RechnungsSitz${tag}`;
    const locationName = `Filiale${tag}`;
    const companyName = `Filiale ${tag} GmbH`;

    await asAdmin(browser, async (admin) => {
        await createCustomer(admin, customerName);
        await openCustomer(admin, customerName);

        // Location with its own company name and VAT ID.
        await admin.getByTestId('location-add').click();
        await expect(admin.getByRole('heading', { name: 'New delivery location' })).toBeVisible();
        await admin.getByTestId('location-name').fill(locationName);
        await admin.getByTestId('location-street').fill('Südbahnstraße 5');
        await admin.getByTestId('location-postalCode').fill('86150');
        await admin.getByTestId('location-city').fill('Augsburg');
        await chooseOption(admin, 'location-country', 'Germany');
        await admin.getByTestId('location-companyName').fill(companyName);
        await admin.getByTestId('location-vatId').fill('DE812345670');
        await admin.getByTestId('location-save').click();
        await expect(admin.getByRole('heading', { name: 'Edit customer' })).toBeVisible();

        // Round trip: both fields come back on edit.
        await admin.locator('tr').filter({ hasText: locationName }).getByTestId('location-edit').click();
        await expect(admin.getByTestId('location-companyName')).toHaveValue(companyName);
        await expect(admin.getByTestId('location-vatId')).toHaveValue('DE812345670');
    });

    // Billing offers that location as the invoice recipient.
    await signIn(page, 'dispo');
    const billable = page.waitForResponse((response) =>
        response.url().includes('/api/invoices/billable-orders'),
    );
    await page.goto('/invoices/new');
    await expect(page.getByRole('heading', { name: 'New invoice' })).toBeVisible();
    await billable;
    await chooseOption(page, 'invoice-customerId', customerName);
    await expect(page.getByTestId('invoice-billTo')).toBeVisible();
    await chooseOption(page, 'invoice-billTo', `${companyName} (${locationName})`);
    await expect(page.getByTestId('invoice-billTo')).toContainText(companyName);

    await asAdmin(browser, async (admin) => {
        await deleteCustomer(admin, customerName);
    });
});
