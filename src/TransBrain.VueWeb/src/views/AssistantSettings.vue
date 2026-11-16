<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import axios from 'axios';
import { useI18n } from '../i18n';
import {
    getAssistantSettings,
    updateAssistantSettings,
    type AssistantSettings,
} from '../api/assistant';
import { useAssistantStore } from '../stores/assistant';
import { vNativeTestid } from '../directives/nativeTestId';

const { t } = useI18n();
const assistant = useAssistantStore();

const settings = ref<AssistantSettings | null>(null);
const loadError = ref(false);

const enabled = ref(false);
const provider = ref('openrouter');
const model = ref('');
const customBaseUrl = ref('');
const autonomousActionsEnabled = ref(false);
const apiKey = ref('');

const saving = ref(false);
const saveErrors = ref<string[]>([]);
const saved = ref(false);

/** The providers are brands and keep their names in both languages; only "custom" is worded. */
const PROVIDER_LABELS: Record<string, string> = {
    openrouter: 'OpenRouter',
    'openai-api': 'OpenAI',
    anthropic: 'Anthropic',
    gemini: 'Google Gemini',
    deepseek: 'DeepSeek',
    xai: 'xAI',
};

const providerItems = computed(() =>
    (settings.value?.knownProviders ?? []).map((id) => ({
        title: id === 'custom' ? t('maintenance.assistant.providerCustom') : (PROVIDER_LABELS[id] ?? id),
        value: id,
    })),
);

const isCustom = computed(() => provider.value === 'custom');

const notProvisioned = computed(() => settings.value !== null && !settings.value.provisioned);

onMounted(async () => {
    try {
        apply(await getAssistantSettings());
    } catch {
        loadError.value = true;
    }
});

function apply(loaded: AssistantSettings): void {
    settings.value = loaded;
    enabled.value = loaded.enabled;
    provider.value = loaded.provider;
    model.value = loaded.model;
    customBaseUrl.value = loaded.customBaseUrl ?? '';
    autonomousActionsEnabled.value = loaded.autonomousActionsEnabled;
}

async function save(): Promise<void> {
    saving.value = true;
    saveErrors.value = [];
    saved.value = false;
    try {
        const trimmedKey = apiKey.value.trim();
        const trimmedBaseUrl = customBaseUrl.value.trim();
        apply(
            await updateAssistantSettings({
                enabled: enabled.value,
                provider: provider.value,
                model: model.value.trim(),
                customBaseUrl: isCustom.value && trimmedBaseUrl.length > 0 ? trimmedBaseUrl : null,
                autonomousActionsEnabled: autonomousActionsEnabled.value,
                // Empty means: keep the key already stored in the agent's volume.
                apiKey: trimmedKey.length > 0 ? trimmedKey : null,
            }),
        );
        apiKey.value = '';
        saved.value = true;
        // The launcher on every page follows the central switch without a reload.
        await assistant.refreshStatus();
    } catch (error) {
        saveErrors.value = messagesFrom(error);
    } finally {
        saving.value = false;
    }
}

/**
 * Prefers the API's problem details over a generic text: validation answers carry per-field
 * messages, a 503 Assistant.NotProvisioned means the agent volume is not wired up at all.
 */
function messagesFrom(error: unknown): string[] {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as
            | { title?: string; detail?: string; errors?: Record<string, string[]> }
            | undefined;
        if (problem?.title === 'Assistant.NotProvisioned') {
            return [t('maintenance.assistant.notProvisioned')];
        }
        const validationMessages = problem?.errors ? Object.values(problem.errors).flat() : [];
        if (validationMessages.length > 0) {
            return validationMessages;
        }
        if (problem?.detail) {
            return [problem.detail];
        }
    }
    return [t('maintenance.assistant.saveError')];
}
</script>

<!--
    Central configuration of the shared AI assistant - which provider and model answer, whether
    the chat is offered at all, and whether the agent may act on its own. Unlike its neighbor
    tabs this page acts on the installation, not on the chosen tenant: the settings live in the
    agent container's data volume.
-->
<template>
    <v-container class="page-narrow">
        <header class="mb-4">
            <h1 class="text-h4">{{ t('maintenance.assistant.title') }}</h1>
            <p class="text-medium-emphasis">{{ t('maintenance.assistant.subtitle') }}</p>
        </header>

        <v-alert
            v-if="loadError"
            type="error"
            variant="tonal"
            class="mb-4"
            data-testid="assistant-settings-load-error"
        >
            {{ t('maintenance.assistant.loadError') }}
        </v-alert>

        <v-alert
            v-if="notProvisioned"
            type="warning"
            variant="tonal"
            class="mb-4"
            data-testid="assistant-settings-not-provisioned"
        >
            {{ t('maintenance.assistant.notProvisioned') }}
        </v-alert>

        <v-card v-if="settings" variant="outlined">
            <v-card-item prepend-icon="mdi-robot" :title="t('maintenance.assistant.title')" />
            <v-card-text>
                <v-switch
                    v-model="enabled"
                    v-native-testid="'assistant-settings-enabled'"
                    :label="t('maintenance.assistant.enabled')"
                    :hint="t('maintenance.assistant.enabledHint')"
                    persistent-hint
                    color="primary"
                    class="mb-4"
                />

                <v-select
                    v-model="provider"
                    :items="providerItems"
                    :label="t('maintenance.assistant.provider')"
                    variant="outlined"
                    density="comfortable"
                    class="mb-4"
                    hide-details
                    data-testid="assistant-settings-provider"
                />

                <v-text-field
                    v-model="model"
                    v-native-testid="'assistant-settings-model'"
                    :label="t('maintenance.assistant.model')"
                    :hint="t('maintenance.assistant.modelHint')"
                    persistent-hint
                    variant="outlined"
                    density="comfortable"
                    class="mb-4"
                />

                <v-text-field
                    v-if="isCustom"
                    v-model="customBaseUrl"
                    v-native-testid="'assistant-settings-base-url'"
                    :label="t('maintenance.assistant.baseUrl')"
                    :hint="t('maintenance.assistant.baseUrlHint')"
                    persistent-hint
                    variant="outlined"
                    density="comfortable"
                    class="mb-4"
                />

                <v-switch
                    v-model="autonomousActionsEnabled"
                    v-native-testid="'assistant-settings-autonomy'"
                    :label="t('maintenance.assistant.autonomy')"
                    :hint="t('maintenance.assistant.autonomyHint')"
                    persistent-hint
                    color="primary"
                    class="mb-4"
                />

                <v-text-field
                    v-model="apiKey"
                    v-native-testid="'assistant-settings-api-key'"
                    :label="t('maintenance.assistant.apiKey')"
                    :hint="
                        settings.hasApiKey
                            ? t('maintenance.assistant.apiKeyKeepHint')
                            : t('maintenance.assistant.apiKeyHint')
                    "
                    persistent-hint
                    type="password"
                    autocomplete="new-password"
                    variant="outlined"
                    density="comfortable"
                />

                <v-alert
                    v-if="saveErrors.length > 0"
                    type="error"
                    variant="tonal"
                    class="mt-4"
                    data-testid="assistant-settings-error"
                >
                    <p v-for="message in saveErrors" :key="message" class="mb-0">{{ message }}</p>
                </v-alert>
            </v-card-text>
            <v-card-actions>
                <v-btn
                    color="primary"
                    variant="flat"
                    prepend-icon="mdi-content-save"
                    data-testid="assistant-settings-save"
                    :loading="saving"
                    @click="save"
                >
                    {{ t('common.save') }}
                </v-btn>
            </v-card-actions>
        </v-card>

        <v-snackbar v-model="saved" color="success" :timeout="3000" data-testid="assistant-settings-saved">
            {{ t('maintenance.assistant.saved') }}
        </v-snackbar>
    </v-container>
</template>

<style scoped>
.page-narrow {
    max-width: 760px;
}
</style>
