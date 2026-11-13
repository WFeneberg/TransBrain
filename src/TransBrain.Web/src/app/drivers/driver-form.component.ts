import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
    FormArray,
    FormBuilder,
    FormControl,
    FormGroup,
    ReactiveFormsModule,
    ValidationErrors,
    Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { forkJoin, switchMap } from 'rxjs';
import { dateFromYear } from '../vehicles/vehicle-validators';
import {
    VehicleCatalogEntry,
    VehicleCatalogOption,
    VehicleCatalogService,
    catalogOptions,
} from '../vehicles/vehicle-catalog.service';
import {
    Driver,
    DriverClothingSizes,
    DriverDocuments,
    DriverPhoneNumber,
    DriverRelative,
    DriverService,
    DriverSocialInsurance,
    DriverSpecialDocument,
    DriverWriteRequest,
} from './driver.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

const MIN_YEAR = 1900;

/** All three driver lists share the same backend cap (Driver.MaxRelatives etc.). */
const MAX_ROWS = 20;

interface RelativeControls {
    lastName: FormControl<string>;
    firstName: FormControl<string>;
    relationship: FormControl<string>;
    dateOfBirth: FormControl<string>;
    phone: FormControl<string>;
}

interface SpecialDocumentControls {
    type: FormControl<string>;
    lastInspectionDate: FormControl<string>;
    validUntil: FormControl<string>;
    comment: FormControl<string>;
}

interface PhoneNumberControls {
    number: FormControl<string>;
    type: FormControl<string>;
    comment: FormControl<string>;
}

@Component({
    selector: 'app-driver-form',
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
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ isEditMode ? i18n.t('drivers.edit') : i18n.t('drivers.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="driver-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">person</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.personal')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.firstName') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="firstName"
                                    data-testid="driver-firstName"
                                />
                                @if (form.controls.firstName.errors; as errors) {
                                    <mat-error data-testid="driver-firstName-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.lastName') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="lastName"
                                    data-testid="driver-lastName"
                                />
                                @if (form.controls.lastName.errors; as errors) {
                                    <mat-error data-testid="driver-lastName-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">badge</mat-icon>
                            <mat-card-title>{{ i18n.t('drivers.section.license') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields one">
                            <div
                                class="license-classes"
                                role="group"
                                [attr.aria-label]="i18n.t('common.licenseClasses')"
                            >
                                <span class="group-label">{{
                                    i18n.t('common.licenseClasses')
                                }}</span>
                                <div class="options">
                                    @for (licenseClass of licenseClassOptions; track licenseClass) {
                                        <mat-checkbox
                                            [checked]="isLicenseClassSelected(licenseClass)"
                                            (change)="toggleLicenseClass(licenseClass)"
                                            [attr.data-testid]="
                                                'driver-licenseClass-' + licenseClass
                                            "
                                            >{{ licenseClass }}</mat-checkbox
                                        >
                                    }
                                </div>
                                @if (attemptedSubmit() && form.controls.licenseClasses.invalid) {
                                    <!-- Not a mat-form-field, so it has no built-in errorState gating (which
                                         hides mat-error until the control is touched or the form submitted) -
                                         attemptedSubmit reproduces that gating manually, so this doesn't show
                                         "required" before the user has done anything. -->
                                    <p
                                        class="group-error"
                                        data-testid="driver-licenseClasses-error"
                                    >
                                        {{ fieldErrorText(form.controls.licenseClasses.errors!) }}
                                    </p>
                                }
                            </div>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('drivers.licenseValidUntil') }}</mat-label>
                                <input
                                    matInput
                                    type="date"
                                    formControlName="licenseValidUntil"
                                    data-testid="driver-licenseValidUntil"
                                />
                                @if (form.controls.licenseValidUntil.errors; as errors) {
                                    <mat-error data-testid="driver-licenseValidUntil-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <ng-container formGroupName="documents">
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('drivers.documents.licenseNumber')
                                    }}</mat-label>
                                    <input
                                        matInput
                                        type="text"
                                        maxlength="50"
                                        formControlName="licenseNumber"
                                        data-testid="driver-licenseNumber"
                                    />
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('drivers.documents.issueDate')
                                    }}</mat-label>
                                    <input
                                        matInput
                                        type="date"
                                        formControlName="issueDate"
                                        data-testid="driver-issueDate"
                                    />
                                    @if (documents.controls.issueDate.errors; as errors) {
                                        <mat-error data-testid="driver-issueDate-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('drivers.documents.issuingAuthority')
                                    }}</mat-label>
                                    <textarea
                                        matInput
                                        rows="3"
                                        maxlength="500"
                                        formControlName="issuingAuthority"
                                        data-testid="driver-issuingAuthority"
                                    ></textarea>
                                </mat-form-field>
                            </ng-container>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        formGroupName="clothingSizes"
                        data-testid="driver-clothingSizes"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">checkroom</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.clothing')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            @for (field of clothingFields; track field.name) {
                                <mat-form-field>
                                    <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                    <input
                                        matInput
                                        type="text"
                                        maxlength="50"
                                        [formControlName]="field.name"
                                        [attr.data-testid]="'driver-' + field.name"
                                    />
                                </mat-form-field>
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width social-card"
                        formGroupName="socialInsurance"
                        data-testid="driver-socialInsurance"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">health_and_safety</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.socialInsurance')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="social-groups">
                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('drivers.social.group.insurance') }}
                                </h3>
                                <div class="fields">
                                    @for (field of insuranceFields; track field.name) {
                                        <mat-form-field>
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            <input
                                                matInput
                                                [type]="field.type"
                                                [attr.maxlength]="field.maxLength"
                                                [formControlName]="field.name"
                                                [attr.data-testid]="'driver-' + field.name"
                                            />
                                            @if (social.controls[field.name].errors; as errors) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'driver-' + field.name + '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                    <mat-form-field class="full-width">
                                        <mat-label>{{
                                            i18n.t('drivers.social.healthInsurance')
                                        }}</mat-label>
                                        <textarea
                                            matInput
                                            rows="3"
                                            maxlength="500"
                                            formControlName="healthInsurance"
                                            data-testid="driver-healthInsurance"
                                        ></textarea>
                                    </mat-form-field>
                                </div>
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('drivers.social.group.bank') }}
                                </h3>
                                <div class="fields">
                                    @for (field of bankFields; track field.name) {
                                        <mat-form-field>
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            <input
                                                matInput
                                                type="text"
                                                [attr.maxlength]="field.maxLength"
                                                [formControlName]="field.name"
                                                [attr.data-testid]="'driver-' + field.name"
                                            />
                                            @if (social.controls[field.name].errors; as errors) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'driver-' + field.name + '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                </div>
                            </section>

                            <section class="group full-width">
                                <h3 class="group-title">
                                    {{ i18n.t('drivers.social.group.familyDoctor') }}
                                </h3>
                                <div class="fields">
                                    <mat-form-field class="full-width">
                                        <mat-label>{{
                                            i18n.t('drivers.social.familyDoctor')
                                        }}</mat-label>
                                        <textarea
                                            matInput
                                            rows="3"
                                            maxlength="500"
                                            formControlName="familyDoctor"
                                            data-testid="driver-familyDoctor"
                                        ></textarea>
                                    </mat-form-field>
                                    <mat-form-field>
                                        <mat-label>{{
                                            i18n.t('drivers.social.familyDoctorPhone')
                                        }}</mat-label>
                                        <input
                                            matInput
                                            type="text"
                                            maxlength="50"
                                            formControlName="familyDoctorPhone"
                                            data-testid="driver-familyDoctorPhone"
                                        />
                                    </mat-form-field>
                                </div>
                            </section>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="driver-relatives-section"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">family_restroom</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.relatives')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content formArrayName="relatives">
                            @for (row of relatives.controls; track row; let i = $index) {
                                <div
                                    class="list-row cols-5"
                                    [formGroupName]="i"
                                    data-testid="driver-relative-row"
                                >
                                    @for (field of relativeFields; track field.name) {
                                        <mat-form-field>
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            <input
                                                matInput
                                                [type]="field.type"
                                                [attr.maxlength]="field.maxLength"
                                                [formControlName]="field.name"
                                                [attr.data-testid]="'driver-relative-' + field.name"
                                            />
                                            @if (row.controls[field.name].errors; as errors) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'driver-relative-' + field.name + '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                    <button
                                        mat-icon-button
                                        type="button"
                                        class="delete"
                                        data-testid="driver-relative-remove"
                                        [attr.aria-label]="i18n.t('drivers.relatives.remove')"
                                        (click)="removeRelative(i)"
                                    >
                                        <mat-icon>delete</mat-icon>
                                    </button>
                                </div>
                            } @empty {
                                <p class="hint" data-testid="driver-relatives-empty">
                                    <mat-icon aria-hidden="true">info</mat-icon
                                    >{{ i18n.t('drivers.relatives.none') }}
                                </p>
                            }
                            <button
                                mat-stroked-button
                                type="button"
                                data-testid="driver-relative-add"
                                [disabled]="relatives.length >= maxRows"
                                (click)="addRelative()"
                            >
                                <mat-icon>person_add</mat-icon>
                                {{ i18n.t('drivers.relatives.add') }}
                            </button>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="driver-specialDocuments-section"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">description</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.specialDocuments')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content formArrayName="specialDocuments">
                            @for (row of specialDocuments.controls; track row; let i = $index) {
                                <div
                                    class="list-row cols-4"
                                    [formGroupName]="i"
                                    data-testid="driver-specialDocument-row"
                                >
                                    @for (field of specialDocumentFields; track field.name) {
                                        <mat-form-field>
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            @if (field.name === 'type') {
                                                <mat-select
                                                    formControlName="type"
                                                    data-testid="driver-specialDocument-type"
                                                >
                                                    @for (
                                                        option of documentTypeOptions(i);
                                                        track option.code
                                                    ) {
                                                        <mat-option [value]="option.code">{{
                                                            optionLabel(option)
                                                        }}</mat-option>
                                                    }
                                                </mat-select>
                                            } @else {
                                                <input
                                                    matInput
                                                    [type]="field.type"
                                                    [attr.maxlength]="field.maxLength"
                                                    [formControlName]="field.name"
                                                    [attr.data-testid]="
                                                        'driver-specialDocument-' + field.name
                                                    "
                                                />
                                            }
                                            @if (row.controls[field.name].errors; as errors) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'driver-specialDocument-' +
                                                        field.name +
                                                        '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                    <button
                                        mat-icon-button
                                        type="button"
                                        class="delete"
                                        data-testid="driver-specialDocument-remove"
                                        [attr.aria-label]="
                                            i18n.t('drivers.specialDocuments.remove')
                                        "
                                        (click)="removeSpecialDocument(i)"
                                    >
                                        <mat-icon>delete</mat-icon>
                                    </button>
                                </div>
                            } @empty {
                                <p class="hint" data-testid="driver-specialDocuments-empty">
                                    <mat-icon aria-hidden="true">info</mat-icon
                                    >{{ i18n.t('drivers.specialDocuments.none') }}
                                </p>
                            }
                            <button
                                mat-stroked-button
                                type="button"
                                data-testid="driver-specialDocument-add"
                                [disabled]="specialDocuments.length >= maxRows"
                                (click)="addSpecialDocument()"
                            >
                                <mat-icon>note_add</mat-icon>
                                {{ i18n.t('drivers.specialDocuments.add') }}
                            </button>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="driver-phoneNumbers-section"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">call</mat-icon>
                            <mat-card-title>{{
                                i18n.t('drivers.section.phoneNumbers')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content formArrayName="phoneNumbers">
                            @for (row of phoneNumbers.controls; track row; let i = $index) {
                                <div
                                    class="list-row cols-3"
                                    [formGroupName]="i"
                                    data-testid="driver-phoneNumber-row"
                                >
                                    @for (field of phoneNumberFields; track field.name) {
                                        <mat-form-field>
                                            <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                            @if (field.name === 'type') {
                                                <mat-select
                                                    formControlName="type"
                                                    data-testid="driver-phoneNumber-type"
                                                >
                                                    <mat-option [value]="''">–</mat-option>
                                                    @for (
                                                        option of phoneTypeOptions(i);
                                                        track option.code
                                                    ) {
                                                        <mat-option [value]="option.code">{{
                                                            optionLabel(option)
                                                        }}</mat-option>
                                                    }
                                                </mat-select>
                                            } @else {
                                                <input
                                                    matInput
                                                    type="text"
                                                    [attr.maxlength]="field.maxLength"
                                                    [formControlName]="field.name"
                                                    [attr.data-testid]="
                                                        'driver-phoneNumber-' + field.name
                                                    "
                                                />
                                            }
                                            @if (row.controls[field.name].errors; as errors) {
                                                <mat-error
                                                    [attr.data-testid]="
                                                        'driver-phoneNumber-' +
                                                        field.name +
                                                        '-error'
                                                    "
                                                    >{{ fieldErrorText(errors) }}</mat-error
                                                >
                                            }
                                        </mat-form-field>
                                    }
                                    <button
                                        mat-icon-button
                                        type="button"
                                        class="delete"
                                        data-testid="driver-phoneNumber-remove"
                                        [attr.aria-label]="i18n.t('drivers.phoneNumbers.remove')"
                                        (click)="removePhoneNumber(i)"
                                    >
                                        <mat-icon>delete</mat-icon>
                                    </button>
                                </div>
                            } @empty {
                                <p class="hint" data-testid="driver-phoneNumbers-empty">
                                    <mat-icon aria-hidden="true">info</mat-icon
                                    >{{ i18n.t('drivers.phoneNumbers.none') }}
                                </p>
                            }
                            <button
                                mat-stroked-button
                                type="button"
                                data-testid="driver-phoneNumber-add"
                                [disabled]="phoneNumbers.length >= maxRows"
                                (click)="addPhoneNumber()"
                            >
                                <mat-icon>add_call</mat-icon>
                                {{ i18n.t('drivers.phoneNumbers.add') }}
                            </button>
                        </mat-card-content>
                    </mat-card>
                </div>

                <div class="action-bar">
                    <button mat-button type="button" data-testid="driver-cancel" (click)="cancel()">
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="driver-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: [
        `
            .license-classes {
                display: flex;
                flex-direction: column;
                gap: 4px;
                padding-bottom: 16px;
            }

            .group-label {
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .options {
                display: flex;
                flex-wrap: wrap;
                gap: 8px 24px;
            }

            .group-error {
                margin: 0;
                color: var(--mat-sys-error);
                font: var(--mat-sys-body-small);
            }

            .list-row {
                display: grid;
                gap: 0 16px;
                align-items: start;
            }

            .list-row.cols-5 {
                grid-template-columns: repeat(5, minmax(0, 1fr)) auto;
            }

            .list-row.cols-4 {
                grid-template-columns: repeat(4, minmax(0, 1fr)) auto;
            }

            .list-row.cols-3 {
                grid-template-columns: repeat(3, minmax(0, 1fr)) auto;
            }

            .list-row button {
                margin-top: 8px;
            }

            .social-groups {
                display: grid;
                grid-template-columns: repeat(2, minmax(0, 1fr));
                gap: 8px 24px;
            }

            .group {
                min-width: 0;
            }

            .group.full-width {
                grid-column: 1 / -1;
            }

            .group-title {
                margin: 0 0 12px;
                font: var(--mat-sys-title-small);
                color: var(--mat-sys-on-surface-variant);
            }

            @media (max-width: 899px) {
                .social-groups {
                    grid-template-columns: minmax(0, 1fr);
                }

                .list-row.cols-5,
                .list-row.cols-4,
                .list-row.cols-3 {
                    grid-template-columns: minmax(0, 1fr) auto;
                }
            }
        `,
    ],
})
export class DriverFormComponent {
    private readonly service = inject(DriverService);
    private readonly catalogService = inject(VehicleCatalogService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly licenseClassOptions = ['B', 'C1', 'C', 'CE'];
    protected readonly driverId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.driverId !== null;
    protected readonly formError = signal<string | null>(null);
    // Gates the licenseClasses paragraph the same way Material's errorState gates mat-error
    // for the other fields (hidden until touched/submitted) - see the template comment.
    protected readonly attemptedSubmit = signal(false);
    private externalUserId: string | null = null;

    protected readonly insuranceFields = [
        { name: 'dateOfBirth', label: 'drivers.social.dateOfBirth', type: 'date', maxLength: null },
        {
            name: 'placeOfBirth',
            label: 'drivers.social.placeOfBirth',
            type: 'text',
            maxLength: 200,
        },
        { name: 'nationality', label: 'drivers.social.nationality', type: 'text', maxLength: 100 },
        {
            name: 'socialInsuranceNumber',
            label: 'drivers.social.socialInsuranceNumber',
            type: 'text',
            maxLength: 50,
        },
        { name: 'entryDate', label: 'drivers.social.entryDate', type: 'date', maxLength: null },
    ] as const;

    protected readonly bankFields = [
        { name: 'bankName', label: 'drivers.social.bankName', maxLength: 200 },
        { name: 'bankCode', label: 'drivers.social.bankCode', maxLength: 20 },
        { name: 'accountNumber', label: 'drivers.social.accountNumber', maxLength: 34 },
        { name: 'bic', label: 'drivers.social.bic', maxLength: 11 },
        { name: 'iban', label: 'drivers.social.iban', maxLength: 34 },
    ] as const;

    protected readonly clothingFields = [
        { name: 'clothingSize', label: 'drivers.clothing.clothingSize' },
        { name: 'shoeSize', label: 'drivers.clothing.shoeSize' },
        { name: 'gloveSize', label: 'drivers.clothing.gloveSize' },
    ] as const;

    protected readonly relativeFields = [
        { name: 'lastName', label: 'common.lastName', type: 'text', maxLength: 100 },
        { name: 'firstName', label: 'common.firstName', type: 'text', maxLength: 100 },
        {
            name: 'relationship',
            label: 'drivers.relatives.relationship',
            type: 'text',
            maxLength: 100,
        },
        { name: 'dateOfBirth', label: 'drivers.social.dateOfBirth', type: 'date', maxLength: null },
        { name: 'phone', label: 'drivers.relatives.phone', type: 'text', maxLength: 50 },
    ] as const;

    protected readonly specialDocumentFields = [
        { name: 'type', label: 'drivers.specialDocuments.type', type: 'text', maxLength: null },
        {
            name: 'lastInspectionDate',
            label: 'drivers.specialDocuments.lastInspectionDate',
            type: 'date',
            maxLength: null,
        },
        {
            name: 'validUntil',
            label: 'drivers.specialDocuments.validUntil',
            type: 'date',
            maxLength: null,
        },
        {
            name: 'comment',
            label: 'drivers.specialDocuments.comment',
            type: 'text',
            maxLength: 500,
        },
    ] as const;

    protected readonly phoneNumberFields = [
        { name: 'type', label: 'drivers.phoneNumbers.type', maxLength: 50 },
        { name: 'number', label: 'drivers.phoneNumbers.number', maxLength: 50 },
        { name: 'comment', label: 'drivers.phoneNumbers.comment', maxLength: 200 },
    ] as const;

    /** The maintainable phone types ("Fahrer-Stammdaten"), loaded from the shared catalog. */
    private readonly phoneTypes = signal<VehicleCatalogEntry[]>([]);

    /** The maintainable special paper types, from the same catalog. */
    private readonly documentTypes = signal<VehicleCatalogEntry[]>([]);

    /** Active entries plus the row's current code, so an inactive stored type is never lost. */
    protected phoneTypeOptions(index: number): VehicleCatalogOption[] {
        return catalogOptions(
            this.phoneTypes(),
            this.phoneNumbers.at(index).controls.type.value || null,
        );
    }

    /** Same keep-the-stored-code rule as for the phone types. */
    protected documentTypeOptions(index: number): VehicleCatalogOption[] {
        return catalogOptions(
            this.documentTypes(),
            this.specialDocuments.at(index).controls.type.value || null,
        );
    }

    protected optionLabel(option: VehicleCatalogOption): string {
        return option.isActive
            ? option.name
            : this.i18n.t('vehicles.catalogInactive', { name: option.name });
    }

    protected readonly maxRows = MAX_ROWS;

    protected readonly form = this.fb.nonNullable.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        licenseClasses: this.fb.nonNullable.control<string[]>([], Validators.required),
        licenseValidUntil: ['', Validators.required],
        documents: this.fb.nonNullable.group({
            licenseNumber: ['', Validators.maxLength(50)],
            issuingAuthority: ['', Validators.maxLength(500)],
            issueDate: ['', dateFromYear(MIN_YEAR)],
        }),
        clothingSizes: this.fb.nonNullable.group({
            clothingSize: ['', Validators.maxLength(50)],
            shoeSize: ['', Validators.maxLength(50)],
            gloveSize: ['', Validators.maxLength(50)],
        }),
        relatives: this.fb.nonNullable.array<FormGroup<RelativeControls>>([]),
        specialDocuments: this.fb.nonNullable.array<FormGroup<SpecialDocumentControls>>([]),
        phoneNumbers: this.fb.nonNullable.array<FormGroup<PhoneNumberControls>>([]),
        socialInsurance: this.fb.nonNullable.group({
            dateOfBirth: ['', dateFromYear(MIN_YEAR)],
            placeOfBirth: ['', Validators.maxLength(200)],
            nationality: ['', Validators.maxLength(100)],
            socialInsuranceNumber: ['', Validators.maxLength(50)],
            entryDate: ['', dateFromYear(MIN_YEAR)],
            healthInsurance: ['', Validators.maxLength(500)],
            bankName: ['', Validators.maxLength(200)],
            bankCode: ['', Validators.maxLength(20)],
            accountNumber: ['', Validators.maxLength(34)],
            bic: ['', Validators.maxLength(11)],
            iban: ['', Validators.maxLength(34)],
            familyDoctor: ['', Validators.maxLength(500)],
            familyDoctorPhone: ['', Validators.maxLength(50)],
        }),
    });

    protected readonly social = this.form.controls.socialInsurance;
    protected readonly documents = this.form.controls.documents;

    protected get relatives(): FormArray<FormGroup<RelativeControls>> {
        return this.form.controls.relatives;
    }

    protected get specialDocuments(): FormArray<FormGroup<SpecialDocumentControls>> {
        return this.form.controls.specialDocuments;
    }

    protected get phoneNumbers(): FormArray<FormGroup<PhoneNumberControls>> {
        return this.form.controls.phoneNumbers;
    }

    protected addRelative(relative?: DriverRelative): void {
        this.relatives.push(
            this.fb.nonNullable.group({
                lastName: [relative?.lastName ?? '', Validators.required],
                firstName: [relative?.firstName ?? ''],
                relationship: [relative?.relationship ?? ''],
                dateOfBirth: [relative?.dateOfBirth?.slice(0, 10) ?? '', dateFromYear(MIN_YEAR)],
                phone: [relative?.phone ?? ''],
            }),
        );
    }

    protected removeRelative(index: number): void {
        this.relatives.removeAt(index);
    }

    protected addSpecialDocument(document?: DriverSpecialDocument): void {
        this.specialDocuments.push(
            this.fb.nonNullable.group({
                type: [document?.type ?? '', Validators.required],
                lastInspectionDate: [
                    document?.lastInspectionDate?.slice(0, 10) ?? '',
                    dateFromYear(MIN_YEAR),
                ],
                validUntil: [document?.validUntil?.slice(0, 10) ?? '', dateFromYear(MIN_YEAR)],
                comment: [document?.comment ?? ''],
            }),
        );
    }

    protected removeSpecialDocument(index: number): void {
        this.specialDocuments.removeAt(index);
    }

    protected addPhoneNumber(phoneNumber?: DriverPhoneNumber): void {
        this.phoneNumbers.push(
            this.fb.nonNullable.group({
                number: [phoneNumber?.number ?? '', Validators.required],
                type: [phoneNumber?.type ?? ''],
                comment: [phoneNumber?.comment ?? ''],
            }),
        );
    }

    protected removePhoneNumber(index: number): void {
        this.phoneNumbers.removeAt(index);
    }

    constructor() {
        this.session.ready
            .pipe(
                switchMap(() =>
                    forkJoin([
                        this.catalogService.list('PhoneType'),
                        this.catalogService.list('DriverDocumentType'),
                    ]),
                ),
            )
            .subscribe({
                next: ([phoneTypes, documentTypes]) => {
                    this.phoneTypes.set(phoneTypes);
                    this.documentTypes.set(documentTypes);
                },
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        `${this.i18n.t('drivers.error.catalogs')} (HTTP ${error.status})`,
                    ),
            });

        if (this.isEditMode) {
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.driverId!)))
                .subscribe({
                    next: (driver: Driver) => {
                        this.externalUserId = driver.externalUserId;
                        this.form.patchValue({
                            firstName: driver.firstName,
                            lastName: driver.lastName,
                            licenseClasses: driver.licenseClasses,
                            licenseValidUntil: driver.licenseValidUntil,
                            documents: this.toDocumentsFormValue(driver.documents),
                            clothingSizes: this.toClothingSizesFormValue(driver.clothingSizes),
                            socialInsurance: this.toSocialFormValue(driver.socialInsurance),
                        });
                        this.relatives.clear();
                        (driver.relatives ?? []).forEach((relative) => this.addRelative(relative));
                        this.specialDocuments.clear();
                        (driver.specialDocuments ?? []).forEach((document) =>
                            this.addSpecialDocument(document),
                        );
                        this.phoneNumbers.clear();
                        (driver.phoneNumbers ?? []).forEach((phoneNumber) =>
                            this.addPhoneNumber(phoneNumber),
                        );
                    },
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            this.describeFailure(error, this.i18n.t('drivers.error.load')),
                        ),
                });
        }
    }

    // See VehicleFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['minYear']) {
            return this.i18n.t(
                'drivers.social.dateTooEarly',
                errors['minYear'] as { year: number },
            );
        }
        return this.i18n.t('common.fieldInvalid');
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

        const {
            socialInsurance: _social,
            documents: _documents,
            clothingSizes: _clothingSizes,
            relatives: _relatives,
            specialDocuments: _specialDocuments,
            phoneNumbers: _phoneNumbers,
            ...basic
        } = this.form.getRawValue();
        const request: DriverWriteRequest = {
            ...basic,
            externalUserId: this.externalUserId,
            socialInsurance: this.socialRequest(),
            documents: this.documentsRequest(),
            clothingSizes: this.clothingSizesRequest(),
            relatives: this.relativesRequest(),
            specialDocuments: this.specialDocumentsRequest(),
            phoneNumbers: this.phoneNumbersRequest(),
        };
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                this.isEditMode
                    ? this.service.update(this.driverId!, request)
                    : this.service.create(request),
            ),
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
    // same comment in full. Field keys arrive PascalCase (.NET property names), nested ones
    // dotted ("SocialInsurance.DateOfBirth") and list rows indexed ("Relatives[1].LastName") -
    // rewrite the index into a path segment and lowercase the first letter of every segment
    // to find the control, like CustomerLocationFormComponent does.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                // "Relatives[1].LastName" -> "relatives.1.lastName"
                const controlPath = field
                    .replace(/\[(\d+)\]/g, '.$1')
                    .split('.')
                    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
                    .join('.');
                const control = this.form.get(controlPath);
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

        this.formError.set(this.describeFailure(error, this.i18n.t('drivers.error.save')));
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

    private toSocialFormValue(social: DriverSocialInsurance | null | undefined) {
        return {
            dateOfBirth: social?.dateOfBirth?.slice(0, 10) ?? '',
            placeOfBirth: social?.placeOfBirth ?? '',
            nationality: social?.nationality ?? '',
            socialInsuranceNumber: social?.socialInsuranceNumber ?? '',
            entryDate: social?.entryDate?.slice(0, 10) ?? '',
            healthInsurance: social?.healthInsurance ?? '',
            bankName: social?.bankName ?? '',
            bankCode: social?.bankCode ?? '',
            accountNumber: social?.accountNumber ?? '',
            bic: social?.bic ?? '',
            iban: social?.iban ?? '',
            familyDoctor: social?.familyDoctor ?? '',
            familyDoctorPhone: social?.familyDoctorPhone ?? '',
        };
    }

    private socialRequest(): DriverSocialInsurance {
        const value = this.social.getRawValue();
        const text = (input: string) => input.trim() || null;
        return {
            dateOfBirth: value.dateOfBirth || null,
            placeOfBirth: text(value.placeOfBirth),
            nationality: text(value.nationality),
            socialInsuranceNumber: text(value.socialInsuranceNumber),
            entryDate: value.entryDate || null,
            healthInsurance: text(value.healthInsurance),
            bankName: text(value.bankName),
            bankCode: text(value.bankCode),
            accountNumber: text(value.accountNumber),
            bic: text(value.bic)?.toUpperCase() ?? null,
            iban: text(value.iban)?.toUpperCase() ?? null,
            familyDoctor: text(value.familyDoctor),
            familyDoctorPhone: text(value.familyDoctorPhone),
        };
    }

    private toDocumentsFormValue(documents: DriverDocuments | null | undefined) {
        return {
            licenseNumber: documents?.licenseNumber ?? '',
            issuingAuthority: documents?.issuingAuthority ?? '',
            issueDate: documents?.issueDate?.slice(0, 10) ?? '',
        };
    }

    private toClothingSizesFormValue(sizes: DriverClothingSizes | null | undefined) {
        return {
            clothingSize: sizes?.clothingSize ?? '',
            shoeSize: sizes?.shoeSize ?? '',
            gloveSize: sizes?.gloveSize ?? '',
        };
    }

    private documentsRequest(): DriverDocuments {
        const value = this.documents.getRawValue();
        const text = (input: string) => input.trim() || null;
        return {
            licenseNumber: text(value.licenseNumber),
            issuingAuthority: text(value.issuingAuthority),
            issueDate: value.issueDate || null,
        };
    }

    private clothingSizesRequest(): DriverClothingSizes {
        const value = this.form.controls.clothingSizes.getRawValue();
        const text = (input: string) => input.trim() || null;
        return {
            clothingSize: text(value.clothingSize),
            shoeSize: text(value.shoeSize),
            gloveSize: text(value.gloveSize),
        };
    }

    private relativesRequest(): DriverRelative[] {
        const text = (input: string) => input.trim() || null;
        return this.relatives.getRawValue().map((relative) => ({
            lastName: relative.lastName.trim(),
            firstName: text(relative.firstName),
            relationship: text(relative.relationship),
            dateOfBirth: relative.dateOfBirth || null,
            phone: text(relative.phone),
        }));
    }

    private specialDocumentsRequest(): DriverSpecialDocument[] {
        const text = (input: string) => input.trim() || null;
        return this.specialDocuments.getRawValue().map((document) => ({
            type: document.type.trim(),
            lastInspectionDate: document.lastInspectionDate || null,
            validUntil: document.validUntil || null,
            comment: text(document.comment),
        }));
    }

    private phoneNumbersRequest(): DriverPhoneNumber[] {
        const text = (input: string) => input.trim() || null;
        return this.phoneNumbers.getRawValue().map((phoneNumber) => ({
            number: phoneNumber.number.trim(),
            type: text(phoneNumber.type),
            comment: text(phoneNumber.comment),
        }));
    }
}
