<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useAssistantStore } from '../stores/assistant';
import { useI18n } from '../i18n';
import { vNativeTestid } from '../directives/nativeTestId';

const auth = useAuthStore();
const assistant = useAssistantStore();
const { t } = useI18n();
const route = useRoute();

const draft = ref('');
const transcript = ref<HTMLElement | null>(null);

// One status query per session, fired as soon as a user the Read policy covers is signed in.
// A pure system-admin is skipped inside the store; the launcher simply never shows for them.
watch(
    () => auth.isAuthenticated && auth.can('read'),
    (eligible) => {
        if (eligible) {
            void assistant.ensureStatus();
        }
    },
    { immediate: true },
);

const visible = computed(
    () => auth.isAuthenticated && auth.can('read') && assistant.status?.enabled === true,
);

const canSend = computed(() => draft.value.trim().length > 0 && !assistant.sending);

// Where the user currently is, so the answer can fit the screen. Routes here carry no titles,
// but the name is included for the day one gets one.
const pageContext = computed(() =>
    route.name ? `${route.path} (${String(route.name)})` : route.path,
);

async function submit(): Promise<void> {
    if (!canSend.value) {
        return;
    }
    const text = draft.value;
    draft.value = '';
    await assistant.send(text, pageContext.value);
}

// Keeps the newest message (or the typing indicator) in view.
watch(
    () => [assistant.messages.length, assistant.sending],
    async () => {
        await nextTick();
        transcript.value?.scrollTo({ top: transcript.value.scrollHeight });
    },
);
</script>

<!--
    The floating chat: a launcher at the bottom right of every page and the panel it toggles.
    Both exist only while the central administration has the assistant switched on - when it is
    off or not provisioned, the status answers enabled=false and nothing is rendered at all.
-->
<template>
    <template v-if="visible">
        <v-btn
            class="assistant-fab"
            icon
            color="primary"
            size="large"
            elevation="8"
            data-testid="assistant-fab"
            :aria-label="assistant.panelOpen ? t('assistant.close') : t('assistant.open')"
            @click="assistant.togglePanel()"
        >
            <v-icon>{{ assistant.panelOpen ? 'mdi-close' : 'mdi-robot' }}</v-icon>
        </v-btn>

        <v-card v-if="assistant.panelOpen" class="assistant-panel" elevation="12" data-testid="assistant-panel">
            <v-card-title class="assistant-header">
                <v-icon size="20" class="mr-2">mdi-robot</v-icon>
                <span class="text-subtitle-1">{{ t('assistant.title') }}</span>
                <v-spacer />
                <v-btn
                    icon="mdi-restart"
                    variant="text"
                    size="small"
                    data-testid="assistant-restart"
                    :aria-label="t('assistant.restart')"
                    :title="t('assistant.restart')"
                    @click="assistant.restart()"
                />
                <v-btn
                    icon="mdi-close"
                    variant="text"
                    size="small"
                    data-testid="assistant-close"
                    :aria-label="t('assistant.close')"
                    :title="t('assistant.close')"
                    @click="assistant.closePanel()"
                />
            </v-card-title>
            <v-divider />

            <div ref="transcript" class="assistant-transcript">
                <p v-if="assistant.messages.length === 0" class="text-medium-emphasis text-body-2 assistant-empty">
                    {{ t('assistant.empty') }}
                </p>
                <div
                    v-for="(message, index) in assistant.messages"
                    :key="index"
                    class="assistant-bubble"
                    :class="message.role === 'user' ? 'assistant-bubble-user' : 'assistant-bubble-assistant'"
                    data-testid="assistant-message"
                    :data-role="message.role"
                >
                    {{ message.content }}
                </div>
                <div v-if="assistant.sending" class="assistant-bubble assistant-bubble-assistant assistant-typing">
                    <v-progress-circular indeterminate size="14" width="2" class="mr-2" />
                    {{ t('assistant.typing') }}
                </div>
            </div>

            <v-alert
                v-if="assistant.errorKey"
                type="error"
                variant="tonal"
                density="compact"
                closable
                class="assistant-error"
                data-testid="assistant-error"
                @click:close="assistant.dismissError()"
            >
                {{ t(assistant.errorKey) }}
            </v-alert>

            <v-divider />
            <div class="assistant-composer">
                <v-textarea
                    v-model="draft"
                    v-native-testid="'assistant-input'"
                    :label="t('assistant.inputLabel')"
                    variant="outlined"
                    density="compact"
                    rows="1"
                    max-rows="4"
                    auto-grow
                    hide-details
                    maxlength="8000"
                    :disabled="assistant.sending"
                    @keydown.enter.exact.prevent="submit"
                />
                <v-btn
                    icon="mdi-send"
                    color="primary"
                    variant="flat"
                    class="ml-2"
                    data-testid="assistant-send"
                    :aria-label="t('assistant.send')"
                    :title="t('assistant.send')"
                    :loading="assistant.sending"
                    :disabled="!canSend"
                    @click="submit"
                />
            </div>
        </v-card>
    </template>
</template>

<style scoped>
.assistant-fab {
    position: fixed;
    right: 16px;
    bottom: 16px;
    z-index: 2000;
}

.assistant-panel {
    position: fixed;
    right: 16px;
    bottom: 84px;
    width: 380px;
    max-width: calc(100vw - 32px);
    height: 520px;
    max-height: calc(100vh - 116px);
    display: flex;
    flex-direction: column;
    z-index: 2000;
}

.assistant-header {
    display: flex;
    align-items: center;
    flex: 0 0 auto;
}

.assistant-transcript {
    flex: 1 1 auto;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 12px;
}

.assistant-empty {
    margin: auto 8px;
    text-align: center;
}

/* Plain text on purpose: replies are shown exactly as sent, line breaks preserved, no markup. */
.assistant-bubble {
    border-radius: 12px;
    padding: 8px 12px;
    max-width: 85%;
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font-size: 0.875rem;
    line-height: 1.4;
}

.assistant-bubble-user {
    align-self: flex-end;
    background: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-on-primary));
}

.assistant-bubble-assistant {
    align-self: flex-start;
    background: rgba(var(--v-theme-on-surface), 0.08);
    color: rgb(var(--v-theme-on-surface));
}

.assistant-typing {
    display: flex;
    align-items: center;
}

.assistant-error {
    flex: 0 0 auto;
    margin: 0 12px 12px;
}

.assistant-composer {
    flex: 0 0 auto;
    display: flex;
    align-items: flex-end;
    padding: 12px;
}
</style>
