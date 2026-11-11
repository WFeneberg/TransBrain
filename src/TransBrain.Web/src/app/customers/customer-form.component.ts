import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import {
    VehicleCatalogEntry,
    VehicleCatalogOption,
    VehicleCatalogService,
    catalogOptions,
} from '../vehicles/vehicle-catalog.service';
import { dateFromYear, wholeNumberInRange } from '../vehicles/vehicle-validators';
import {
    Customer,
    CustomerAdditionalData,
    CustomerService,
    CustomerWriteRequest,
} from './customer.service';
import { CustomerLocation, CustomerLocationService } from './customer-location.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

const MAX_PAYMENT_TERM_DAYS = 365;

// Mirrors CustomerAdditionalInfo in the domain.
const MAX_HEADCOUNT = 1_000_000;
const MIN_FOUNDING_YEAR = 1800;
const MAX_COMMENT_LENGTH = 2000;

const PASCAL_SEGMENTS = /(^|\.)([A-Z])/g;

const ADDITIONAL_FLAGS = [
    'requiresPalletCheck',
    'excludeTollFromInvoice',
    'reimburseToll',
] as const;

@Component({
    selector: 'app-customer-form',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatSelectModule,
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

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        formGroupName="additional"
                        data-testid="customer-additional"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">post_add</mat-icon>
                            <mat-card-title>{{
                                i18n.t('customers.section.additional')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="additional-groups">
                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('customers.additional.secondaryAddress') }}
                                </h3>
                                <div class="fields">
                                    @for (field of secondaryAddressFields; track field.name) {
                                        <mat-form-field [class.full-width]="field.wide">
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            <input
                                                matInput
                                                [formControlName]="field.name"
                                                [attr.maxlength]="field.maxLength"
                                                [attr.data-testid]="'customer-' + field.name"
                                            />
                                            @if (
                                                additional.controls[field.name].errors;
                                                as errors
                                            ) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'customer-' + field.name + '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                </div>
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('customers.additional.companyFigures') }}
                                </h3>
                                <div class="fields">
                                    <mat-form-field>
                                        <mat-label>{{
                                            i18n.t('customers.additional.foundedOn')
                                        }}</mat-label>
                                        <input
                                            matInput
                                            type="date"
                                            formControlName="foundedOn"
                                            data-testid="customer-foundedOn"
                                        />
                                        @if (additional.controls.foundedOn.errors; as errors) {
                                            <mat-error data-testid="customer-foundedOn-error">{{
                                                fieldErrorText(errors)
                                            }}</mat-error>
                                        }
                                    </mat-form-field>
                                    <mat-form-field>
                                        <mat-label>{{
                                            i18n.t('customers.additional.customerClass')
                                        }}</mat-label>
                                        <mat-select
                                            formControlName="customerClass"
                                            data-testid="customer-customerClass"
                                        >
                                            <mat-option [value]="null">–</mat-option>
                                            @for (
                                                option of customerClassOptions();
                                                track option.code
                                            ) {
                                                <mat-option [value]="option.code">{{
                                                    optionLabel(option)
                                                }}</mat-option>
                                            }
                                        </mat-select>
                                        @if (additional.controls.customerClass.errors; as errors) {
                                            <mat-error data-testid="customer-customerClass-error">{{
                                                fieldErrorText(errors)
                                            }}</mat-error>
                                        }
                                    </mat-form-field>
                                    <mat-form-field>
                                        <mat-label>{{
                                            i18n.t('customers.additional.workerCount')
                                        }}</mat-label>
                                        <input
                                            matInput
                                            type="number"
                                            min="0"
                                            formControlName="workerCount"
                                            data-testid="customer-workerCount"
                                        />
                                        @if (additional.controls.workerCount.errors; as errors) {
                                            <mat-error data-testid="customer-workerCount-error">{{
                                                fieldErrorText(errors)
                                            }}</mat-error>
                                        }
                                    </mat-form-field>
                                    <mat-form-field>
                                        <mat-label>{{
                                            i18n.t('customers.additional.salariedEmployeeCount')
                                        }}</mat-label>
                                        <input
                                            matInput
                                            type="number"
                                            min="0"
                                            formControlName="salariedEmployeeCount"
                                            data-testid="customer-salariedEmployeeCount"
                                        />
                                        @if (
                                            additional.controls.salariedEmployeeCount.errors;
                                            as errors
                                        ) {
                                            <mat-error
                                                data-testid="customer-salariedEmployeeCount-error"
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                </div>
                                <div class="flags">
                                    @for (flag of additionalFlags; track flag) {
                                        <mat-checkbox
                                            [formControlName]="flag"
                                            [attr.data-testid]="'customer-' + flag"
                                        >
                                            {{ i18n.t(flagKey(flag)) }}
                                        </mat-checkbox>
                                    }
                                </div>
                            </section>

                            <mat-form-field class="comment">
                                <mat-label>{{ i18n.t('customers.additional.comment') }}</mat-label>
                                <textarea
                                    matInput
                                    rows="4"
                                    formControlName="comment"
                                    [attr.maxlength]="maxCommentLength"
                                    data-testid="customer-comment"
                                ></textarea>
                                @if (additional.controls.comment.errors; as errors) {
                                    <mat-error data-testid="customer-comment-error">{{
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
                                            <ng-container matColumnDef="contacts">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('customers.location.contacts') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let l"
                                                    data-testid="location-contacts"
                                                >
                                                    @for (c of l.contacts; track $index) {
                                                        <div class="contact-line">
                                                            <span class="strong">{{ c.name }}</span>
                                                            @if (c.phone) {
                                                                · {{ c.phone }}
                                                            }
                                                            @if (c.email) {
                                                                · {{ c.email }}
                                                            }
                                                        </div>
                                                    } @empty {
                                                        –
                                                    }
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

            td[data-testid='location-contacts'] {
                padding-top: 6px;
                padding-bottom: 6px;
            }

            .contact-line {
                white-space: nowrap;
                padding: 2px 0;
            }

            .additional-groups {
                display: grid;
                grid-template-columns: repeat(2, minmax(0, 1fr));
                gap: 8px 24px;
            }

            .group {
                min-width: 0;
            }

            .group-title {
                margin: 0 0 12px;
                font: var(--mat-sys-title-small);
                color: var(--mat-sys-on-surface-variant);
            }

            .flags {
                display: flex;
                flex-direction: column;
                align-items: flex-start;
            }

            .comment {
                grid-column: 1 / -1;
                width: 100%;
            }

            @media (max-width: 899px) {
                .additional-groups {
                    grid-template-columns: minmax(0, 1fr);
                }
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
    private readonly catalogService = inject(VehicleCatalogService);
    private readonly dialog = inject(MatDialog);
    // See DriverFormComponent: a directly opened form must wait for the stored session.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly locationColumns = [
        'name',
        'city',
        'contacts',
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

    protected readonly secondaryAddressFields = [
        {
            name: 'secondaryName',
            label: 'customers.additional.secondaryName',
            maxLength: 200,
            wide: true,
        },
        { name: 'secondaryStreet', label: 'common.street', maxLength: 200, wide: true },
        { name: 'secondaryPostalCode', label: 'common.postalCode', maxLength: 20, wide: false },
        { name: 'secondaryCity', label: 'common.city', maxLength: 200, wide: false },
        {
            name: 'secondaryCountry',
            label: 'customers.additional.secondaryCountry',
            maxLength: 2,
            wide: false,
        },
    ] as const;
    protected readonly additionalFlags = ADDITIONAL_FLAGS;
    protected readonly maxCommentLength = MAX_COMMENT_LENGTH;

    private readonly customerClasses = signal<VehicleCatalogEntry[]>([]);
    // The class the customer is SAVED with stays selectable even after it was deactivated.
    private readonly storedCustomerClass = signal<string | null>(null);
    protected readonly customerClassOptions = computed(() =>
        catalogOptions(this.customerClasses(), this.storedCustomerClass()),
    );

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
        additional: this.fb.nonNullable.group({
            secondaryName: ['', Validators.maxLength(200)],
            secondaryStreet: ['', Validators.maxLength(200)],
            secondaryPostalCode: ['', Validators.maxLength(20)],
            secondaryCity: ['', Validators.maxLength(200)],
            secondaryCountry: ['', Validators.pattern(/^[A-Za-z]{2}$/)],
            foundedOn: ['', dateFromYear(MIN_FOUNDING_YEAR)],
            workerCount: this.fb.control<number | null>(0, wholeNumberInRange(0, MAX_HEADCOUNT)),
            salariedEmployeeCount: this.fb.control<number | null>(
                0,
                wholeNumberInRange(0, MAX_HEADCOUNT),
            ),
            requiresPalletCheck: [false],
            excludeTollFromInvoice: [false],
            reimburseToll: [false],
            customerClass: this.fb.control<string | null>(null),
            comment: ['', Validators.maxLength(MAX_COMMENT_LENGTH)],
        }),
    });

    protected readonly additional = this.form.controls.additional;

    constructor() {
        this.session.ready
            .pipe(switchMap(() => this.catalogService.list('CustomerClass')))
            .subscribe({
                next: (entries) => this.customerClasses.set(entries),
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        `${this.i18n.t('customers.error.catalogLoad')} (HTTP ${error.status})`,
                    ),
            });

        if (this.isEditMode) {
            this.session.ready.subscribe((isAuthenticated) => {
                if (isAuthenticated) {
                    this.loadLocations();
                }
            });
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.customerId!)))
                .subscribe({
                    next: (customer: Customer) => {
                        this.storedCustomerClass.set(customer.additional?.customerClass ?? null);
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
                            additional: this.toAdditionalFormValue(customer.additional),
                        });
                    },
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
        if (errors['range']) {
            return this.i18n.t(
                'common.fieldRange',
                errors['range'] as { min: number; max: number },
            );
        }
        if (errors['minYear']) {
            return this.i18n.t(
                'customers.additional.foundedOnTooEarly',
                errors['minYear'] as { year: number },
            );
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected flagKey(flag: (typeof ADDITIONAL_FLAGS)[number]) {
        return `customers.additional.${flag}` as const;
    }

    protected optionLabel(option: VehicleCatalogOption): string {
        return option.isActive
            ? option.name
            : this.i18n.t('vehicles.catalogInactive', { name: option.name });
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request: CustomerWriteRequest = {
            ...this.form.getRawValue(),
            additional: this.additionalRequest(),
        };
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

    // Field keys arrive PascalCase (.NET property names), nested ones dotted ("Additional.WorkerCount");
    // lowercase the first letter of every segment to find the control.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const controlName = field.replace(
                    PASCAL_SEGMENTS,
                    (_, separator: string, letter: string) => separator + letter.toLowerCase(),
                );
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

    private toAdditionalFormValue(additional: CustomerAdditionalData | null | undefined) {
        return {
            secondaryName: additional?.secondaryName ?? '',
            secondaryStreet: additional?.secondaryStreet ?? '',
            secondaryPostalCode: additional?.secondaryPostalCode ?? '',
            secondaryCity: additional?.secondaryCity ?? '',
            secondaryCountry: additional?.secondaryCountry ?? '',
            foundedOn: additional?.foundedOn?.slice(0, 10) ?? '',
            workerCount: additional?.workerCount ?? 0,
            salariedEmployeeCount: additional?.salariedEmployeeCount ?? 0,
            requiresPalletCheck: additional?.requiresPalletCheck ?? false,
            excludeTollFromInvoice: additional?.excludeTollFromInvoice ?? false,
            reimburseToll: additional?.reimburseToll ?? false,
            customerClass: additional?.customerClass ?? null,
            comment: additional?.comment ?? '',
        };
    }

    private additionalRequest(): CustomerAdditionalData {
        const value = this.additional.getRawValue();
        const text = (input: string) => input.trim() || null;
        return {
            secondaryName: text(value.secondaryName),
            secondaryStreet: text(value.secondaryStreet),
            secondaryPostalCode: text(value.secondaryPostalCode),
            secondaryCity: text(value.secondaryCity),
            secondaryCountry: text(value.secondaryCountry)?.toUpperCase() ?? null,
            foundedOn: value.foundedOn || null,
            workerCount: Number(value.workerCount ?? 0),
            salariedEmployeeCount: Number(value.salariedEmployeeCount ?? 0),
            requiresPalletCheck: value.requiresPalletCheck,
            excludeTollFromInvoice: value.excludeTollFromInvoice,
            reimburseToll: value.reimburseToll,
            customerClass: value.customerClass,
            comment: text(value.comment),
        };
    }
}
