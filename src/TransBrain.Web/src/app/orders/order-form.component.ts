import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, shareReplay, switchMap } from 'rxjs';
import { Order, OrderService, OrderWriteRequest } from './order.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-order-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
    template: `
        <h1>{{ isEditMode ? 'Edit order' : 'New order' }}</h1>
        @if (formError(); as message) {
            <p data-testid="order-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            <fieldset formGroupName="consignor">
                <legend>Consignor</legend>
                <mat-form-field>
                    <mat-label>Name</mat-label>
                    <input matInput formControlName="name" data-testid="order-consignor-name" />
                    @if (form.controls.consignor.controls.name.errors; as errors) {
                        <mat-error data-testid="order-consignor-name-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Street</mat-label>
                    <input matInput formControlName="street" data-testid="order-consignor-street" />
                    @if (form.controls.consignor.controls.street.errors; as errors) {
                        <mat-error data-testid="order-consignor-street-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Postal code</mat-label>
                    <input matInput formControlName="postalCode" data-testid="order-consignor-postalCode" />
                    @if (form.controls.consignor.controls.postalCode.errors; as errors) {
                        <mat-error data-testid="order-consignor-postalCode-error">
                            {{ fieldErrorText(errors) }}
                        </mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>City</mat-label>
                    <input matInput formControlName="city" data-testid="order-consignor-city" />
                    @if (form.controls.consignor.controls.city.errors; as errors) {
                        <mat-error data-testid="order-consignor-city-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Country</mat-label>
                    <input matInput formControlName="country" data-testid="order-consignor-country" />
                    @if (form.controls.consignor.controls.country.errors; as errors) {
                        <mat-error data-testid="order-consignor-country-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            </fieldset>
            <fieldset formGroupName="consignee">
                <legend>Consignee</legend>
                <mat-form-field>
                    <mat-label>Name</mat-label>
                    <input matInput formControlName="name" data-testid="order-consignee-name" />
                    @if (form.controls.consignee.controls.name.errors; as errors) {
                        <mat-error data-testid="order-consignee-name-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Street</mat-label>
                    <input matInput formControlName="street" data-testid="order-consignee-street" />
                    @if (form.controls.consignee.controls.street.errors; as errors) {
                        <mat-error data-testid="order-consignee-street-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Postal code</mat-label>
                    <input matInput formControlName="postalCode" data-testid="order-consignee-postalCode" />
                    @if (form.controls.consignee.controls.postalCode.errors; as errors) {
                        <mat-error data-testid="order-consignee-postalCode-error">
                            {{ fieldErrorText(errors) }}
                        </mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>City</mat-label>
                    <input matInput formControlName="city" data-testid="order-consignee-city" />
                    @if (form.controls.consignee.controls.city.errors; as errors) {
                        <mat-error data-testid="order-consignee-city-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Country</mat-label>
                    <input matInput formControlName="country" data-testid="order-consignee-country" />
                    @if (form.controls.consignee.controls.country.errors; as errors) {
                        <mat-error data-testid="order-consignee-country-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            </fieldset>
            <fieldset>
                <legend>Cargo</legend>
                <mat-form-field>
                    <mat-label>Description</mat-label>
                    <input matInput formControlName="cargoDescription" data-testid="order-cargoDescription" />
                    @if (form.controls.cargoDescription.errors; as errors) {
                        <mat-error data-testid="order-cargoDescription-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Weight (kg)</mat-label>
                    <input
                        matInput
                        type="number"
                        formControlName="cargoWeightKg"
                        data-testid="order-cargoWeightKg"
                    />
                    @if (form.controls.cargoWeightKg.errors; as errors) {
                        <mat-error data-testid="order-cargoWeightKg-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>Load meters</mat-label>
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
                        <mat-error data-testid="order-cargoLoadMeters-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            </fieldset>
            <fieldset>
                <legend>Pickup window</legend>
                <mat-form-field>
                    <mat-label>From</mat-label>
                    <input
                        matInput
                        type="datetime-local"
                        formControlName="pickupFrom"
                        data-testid="order-pickupFrom"
                    />
                    @if (form.controls.pickupFrom.errors; as errors) {
                        <mat-error data-testid="order-pickupFrom-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>To</mat-label>
                    <input matInput type="datetime-local" formControlName="pickupTo" data-testid="order-pickupTo" />
                    @if (form.controls.pickupTo.errors; as errors) {
                        <mat-error data-testid="order-pickupTo-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            </fieldset>
            <fieldset>
                <legend>Delivery window</legend>
                <mat-form-field>
                    <mat-label>From</mat-label>
                    <input
                        matInput
                        type="datetime-local"
                        formControlName="deliveryFrom"
                        data-testid="order-deliveryFrom"
                    />
                    @if (form.controls.deliveryFrom.errors; as errors) {
                        <mat-error data-testid="order-deliveryFrom-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
                <mat-form-field>
                    <mat-label>To</mat-label>
                    <input
                        matInput
                        type="datetime-local"
                        formControlName="deliveryTo"
                        data-testid="order-deliveryTo"
                    />
                    @if (form.controls.deliveryTo.errors; as errors) {
                        <mat-error data-testid="order-deliveryTo-error">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            </fieldset>
            <button mat-raised-button type="submit" data-testid="order-save">Save</button>
            <button mat-button type="button" data-testid="order-cancel-edit" (click)="cancel()">Cancel</button>
        </form>
    `,
})
export class OrderFormComponent {
    private readonly service = inject(OrderService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly oidc = inject(OidcSecurityService);

    // angular-auth-oidc-client only hydrates its stored session when checkAuth() runs, and
    // until it has, the auth interceptor finds no access token and sends the request
    // unauthenticated. The list components call it on construction; a form reached by a DIRECT
    // navigation - a bookmarked /orders/new, a page reload while editing, browser back - has no
    // list component in its lifetime, so every request from it would answer 401 and the user
    // would be told "The order could not be saved. (HTTP 401)" while plainly signed in.
    // shareReplay(1) so the check runs once and both the load and the save share its result.
    private readonly session = this.oidc.checkAuth().pipe(shareReplay(1));

    protected readonly orderId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.orderId !== null;
    protected readonly formError = signal<string | null>(null);

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
    });

    constructor() {
        if (this.isEditMode) {
            this.session.pipe(switchMap(() => this.service.getById(this.orderId!))).subscribe({
                next: (order: Order) => this.patchFrom(order),
                // The load path gets its own message: "could not be saved" on a failed
                // getById would be plainly wrong.
                error: (error: HttpErrorResponse) =>
                    this.formError.set(this.describeFailure(error, 'The order could not be loaded.')),
            });
        }
    }

    // See DriverFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return 'This field is required.';
        }
        if (errors['min']) {
            return 'This value must be greater than zero.';
        }
        return 'This field is invalid.';
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
        };

        const save$: Observable<Order> = this.session.pipe(
            switchMap(() => (this.isEditMode
                ? this.service.update(this.orderId!, request)
                : this.service.create(request))),
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
                    this.formError.update((existing) => (existing ? `${existing} ${message}` : message));
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error, 'The order could not be saved.'));
    }

    // See DriverFormComponent.describeFailure: `fallback` is supplied per call site (loading
    // vs. saving) because a shared string is wrong for at least one of them.
    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
