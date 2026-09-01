import { expect, test } from '@playwright/test';
import { signIn } from './login';

test('unauthenticatedVisitor_atRoot_seesSignInButtonAndNoNavigation', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
    await expect(page.getByTestId('nav-tours')).toBeHidden();
});

test('adminUser_onHome_seesEveryAreaAndEveryAddAction', async ({ page }) => {
    await signIn(page, 'admin');

    await expect(page.getByTestId('home-role-chip')).toHaveText('admin');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`nav-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}-add`)).toBeVisible();
    }
});

test('disponentUser_onHome_seesEveryAreaButNoMasterDataAddActions', async ({ page }) => {
    await signIn(page, 'dispo');

    await expect(page.getByTestId('home-role-chip')).toHaveText('disponent');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
    }
    // dispatch.write yes, masterData.write no - the distinction this whole layer exists for.
    await expect(page.getByTestId('home-tile-orders-add')).toBeVisible();
    await expect(page.getByTestId('home-tile-tours-add')).toBeVisible();
    await expect(page.getByTestId('home-tile-vehicles-add')).toBeHidden();
    await expect(page.getByTestId('home-tile-drivers-add')).toBeHidden();
});

test('fahrerUser_onHome_seesOnlyToursAndNoAddActions', async ({ page }) => {
    await signIn(page, 'fahrer');

    await expect(page.getByTestId('home-role-chip')).toHaveText('fahrer');
    await expect(page.getByTestId('home-tile-tours')).toBeVisible();
    await expect(page.getByTestId('nav-tours')).toBeVisible();
    for (const area of ['vehicles', 'drivers', 'orders']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeHidden();
        await expect(page.getByTestId(`nav-${area}`)).toBeHidden();
    }
    await expect(page.getByTestId('home-tile-tours-add')).toBeHidden();
});

test('viewerUser_onHome_seesEveryAreaAndNoAddActionAtAll', async ({ page }) => {
    await signIn(page, 'viewer');

    await expect(page.getByTestId('home-role-chip')).toHaveText('viewer');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}-add`)).toBeHidden();
    }
});

test('signedInUser_afterSigningOut_isBackAtTheSignInButton', async ({ page }) => {
    await signIn(page, 'admin');
    await page.getByTestId('logout').click();
    await expect(page.getByTestId('login')).toBeVisible();
});

test('viewerUser_openingTheVehicleForm_isSentBackToHome', async ({ page }) => {
    await signIn(page, 'viewer');

    await page.goto('/vehicles/new');

    // Redirected, not 403'd: a user who typed a URL they cannot use lands on their own home.
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page).toHaveURL(/\/$/);
});

test('fahrerUser_openingTheVehicleList_isLetThrough', async ({ page }) => {
    await signIn(page, 'fahrer');

    await page.goto('/vehicles');

    // Hidden from the navigation is not the same as forbidden: Policies.Read covers a fahrer,
    // so inventing a client-side block here would be a second, disagreeing truth.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
});

test('disponentUser_openingTheOrderForm_isLetThrough', async ({ page }) => {
    await signIn(page, 'dispo');

    await page.goto('/orders/new');

    await expect(page.getByTestId('order-save')).toBeVisible();
});

test('unauthenticatedVisitor_openingTheTourList_isSentBackToHome', async ({ page }) => {
    await page.goto('/tours');

    await expect(page.getByTestId('login')).toBeVisible();
});

// The counts are asserted as "some number", not as an exact value. The database does start empty
// on every AppHost run, but other specs in this suite create rows, and so does anyone who used
// the app before running the tests. Pinning a number here would make this test fail for reasons
// that have nothing to do with what it is about: which blocks each role gets.
const A_NUMBER = /^\d+$/;

test('adminUser_onHome_seesEveryKpiAndTheDispatchWorkList', async ({ page }) => {
    await signIn(page, 'admin');

    await expect(page.getByTestId('home-kpi-vehicles-available')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-kpi-vehicles-workshop')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-kpi-drivers-available')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-kpi-orders-draft')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-kpi-tours-today')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-draft-orders')).toBeVisible();
    await expect(page.getByTestId('home-plan-tour')).toBeVisible();
    // An admin is not bound to a Driver record, so "my tours" means nothing for them.
    await expect(page.getByTestId('home-my-tours')).toBeHidden();
});

test('viewerUser_onHome_seesKpisButNoWorkList', async ({ page }) => {
    await signIn(page, 'viewer');

    await expect(page.getByTestId('home-kpi-orders-draft')).toHaveText(A_NUMBER);
    // dispatch.write is what carries the work list, and a viewer does not have it.
    await expect(page.getByTestId('home-draft-orders')).toBeHidden();
    await expect(page.getByTestId('home-my-tours')).toBeHidden();
});

test('fahrerUser_onHome_seesOnlyTheOwnTourBlock', async ({ page }) => {
    await signIn(page, 'fahrer');

    await expect(page.getByTestId('home-my-tours')).toBeVisible();
    await expect(page.getByTestId('home-kpi-tours-today')).toHaveText(A_NUMBER);
    await expect(page.getByTestId('home-draft-orders')).toBeHidden();
    await expect(page.getByTestId('home-kpi-vehicles-available')).toBeHidden();
    await expect(page.getByTestId('home-kpi-drivers-available')).toBeHidden();
    await expect(page.getByTestId('home-kpi-orders-draft')).toBeHidden();
});
