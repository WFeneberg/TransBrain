import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { Customer, CustomerService, CustomerWriteRequest } from './customer.service';
import { CustomerLocation, CustomerLocationService } from './customer-location.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MAX_PAYMENT_TERM_DAYS = 365;

@Component({
    selector: 'app-customer-form',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ isEditMode ? i18n.t('customers.edit') : i18n.t('customers.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="customer-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    @for (section of sections; track section.title) {
                        <mat-card appearance="outlined">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">{{
                                    section.icon
                                }}</mat-icon>
                                <mat-card-title>{{ i18n.t(section.title) }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content class="fields">
                                @for (field of section.fields; track field.name) {
                                    <mat-form-field>
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'customer-' + field.name"
                                        />
                                        @if (form.controls[field.name].errors; as errors) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'customer-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            </mat-card-content>
                        </mat-card>
                    }

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">payments</mat-icon>
                            <mat-card-title>{{
                                i18n.t('customers.section.billing')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('customers.paymentTermDays') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="paymentTermDays"
                                    data-testid="customer-paymentTermDays"
                                />
                                @if (form.controls.paymentTermDays.errors; as errors) {
                                    <mat-error data-testid="customer-paymentTermDays-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    @if (isEditMode) {
                        <mat-card appearance="outlined" class="full-width">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">pin_drop</mat-icon>
                                <mat-card-title>{{ i18n.t('customers.locations') }}</mat-card-title>
                                <span class="spacer"></span>
                                <a
                                    mat-stroked-button
                                    [routerLink]="['/customers', customerId, 'locations', 'new']"
                                    data-testid="location-add"
                                >
                                    <mat-icon>add</mat-icon>
                                    {{ i18n.t('customers.location.add') }}
                                </a>
                            </mat-card-header>
                            <mat-card-content>
                                @if (locationError(); as message) {
                                    <p class="error-banner" data-testid="location-action-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                <div class="table-card">
                                    <div class="table-scroll">
                                        <table mat-table [dataSource]="locations()">
                                            <ng-container matColumnDef="name">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('customers.location.name') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let l"
                                                    class="strong"
                                                    data-testid="location-name"
                                                >
                                                    {{ l.name }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="city">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.city') }}
                                                </th>
                                                <td mat-cell *matCellDef="let l">
                                                    {{ l.postalCode }} {{ l.city }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="contactPerson">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('customers.contactPerson') }}
                                                </th>
                                                <td mat-cell *matCellDef="let l">
                                                    {{ l.contactPerson || '–' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="contactPhone">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('customers.location.contactPhone') }}
                                                </th>
                                                <td mat-cell *matCellDef="let l">
                                                    {{ l.phone || '–' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="contactEmail">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('customers.location.contactEmail') }}
                                                </th>
                                                <td mat-cell *matCellDef="let l">
                                                    {{ l.email || '–' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="actions">
                                                <th
                                                    mat-header-cell
                                                    *matHeaderCellDef
                                                    class="actions-cell"
                                                >
                                                    <span class="cdk-visually-hidden">{{
                                                        i18n.t('common.actions')
                                                    }}</span>
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let l"
                                                    class="actions-cell"
                                                >
                                                    <a
                                                        mat-icon-button
                                                        [routerLink]="[
                                                            '/customers',
                                                            customerId,
                                                            'locations',
                                                            l.id,
                                                        ]"
                                                        data-testid="location-edit"
                                                        [matTooltip]="i18n.t('common.edit')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.edit') + ' ' + l.name
                                                        "
                                                    >
                                                        <mat-icon>edit</mat-icon>
                                                    </a>
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        class="delete"
                                                        data-testid="location-delete"
                                                        [matTooltip]="i18n.t('common.delete')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.delete') + ' ' + l.name
                                                        "
                                                        (click)="deleteLocation(l)"
                                                    >
                                                        <mat-icon>delete</mat-icon>
                                                    </button>
                                                </td>
                                            </ng-container>
                                            <tr
                                                mat-header-row
                                                *matHeaderRowDef="locationColumns"
                                            ></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: locationColumns"
                                            ></tr>
                                            <tr class="mat-mdc-row" *matNoDataRow>
                                                <td
                                                    class="empty"
                                                    [attr.colspan]="locationColumns.length"
                                                >
                                                    <mat-icon aria-hidden="true"
                                                        >wrong_location</mat-icon
                                                    >
                                                    <span data-testid="location-empty">{{
                                                        i18n.t('customers.location.empty')
                                                    }}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </div>
                                </div>
                            </mat-card-content>
                        </mat-card>
                    }
                </div>

                <div class="action-bar">
                    <button
                        mat-button
                        type="button"
                        data-testid="customer-cancel"
                        (click)="cancel()"
                    >
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="customer-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: [
        `
            .spacer {
                flex: 1 1 auto;
            }

            mat-card-content .error-banner {
                margin-bottom: 12px;
            }
        `,
    ],
})
export class CustomerFormComponent {
    private readonly service = inject(CustomerService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly locationService = inject(CustomerLocationService);
    private readonly dialog = inject(MatDialog);
    // See DriverFormComponent: a directly opened form must wait for the stored session.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly locationColumns = [
        'name',
        'city',
        'contactPerson',
        'contactPhone',
        'contactEmail',
        'actions',
    ];
    protected readonly locations = signal<CustomerLocation[]>([]);
    protected readonly locationError = signal<string | null>(null);

    protected readonly sections = [
        {
            title: 'customers.section.company',
            icon: 'business',
            fields: [
                { name: 'name', label: 'common.name' },
                { name: 'vatId', label: 'customers.vatId' },
            ],
        },
        {
            title: 'customers.section.address',
            icon: 'place',
            fields: [
                { name: 'street', label: 'common.street' },
                { name: 'postalCode', label: 'common.postalCode' },
                { name: 'city', label: 'common.city' },
                { name: 'country', label: 'common.country' },
            ],
        },
        {
            title: 'customers.section.contact',
            icon: 'contact_phone',
            fields: [
                { name: 'contactPerson', label: 'customers.contactPerson' },
                { name: 'email', label: 'customers.email' },
                { name: 'phone', label: 'customers.phone' },
            ],
        },
    ] as const;

    protected readonly customerId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.customerId !== null;
    protected readonly formError = signal<string | null>(null);

    protected readonly form = this.fb.nonNullable.group({
        name: ['', Validators.required],
        street: ['', Validators.required],
        postalCode: ['', Validators.required],
        city: ['', Validators.required],
        country: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
        contactPerson: [''],
        email: ['', Validators.email],
        phone: [''],
        vatId: [''],
        paymentTermDays: [
            30,
            [Validators.required, Validators.min(0), Validators.max(MAX_PAYMENT_TERM_DAYS)],
        ],
    });

    constructor() {
        if (this.isEditMode) {
            this.session.ready.subscribe((isAuthenticated) => {
                if (isAuthenticated) {
                    this.loadLocations();
                }
            });
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.customerId!)))
                .subscribe({
                    next: (customer: Customer) =>
                        this.form.patchValue({
                            name: customer.name,
                            street: customer.street,
                            postalCode: customer.postalCode,
                            city: customer.city,
                            country: customer.country,
                            contactPerson: customer.contactPerson ?? '',
                            email: customer.email ?? '',
                            phone: customer.phone ?? '',
                            vatId: customer.vatId ?? '',
                            paymentTermDays: customer.paymentTermDays,
                        }),
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            this.describeFailure(error, this.i18n.t('customers.error.load')),
                        ),
                });
        }
    }

    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request: CustomerWriteRequest = this.form.getRawValue();
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                this.isEditMode
                    ? this.service.update(this.customerId!, request)
                    : this.service.create(request),
            ),
        );

        save$.subscribe({
            next: () => this.router.navigateByUrl('/customers'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/customers');
    }

    protected deleteLocation(location: CustomerLocation): void {
        this.locationError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('customers.location.deleteTitle'),
            message: this.i18n.t('customers.location.deleteConfirm', { name: location.name }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'location-delete-confirm',
            cancelTestId: 'location-delete-cancel',
            icon: 'delete',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.locationService.remove(this.customerId!, location.id)),
            )
            .subscribe({
                next: () => this.loadLocations(),
                error: (error: HttpErrorResponse) =>
                    this.locationError.set(
                        this.describeFailure(error, this.i18n.t('customers.location.error.delete')),
                    ),
            });
    }

    private loadLocations(): void {
        this.locationService.list(this.customerId!).subscribe({
            next: (locations) => this.locations.set(locations),
            error: (error: HttpErrorResponse) =>
                this.locationError.set(
                    this.describeFailure(error, this.i18n.t('customers.location.error.list')),
                ),
        });
    }

    // Field keys arrive PascalCase (.NET property names); lowercase the first letter to find the control.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const controlName = field.charAt(0).toLowerCase() + field.slice(1);
                const control = this.form.get(controlName);
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                } else {
                    this.formError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error, this.i18n.t('customers.error.save')));
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
