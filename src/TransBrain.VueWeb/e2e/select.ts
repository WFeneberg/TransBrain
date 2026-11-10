import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Vuetify's v-select renders its options virtually: only the ones inside the menu's visible
 * window exist in the DOM, so an option further down - "Tank" sits near the end of the
 * alphabetically ordered vehicle classes - cannot be found until the list is scrolled to it.
 *
 * Scrolls the open listbox until `name` is rendered. With `stopAtEnd` it gives up as soon as the
 * last option is rendered, which is what proving an option is NOT offered needs; without it, it
 * keeps going until the timeout, because the options may still be loading.
 */
async function scrollToOption(page: Page, testId: string, name: string, stopAtEnd: boolean): Promise<Locator> {
    // A just-closed menu of another select can still be in the DOM; Vuetify labels each listbox
    // with its own field's label id, which pins down the right one.
    const inputId = await page.getByTestId(testId).locator('input').first().getAttribute('id');
    const listbox = page.locator(`[role="listbox"][aria-labelledby="${inputId}-label"]`);
    await expect(listbox).toBeVisible();
    const option = listbox.getByRole('option', { name, exact: true });
    const deadline = Date.now() + 10_000;
    while (Date.now() < deadline) {
        if ((await option.count()) > 0) {
            return option;
        }
        const atEnd = await listbox
            .getByRole('option')
            .evaluateAll((options) => options.some((el) => el.getAttribute('aria-posinset') === el.getAttribute('aria-setsize')));
        if (atEnd && stopAtEnd) {
            return option;
        }
        await listbox.hover();
        await page.mouse.wheel(0, atEnd ? -2000 : 200);
        await page.waitForTimeout(100);
    }
    return option;
}

/** Opens the v-select carrying `testId` and picks the option named exactly `name`. */
export async function chooseOption(page: Page, testId: string, name: string): Promise<void> {
    await page.getByTestId(testId).click();
    await (await scrollToOption(page, testId, name, false)).click();
}

/** Opens the v-select carrying `testId`, checks the WHOLE list for `name`, and closes it again. */
export async function expectOptionNotOffered(page: Page, testId: string, name: string): Promise<void> {
    await page.getByTestId(testId).click();
    await expect(await scrollToOption(page, testId, name, true)).toHaveCount(0);
    await page.keyboard.press('Escape');
}
