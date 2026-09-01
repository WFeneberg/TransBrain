import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import {switchMap} from 'rxjs';
import { Driver, DriverService, DriverWriteRequest } from './driver.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-driver-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
    template: `
        <h1>{{ isEditMode ? 'Edit driver' : 'New driver' }}</h1>
        @if (formError(); as message) {
            <p data-testid="driver-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            <mat-form-field>
                <mat-label>First name</mat-label>
                <input matInput formControlName="firstName" data-testid="driver-firstName" />
                @if (form.controls.firstName.errors; as errors) {
                    <mat-error data-testid="driver-firstName-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <mat-form-field>
                <mat-label>Last name</mat-label>
                <input matInput formControlName="lastName" data-testid="driver-lastName" />
                @if (form.controls.lastName.errors; as errors) {
                    <mat-error data-testid="driver-lastName-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <fieldset>
                <legend>License classes</legend>
                @for (licenseClass of licenseClassOptions; track licenseClass) {
                    <label>
                        <input
                            type="checkbox"
                            [checked]="isLicenseClassSelected(licenseClass)"
                            (change)="toggleLicenseClass(licenseClass)"
                            [attr.data-testid]="'driver-licenseClass-' + licenseClass"
                        />
                        {{ licenseClass }}
                    </label>
                }
                @if (attemptedSubmit() && form.controls.licenseClasses.invalid) {
                    <!-- Not a mat-form-field, so it has no built-in errorState gating (which
                         hides mat-error until the control is touched or the form submitted) -
                         attemptedSubmit reproduces that gating manually, so this doesn't show
                         "required" before the user has done anything. -->
                    <p data-testid="driver-licenseClasses-error">
                        {{ fieldErrorText(form.controls.licenseClasses.errors!) }}
                    </p>
                }
            </fieldset>
            <mat-form-field>
                <mat-label>License valid until</mat-label>
                <input
                    matInput
                    type="date"
                    formControlName="licenseValidUntil"
                    data-testid="driver-licenseValidUntil"
                />
                @if (form.controls.licenseValidUntil.errors; as errors) {
                    <mat-error data-testid="driver-licenseValidUntil-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <button mat-raised-button type="submit" data-testid="driver-save">Save</button>
            <button mat-button type="button" data-testid="driver-cancel" (click)="cancel()">Cancel</button>
        </form>
    `,
})
export class DriverFormComponent {
    private readonly service = inject(DriverService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);

    protected readonly licenseClassOptions = ['B', 'C1', 'C', 'CE'];
    protected readonly driverId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.driverId !== null;
    protected readonly formError = signal<string | null>(null);
    // Gates the licenseClasses paragraph the same way Material's errorState gates mat-error
    // for the other fields (hidden until touched/submitted) - see the template comment.
    protected readonly attemptedSubmit = signal(false);
    private externalUserId: string | null = null;

    protected readonly form = this.fb.nonNullable.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        licenseClasses: this.fb.nonNullable.control<string[]>([], Validators.required),
        licenseValidUntil: ['', Validators.required],
    });

    constructor() {
        if (this.isEditMode) {
            this.session.ready.pipe(switchMap(() => this.service.getById(this.driverId!))).subscribe({
                next: (driver: Driver) => {
                    this.externalUserId = driver.externalUserId;
                    this.form.patchValue({
                        firstName: driver.firstName,
                        lastName: driver.lastName,
                        licenseClasses: driver.licenseClasses,
                        licenseValidUntil: driver.licenseValidUntil,
                    });
                },
                error: (error: HttpErrorResponse) =>
                    this.formError.set(this.describeFailure(error, 'The driver could not be loaded.')),
            });
        }
    }

    // See VehicleFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return 'This field is required.';
        }
        return 'This field is invalid.';
    }

    protected isLicenseClassSelected(licenseClass: string): boolean {
        return this.form.controls.licenseClasses.value.includes(licenseClass);
    }

    protected toggleLicenseClass(licenseClass: string): void {
        const current = this.form.controls.licenseClasses.value;
        const next = current.includes(licenseClass)
            ? current.filter((c) => c !== licenseClass)
            : [...current, licenseClass];
        this.form.controls.licenseClasses.setValue(next);
    }

    protected save(): void {
        this.formError.set(null);
        this.attemptedSubmit.set(true);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request: DriverWriteRequest = { ...this.form.getRawValue(), externalUserId: this.externalUserId };
        const save$ = this.session.ready.pipe(
            switchMap(() => (this.isEditMode
                ? this.service.update(this.driverId!, request)
                : this.service.create(request))),
        );

        save$.subscribe({
            next: () => this.router.navigateByUrl('/drivers'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/drivers');
    }

    // Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed
    // by error code rather than by field name. Task 1 of the 2026-08-29 master-data-completion
    // phase fixed the API to group every failure under its real field name instead, so binding
    // it is now correct - this supersedes that earlier rule. See VehicleFormComponent for the
    // same comment in full; the PascalCase-to-camelCase key lowercasing works the same way here.
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

        this.formError.set(this.describeFailure(error, 'The driver could not be saved.'));
    }

    // No `errors` dictionary means a domain-level failure (404, 409, or a rejected domain
    // invariant) rather than a per-field one - `detail` is the stable, human-readable message
    // for all of those. `fallback` is supplied per call site (loading vs. saving) - see
    // VehicleFormComponent.describeFailure for why a shared fallback string is wrong.
    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
