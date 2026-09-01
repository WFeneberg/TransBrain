import { chromium } from '@playwright/test';

/**
 * Playwright globalSetup: pay the cold-start cost once, before any test runs.
 *
 * Two things are cold at the start of a run and both land entirely on whichever test
 * authenticates first: the dev server compiles the app on demand (Vite and the Angular CLI both
 * do), and Keycloak has to serve its login page for the first time. Measured on this machine,
 * that first sign-in has exceeded 30s right after a source change, while every later one takes
 * under 2s - so the first authenticated test of a run would fail and the same test would pass on
 * a re-run, which is the exact shape of a flake nobody trusts.
 *
 * Warming up here rather than raising the assertion timeouts keeps every real failure fast: an
 * element that genuinely never appears still fails in seconds.
 */
export default async function warmUp(): Promise<void> {
    const baseURL = process.env['PLAYWRIGHT_BASE_URL'] ?? 'http://localhost:4200';
    const browser = await chromium.launch();
    const page = await browser.newPage();

    try {
        await page.goto(baseURL, { timeout: 120_000 });
        await page.getByTestId('login').click({ timeout: 120_000 });
        // The Keycloak login form. Reaching it compiles whatever the app needs to start the OIDC
        // redirect and makes Keycloak render its theme once.
        await page.locator('#username').waitFor({ timeout: 120_000 });
    } finally {
        await browser.close();
    }
}
