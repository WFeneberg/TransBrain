import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { Customer, CustomerService } from './customer.service';

/**
 * The API's maximum page size. There are no pagination controls yet, so one request is all a
 * user sees. See DriverListComponent for the same stopgap.
 */
const LIST_PAGE_SIZE = 100;

@Component({
    selector: 'app-customer-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1>{{ i18n.t('nav.customers') }}</h1>
            @if (session.can('masterData.write')) {
                <a mat-raised-button routerLink="/customers/new" data-testid="customer-add">{{ i18n.t('customers.add') }}</a>
            }
            @if (actionError(); as message) {
                <p data-testid="customer-action-error">{{ message }}</p>
            }
            @if (errorMessage(); as message) {
                <p data-testid="customer-list-error">{{ message }}</p>
            } @else {
                <table mat-table [dataSource]="customers()">
                    <ng-container matColumnDef="name">
                        <th mat-header-cell *matHeaderCellDef>{{ i18n.t('common.name') }}</th>
                        <td mat-cell *matCellDef="let c" data-testid="customer-name">{{ c.name }}</td>
                    </ng-container>
                    <ng-container matColumnDef="city">
                        <th mat-header-cell *matHeaderCellDef>{{ i18n.t('common.city') }}</th>
                        <td mat-cell *matCellDef="let c">{{ c.postalCode }} {{ c.city }}</td>
                    </ng-container>
                    <ng-container matColumnDef="contactPerson">
                        <th mat-header-cell *matHeaderCellDef>{{ i18n.t('customers.contactPerson') }}</th>
                        <td mat-cell *matCellDef="let c">{{ c.contactPerson }}</td>
                    </ng-container>
                    <ng-container matColumnDef="email">
                        <th mat-header-cell *matHeaderCellDef>{{ i18n.t('customers.email') }}</th>
                        <td mat-cell *matCellDef="let c">{{ c.email }}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>{{ i18n.t('common.actions') }}</th>
                        <td mat-cell *matCellDef="let c">
                            @if (session.can('masterData.write')) {
                                <a mat-button [routerLink]="['/customers', c.id]" data-testid="customer-edit">{{ i18n.t('common.edit') }}</a>
                                <button mat-button data-testid="customer-delete" (click)="delete(c)">{{ i18n.t('common.delete') }}</button>
                            }
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="columns"></tr>
                    <tr mat-row *matRowDef="let row; columns: columns"></tr>
                </table>
            }
        } @else {
            <p>{{ i18n.t('customers.signIn') }}</p>
        }
    `,
})
export class CustomerListComponent {
    private readonly service = inject(CustomerService);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly columns = ['name', 'city', 'contactPerson', 'email', 'actions'];
    protected readonly customers = signal<Customer[]>([]);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage so a failed delete does not hide the table (see DriverListComponent).
    protected readonly actionError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    protected delete(customer: Customer): void {
        this.actionError.set(null);
        this.service.remove(customer.id).subscribe({
            next: () => this.refresh(),
            error: (error: HttpErrorResponse) =>
                this.actionError.set(this.describe(error, this.i18n.t('customers.error.delete'))),
        });
    }

    private refresh(): void {
        this.service.list(1, LIST_PAGE_SIZE).subscribe({
            next: (page) => this.customers.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.errorMessage.set(this.describe(error, this.i18n.t('customers.error.list'))),
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
