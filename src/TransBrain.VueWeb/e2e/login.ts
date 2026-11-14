import { expect, type Page } from '@playwright/test';

export type TestRole = 'admin' | 'dispo' | 'fahrer' | 'viewer' | 'system';

/** Realm passwords are the username prefix, see docs/KEYCLOAK.md. */
const PASSWORDS: Record<TestRole, string> = {
    admin: 'admin',
    dispo: 'dispo',
    fahrer: 'fahrer',
    viewer: 'viewer',
    system: 'system',
};

/** Tenant 1 of the dev realm (TenantDefaults.DefaultTenantId); the seed data lives here. */
export const DEFAULT_TENANT_ID = '0199c0de-0000-7000-8000-000000000001';

/**
 * Chooses the tenant the signed-in system administrator works on. The maintenance area shows
 * its tool tabs only after this choice; the manual GUID field is used because it works whether
 * or not the tenant already owns rows (the dropdown only lists tenants with data).
 */
export async function selectSystemTenant(page: Page, tenantId = DEFAULT_TENANT_ID): Promise<void> {
    await page.getByTestId('system-tenant-manual').fill(tenantId);
    await page.getByTestId('system-tenant-apply').click();
    await expect(page.getByTestId('system-tenant-active')).toContainText(tenantId);
}

/**
 * Signs in through the real Keycloak login form and waits for the home page.
 *
 * Every spec in this suite needs this, and each copy of it used to carry its own version of the
 * '#password' workaround below - one of them subtly different. Playwright gives each test a
 * fresh browser context, so there is no session to clear between roles: a test that wants a
 * different role simply calls this with a different one.
 */
export async function signIn(page: Page, role: TestRole): Promise<void> {
    // The specs assert English labels; the app defaults to German, see src/i18n.
    await page.addInitScript(() => window.localStorage.setItem('transbrain.locale', 'en'));
    await page.goto('/');
    await page.getByTestId('login').click();
    // A redirect out to Keycloak is legitimately slower than any assertion against our own
    // pages, and on a cold Vite dev server the very first one in a run has been observed to
    // exceed the 5s default and fail the first authenticated test of the suite. Wait for the
    // form explicitly rather than letting the default timeout decide.
    await page.locator('#username').waitFor({ timeout: 30_000 });
    // Keycloak's default theme also renders a "Show password" toggle button whose aria-label
    // contains the substring "password", so `getByLabel('Password')` matches both it and the
    // real input under Playwright's default case-insensitive substring match and throws a
    // strict-mode violation. Target the two form fields by their stable Keycloak-theme ids.
    await page.locator('#username').fill(`${role}.user`);
    await page.locator('#password').fill(PASSWORDS[role]);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByTestId('home-greeting')).toBeVisible();
}
