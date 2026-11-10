import { expect, test, type Locator, type Page } from '@playwright/test';
import { signIn } from './login';

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

function randomSuffix(length: number): string {
    return Array.from({ length }, () => ALPHABET[Math.floor(Math.random() * ALPHABET.length)]).join('');
}

function contactRow(page: Page, index: number): Locator {
    return page.getByTestId('location-contact-row').nth(index);
}

async function fillContact(page: Page, index: number, name: string, phone: string, email: string): Promise<void> {
    const row = contactRow(page, index);
    await row.getByTestId('location-contact-name').fill(name);
    await row.getByTestId('location-contact-phone').fill(phone);
    await row.getByTestId('location-contact-email').fill(email);
}

async function createCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers');
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
    await page.getByTestId('customer-add').click();
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
    await page.getByTestId('customer-name').fill(name);
    await page.getByTestId('customer-street').fill('Hauptstraße 1');
    await page.getByTestId('customer-postalCode').fill('6600');
    await page.getByTestId('customer-city').fill('Reutte');
    await page.getByTestId('customer-country').fill('AT');
    await page.getByTestId('customer-save').click();
    await expect(page.getByRole('heading', { name: 'Customers', exact: true })).toBeVisible();
}

async function openCustomer(page: Page, name: string): Promise<void> {
    await page.getByTestId('customer-search').fill(name);
    const row = page.locator('tr').filter({ hasText: name });
    await expect(row).toBeVisible();
    await row.getByTestId('customer-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
    await expect(page.getByTestId('customer-name')).toHaveValue(name);
}

async function deleteCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers');
    await page.getByTestId('customer-search').fill(name);
    await page.locator('tr').filter({ hasText: name }).getByTestId('customer-delete').click();
    await page.getByTestId('customer-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: name })).toHaveCount(0);
}

test('adminUser_recordsSeveralContactPersonsPerDeliveryLocation_andReplacesThemOnEdit', async ({ page }) => {
    await signIn(page, 'admin');
    const customerName = `E2E Kunde ${randomSuffix(6)}`;
    const locationName = `Lager ${randomSuffix(4)}`;
    await createCustomer(page, customerName);
    await openCustomer(page, customerName);

    // New delivery location with two contact persons.
    await page.getByTestId('location-add').click();
    await expect(page.getByRole('heading', { name: 'New delivery location' })).toBeVisible();
    await expect(page.getByTestId('location-save')).toBeVisible();
    await page.getByTestId('location-name').fill(locationName);
    await page.getByTestId('location-street').fill('Südbahnstraße 5');
    await page.getByTestId('location-postalCode').fill('1100');
    await page.getByTestId('location-city').fill('Wien');
    await page.getByTestId('location-country').fill('AT');
    await expect(page.getByTestId('location-contacts-empty')).toBeVisible();
    await page.getByTestId('location-contact-add').click();
    await page.getByTestId('location-contact-add').click();
    await fillContact(page, 0, 'Hans Huber', '+43 1 111', 'h.huber@muster.at');
    await fillContact(page, 1, 'Eva Berger', '', 'e.berger@muster.at');
    await page.getByTestId('location-save').click();

    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
    const locationRow = page.locator('tr').filter({ hasText: locationName });
    const contactsCell = locationRow.getByTestId('location-contacts');
    await expect(contactsCell).toContainText('Hans Huber');
    await expect(contactsCell).toContainText('+43 1 111');
    await expect(contactsCell).toContainText('e.berger@muster.at');

    // Edit: both contacts come back in order; drop the first and add a third.
    await locationRow.getByTestId('location-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit delivery location' })).toBeVisible();
    await expect(page.getByTestId('location-contact-row')).toHaveCount(2);
    await expect(contactRow(page, 0).getByTestId('location-contact-name')).toHaveValue('Hans Huber');
    await expect(contactRow(page, 0).getByTestId('location-contact-phone')).toHaveValue('+43 1 111');
    await expect(contactRow(page, 1).getByTestId('location-contact-name')).toHaveValue('Eva Berger');
    await expect(contactRow(page, 1).getByTestId('location-contact-email')).toHaveValue('e.berger@muster.at');
    await contactRow(page, 0).getByTestId('location-contact-remove').click();
    await expect(page.getByTestId('location-contact-row')).toHaveCount(1);
    await page.getByTestId('location-contact-add').click();
    await fillContact(page, 1, 'Ida Lang', '+43 1 333', '');
    await page.getByTestId('location-save').click();

    await expect(page.getByRole('heading', { name: 'Edit customer' })).toBeVisible();
    await expect(contactsCell).toContainText('Eva Berger');
    await expect(contactsCell).toContainText('Ida Lang');
    await expect(contactsCell).toContainText('+43 1 333');
    await expect(contactsCell).not.toContainText('Hans Huber');

    await deleteCustomer(page, customerName);
});

test('contactPersonWithoutName_showsTheRowError', async ({ page }) => {
    await signIn(page, 'admin');
    const customerName = `E2E Kunde ${randomSuffix(6)}`;
    await createCustomer(page, customerName);
    await openCustomer(page, customerName);

    await page.getByTestId('location-add').click();
    await expect(page.getByTestId('location-save')).toBeVisible();
    await page.getByTestId('location-name').fill('Lager Fehler');
    await page.getByTestId('location-street').fill('Südbahnstraße 5');
    await page.getByTestId('location-postalCode').fill('1100');
    await page.getByTestId('location-city').fill('Wien');
    await page.getByTestId('location-country').fill('AT');
    await page.getByTestId('location-contact-add').click();
    await fillContact(page, 0, '', '+43 1 111', 'keine-mail');
    await page.getByTestId('location-save').click();

    await expect(contactRow(page, 0).getByTestId('location-contact-name-error')).toBeVisible();
    await expect(contactRow(page, 0).getByTestId('location-contact-email-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New delivery location' })).toBeVisible();

    await deleteCustomer(page, customerName);
});