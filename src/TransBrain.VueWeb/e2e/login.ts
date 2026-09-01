import { expect, type Page } from '@playwright/test';

export type TestRole = 'admin' | 'dispo' | 'fahrer' | 'viewer';

/** Realm passwords are the username prefix, see docs/KEYCLOAK.md. */
const PASSWORDS: Record<TestRole, string> = {
    admin: 'admin',
    dispo: 'dispo',
    fahrer: 'fahrer',
    viewer: 'viewer',
};

/**
 * Signs in through the real Keycloak login form and waits for the home page.
 *
 * Every spec in this suite needs this, and each copy of it used to carry its own version of the
 * '#password' workaround below - one of them subtly different. Playwright gives each test a
 * fresh browser context, so there is no session to clear between roles: a test that wants a
 * different role simply calls this with a different one.
 */
export async function signIn(page: Page, role: TestRole): Promise<void> {
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
