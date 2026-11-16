import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { I18nService, TranslationKey } from '../i18n/i18n.service';

/** Mirror of AssistantStatusResponse in the API. */
export interface AssistantStatus {
    enabled: boolean;
    ready: boolean;
}

export interface AssistantMessage {
    role: 'user' | 'assistant';
    content: string;
}

interface AssistantChatResponse {
    reply: string;
}

/**
 * The server accepts at most 50 messages per request and receives the WHOLE conversation each
 * turn (the backend is stateless), so older turns are trimmed client-side before sending. The
 * page context is capped server-side at 300 characters.
 */
const MAX_MESSAGES_PER_REQUEST = 50;
export const MAX_MESSAGE_LENGTH = 8000;
const MAX_PAGE_CONTEXT_LENGTH = 300;

/** The 503 error codes the API answers chat requests with (ProblemDetails title). */
const ERROR_MESSAGE_KEYS: Record<string, TranslationKey> = {
    'Assistant.Disabled': 'assistant.error.disabled',
    'Assistant.NotConfigured': 'assistant.error.notConfigured',
    'Assistant.Unreachable': 'assistant.error.unavailable',
    'Assistant.AuthFailed': 'assistant.error.authFailed',
    'Assistant.UpstreamError': 'assistant.error.unavailable',
};

/**
 * The one place the SPA talks to the AI assistant. Holds the conversation for the lifetime of
 * the browser session (it is not persisted anywhere), so the chat panel can be closed and
 * reopened on any page without losing the thread. The status is queried once after sign-in;
 * a user whose role may not call the endpoint (403) or a failing call simply means: no chat.
 */
@Injectable({ providedIn: 'root' })
export class AssistantService {
    private readonly http = inject(HttpClient);
    private readonly i18n = inject(I18nService);

    private readonly status = signal<AssistantStatus | null>(null);
    private readonly conversation = signal<AssistantMessage[]>([]);
    private readonly waiting = signal(false);
    private readonly errorKey = signal<TranslationKey | null>(null);
    private statusRequested = false;

    /** Whether the chat UI should exist at all; false until the status has been fetched. */
    readonly enabled = computed(() => this.status()?.enabled ?? false);

    readonly messages = this.conversation.asReadonly();
    readonly sending = this.waiting.asReadonly();

    /** The current, dismissible error - already a user-facing German/English text. */
    readonly error = computed(() => {
        const key = this.errorKey();
        return key === null ? null : this.i18n.t(key);
    });

    /**
     * Queries `/api/assistant/status` once per app load, after sign-in. Any failure - including
     * the 403 a system administrator gets, because chat needs one of the four tenant roles -
     * leaves the widget hidden rather than surfacing an error nobody can act on.
     */
    loadStatus(): void {
        if (this.statusRequested) {
            return;
        }
        this.statusRequested = true;
        this.http.get<AssistantStatus>('/api/assistant/status').subscribe({
            next: (status) => this.status.set(status),
            error: () => this.status.set({ enabled: false, ready: false }),
        });
    }

    /** Appends the user's message and asks the API for the assistant's reply. */
    send(content: string, pageContext: string | null): void {
        const text = content.trim();
        if (text === '' || this.waiting()) {
            return;
        }

        this.errorKey.set(null);
        const history = [
            ...this.conversation(),
            { role: 'user', content: text.slice(0, MAX_MESSAGE_LENGTH) } satisfies AssistantMessage,
        ];
        this.conversation.set(history);
        this.waiting.set(true);

        const payload = {
            messages: history.slice(-MAX_MESSAGES_PER_REQUEST),
            pageContext: pageContext === null ? null : pageContext.slice(0, MAX_PAGE_CONTEXT_LENGTH),
        };
        this.http.post<AssistantChatResponse>('/api/assistant/chat', payload).subscribe({
            next: (response) => {
                this.conversation.update((messages) => [
                    ...messages,
                    { role: 'assistant', content: response.reply },
                ]);
                this.waiting.set(false);
            },
            error: (error: HttpErrorResponse) => {
                // The failed turn stays in the list: the server is stateless, so simply sending
                // the next message retries with the full history, nothing is lost.
                this.errorKey.set(this.messageKeyFor(error));
                this.waiting.set(false);
            },
        });
    }

    restart(): void {
        this.conversation.set([]);
        this.errorKey.set(null);
    }

    dismissError(): void {
        this.errorKey.set(null);
    }

    /** Maps the API's ProblemDetails title (the error code) onto a friendly message. */
    private messageKeyFor(error: HttpErrorResponse): TranslationKey {
        const problem = error.error as { title?: string } | null | undefined;
        const known = problem?.title === undefined ? undefined : ERROR_MESSAGE_KEYS[problem.title];
        if (known) {
            return known;
        }
        return error.status === 503 ? 'assistant.error.unavailable' : 'assistant.error.failed';
    }
}
