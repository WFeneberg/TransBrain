import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { LocationMapComponent, MapMarker } from '../shared/location-map.component';
import { CountrySelectComponent } from '../shared/country-select.component';
import { Observable, switchMap } from 'rxjs';
import {
    LocatedParty,
    Order,
    OrderLocations,
    OrderService,
    OrderWriteRequest,
} from './order.service';
import {
    CustomerLocationOption,
    CustomerLocationService,
} from '../customers/customer-location.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-order-form',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        LocationMapComponent,
        CountrySelectComponent,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ isEditMode ? i18n.t('orders.edit') : i18n.t('orders.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="order-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">outbox</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.consignor') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields" formGroupName="consignor">
                            @for (field of addressFields; track field.name) {
                                @if (field.name === 'country') {
                                    <app-country-select
                                        [control]="form.controls.consignor.controls.country"
                                        [label]="i18n.t(field.label)"
                                        testid="order-consignor-country"
                                    />
                                } @else {
                                    <mat-form-field [class.span-2]="field.name === 'name'">
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'order-consignor-' + field.name"
                                        />
                                        @if (
                                            form.controls.consignor.controls[field.name].errors;
                                            as errors
                                        ) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'order-consignor-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">move_to_inbox</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.consignee') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field class="span-2">
                                <mat-label>{{ i18n.t('orders.consigneeLocation') }}</mat-label>
                                <select
                                    matNativeControl
                                    formControlName="consigneeLocationId"
                                    (change)="onLocationChange($event)"
                                    data-testid="order-consigneeLocation"
                                >
                                    <option value="">
                                        {{ i18n.t('orders.consigneeLocation.none') }}
                                    </option>
                                    @for (option of locationOptions(); track option.id) {
                                        <option [value]="option.id">
                                            {{ option.customerName }}: {{ option.name }},
                                            {{ option.city }}
                                        </option>
                                    }
                                </select>
                            </mat-form-field>
                            @if (locationPickerError(); as message) {
                                <p class="hint span-2" data-testid="order-consigneeLocation-error">
                                    <mat-icon aria-hidden="true">info</mat-icon>{{ message }}
                                </p>
                            }
                        </mat-card-content>
                        <mat-card-content class="fields" formGroupName="consignee">
                            @for (field of addressFields; track field.name) {
                                @if (field.name === 'country') {
                                    <app-country-select
                                        [control]="form.controls.consignee.controls.country"
                                        [label]="i18n.t(field.label)"
                                        testid="order-consignee-country"
                                    />
                                } @else {
                                    <mat-form-field [class.span-2]="field.name === 'name'">
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'order-consignee-' + field.name"
                                        />
                                        @if (
                                            form.controls.consignee.controls[field.name].errors;
                                            as errors
                                        ) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'order-consignee-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">inventory_2</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.cargo') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field class="span-2">
                                <mat-label>{{ i18n.t('orders.cargoDescription') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="cargoDescription"
                                    data-testid="order-cargoDescription"
                                />
                                @if (form.controls.cargoDescription.errors; as errors) {
                                    <mat-error data-testid="order-cargoDescription-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('orders.weightKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="cargoWeightKg"
                                    data-testid="order-cargoWeightKg"
                                />
                                @if (form.controls.cargoWeightKg.errors; as errors) {
                                    <mat-error data-testid="order-cargoWeightKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.loadMeters') }}</mat-label>
                                <!-- step="any" because the default step=1 makes a native number input
                                     reject a decimal like 8.4 - the same trap Phase 2 hit in the Vue form. -->
                                <input
                                    matInput
                                    type="number"
                                    step="any"
                                    formControlName="cargoLoadMeters"
                                    data-testid="order-cargoLoadMeters"
                                />
                                @if (form.controls.cargoLoadMeters.errors; as errors) {
                                    <mat-error data-testid="order-cargoLoadMeters-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">schedule</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.section.windows') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <p class="window-label">{{ i18n.t('orders.pickupWindow') }}</p>
                            <div class="fields">
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.from') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="pickupFrom"
                                        data-testid="order-pickupFrom"
                                    />
                                    @if (form.controls.pickupFrom.errors; as errors) {
                                        <mat-error data-testid="order-pickupFrom-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.to') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="pickupTo"
                                        data-testid="order-pickupTo"
                                    />
                                    @if (form.controls.pickupTo.errors; as errors) {
                                        <mat-error data-testid="order-pickupTo-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                            </div>
                            <p class="window-label">{{ i18n.t('orders.deliveryWindow') }}</p>
                            <div class="fields">
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.from') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="deliveryFrom"
                                        data-testid="order-deliveryFrom"
                                    />
                                    @if (form.controls.deliveryFrom.errors; as errors) {
                                        <mat-error data-testid="order-deliveryFrom-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.to') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="deliveryTo"
                                        data-testid="order-deliveryTo"
                                    />
                                    @if (form.controls.deliveryTo.errors; as errors) {
                                        <mat-error data-testid="order-deliveryTo-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                            </div>
                        </mat-card-content>
                    </mat-card>

                    @if (isEditMode) {
                        <mat-card appearance="outlined" class="full-width" data-testid="order-map">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">map</mat-icon>
                                <mat-card-title>{{ i18n.t('orders.map') }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                @if (locationError(); as message) {
                                    <p class="hint" data-testid="order-map-error">
                                        <mat-icon aria-hidden="true">info</mat-icon>{{ message }}
                                    </p>
                                }
                                @if (locations(); as located) {
                                    @if (mapMarkers().length) {
                                        <app-location-map [markers]="mapMarkers()" />
                                    }
                                    @if (!located.consignor) {
                                        <p class="hint" data-testid="order-map-consignor-missing">
                                            <mat-icon aria-hidden="true">info</mat-icon
                                            >{{ i18n.t('orders.map.consignorMissing') }}
                                        </p>
                                    }
                                    @if (!located.consignee) {
                                        <p class="hint" data-testid="order-map-consignee-missing">
                                            <mat-icon aria-hidden="true">info</mat-icon
                                            >{{ i18n.t('orders.map.consigneeMissing') }}
                                        </p>
                                    }
                                }
                            </mat-card-content>
                        </mat-card>
                    }
                </div>

                <div class="action-bar">
                    <button
                        mat-button
                        type="button"
                        data-testid="order-cancel-edit"
                        (click)="cancel()"
                    >
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="order-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: [
        `
            .span-2 {
                grid-column: 1 / -1;
            }

            .window-label {
                margin: 0 0 8px;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-title-small);
            }
        `,
    ],
})
export class OrderFormComponent {
    private readonly service = inject(OrderService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly locationService = inject(CustomerLocationService);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly addressFields = [
        { name: 'name', label: 'common.name' },
        { name: 'street', label: 'common.street' },
        { name: 'postalCode', label: 'common.postalCode' },
        { name: 'city', label: 'common.city' },
        { name: 'country', label: 'common.country' },
    ] as const;

    protected readonly orderId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.orderId !== null;
    protected readonly formError = signal<string | null>(null);
    protected readonly locations = signal<OrderLocations | null>(null);
    protected readonly locationError = signal<string | null>(null);
    protected readonly locationOptions = signal<CustomerLocationOption[]>([]);
    protected readonly locationPickerError = signal<string | null>(null);
    protected readonly mapMarkers = computed<MapMarker[]>(() => {
        const located = this.locations();
        if (!located) {
            return [];
        }
        return [
            this.marker(located.consignor, 'A', this.i18n.t('orders.consignor')),
            this.marker(located.consignee, 'B', this.i18n.t('orders.consignee')),
        ].filter((marker): marker is MapMarker => marker !== null);
    });

    protected readonly form = this.fb.nonNullable.group({
        consignor: this.addressGroup(),
        consignee: this.addressGroup(),
        cargoDescription: ['', Validators.required],
        cargoWeightKg: [0, [Validators.required, Validators.min(1)]],
        cargoLoadMeters: [0, [Validators.required, Validators.min(0.01)]],
        pickupFrom: ['', Validators.required],
        pickupTo: ['', Validators.required],
        deliveryFrom: ['', Validators.required],
        deliveryTo: ['', Validators.required],
        consigneeLocationId: [''],
    });

    constructor() {
        this.loadLocationOptions();
        if (this.isEditMode) {
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.orderId!)))
                .subscribe({
                    next: (order: Order) => this.patchFrom(order),
                    // The load path gets its own message: "could not be saved" on a failed
                    // getById would be plainly wrong.
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            this.describeFailure(error, this.i18n.t('orders.error.load')),
                        ),
                });
            this.loadLocations();
        }
    }

    // Picking a location only copies its address into the consignee fields. The order keeps
    // that copy and the reference, so later edits to the location do not change this order.
    protected onLocationChange(event: Event): void {
        const id = (event.target as HTMLSelectElement).value;
        const option = this.locationOptions().find((o) => o.id === id);
        if (!option) {
            return;
        }
        this.form.controls.consignee.patchValue({
            name: option.name,
            street: option.street,
            postalCode: option.postalCode,
            city: option.city,
            country: option.country,
        });
    }

    private loadLocationOptions(): void {
        this.session.ready.pipe(switchMap(() => this.locationService.listAll())).subscribe({
            next: (page) => this.locationOptions.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.locationPickerError.set(
                    this.describeFailure(error, this.i18n.t('orders.consigneeLocation.error')),
                ),
        });
    }

    // The map is advisory: a failure here must never block the form, so it has its own error.
    // 503 means routing/geocoding is not configured or unreachable, which deserves its own wording.
    private loadLocations(): void {
        this.session.ready
            .pipe(switchMap(() => this.service.getLocations(this.orderId!)))
            .subscribe({
                next: (located) => this.locations.set(located),
                error: (error: HttpErrorResponse) =>
                    this.locationError.set(
                        error.status === 503
                            ? this.i18n.t('map.unavailable')
                            : this.describeFailure(error, this.i18n.t('map.error.load')),
                    ),
            });
    }

    private marker(party: LocatedParty | null, label: string, role: string): MapMarker | null {
        if (!party) {
            return null;
        }
        return {
            latitude: party.latitude,
            longitude: party.longitude,
            label,
            title: `${role}: ${party.name}, ${party.address}`,
        };
    }

    // See DriverFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['min']) {
            return this.i18n.t('common.fieldPositive');
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const value = this.form.getRawValue();
        const request: OrderWriteRequest = {
            consignor: value.consignor,
            consignee: value.consignee,
            cargoDescription: value.cargoDescription,
            cargoWeightKg: Number(value.cargoWeightKg),
            cargoLoadMeters: Number(value.cargoLoadMeters),
            pickupFrom: this.toIso(value.pickupFrom),
            pickupTo: this.toIso(value.pickupTo),
            deliveryFrom: this.toIso(value.deliveryFrom),
            deliveryTo: this.toIso(value.deliveryTo),
            consigneeLocationId: value.consigneeLocationId || null,
        };

        const save$: Observable<Order> = this.session.ready.pipe(
            switchMap(() =>
                this.isEditMode
                    ? this.service.update(this.orderId!, request)
                    : this.service.create(request),
            ),
        );

        save$.subscribe({
            next: () => this.router.navigateByUrl('/orders'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/orders');
    }

    private addressGroup() {
        return this.fb.nonNullable.group({
            name: ['', Validators.required],
            street: ['', Validators.required],
            postalCode: ['', Validators.required],
            city: ['', Validators.required],
            country: ['DE', Validators.required],
        });
    }

    private patchFrom(order: Order): void {
        this.form.patchValue({
            consignor: order.consignor,
            consignee: order.consignee,
            cargoDescription: order.cargoDescription,
            cargoWeightKg: order.cargoWeightKg,
            cargoLoadMeters: order.cargoLoadMeters,
            pickupFrom: this.toLocalInput(order.pickupFrom),
            pickupTo: this.toLocalInput(order.pickupTo),
            deliveryFrom: this.toLocalInput(order.deliveryFrom),
            deliveryTo: this.toLocalInput(order.deliveryTo),
            consigneeLocationId: order.consigneeLocationId ?? '',
        });
    }

    // A datetime-local input has no zone, so its value is the user's wall-clock time. Sending
    // it verbatim would let the API read it as UTC and shift the window by the local offset;
    // constructing a Date first makes the browser attach the local zone before serialising.
    private toIso(localValue: string): string {
        return new Date(localValue).toISOString();
    }

    // The inverse: an ISO instant rendered back into the local wall-clock string the input
    // expects ("YYYY-MM-DDTHH:mm"), so a round-trip through the edit form is lossless.
    private toLocalInput(iso: string): string {
        const date = new Date(iso);
        const offsetMinutes = date.getTimezoneOffset();
        const local = new Date(date.getTime() - offsetMinutes * 60_000);
        return local.toISOString().slice(0, 16);
    }

    // See DriverFormComponent.applyServerErrors for why binding ProblemDetails' `errors`
    // dictionary is correct here (it supersedes a Phase 1 instruction that forbade it).
    // Order keys can be DOTTED: the validator's nested rules produce "Consignor.Name" and
    // "Consignee.City" alongside flat keys like "CargoWeightKg", so every dotted segment is
    // lowercased individually and the result used as an Angular control path. A key that
    // names no control still reaches the user, as a form-level message.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const path = field
                    .split('.')
                    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
                    .join('.');
                const control = this.form.get(path);
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                    control.markAsTouched();
                } else {
                    this.formError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error, this.i18n.t('orders.error.save')));
    }

    // See DriverFormComponent.describeFailure: `fallback` is supplied per call site (loading
    // vs. saving) because a shared string is wrong for at least one of them.
    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
