import { expect, test } from '@playwright/test';
import { signIn } from './login';

test('unauthenticated_visitor_seesSignInButton', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
});

test('adminUser_afterKeycloakLogin_seesVehicleList', async ({ page }) => {
    // Signing in lands on the home page now, so the list is one navigation further on.
    await signIn(page, 'admin');
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // The database starts fresh on every run, so the list is empty at this point - the
    // heading assertion above already proves that renders regardless of row count. To also
    // prove a row written through the API actually reaches the rendered list (not just that
    // the table exists), seed one vehicle here as admin.user, mirroring the Angular walking
    // skeleton's own test for the same reason. Reuse the access token the SPA itself just
    // obtained via the real browser login above (admin.user carries the realm's "admin" role,
    // which POST /api/vehicles requires) rather than scripting a second, separate OIDC
    // exchange purely for test setup - that would duplicate the exact flow this test already
    // exercises through the UI, adding fragility without adding proof.
    //
    // oidc-client-ts stores the signed-in user under `user:<authority>:<client_id>`, further
    // prefixed with "oidc." by WebStorageStateStore's default `prefix` option (undocumented in
    // userManager.ts since it is left at its default), in whichever store userManager.ts
    // configures - here window.sessionStorage, matching the Angular app's storage choice.
    // Verified during execution: reading the unprefixed key returned null even though login
    // had genuinely succeeded.
    const accessToken = await page.evaluate(() => {
        const raw = sessionStorage.getItem('oidc.user:https://localhost:8080/realms/transbrain:transbrain-spa');
        return raw ? JSON.parse(raw).access_token : null;
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

    // The component fetches the list once, on mount - reload to observe the seeded row. There
    // is no refresh token in play here; what re-establishes the session across the reload is
    // simply that the signed-in user oidc-client-ts wrote to sessionStorage survives it, so
    // userManager.getUser() finds it again with no second trip through the Keycloak login form.
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.getByTestId('vehicle-plate').filter({ hasText: licensePlate })).toBeVisible();
});

test('viewerUser_onTheVehicleList_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');
    await page.goto('/vehicles');

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.getByTestId('vehicle-add')).toBeHidden();
    await expect(page.getByTestId('vehicle-edit')).toBeHidden();
    await expect(page.getByTestId('vehicle-delete')).toBeHidden();
});
