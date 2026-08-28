import { expect, test } from '@playwright/test';

test('unauthenticated_visitor_seesSignInButton', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
});

test('adminUser_afterKeycloakLogin_seesVehicleList', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('login').click();
    // Keycloak's default theme also renders a "Show password" toggle button whose
    // aria-label contains the substring "password", so `getByLabel('Password')` matches
    // both it and the real input under Playwright's default case-insensitive substring
    // match and throws a strict-mode violation. Target the two form fields by their
    // stable Keycloak-theme ids instead of by label text.
    await page.locator('#username').fill('admin.user');
    await page.locator('#password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
});
