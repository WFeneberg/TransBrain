import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
    title: string;
    message: string;
    confirmLabel: string;
    cancelLabel: string;
    confirmTestId: string;
    cancelTestId: string;
    icon?: string;
}

/** Closes with `true` only on confirm; cancel, Escape and a backdrop click all close with a falsy result. */
@Component({
    selector: 'app-confirm-dialog',
    standalone: true,
    imports: [MatDialogModule, MatButtonModule, MatIconModule],
    template: `
        <h2 mat-dialog-title class="title">
            <mat-icon class="title-icon" aria-hidden="true">{{ data.icon ?? 'warning' }}</mat-icon>
            <span>{{ data.title }}</span>
        </h2>
        <mat-dialog-content>
            <p class="message">{{ data.message }}</p>
        </mat-dialog-content>
        <mat-dialog-actions align="end">
            <button mat-button [mat-dialog-close]="false" [attr.data-testid]="data.cancelTestId">
                {{ data.cancelLabel }}
            </button>
            <button
                mat-flat-button
                class="confirm"
                [mat-dialog-close]="true"
                cdkFocusInitial
                [attr.data-testid]="data.confirmTestId"
            >
                {{ data.confirmLabel }}
            </button>
        </mat-dialog-actions>
    `,
    styles: [
        `
            .title {
                display: flex;
                align-items: center;
                gap: 12px;
            }

            .title-icon {
                color: var(--mat-sys-error);
            }

            .message {
                margin: 0;
                color: var(--mat-sys-on-surface-variant);
            }

            .confirm {
                --mat-button-filled-container-color: var(--mat-sys-error);
                --mat-button-filled-label-text-color: var(--mat-sys-on-error);
            }
        `,
    ],
})
export class ConfirmDialogComponent {
    protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
