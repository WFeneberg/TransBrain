import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

/**
 * The AI assistant: the floating chat on every page and its settings tab in the Wartung area.
 *
 * Unlike the other specs these tests mock the assistant endpoints with page.route: whether the
 * dev stack's Hermes container is provisioned, enabled and holds a working LLM key depends on
 * the machine, and a real model answer is neither fast nor deterministic. The mocks mirror the
 * documented API contract exactly; everything in front of them (auth, routing, the widget, the
 * settings form) runs for real.
 */

const SETTINGS = {
    provisioned: true,
    enabled: false,
    provider: 'openrouter',
    model: '',
    customBaseUrl: null as string | null,
    autonomousActionsEnabled: false,
    hasApiKey: false,
    knownProviders: ['openrouter', 'openai-api', 'anthropic', 'gemini', 'deepseek', 'xai', 'custom'],
};

async function mockStatus(page: Page, enabled: boolean): Promise<void> {
    await page.route('**/api/assistant/status', (route) =>
        route.fulfill({ json: { enabled, ready: enabled } }),
    );
}

test('adminUser_chattingWithAssistant_showsUserAndAssistantBubbles', async ({ page }) => {
    await mockStatus(page, true);
    let chatBody: { messages: { role: string; content: string }[]; pageContext: string | null } | undefined;
    await page.route('**/api/assistant/chat', async (route) => {
        chatBody = route.request().postDataJSON();
        // A real agent answer takes seconds; a short delay keeps the typing indicator visible
        // long enough to assert it.
        await new Promise((resolve) => setTimeout(resolve, 800));
        await route.fulfill({ json: { reply: 'Hello! I can help with tours.\nJust ask.' } });
    });

    await signIn(page, 'admin');
    await page.getByTestId('assistant-fab').click();
    await expect(page.getByTestId('assistant-panel')).toBeVisible();

    // Enter sends; the draft becomes the right-aligned user bubble, the mocked reply the
    // assistant bubble. The newline of the reply must survive (the panel preserves line breaks).
    await page.getByTestId('assistant-input').fill('Which tours are planned today?');
    await page.getByTestId('assistant-input').press('Enter');
    await expect(page.getByTestId('assistant-typing')).toBeVisible();
    await expect(page.getByTestId('assistant-message').first()).toContainText('Which tours are planned today?');
    await expect(page.getByTestId('assistant-message').last()).toContainText('Just ask.');
    await expect(page.getByTestId('assistant-message')).toHaveCount(2);
    expect(chatBody?.messages.at(-1)).toEqual({ role: 'user', content: 'Which tours are planned today?' });
    expect(chatBody?.pageContext).toContain('/');

    // Restart drops the conversation client-side.
    await page.getByTestId('assistant-restart').click();
    await expect(page.getByTestId('assistant-message')).toHaveCount(0);
});

test('adminUser_assistantUnreachable_showsDismissibleHint', async ({ page }) => {
    await mockStatus(page, true);
    await page.route('**/api/assistant/chat', (route) =>
        route.fulfill({
            status: 503,
            contentType: 'application/problem+json',
            body: JSON.stringify({
                title: 'Assistant.Unreachable',
                detail: 'The assistant agent could not be reached.',
                status: 503,
            }),
        }),
    );

    await signIn(page, 'admin');
    await page.getByTestId('assistant-fab').click();
    await page.getByTestId('assistant-input').fill('Hello?');
    await page.getByTestId('assistant-send').click();

    // The ProblemDetails title maps to a friendly message; the failed question stays in the
    // list so the next send retries it as part of the history.
    await expect(page.getByTestId('assistant-error')).toContainText(
        'The AI assistant is currently unavailable. Please try again later.',
    );
    await expect(page.getByTestId('assistant-message')).toHaveCount(1);
    await page.getByTestId('assistant-error-dismiss').click();
    await expect(page.getByTestId('assistant-error')).toBeHidden();
});

test('adminUser_assistantDisabled_hidesTheChatButton', async ({ page }) => {
    // enabled=false means off or not provisioned: no chat UI at all, not even the button.
    await mockStatus(page, false);
    await signIn(page, 'admin');
    await expect(page.getByTestId('assistant-fab')).toBeHidden();
});

test('systemUser_savingAssistantSettings_showsConfirmation', async ({ page }) => {
    let putBody:
        | {
              enabled: boolean;
              provider: string;
              model: string;
              customBaseUrl: string | null;
              autonomousActionsEnabled: boolean;
              apiKey: string | null;
          }
        | undefined;
    await page.route('**/api/system/assistant-settings', async (route) => {
        if (route.request().method() === 'GET') {
            await route.fulfill({ json: SETTINGS });
            return;
        }
        putBody = route.request().postDataJSON();
        await route.fulfill({
            json: {
                ...SETTINGS,
                enabled: putBody!.enabled,
                model: putBody!.model,
                hasApiKey: putBody!.apiKey !== null,
            },
        });
    });

    // The chat itself is a tenant-role feature; the system administrator only configures it,
    // so no floating button appears for this account.
    await signIn(page, 'system');
    await expect(page.getByTestId('assistant-fab')).toBeHidden();

    // The KI-Assistent tab needs NO tenant selection - it configures the one shared agent.
    await page.getByTestId('nav-maintenance').click();
    await page.getByTestId('maintenance-tab-assistant').click();
    await expect(page.getByRole('heading', { name: 'AI assistant' })).toBeVisible();

    await page.getByTestId('assistant-settings-enabled').click();
    await page.getByTestId('assistant-settings-model').fill('openrouter/auto');
    await page.getByTestId('assistant-settings-api-key').fill('sk-test-not-a-real-key');
    await page.getByTestId('assistant-settings-save').click();

    await expect(page.getByText('The AI assistant settings have been saved.')).toBeVisible();
    expect(putBody).toEqual({
        enabled: true,
        provider: 'openrouter',
        model: 'openrouter/auto',
        customBaseUrl: null,
        autonomousActionsEnabled: false,
        apiKey: 'sk-test-not-a-real-key',
    });
});

test('systemUser_assistantNotProvisioned_showsWarningBanner', async ({ page }) => {
    await page.route('**/api/system/assistant-settings', (route) =>
        route.fulfill({ json: { ...SETTINGS, provisioned: false } }),
    );

    await signIn(page, 'system');
    await page.getByTestId('nav-maintenance').click();
    await page.getByTestId('maintenance-tab-assistant').click();
    await expect(page.getByTestId('assistant-settings-not-provisioned')).toBeVisible();
});
