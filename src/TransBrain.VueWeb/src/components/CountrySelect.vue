<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from '../i18n';
import { COUNTRY_CODES } from '../i18n/countries';

const props = defineProps<{
    modelValue: string;
    label: string;
    testid: string;
    clearable?: boolean;
    disabled?: boolean;
    errorMessages?: string[] | string | null;
}>();

const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>();

const { locale } = useI18n();

const displayNames = computed(() => new Intl.DisplayNames([locale.value], { type: 'region' }));

function countryName(code: string): string {
    try {
        return displayNames.value.of(code.toUpperCase()) ?? code;
    } catch {
        return code;
    }
}

// A stored value outside the offered list still needs an option, or it could not stay selected.
const items = computed(() => {
    const collator = new Intl.Collator(locale.value);
    const codes = [...COUNTRY_CODES];
    const stored = props.modelValue?.toUpperCase();
    if (stored && !COUNTRY_CODES.includes(stored)) {
        codes.unshift(stored);
    }
    return codes
        .map((code) => ({ code, name: countryName(code) }))
        .sort((a, b) => collator.compare(a.name, b.name));
});
</script>

<template>
    <v-select
        :model-value="modelValue === '' ? null : modelValue"
        :data-testid="testid"
        :label="label"
        :items="items"
        item-title="name"
        item-value="code"
        :clearable="clearable"
        :disabled="disabled"
        :error-messages="errorMessages ?? []"
        variant="outlined"
        density="comfortable"
        hide-details="auto"
        @update:model-value="emit('update:modelValue', ($event as string | null) ?? '')"
    >
        <template #selection="{ item }">
            <span class="fi flag" :class="'fi-' + item.code.toLowerCase()" aria-hidden="true" />
            {{ item.name }}
        </template>
        <template #item="{ item, props: itemProps }">
            <v-list-item v-bind="itemProps">
                <template #prepend>
                    <span
                        class="fi flag"
                        :class="'fi-' + item.code.toLowerCase()"
                        aria-hidden="true"
                    />
                </template>
            </v-list-item>
        </template>
        <template #message="{ message }">
            <span :data-testid="testid + '-error'">{{ message }}</span>
        </template>
    </v-select>
</template>

<style scoped>
.flag {
    margin-right: 8px;
    border-radius: 2px;
}
</style>
