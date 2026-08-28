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

    // The database starts fresh on every run, so the list is empty at this point - the
    // heading assertion above already proves that renders regardless of row count. To also
    // prove a row written through the API actually reaches the rendered list (not just that
    // the table exists), seed one vehicle here as admin.user. Reuse the access token the SPA
    // itself just obtained via the real browser login above (admin.user carries the realm's
    // "admin" role, which POST /api/vehicles requires) rather than scripting a second,
    // separate OIDC exchange purely for test setup - that would duplicate the exact flow this
    // test already exercises through the UI, adding fragility without adding proof.
    const accessToken = await page.evaluate(() => {
        const raw = sessionStorage.getItem('0-transbrain-spa');
        return raw ? JSON.parse(raw).authnResult?.access_token : null;
    });
    expect(accessToken).toBeTruthy();

    const licensePlate = `E2E${Date.now().toString(36).toUpperCase()}`;
    const createResponse = await page.request.post('/api/vehicles', {
        headers: { Authorization: `Bearer ${accessToken}` },
        data: {
            licensePlate,
            type: 'Tractor',
            payloadKg: 12000,
            loadMeters: 13.6,
            nextInspectionDue: '2027-06-01',
        },
    });
    expect(createResponse.status()).toBe(201);

    // The component fetches the list once, in its constructor - reload to observe the seeded
    // row. The stored refresh token lets checkAuth() re-establish the session silently, with
    // no second trip through the Keycloak login form.
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.getByTestId('vehicle-plate').filter({ hasText: licensePlate })).toBeVisible();
});
