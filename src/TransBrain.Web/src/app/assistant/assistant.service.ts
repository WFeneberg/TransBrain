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
    /** Names of the files sent with this message - display only, the bytes are never kept. */
    attachmentNames?: string[];
}

/** Mirror of the optional `attachments` items in the chat request. */
export interface AssistantAttachment {
    fileName: string;
    contentType: string;
    /** Raw file bytes, base64-encoded WITHOUT a `data:...;base64,` prefix. */
    dataBase64: string;
}

/** A queued, not-yet-sent attachment; the size feeds the chip label and the total limit. */
export interface PendingAttachment extends AssistantAttachment {
    sizeBytes: number;
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

/** Attachment limits, mirrored from the server's validation of the chat request. */
export const MAX_ATTACHMENTS_PER_MESSAGE = 5;
export const MAX_ATTACHMENTS_TOTAL_BYTES = 10 * 1024 * 1024;
const MAX_ATTACHMENT_NAME_LENGTH = 150;

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
    private readonly pending = signal<PendingAttachment[]>([]);
    private statusRequested = false;

    /** Whether the chat UI should exist at all; false until the status has been fetched. */
    readonly enabled = computed(() => this.status()?.enabled ?? false);

    readonly messages = this.conversation.asReadonly();
    readonly sending = this.waiting.asReadonly();

    /** Files queued for the NEXT message only - they are not resent with the history. */
    readonly pendingAttachments = this.pending.asReadonly();

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
        const attachments = this.pending();
        const userMessage: AssistantMessage = { role: 'user', content: text.slice(0, MAX_MESSAGE_LENGTH) };
        if (attachments.length > 0) {
            userMessage.attachmentNames = attachments.map((attachment) => attachment.fileName);
        }
        const history = [...this.conversation(), userMessage];
        this.conversation.set(history);
        this.waiting.set(true);

        const payload = {
            // Only role and content go to the API - attachmentNames is client-side display state.
            messages: history
                .slice(-MAX_MESSAGES_PER_REQUEST)
                .map(({ role, content }) => ({ role, content })),
            pageContext: pageContext === null ? null : pageContext.slice(0, MAX_PAGE_CONTEXT_LENGTH),
            // Attachments belong to THIS message only; `undefined` keeps them out of the JSON.
            attachments:
                attachments.length === 0
                    ? undefined
                    : attachments.map(({ fileName, contentType, dataBase64 }) => ({
                          fileName,
                          contentType,
                          dataBase64,
                      })),
        };
        this.http.post<AssistantChatResponse>('/api/assistant/chat', payload).subscribe({
            next: (response) => {
                this.conversation.update((messages) => [
                    ...messages,
                    { role: 'assistant', content: response.reply },
                ]);
                this.pending.set([]);
                this.waiting.set(false);
            },
            error: (error: HttpErrorResponse) => {
                // The failed turn stays in the list: the server is stateless, so simply sending
                // the next message retries with the full history, nothing is lost. The queued
                // attachments also stay, so that retry carries them again.
                this.errorKey.set(this.messageKeyFor(error));
                this.waiting.set(false);
            },
        });
    }

    /**
     * Reads the chosen files and queues them for the next message. A selection that would
     * break the server's limits is rejected as a whole; already queued files stay untouched.
     */
    async attachFiles(files: readonly File[]): Promise<void> {
        if (files.length === 0 || this.waiting()) {
            return;
        }
        const queued = this.pending();
        if (queued.length + files.length > MAX_ATTACHMENTS_PER_MESSAGE) {
            this.errorKey.set('assistant.error.tooManyAttachments');
            return;
        }
        const totalBytes =
            queued.reduce((sum, attachment) => sum + attachment.sizeBytes, 0) +
            files.reduce((sum, file) => sum + file.size, 0);
        if (totalBytes > MAX_ATTACHMENTS_TOTAL_BYTES) {
            this.errorKey.set('assistant.error.attachmentsTooLarge');
            return;
        }
        try {
            const read = await Promise.all(files.map((file) => this.toAttachment(file)));
            this.errorKey.set(null);
            this.pending.update((current) => [...current, ...read]);
        } catch {
            this.errorKey.set('assistant.error.attachmentUnreadable');
        }
    }

    removeAttachment(index: number): void {
        this.pending.update((current) => current.filter((_, i) => i !== index));
    }

    restart(): void {
        this.conversation.set([]);
        this.pending.set([]);
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

    /** Base64-encodes one file; readAsDataURL yields `data:<type>;base64,<data>`. */
    private toAttachment(file: File): Promise<PendingAttachment> {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                const dataUrl = reader.result as string;
                resolve({
                    fileName: file.name.slice(0, MAX_ATTACHMENT_NAME_LENGTH),
                    contentType: file.type === '' ? 'application/octet-stream' : file.type,
                    dataBase64: dataUrl.slice(dataUrl.indexOf(',') + 1),
                    sizeBytes: file.size,
                });
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(file);
        });
    }
}
