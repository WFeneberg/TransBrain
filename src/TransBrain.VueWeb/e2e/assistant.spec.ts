import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

/**
 * The AI assistant: central settings in the Wartung area and the floating chat launcher that
 * follows them. The suite runs against the real stack, so both tests restore the configuration
 * they found - the settings live in the agent container's data volume, not in a per-run
 * database. Chat requests are only sent while the assistant is enabled WITHOUT a stored API key
 * (the deterministic 503 "not configured" answer); as soon as a real key or a custom endpoint
 * is configured locally, the live-chat part is skipped rather than spending provider tokens.
 */

/** The English label of the custom provider entry, see maintenance.assistant.providerCustom. */
const CUSTOM_PROVIDER_LABEL = 'Custom OpenAI-compatible endpoint';

interface SettingsSnapshot {
    enabled: boolean;
    providerLabel: string;
    model: string;
    autonomy: boolean;
    baseUrl: string | null;
    hasKey: boolean;
}

async function openAssistantSettings(page: Page): Promise<void> {
    await page.getByTestId('nav-maintenance').click();
    // Deliberately WITHOUT selecting a tenant first: the assistant settings configure the
    // installation's shared agent, so their tab works while the tenant-scoped tabs are disabled.
    await page.getByTestId('maintenance-tab-assistant').click();
    await expect(page.getByRole('heading', { name: 'AI assistant' })).toBeVisible();
}

/** Reads the loaded form, so a test can put everything back the way it found it. */
async function readSettings(page: Page): Promise<SettingsSnapshot> {
    await expect(page.getByTestId('assistant-settings-save')).toBeVisible();
    const enabled = await page.getByTestId('assistant-settings-enabled').isChecked();
    const providerLabel = (
        (await page
            .getByTestId('assistant-settings-provider')
            .locator('.v-select__selection-text')
            .textContent()) ?? ''
    ).trim();
    const model = await page.getByTestId('assistant-settings-model').inputValue();
    const autonomy = await page.getByTestId('assistant-settings-autonomy').isChecked();
    const baseUrlField = page.getByTestId('assistant-settings-base-url');
    const baseUrl = (await baseUrlField.count()) > 0 ? await baseUrlField.inputValue() : null;
    // The response never carries the key itself; the field's hint says whether one is stored.
    const hasKey = await page.getByText('A key is stored – leave empty to keep it.').isVisible();
    return { enabled, providerLabel, model, autonomy, baseUrl, hasKey };
}

async function setSwitch(page: Page, testId: string, value: boolean): Promise<void> {
    const control = page.getByTestId(testId);
    if (value) {
        await control.check();
    } else {
        await control.uncheck();
    }
}

async function restoreSettings(page: Page, snapshot: SettingsSnapshot): Promise<void> {
    await chooseOption(page, 'assistant-settings-provider', snapshot.providerLabel);
    if (snapshot.baseUrl !== null) {
        await page.getByTestId('assistant-settings-base-url').fill(snapshot.baseUrl);
    }
    await page.getByTestId('assistant-settings-model').fill(snapshot.model);
    await setSwitch(page, 'assistant-settings-enabled', snapshot.enabled);
    await setSwitch(page, 'assistant-settings-autonomy', snapshot.autonomy);
    await page.getByTestId('assistant-settings-save').click();
    await expect(page.getByTestId('assistant-settings-saved')).toBeVisible();
}

test('systemAdmin_managesAssistantSettings', async ({ page }) => {
    await signIn(page, 'system');
    await openAssistantSettings(page);

    // The dev stack wires the agent volume through the AppHost, so the warning stays away.
    await expect(page.getByTestId('assistant-settings-not-provisioned')).toHaveCount(0);

    const original = await readSettings(page);

    // The base URL field exists only for the custom provider.
    await expect(page.getByTestId('assistant-settings-base-url')).toHaveCount(
        original.providerLabel === CUSTOM_PROVIDER_LABEL ? 1 : 0,
    );
    await chooseOption(page, 'assistant-settings-provider', CUSTOM_PROVIDER_LABEL);
    await expect(page.getByTestId('assistant-settings-base-url')).toBeVisible();

    // Saving a custom provider without a base URL is answered 400 by the API; the message
    // lands in the error box and nothing is stored (validation runs before any write).
    await setSwitch(page, 'assistant-settings-enabled', true);
    await page.getByTestId('assistant-settings-model').fill('e2e/model-check');
    await page.getByTestId('assistant-settings-base-url').fill('');
    await page.getByTestId('assistant-settings-save').click();
    await expect(page.getByTestId('assistant-settings-error')).toContainText('base URL is required');

    // A relative URL is rejected just the same.
    await page.getByTestId('assistant-settings-base-url').fill('not-a-url');
    await page.getByTestId('assistant-settings-save').click();
    await expect(page.getByTestId('assistant-settings-error')).toContainText('absolute http(s) URL');

    // Back to the state found at the start; only this save is expected to succeed.
    await restoreSettings(page, original);
    await expect(page.getByTestId('assistant-settings-base-url')).toHaveCount(
        original.providerLabel === CUSTOM_PROVIDER_LABEL ? 1 : 0,
    );
});

test('assistantLauncher_followsTheCentralSwitch', async ({ page, browser }) => {
    // The system administrator drives the central switch on `page`; the regular users who are
    // supposed to see (or not see) the launcher each get their own browser context.
    await signIn(page, 'system');
    await openAssistantSettings(page);
    const original = await readSettings(page);

    // Switched off: no launcher for anybody.
    await setSwitch(page, 'assistant-settings-enabled', false);
    await page.getByTestId('assistant-settings-save').click();
    await expect(page.getByTestId('assistant-settings-saved')).toBeVisible();

    const offContext = await browser.newContext();
    const offPage = await offContext.newPage();
    await signIn(offPage, 'admin');
    // The launcher only ever renders after /api/assistant/status answers enabled=true, so the
    // absence is asserted once that request of a fresh page load has completed.
    const offStatus = offPage.waitForResponse((response) => response.url().includes('/api/assistant/status'));
    await offPage.reload();
    await offStatus;
    await expect(offPage.getByTestId('assistant-fab')).toHaveCount(0);
    await offContext.close();

    // Switched on: the launcher appears. A model is required while enabled, so an empty stored
    // model gets a plausible stand-in that the restore at the end clears again.
    await setSwitch(page, 'assistant-settings-enabled', true);
    if (original.model.trim().length === 0) {
        await page.getByTestId('assistant-settings-model').fill('openrouter/auto');
    }
    await page.getByTestId('assistant-settings-save').click();
    await expect(page.getByTestId('assistant-settings-saved')).toBeVisible();

    const onContext = await browser.newContext();
    const onPage = await onContext.newPage();
    await signIn(onPage, 'admin');
    await onPage.getByTestId('assistant-fab').click();
    await expect(onPage.getByTestId('assistant-panel')).toBeVisible();
    await expect(onPage.getByTestId('assistant-panel')).toContainText('AI assistant');
    await expect(onPage.getByTestId('assistant-input')).toBeVisible();
    await expect(onPage.getByTestId('assistant-send')).toBeVisible();
    await expect(onPage.getByTestId('assistant-attach')).toBeVisible();
    await expect(onPage.getByTestId('assistant-restart')).toBeVisible();

    // Chat only when the answer is deterministic: without a stored key (and not on a custom
    // endpoint) the assistant is enabled but not ready, and the API answers 503 NotConfigured
    // instead of spending real provider tokens.
    if (!original.hasKey && original.providerLabel !== CUSTOM_PROVIDER_LABEL) {
        await onPage.getByTestId('assistant-input').fill('Hallo');
        await onPage.getByTestId('assistant-input').press('Enter');
        const message = onPage.getByTestId('assistant-message');
        await expect(message).toHaveCount(1);
        await expect(message).toHaveAttribute('data-role', 'user');
        await expect(message).toContainText('Hallo');
        await expect(onPage.getByTestId('assistant-error')).toContainText('not fully configured');

        // Restart drops the conversation and the hint.
        await onPage.getByTestId('assistant-restart').click();
        await expect(onPage.getByTestId('assistant-message')).toHaveCount(0);
        await expect(onPage.getByTestId('assistant-error')).toHaveCount(0);
    }
    await onContext.close();

    await restoreSettings(page, original);
});
