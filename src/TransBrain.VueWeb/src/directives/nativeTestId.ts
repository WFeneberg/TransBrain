import type { Directive } from 'vue';

function apply(root: HTMLElement, testId: string): void {
    const control = root.querySelector<HTMLElement>('input, textarea');
    if (control && control.getAttribute('data-testid') !== testId) {
        control.setAttribute('data-testid', testId);
    }
}

/**
 * Puts a data-testid on the native <input>/<textarea> inside a Vuetify field.
 *
 * Vuetify's filterInputAttrs() routes every `data-*` attribute of v-text-field, v-textarea and
 * v-checkbox to the component's root <div>, never to the control inside it, so a plain
 * data-testid would make Playwright's fill(), check(), toHaveValue() and toBeChecked() resolve
 * to a div and fail. The directive value is not an attribute, so the root stays untagged and
 * the testid stays unique.
 */
export const vNativeTestid: Directive<HTMLElement, string> = {
    mounted: (el, binding) => apply(el, binding.value),
    updated: (el, binding) => apply(el, binding.value),
};
