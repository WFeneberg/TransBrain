import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { CustomerLocationPosition, CustomerLocationService, CustomerLocationWriteRequest } from './customer-location.service';
import { LocationMapComponent, MapMarker } from '../shared/location-map.component';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
}

@Component({
    selector: 'app-customer-location-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, LocationMapComponent],
    template: `
        <h1>{{ isEditMode ? i18n.t('customers.location.edit') : i18n.t('customers.location.new') }}</h1>
        @if (formError(); as message) {
            <p data-testid="location-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            @for (field of textFields; track field.name) {
                <mat-form-field>
                    <mat-label>{{ i18n.t(field.label) }}</mat-label>
                    <input matInput [formControlName]="field.name" [attr.data-testid]="'location-' + field.name" />
                    @if (form.controls[field.name].errors; as errors) {
                        <mat-error [attr.data-testid]="'location-' + field.name + '-error'">{{ fieldErrorText(errors) }}</mat-error>
                    }
                </mat-form-field>
            }
            <button mat-raised-button type="submit" data-testid="location-save">{{ i18n.t('common.save') }}</button>
            <button mat-button type="button" data-testid="location-cancel" (click)="cancel()">{{ i18n.t('common.cancel') }}</button>
        </form>
        @if (isEditMode) {
            <fieldset data-testid="location-map-section">
                <legend>{{ i18n.t('customers.location.map') }}</legend>
                @if (mapError(); as message) {
                    <p data-testid="location-map-error">{{ message }}</p>
                }
                @if (position(); as located) {
                    <app-location-map [markers]="markers()" />
                }
            </fieldset>
        }
    `,
})
export class CustomerLocationFormComponent {
    private readonly service = inject(CustomerLocationService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    // See DriverFormComponent: a directly opened form must wait for the stored session.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly textFields = [
        { name: 'name', label: 'customers.location.name' },
        { name: 'street', label: 'common.street' },
        { name: 'postalCode', label: 'common.postalCode' },
        { name: 'city', label: 'common.city' },
        { name: 'country', label: 'common.country' },
        { name: 'contactPerson', label: 'customers.contactPerson' },
        { name: 'email', label: 'customers.location.contactEmail' },
        { name: 'phone', label: 'customers.location.contactPhone' },
    ] as const;

    protected readonly customerId = this.route.snapshot.paramMap.get('id')!;
    protected readonly locationId = this.route.snapshot.paramMap.get('locationId');
    protected readonly isEditMode = this.locationId !== null;
    protected readonly formError = signal<string | null>(null);
    protected readonly position = signal<CustomerLocationPosition | null>(null);
    protected readonly mapError = signal<string | null>(null);
    protected readonly markers = computed<MapMarker[]>(() => {
        const located = this.position();
        return located
            ? [{ latitude: located.latitude, longitude: located.longitude, label: 'A', title: `${located.name}: ${located.address}` }]
            : [];
    });

    protected readonly form = this.fb.nonNullable.group({
        name: ['', Validators.required],
        street: ['', Validators.required],
        postalCode: ['', Validators.required],
        city: ['', Validators.required],
        country: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
        contactPerson: [''],
        email: ['', Validators.email],
        phone: [''],
    });

    constructor() {
        if (this.isEditMode) {
            this.session.ready.subscribe((isAuthenticated) => {
                if (isAuthenticated) {
                    this.loadPosition();
                }
            });
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.customerId, this.locationId!)))
                .subscribe({
                    next: (location) => {
                        if (!location) {
                            this.formError.set(this.i18n.t('customers.location.error.load'));
                            return;
                        }
                        this.form.patchValue({
                            name: location.name,
                            street: location.street,
                            postalCode: location.postalCode,
                            city: location.city,
                            country: location.country,
                            contactPerson: location.contactPerson ?? '',
                            email: location.email ?? '',
                            phone: location.phone ?? '',
                        });
                    },
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(this.describeFailure(error, this.i18n.t('customers.location.error.load'))),
                });
        }
    }

    // The map is advisory: a failure must not block editing, so it has its own message.
    // 404 means the address matches no place; 503 means geocoding is not configured or unreachable.
    private loadPosition(): void {
        this.service.position(this.customerId, this.locationId!).subscribe({
            next: (located) => this.position.set(located),
            error: (error: HttpErrorResponse) => {
                if (error.status === 503) {
                    this.mapError.set(this.i18n.t('map.unavailable'));
                } else if (error.status === 404) {
                    this.mapError.set(this.i18n.t('customers.location.map.notFound'));
                } else {
                    this.mapError.set(this.i18n.t('map.error.load'));
                }
            },
        });
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

        const request: CustomerLocationWriteRequest = this.form.getRawValue();
        const save$ = this.session.ready.pipe(
            switchMap(() => (this.isEditMode
                ? this.service.update(this.customerId, this.locationId!, request)
                : this.service.create(this.customerId, request))),
        );

        save$.subscribe({
            next: () => this.router.navigateByUrl(`/customers/${this.customerId}`),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl(`/customers/${this.customerId}`);
    }

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
                    this.formError.update((existing) => (existing ? `${existing} ${message}` : message));
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error, this.i18n.t('customers.location.error.save')));
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
