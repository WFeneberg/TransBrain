import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

// Street names are unique per run - the database is not necessarily reset between local runs.
function uniqueSuffix(): string {
    return Date.now().toString(36).toUpperCase();
}

async function openGeodata(page: Page): Promise<void> {
    await page.getByTestId('nav-geodata').click();
    await expect(page.getByRole('heading', { name: 'Geodata maintenance' })).toBeVisible();
}

test('adminUser_maintainsManualCoordinates', async ({ page }) => {
    await signIn(page, 'admin');
    await openGeodata(page);

    const street = `E2E Weg ${uniqueSuffix()}`;

    // Add manual coordinates for an address the geocoder does not know.
    await page.getByTestId('geodata-add').click();
    await expect(page.getByTestId('geodata-dialog')).toBeVisible();
    await page.getByTestId('geodata-street').fill(street);
    await page.getByTestId('geodata-postal').fill('86150');
    await page.getByTestId('geodata-city').fill('Augsburg');
    await page.getByTestId('geodata-country').fill('DE');
    await page.getByTestId('geodata-latitude').fill('48.37154');
    await page.getByTestId('geodata-longitude').fill('10.89851');
    await page.getByTestId('geodata-save').click();
    await expect(page.getByTestId('geodata-dialog')).toBeHidden();

    // The new entry is listed as a manual, unused override.
    await page.getByTestId('geodata-filter').fill(street);
    const row = page.getByTestId('geodata-row').filter({ hasText: street });
    await expect(row).toHaveCount(1);
    await expect(row.getByTestId('geodata-entry-source')).toHaveText('Manual');
    await expect(row.getByTestId('geodata-entry-coords')).toHaveText('48.37154, 10.89851');
    await expect(row.getByTestId('geodata-entry-usage')).toHaveText('Not in use');
    // A manual row offers no geocoding attempt - the override already wins.
    await expect(row.getByTestId('geodata-resolve')).toHaveCount(0);

    // Correct the coordinates: the address is fixed, only the position changes.
    await row.getByTestId('geodata-edit').click();
    await expect(page.getByTestId('geodata-dialog')).toBeVisible();
    await expect(page.getByTestId('geodata-street')).not.toBeEditable();
    await page.getByTestId('geodata-latitude').fill('48.40001');
    await page.getByTestId('geodata-save').click();
    await expect(page.getByTestId('geodata-dialog')).toBeHidden();
    await expect(row.getByTestId('geodata-entry-coords')).toHaveText('48.40001, 10.89851');

    // Remove the override: the unused address disappears from the list entirely.
    await row.getByTestId('geodata-delete').click();
    await page.getByTestId('geodata-delete-confirm').click();
    await expect(page.getByTestId('geodata-row').filter({ hasText: street })).toHaveCount(0);
    await expect(page.getByTestId('geodata-error')).toBeHidden();
});

test('invalidLatitude_staysInTheDialog', async ({ page }) => {
    await signIn(page, 'admin');
    await openGeodata(page);

    await page.getByTestId('geodata-add').click();
    await page.getByTestId('geodata-street').fill(`E2E Fehler ${uniqueSuffix()}`);
    await page.getByTestId('geodata-postal').fill('86150');
    await page.getByTestId('geodata-city').fill('Augsburg');
    await page.getByTestId('geodata-country').fill('DE');
    await page.getByTestId('geodata-latitude').fill('123');
    await page.getByTestId('geodata-longitude').fill('10.89851');
    await page.getByTestId('geodata-save').click();

    await expect(page.getByTestId('geodata-latitude-error')).toContainText('between -90 and 90');
    await expect(page.getByTestId('geodata-dialog')).toBeVisible();
    await page.getByTestId('geodata-cancel').click();
    await expect(page.getByTestId('geodata-dialog')).toBeHidden();
});

test('viewerUser_onGeodata_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');

    // The maintenance page is not offered to a viewer ...
    await expect(page.getByTestId('nav-geodata')).toHaveCount(0);

    // ... but reading it directly works, matching the API's read policy.
    await page.goto('/geodata');
    await expect(page.getByRole('heading', { name: 'Geodata maintenance' })).toBeVisible();
    await expect(page.getByTestId('geodata-count')).toBeVisible();
    await expect(page.getByTestId('geodata-add')).toHaveCount(0);
    await expect(page.getByTestId('geodata-edit')).toHaveCount(0);
    await expect(page.getByTestId('geodata-delete')).toHaveCount(0);
});
