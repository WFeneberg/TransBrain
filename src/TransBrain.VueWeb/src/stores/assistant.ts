import { defineStore } from 'pinia';
import { ref } from 'vue';
import axios from 'axios';
import {
    getAssistantStatus,
    sendAssistantChat,
    type AssistantChatMessage,
    type AssistantStatus,
} from '../api/assistant';
import { useAuthStore } from './auth';
import type { MessageKey } from '../i18n';

/** The server rejects requests above 50 messages; the client trims the oldest beforehand. */
const MAX_MESSAGES_PER_REQUEST = 50;

/** Server-side caps mirrored here so a request never fails on length alone. */
const MAX_MESSAGE_LENGTH = 8000;
const MAX_PAGE_CONTEXT_LENGTH = 300;

/** ProblemDetails titles the chat endpoint answers 503 with → the friendly hint to show. */
const ERROR_KEYS_BY_TITLE: Record<string, MessageKey> = {
    'Assistant.Disabled': 'assistant.error.disabled',
    'Assistant.NotConfigured': 'assistant.error.notConfigured',
    'Assistant.NotProvisioned': 'assistant.error.notProvisioned',
    'Assistant.Unreachable': 'assistant.error.unreachable',
    'Assistant.AuthFailed': 'assistant.error.authFailed',
    'Assistant.UpstreamError': 'assistant.error.upstream',
};

/**
 * The assistant chat for the whole session: the status that decides whether the launcher shows,
 * and the running conversation. The conversation lives here on purpose - the panel can be closed
 * and reopened on another page without losing the thread; a reload starts fresh.
 */
export const useAssistantStore = defineStore('assistant', () => {
    const status = ref<AssistantStatus | null>(null);
    const panelOpen = ref(false);
    const messages = ref<AssistantChatMessage[]>([]);
    const sending = ref(false);
    const errorKey = ref<MessageKey | null>(null);

    let statusRequest: Promise<void> | null = null;

    /**
     * Queries /api/assistant/status once per session, no matter how many components ask. Only
     * users the Read policy covers may ask at all - a pure system-admin is not among them, so
     * the call is skipped instead of logging a guaranteed 403.
     */
    async function ensureStatus(): Promise<void> {
        const auth = useAuthStore();
        if (!auth.isAuthenticated || !auth.can('read')) {
            return;
        }
        statusRequest ??= getAssistantStatus()
            .then((loaded) => {
                status.value = loaded;
            })
            .catch(() => {
                // An unreachable or refusing API means: offer no chat.
                status.value = { enabled: false, ready: false };
            });
        await statusRequest;
    }

    /** Re-reads the status, e.g. after the central administration switched the assistant. */
    async function refreshStatus(): Promise<void> {
        statusRequest = null;
        status.value = null;
        await ensureStatus();
    }

    function togglePanel(): void {
        panelOpen.value = !panelOpen.value;
    }

    function closePanel(): void {
        panelOpen.value = false;
    }

    function dismissError(): void {
        errorKey.value = null;
    }

    function restart(): void {
        messages.value = [];
        errorKey.value = null;
    }

    /**
     * Appends the user's message and asks the backend. The failed user message stays visible in
     * the transcript so nothing typed is ever silently lost; the next attempt simply resends it
     * as part of the full conversation.
     */
    async function send(content: string, pageContext: string | null): Promise<void> {
        const text = content.trim().slice(0, MAX_MESSAGE_LENGTH);
        if (text.length === 0 || sending.value) {
            return;
        }

        messages.value.push({ role: 'user', content: text });
        errorKey.value = null;
        sending.value = true;
        try {
            const reply = await sendAssistantChat(
                messages.value.slice(-MAX_MESSAGES_PER_REQUEST),
                pageContext ? pageContext.slice(0, MAX_PAGE_CONTEXT_LENGTH) : null,
            );
            messages.value.push({ role: 'assistant', content: reply });
        } catch (error) {
            errorKey.value = errorKeyFrom(error);
        } finally {
            sending.value = false;
        }
    }

    return {
        status,
        panelOpen,
        messages,
        sending,
        errorKey,
        ensureStatus,
        refreshStatus,
        togglePanel,
        closePanel,
        dismissError,
        restart,
        send,
    };
});

/** Maps the RFC 7807 title (the error code) to a friendly hint; timeouts read as unreachable. */
function errorKeyFrom(error: unknown): MessageKey {
    if (axios.isAxiosError(error)) {
        if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
            return 'assistant.error.unreachable';
        }
        const title = (error.response?.data as { title?: string } | undefined)?.title;
        if (title && title in ERROR_KEYS_BY_TITLE) {
            return ERROR_KEYS_BY_TITLE[title];
        }
    }
    return 'assistant.error.generic';
}
