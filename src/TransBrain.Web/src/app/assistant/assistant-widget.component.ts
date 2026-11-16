import {
    ChangeDetectionStrategy,
    Component,
    ElementRef,
    effect,
    inject,
    signal,
    viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { AssistantService, MAX_MESSAGE_LENGTH } from './assistant.service';

/**
 * The floating AI chat, mounted once by the App component and therefore available on every
 * page. It renders nothing at all unless the signed-in user may use the assistant AND the
 * administration has enabled it (`/api/assistant/status`); the conversation itself lives in
 * AssistantService, so closing the panel or navigating does not lose it.
 */
@Component({
    selector: 'app-assistant-widget',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatButtonModule, MatIconModule, MatTooltipModule],
    template: `
        @if (session.isAuthenticated() && assistant.enabled()) {
            @if (open()) {
                <section
                    class="panel"
                    data-testid="assistant-panel"
                    role="dialog"
                    [attr.aria-label]="i18n.t('assistant.title')"
                >
                    <header>
                        <mat-icon aria-hidden="true">smart_toy</mat-icon>
                        <h2>{{ i18n.t('assistant.title') }}</h2>
                        <span class="spacer"></span>
                        <button
                            mat-icon-button
                            type="button"
                            data-testid="assistant-restart"
                            [matTooltip]="i18n.t('assistant.restart')"
                            [attr.aria-label]="i18n.t('assistant.restart')"
                            (click)="restart()"
                        >
                            <mat-icon>restart_alt</mat-icon>
                        </button>
                        <button
                            mat-icon-button
                            type="button"
                            data-testid="assistant-close"
                            [matTooltip]="i18n.t('assistant.close')"
                            [attr.aria-label]="i18n.t('assistant.close')"
                            (click)="open.set(false)"
                        >
                            <mat-icon>close</mat-icon>
                        </button>
                    </header>

                    <div class="messages" #messageList>
                        @if (assistant.messages().length === 0 && !assistant.sending()) {
                            <p class="empty">{{ i18n.t('assistant.emptyHint') }}</p>
                        }
                        @for (message of assistant.messages(); track $index) {
                            <div
                                class="message"
                                [class.from-user]="message.role === 'user'"
                                data-testid="assistant-message"
                                [attr.data-role]="message.role"
                            >
                                {{ message.content }}
                            </div>
                        }
                        @if (assistant.sending()) {
                            <div
                                class="message typing"
                                data-testid="assistant-typing"
                                [attr.aria-label]="i18n.t('assistant.typing')"
                            >
                                <span class="dot"></span>
                                <span class="dot"></span>
                                <span class="dot"></span>
                            </div>
                        }
                    </div>

                    @if (assistant.error(); as message) {
                        <div class="error" data-testid="assistant-error">
                            <span>{{ message }}</span>
                            <button
                                mat-icon-button
                                type="button"
                                data-testid="assistant-error-dismiss"
                                [attr.aria-label]="i18n.t('assistant.error.dismiss')"
                                (click)="assistant.dismissError()"
                            >
                                <mat-icon>close</mat-icon>
                            </button>
                        </div>
                    }

                    <footer>
                        <textarea
                            #input
                            rows="2"
                            data-testid="assistant-input"
                            [value]="draft()"
                            [attr.maxlength]="maxMessageLength"
                            [placeholder]="i18n.t('assistant.inputLabel')"
                            [attr.aria-label]="i18n.t('assistant.inputLabel')"
                            (input)="draft.set(input.value)"
                            (keydown.enter)="onEnter($event)"
                        ></textarea>
                        <button
                            mat-icon-button
                            type="button"
                            class="send"
                            data-testid="assistant-send"
                            [matTooltip]="i18n.t('assistant.send')"
                            [attr.aria-label]="i18n.t('assistant.send')"
                            [disabled]="assistant.sending() || draft().trim() === ''"
                            (click)="send()"
                        >
                            <mat-icon>send</mat-icon>
                        </button>
                    </footer>
                </section>
            }
            <button
                mat-fab
                type="button"
                class="fab"
                data-testid="assistant-fab"
                [attr.aria-label]="i18n.t(open() ? 'assistant.close' : 'assistant.open')"
                (click)="open.set(!open())"
            >
                <mat-icon>{{ open() ? 'close' : 'smart_toy' }}</mat-icon>
            </button>
        }
    `,
    styles: `
        .fab {
            position: fixed;
            right: 24px;
            bottom: 24px;
            z-index: 1000;
        }

        .panel {
            position: fixed;
            right: 24px;
            bottom: 96px;
            z-index: 1000;
            display: flex;
            flex-direction: column;
            width: 380px;
            max-width: calc(100vw - 32px);
            height: 520px;
            max-height: calc(100dvh - 128px);
            border: 1px solid var(--mat-sys-outline-variant);
            border-radius: var(--mat-sys-corner-large);
            background: var(--mat-sys-surface);
            box-shadow: var(--mat-sys-level3);
            overflow: hidden;
        }

        header {
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 8px 8px 8px 16px;
            background: var(--mat-sys-surface-container);

            h2 {
                font: var(--mat-sys-title-medium);
                margin: 0;
            }

            .spacer {
                flex: 1 1 auto;
            }
        }

        .messages {
            flex: 1 1 auto;
            overflow-y: auto;
            display: flex;
            flex-direction: column;
            gap: 8px;
            padding: 12px;
        }

        .empty {
            margin: auto 8px;
            text-align: center;
            color: var(--mat-sys-on-surface-variant);
            font: var(--mat-sys-body-medium);
        }

        .message {
            max-width: 85%;
            padding: 8px 12px;
            border-radius: 12px 12px 12px 4px;
            background: var(--mat-sys-surface-container-high);
            color: var(--mat-sys-on-surface);
            font: var(--mat-sys-body-medium);
            /* The assistant answers in plain text; preserve its line breaks without a
               markdown renderer. */
            white-space: pre-wrap;
            overflow-wrap: break-word;
            align-self: flex-start;
        }

        .message.from-user {
            align-self: flex-end;
            border-radius: 12px 12px 4px 12px;
            background: var(--mat-sys-primary-container);
            color: var(--mat-sys-on-primary-container);
        }

        .typing {
            display: flex;
            gap: 4px;
            align-items: center;

            .dot {
                width: 7px;
                height: 7px;
                border-radius: 50%;
                background: var(--mat-sys-on-surface-variant);
                animation: assistant-typing 1.2s infinite ease-in-out;
            }

            .dot:nth-child(2) {
                animation-delay: 0.2s;
            }

            .dot:nth-child(3) {
                animation-delay: 0.4s;
            }
        }

        @keyframes assistant-typing {
            0%,
            60%,
            100% {
                opacity: 0.3;
            }
            30% {
                opacity: 1;
            }
        }

        .error {
            display: flex;
            align-items: center;
            gap: 4px;
            margin: 0 12px 8px;
            padding: 4px 4px 4px 12px;
            border-radius: var(--mat-sys-corner-medium);
            background: var(--mat-sys-error-container);
            color: var(--mat-sys-on-error-container);
            font: var(--mat-sys-body-small);

            span {
                flex: 1 1 auto;
            }
        }

        footer {
            display: flex;
            align-items: flex-end;
            gap: 4px;
            padding: 8px;
            border-top: 1px solid var(--mat-sys-outline-variant);

            textarea {
                flex: 1 1 auto;
                resize: none;
                border: none;
                outline: none;
                background: transparent;
                color: var(--mat-sys-on-surface);
                font: var(--mat-sys-body-medium);
                padding: 8px;
            }
        }

        @media (max-width: 599px) {
            .fab {
                right: 16px;
                bottom: 16px;
            }

            .panel {
                right: 16px;
                bottom: 88px;
                max-height: calc(100dvh - 112px);
            }
        }
    `,
})
export class AssistantWidgetComponent {
    protected readonly i18n = inject(I18nService);
    protected readonly session = inject(SessionService);
    protected readonly assistant = inject(AssistantService);
    private readonly router = inject(Router);

    protected readonly open = signal(false);
    protected readonly draft = signal('');
    protected readonly maxMessageLength = MAX_MESSAGE_LENGTH;

    private readonly messageList = viewChild<ElementRef<HTMLElement>>('messageList');

    constructor() {
        // The one status query of the app: once sign-in has settled. `ready` replays its
        // settled value, so mounting this widget never triggers a second checkAuth().
        this.session.ready.subscribe((authenticated) => {
            if (authenticated) {
                this.assistant.loadStatus();
            }
        });

        // Keep the newest message in view. The effect re-runs when messages, the typing
        // indicator or the panel itself (viewChild) change; the timeout waits for the render.
        effect(() => {
            this.assistant.messages();
            this.assistant.sending();
            const list = this.messageList();
            if (list) {
                setTimeout(() => {
                    list.nativeElement.scrollTop = list.nativeElement.scrollHeight;
                });
            }
        });
    }

    protected onEnter(event: Event): void {
        // Only plain Enter sends - Angular's `keydown.enter` does not fire for Shift+Enter,
        // which therefore keeps its default behavior: a new line.
        event.preventDefault();
        this.send();
    }

    protected send(): void {
        const text = this.draft().trim();
        if (text === '' || this.assistant.sending()) {
            return;
        }
        this.assistant.send(text, this.pageContext());
        this.draft.set('');
    }

    protected restart(): void {
        this.assistant.restart();
        this.draft.set('');
    }

    /** Where the user currently is, so answers can refer to the open page. */
    private pageContext(): string {
        const url = this.router.url;
        const title = document.title;
        return title === '' ? url : `${url} (${title})`;
    }
}
