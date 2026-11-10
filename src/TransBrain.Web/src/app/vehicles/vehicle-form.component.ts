import { HttpErrorResponse } from '@angular/common/http';
import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import {
    AbstractControl,
    FormBuilder,
    FormControl,
    ReactiveFormsModule,
    ValidationErrors,
    Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { filter, forkJoin, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { Driver, DriverService } from '../drivers/driver.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { InspectionUrgency, formatDate, formatDateTime, inspectionUrgency } from './vehicle-format';
import {
    VehicleCatalogEntry,
    VehicleCatalogOption,
    VehicleCatalogService,
    catalogIndex,
    catalogName,
    catalogOptions,
} from './vehicle-catalog.service';
import {
    VehicleDocumentDialogComponent,
    VehicleDocumentDialogData,
} from './vehicle-document-dialog.component';
import { VehicleDocument, VehicleDocumentService } from './vehicle-document.service';
import { amountInRange, dateFromYear } from './vehicle-validators';
import {
    EMPTY_VEHICLE_COMMERCIAL,
    EMPTY_VEHICLE_EQUIPMENT,
    MARBLE_RACK_MATERIALS,
    MarbleRackMaterial,
    VEHICLE_TYPES,
    Vehicle,
    VehicleEquipment,
    VehicleService,
    VehicleStatus,
    VehicleType,
    VehicleWriteRequest,
} from './vehicle.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

/** Same cap as the list pages - the default-driver picker must offer every driver. */
const DRIVER_PAGE_SIZE = 100;

/** Optional numbers may stay empty, but a filled-in value must be greater than zero. */
function positiveOrEmpty(control: AbstractControl<number | null>): ValidationErrors | null {
    const value = control.value;
    return value === null || Number(value) > 0 ? null : { positive: true };
}

/** Optional whole number: empty is fine, a filled-in value must be an integer within [min, max]. */
function wholeNumberInRange(min: number, max: number) {
    return (control: AbstractControl<number | null>): ValidationErrors | null => {
        const value = control.value;
        if (value === null || (value as unknown) === '') {
            return null;
        }
        const number = Number(value);
        return Number.isInteger(number) && number >= min && number <= max
            ? null
            : { range: { min, max } };
    };
}

type EquipmentFlag = {
    [K in keyof VehicleEquipment]: VehicleEquipment[K] extends boolean ? K : never;
}[keyof VehicleEquipment];

type EquipmentNumber = {
    [K in keyof VehicleEquipment]: VehicleEquipment[K] extends number | null ? K : never;
}[keyof VehicleEquipment];

interface EquipmentNumberField {
    name: EquipmentNumber;
    min: number;
    max: number;
}

const EQUIPMENT_FLAGS: readonly EquipmentFlag[] = [
    'hasPartitionWall',
    'hasPalletBasket',
    'hasDangerousGoodsEquipment',
    'isDangerousGoodsApproved',
    'hasLiftAxle',
    'hasDoubleDeck',
    'hasDoubleDeckBeams',
    'hasSmallLadder',
    'hasLargeLadder',
];

const LOADING_SPACE_FIELDS: readonly EquipmentNumberField[] = [
    { name: 'interiorLengthMm', min: 1, max: 30000 },
    { name: 'interiorWidthMm', min: 1, max: 5000 },
    { name: 'interiorHeightMm', min: 1, max: 5000 },
    { name: 'tankVolumeLiters', min: 1, max: 100000 },
];

const LOAD_SECURING_FIELDS: readonly EquipmentNumberField[] = [
    { name: 'supportPoleCount', min: 0, max: 999 },
    { name: 'loadBarCount', min: 0, max: 999 },
    { name: 'lashingStrapCount', min: 0, max: 999 },
    { name: 'chainTensionerCount', min: 0, max: 999 },
    { name: 'palletCount', min: 0, max: 999 },
];

const MARBLE_RACK_COUNT_FIELD: EquipmentNumberField = { name: 'marbleRackCount', min: 0, max: 999 };

const EQUIPMENT_NUMBER_FIELDS: readonly EquipmentNumberField[] = [
    ...LOADING_SPACE_FIELDS,
    ...LOAD_SECURING_FIELDS,
    MARBLE_RACK_COUNT_FIELD,
];

/** "Equipment.TankVolumeLiters" -> "equipment.tankVolumeLiters". */
function toControlPath(field: string): string {
    return field
        .split('.')
        .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
        .join('.');
}

const MIN_YEAR = 1900;
const DATA_SHEET_MAX_LENGTH = 10000;
const BASE_DOCUMENT_COLUMNS = [
    'type',
    'lastCheckedOn',
    'validUntil',
    'status',
    'remark',
    'modified',
];

const DOCUMENT_STATUS: Record<InspectionUrgency, { tone: string; key: TranslationKey }> = {
    overdue: { tone: 'error', key: 'vehicles.documents.status.expired' },
    dueSoon: { tone: 'warning', key: 'vehicles.documents.status.expiresSoon' },
    ok: { tone: 'success', key: 'vehicles.documents.status.valid' },
};

function textOrNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed === '' ? null : trimmed;
}

function numberOrNull(value: number | null): number | null {
    return value === null || (value as unknown) === '' ? null : Number(value);
}

@Component({
    selector: 'app-vehicle-form',
    standalone: true,
    imports: [
        NgTemplateOutlet,
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatCheckboxModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatRadioModule,
        MatSelectModule,
        MatTableModule,
        MatTooltipModule,
    ],
    template: `
        <div class="page">
            <header class="page-header">
                <div class="title-block">
                    <div class="title-row">
                        <h1>{{ isEditMode ? i18n.t('vehicles.edit') : i18n.t('vehicles.new') }}</h1>
                        @if (vehicle(); as v) {
                            <mat-chip-set>
                                <mat-chip
                                    class="chip"
                                    [attr.data-tone]="statusTone(v.status)"
                                    data-testid="vehicle-status"
                                >
                                    {{ i18n.t(statusKey(v.status)) }}
                                </mat-chip>
                            </mat-chip-set>
                        }
                    </div>
                    @if (modifiedText(); as text) {
                        <p class="audit" data-testid="vehicle-modified">
                            <mat-icon aria-hidden="true">history</mat-icon>{{ text }}
                        </p>
                    }
                </div>
                @if (vehicle(); as v) {
                    @if (session.can('masterData.write')) {
                        <div class="status-actions">
                            @switch (v.status) {
                                @case ('Available') {
                                    <button
                                        mat-stroked-button
                                        type="button"
                                        data-testid="vehicle-status-workshop"
                                        [disabled]="busy()"
                                        (click)="changeStatus('InWorkshop')"
                                    >
                                        <mat-icon>build</mat-icon>
                                        {{ i18n.t('vehicles.action.workshop') }}
                                    </button>
                                }
                                @case ('InWorkshop') {
                                    <button
                                        mat-stroked-button
                                        type="button"
                                        data-testid="vehicle-status-available"
                                        [disabled]="busy()"
                                        (click)="changeStatus('Available')"
                                    >
                                        <mat-icon>check_circle</mat-icon>
                                        {{ i18n.t('vehicles.action.available') }}
                                    </button>
                                }
                                @default {
                                    <p class="hint" data-testid="vehicle-status-hint">
                                        <mat-icon aria-hidden="true">info</mat-icon>
                                        {{ i18n.t('vehicles.decommissionedHint') }}
                                    </p>
                                }
                            }
                            @if (v.status !== 'Decommissioned') {
                                <button
                                    mat-stroked-button
                                    type="button"
                                    class="danger"
                                    data-testid="vehicle-status-decommission"
                                    [disabled]="busy()"
                                    (click)="decommission(v)"
                                >
                                    <mat-icon>block</mat-icon>
                                    {{ i18n.t('vehicles.action.decommission') }}
                                </button>
                            }
                        </div>
                    }
                }
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="vehicle-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">badge</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.general')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.licensePlate') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="licensePlate"
                                    data-testid="vehicle-licensePlate"
                                />
                                @if (form.controls.licensePlate.errors; as errors) {
                                    <mat-error data-testid="vehicle-licensePlate-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <div class="checkbox-cell">
                                <mat-checkbox
                                    formControlName="isLicensePlateConfidential"
                                    data-testid="vehicle-isLicensePlateConfidential"
                                >
                                    {{ i18n.t('vehicles.isLicensePlateConfidential') }}
                                </mat-checkbox>
                            </div>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.type') }}</mat-label>
                                <mat-select formControlName="type" data-testid="vehicle-type">
                                    @for (option of vehicleTypes; track option) {
                                        <mat-option [value]="option">{{
                                            i18n.t(typeKey(option))
                                        }}</mat-option>
                                    }
                                </mat-select>
                                @if (form.controls.type.errors; as errors) {
                                    <mat-error data-testid="vehicle-type-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.vehicleClass') }}</mat-label>
                                <mat-select
                                    formControlName="vehicleClass"
                                    data-testid="vehicle-vehicleClass"
                                >
                                    <mat-option [value]="null">–</mat-option>
                                    @for (option of vehicleClassOptions(); track option.code) {
                                        <mat-option [value]="option.code">{{
                                            optionLabel(option)
                                        }}</mat-option>
                                    }
                                </mat-select>
                                @if (form.controls.vehicleClass.errors; as errors) {
                                    <mat-error data-testid="vehicle-vehicleClass-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.make') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="make"
                                    maxlength="100"
                                    data-testid="vehicle-make"
                                />
                                @if (form.controls.make.errors; as errors) {
                                    <mat-error data-testid="vehicle-make-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.model') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="model"
                                    maxlength="100"
                                    data-testid="vehicle-model"
                                />
                                @if (form.controls.model.errors; as errors) {
                                    <mat-error data-testid="vehicle-model-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.carrierGroup') }}</mat-label>
                                <mat-select
                                    formControlName="carrierGroup"
                                    data-testid="vehicle-carrierGroup"
                                >
                                    <mat-option [value]="null">–</mat-option>
                                    @for (option of carrierGroupOptions(); track option.code) {
                                        <mat-option [value]="option.code">{{
                                            optionLabel(option)
                                        }}</mat-option>
                                    }
                                </mat-select>
                                @if (form.controls.carrierGroup.errors; as errors) {
                                    <mat-error data-testid="vehicle-carrierGroup-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.defaultDriver') }}</mat-label>
                                <mat-select
                                    formControlName="defaultDriverId"
                                    data-testid="vehicle-defaultDriverId"
                                >
                                    <mat-option [value]="null">{{
                                        i18n.t('vehicles.noDefaultDriver')
                                    }}</mat-option>
                                    @for (driver of drivers(); track driver.id) {
                                        <mat-option [value]="driver.id"
                                            >{{ driver.lastName }},
                                            {{ driver.firstName }}</mat-option
                                        >
                                    }
                                </mat-select>
                                @if (driverError()) {
                                    <mat-hint data-testid="vehicle-defaultDriverId-hint">
                                        {{ i18n.t('vehicles.error.drivers') }}
                                    </mat-hint>
                                }
                                @if (form.controls.defaultDriverId.errors; as errors) {
                                    <mat-error data-testid="vehicle-defaultDriverId-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">settings</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.technical')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.manufacturedOn') }}</mat-label>
                                <input
                                    matInput
                                    type="month"
                                    formControlName="manufacturedOn"
                                    data-testid="vehicle-manufacturedOn"
                                />
                                @if (form.controls.manufacturedOn.errors; as errors) {
                                    <mat-error data-testid="vehicle-manufacturedOn-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.powerKw') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="powerKw"
                                    data-testid="vehicle-powerKw"
                                />
                                @if (form.controls.powerKw.errors; as errors) {
                                    <mat-error data-testid="vehicle-powerKw-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.chassisNumber') }}</mat-label>
                                <input
                                    matInput
                                    class="uppercase"
                                    formControlName="chassisNumber"
                                    maxlength="30"
                                    data-testid="vehicle-chassisNumber"
                                />
                                @if (form.controls.chassisNumber.errors; as errors) {
                                    <mat-error data-testid="vehicle-chassisNumber-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.engineNumber') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="engineNumber"
                                    maxlength="30"
                                    data-testid="vehicle-engineNumber"
                                />
                                @if (form.controls.engineNumber.errors; as errors) {
                                    <mat-error data-testid="vehicle-engineNumber-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.nextInspection') }}</mat-label>
                                <input
                                    matInput
                                    type="date"
                                    formControlName="nextInspectionDue"
                                    data-testid="vehicle-nextInspectionDue"
                                />
                                @if (form.controls.nextInspectionDue.errors; as errors) {
                                    <mat-error data-testid="vehicle-nextInspectionDue-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">straighten</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.dimensions')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields three">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.lengthMm') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="lengthMm"
                                    data-testid="vehicle-lengthMm"
                                />
                                @if (form.controls.lengthMm.errors; as errors) {
                                    <mat-error data-testid="vehicle-lengthMm-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.widthMm') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="widthMm"
                                    data-testid="vehicle-widthMm"
                                />
                                @if (form.controls.widthMm.errors; as errors) {
                                    <mat-error data-testid="vehicle-widthMm-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.heightMm') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="heightMm"
                                    data-testid="vehicle-heightMm"
                                />
                                @if (form.controls.heightMm.errors; as errors) {
                                    <mat-error data-testid="vehicle-heightMm-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.loadMeters') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    step="0.01"
                                    formControlName="loadMeters"
                                    data-testid="vehicle-loadMeters"
                                />
                                @if (form.controls.loadMeters.errors; as errors) {
                                    <mat-error data-testid="vehicle-loadMeters-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">scale</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.weights')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.payloadKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="payloadKg"
                                    data-testid="vehicle-payloadKg"
                                />
                                @if (form.controls.payloadKg.errors; as errors) {
                                    <mat-error data-testid="vehicle-payloadKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.curbWeightKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="curbWeightKg"
                                    data-testid="vehicle-curbWeightKg"
                                />
                                @if (form.controls.curbWeightKg.errors; as errors) {
                                    <mat-error data-testid="vehicle-curbWeightKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.grossWeightKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="grossWeightKg"
                                    data-testid="vehicle-grossWeightKg"
                                />
                                @if (form.controls.grossWeightKg.errors; as errors) {
                                    <mat-error data-testid="vehicle-grossWeightKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.maxFifthWheelLoadKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="maxFifthWheelLoadKg"
                                    data-testid="vehicle-maxFifthWheelLoadKg"
                                />
                                @if (form.controls.maxFifthWheelLoadKg.errors; as errors) {
                                    <mat-error data-testid="vehicle-maxFifthWheelLoadKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        formGroupName="equipment"
                        data-testid="vehicle-equipment"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">construction</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.equipment')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="equipment-groups">
                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.equipment.group.body') }}
                                </h3>
                                @for (flag of equipmentFlags; track flag) {
                                    <mat-checkbox
                                        [formControlName]="flag"
                                        [attr.data-testid]="'vehicle-' + flag"
                                    >
                                        {{ i18n.t(equipmentKey(flag)) }}
                                    </mat-checkbox>
                                }
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.equipment.group.loadingSpace') }}
                                </h3>
                                @for (field of loadingSpaceFields; track field.name) {
                                    <ng-container
                                        *ngTemplateOutlet="
                                            numberField;
                                            context: { $implicit: field }
                                        "
                                    />
                                }
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.equipment.group.loadSecuring') }}
                                </h3>
                                @for (field of loadSecuringFields; track field.name) {
                                    <ng-container
                                        *ngTemplateOutlet="
                                            numberField;
                                            context: { $implicit: field }
                                        "
                                    />
                                }
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.equipment.group.marbleRacks') }}
                                </h3>
                                <ng-container
                                    *ngTemplateOutlet="
                                        numberField;
                                        context: { $implicit: marbleRackCountField }
                                    "
                                />
                                <span id="vehicle-marbleRackMaterial-label" class="radio-label">{{
                                    i18n.t('vehicles.equipment.marbleRackMaterial')
                                }}</span>
                                <mat-radio-group
                                    class="radio-group"
                                    formControlName="marbleRackMaterial"
                                    aria-labelledby="vehicle-marbleRackMaterial-label"
                                    data-testid="vehicle-marbleRackMaterial"
                                >
                                    <mat-radio-button
                                        [value]="null"
                                        data-testid="vehicle-marbleRackMaterial-none"
                                    >
                                        {{
                                            i18n.t(
                                                'vehicles.equipment.marbleRackMaterialOption.none'
                                            )
                                        }}
                                    </mat-radio-button>
                                    @for (material of marbleRackMaterials; track material) {
                                        <mat-radio-button
                                            [value]="material"
                                            [attr.data-testid]="
                                                'vehicle-marbleRackMaterial-' + material
                                            "
                                        >
                                            {{ i18n.t(materialKey(material)) }}
                                        </mat-radio-button>
                                    }
                                </mat-radio-group>
                                @if (equipment.controls.marbleRackMaterial.errors; as errors) {
                                    <mat-error
                                        class="radio-error"
                                        data-testid="vehicle-marbleRackMaterial-error"
                                        >{{ fieldErrorText(errors) }}</mat-error
                                    >
                                }
                            </section>
                        </mat-card-content>

                        <ng-template #numberField let-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t(equipmentKey(field.name)) }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    [formControl]="numberControl(field)"
                                    [attr.data-testid]="'vehicle-' + field.name"
                                />
                                @if (numberControl(field).errors; as errors) {
                                    <mat-error
                                        [attr.data-testid]="'vehicle-' + field.name + '-error'"
                                        >{{ fieldErrorText(errors) }}</mat-error
                                    >
                                }
                            </mat-form-field>
                        </ng-template>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        formGroupName="commercial"
                        data-testid="vehicle-commercial"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">euro</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.commercial')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{
                                    i18n.t('vehicles.commercial.purchasePrice')
                                }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    step="0.01"
                                    formControlName="purchasePrice"
                                    data-testid="vehicle-purchasePrice"
                                />
                                <span matTextSuffix>€</span>
                                @if (commercial.controls.purchasePrice.errors; as errors) {
                                    <mat-error data-testid="vehicle-purchasePrice-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.commercial.purchasedOn') }}</mat-label>
                                <input
                                    matInput
                                    type="date"
                                    formControlName="purchasedOn"
                                    data-testid="vehicle-purchasedOn"
                                />
                                @if (commercial.controls.purchasedOn.errors; as errors) {
                                    <mat-error data-testid="vehicle-purchasedOn-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{
                                    i18n.t('vehicles.commercial.purchaseOdometerKm')
                                }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="purchaseOdometerKm"
                                    data-testid="vehicle-purchaseOdometerKm"
                                />
                                @if (commercial.controls.purchaseOdometerKm.errors; as errors) {
                                    <mat-error data-testid="vehicle-purchaseOdometerKm-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{
                                    i18n.t('vehicles.commercial.firstRegisteredOn')
                                }}</mat-label>
                                <input
                                    matInput
                                    type="date"
                                    formControlName="firstRegisteredOn"
                                    data-testid="vehicle-firstRegisteredOn"
                                />
                                @if (commercial.controls.firstRegisteredOn.errors; as errors) {
                                    <mat-error data-testid="vehicle-firstRegisteredOn-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{
                                    i18n.t('vehicles.commercial.usefulLifeYears')
                                }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="usefulLifeYears"
                                    data-testid="vehicle-usefulLifeYears"
                                />
                                @if (commercial.controls.usefulLifeYears.errors; as errors) {
                                    <mat-error data-testid="vehicle-usefulLifeYears-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{
                                    i18n.t('vehicles.commercial.usefulLifeKm')
                                }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="usefulLifeKm"
                                    data-testid="vehicle-usefulLifeKm"
                                />
                                @if (commercial.controls.usefulLifeKm.errors; as errors) {
                                    <mat-error data-testid="vehicle-usefulLifeKm-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.commercial.costCenter') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="costCenter"
                                    maxlength="30"
                                    data-testid="vehicle-costCenter"
                                />
                                @if (commercial.controls.costCenter.errors; as errors) {
                                    <mat-error data-testid="vehicle-costCenter-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        formGroupName="commercial"
                        data-testid="vehicle-insurance"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">shield</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.insurance')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="insurance-groups">
                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.insurance.vehicle') }}
                                </h3>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('vehicles.insurance.insurer') }}</mat-label>
                                    <mat-select
                                        formControlName="vehicleInsurer"
                                        data-testid="vehicle-vehicleInsurer"
                                    >
                                        <mat-option [value]="null">–</mat-option>
                                        @for (option of vehicleInsurerOptions(); track option.code) {
                                            <mat-option [value]="option.code">{{
                                                optionLabel(option)
                                            }}</mat-option>
                                        }
                                    </mat-select>
                                    @if (commercial.controls.vehicleInsurer.errors; as errors) {
                                        <mat-error data-testid="vehicle-vehicleInsurer-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('vehicles.insurance.policyNumbers')
                                    }}</mat-label>
                                    <textarea
                                        matInput
                                        rows="3"
                                        formControlName="vehicleInsurancePolicyNumbers"
                                        maxlength="500"
                                        data-testid="vehicle-vehicleInsurancePolicyNumbers"
                                    ></textarea>
                                    @if (
                                        commercial.controls.vehicleInsurancePolicyNumbers.errors;
                                        as errors
                                    ) {
                                        <mat-error
                                            data-testid="vehicle-vehicleInsurancePolicyNumbers-error"
                                            >{{ fieldErrorText(errors) }}</mat-error
                                        >
                                    }
                                </mat-form-field>
                            </section>

                            <section class="group">
                                <h3 class="group-title">
                                    {{ i18n.t('vehicles.insurance.transport') }}
                                </h3>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('vehicles.insurance.insurer') }}</mat-label>
                                    <mat-select
                                        formControlName="transportInsurer"
                                        data-testid="vehicle-transportInsurer"
                                    >
                                        <mat-option [value]="null">–</mat-option>
                                        @for (
                                            option of transportInsurerOptions();
                                            track option.code
                                        ) {
                                            <mat-option [value]="option.code">{{
                                                optionLabel(option)
                                            }}</mat-option>
                                        }
                                    </mat-select>
                                    @if (commercial.controls.transportInsurer.errors; as errors) {
                                        <mat-error data-testid="vehicle-transportInsurer-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{
                                        i18n.t('vehicles.insurance.policyNumbers')
                                    }}</mat-label>
                                    <textarea
                                        matInput
                                        rows="3"
                                        formControlName="transportInsurancePolicyNumbers"
                                        maxlength="500"
                                        data-testid="vehicle-transportInsurancePolicyNumbers"
                                    ></textarea>
                                    @if (
                                        commercial.controls.transportInsurancePolicyNumbers.errors;
                                        as errors
                                    ) {
                                        <mat-error
                                            data-testid="vehicle-transportInsurancePolicyNumbers-error"
                                            >{{ fieldErrorText(errors) }}</mat-error
                                        >
                                    }
                                </mat-form-field>
                            </section>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="vehicle-documents"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">verified</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.documents')
                            }}</mat-card-title>
                            @if (isEditMode && session.can('masterData.write')) {
                                <button
                                    mat-stroked-button
                                    type="button"
                                    class="card-action"
                                    data-testid="vehicle-document-add"
                                    (click)="openDocumentDialog(null)"
                                >
                                    <mat-icon>add</mat-icon>
                                    {{ i18n.t('vehicles.documents.add') }}
                                </button>
                            }
                        </mat-card-header>
                        <mat-card-content>
                            @if (!isEditMode) {
                                <p class="hint" data-testid="vehicle-documents-hint">
                                    <mat-icon aria-hidden="true">info</mat-icon>
                                    {{ i18n.t('vehicles.documents.hint') }}
                                </p>
                            } @else {
                                @if (documentError(); as message) {
                                    <p class="error-banner" data-testid="vehicle-documents-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (documents().length > 0) {
                                    <div class="table-scroll">
                                        <table mat-table [dataSource]="documents()">
                                            <ng-container matColumnDef="type">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('vehicles.documents.type') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let d"
                                                    class="document-type"
                                                    data-testid="vehicle-document-type"
                                                >
                                                    {{ documentTypeName(d) }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="lastCheckedOn">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('vehicles.documents.lastCheckedOn') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let d"
                                                    data-testid="vehicle-document-lastCheckedOn"
                                                >
                                                    {{ date(d.lastCheckedOn) }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="validUntil">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('vehicles.documents.validUntil') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let d"
                                                    data-testid="vehicle-document-validUntil"
                                                >
                                                    {{ date(d.validUntil) }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="status">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('common.status') }}
                                                </th>
                                                <td mat-cell *matCellDef="let d">
                                                    @if (d.validUntil) {
                                                        <mat-chip-set>
                                                            <mat-chip
                                                                class="chip"
                                                                [attr.data-tone]="
                                                                    documentStatus(d.validUntil).tone
                                                                "
                                                                data-testid="vehicle-document-status"
                                                                >{{
                                                                    i18n.t(
                                                                        documentStatus(d.validUntil)
                                                                            .key
                                                                    )
                                                                }}</mat-chip
                                                            >
                                                        </mat-chip-set>
                                                    }
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="remark">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('vehicles.documents.remark') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let d"
                                                    class="remark"
                                                    data-testid="vehicle-document-remark"
                                                >
                                                    {{ d.remark }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="modified">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('vehicles.documents.modified') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let d"
                                                    class="modified"
                                                    data-testid="vehicle-document-modified"
                                                >
                                                    {{ documentModified(d) }}
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
                                                <td mat-cell *matCellDef="let d" class="actions-cell">
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        data-testid="vehicle-document-edit"
                                                        [matTooltip]="i18n.t('common.edit')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.edit') +
                                                            ' ' +
                                                            documentTypeName(d)
                                                        "
                                                        (click)="openDocumentDialog(d)"
                                                    >
                                                        <mat-icon>edit</mat-icon>
                                                    </button>
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        class="delete"
                                                        data-testid="vehicle-document-delete"
                                                        [matTooltip]="i18n.t('common.delete')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.delete') +
                                                            ' ' +
                                                            documentTypeName(d)
                                                        "
                                                        (click)="deleteDocument(d)"
                                                    >
                                                        <mat-icon>delete</mat-icon>
                                                    </button>
                                                </td>
                                            </ng-container>
                                            <tr
                                                mat-header-row
                                                *matHeaderRowDef="documentColumns()"
                                            ></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: documentColumns()"
                                                data-testid="vehicle-document-row"
                                            ></tr>
                                        </table>
                                    </div>
                                } @else if (documentsLoaded()) {
                                    <p class="hint" data-testid="vehicle-documents-empty">
                                        <mat-icon aria-hidden="true">verified</mat-icon>
                                        {{ i18n.t('vehicles.documents.empty') }}
                                    </p>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined" class="full-width">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">notes</mat-icon>
                            <mat-card-title>{{ i18n.t('vehicles.notes') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields one">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.notes') }}</mat-label>
                                <textarea
                                    matInput
                                    rows="5"
                                    formControlName="notes"
                                    maxlength="2000"
                                    data-testid="vehicle-notes"
                                ></textarea>
                                <mat-hint align="end"
                                    >{{ form.controls.notes.value.length }} / 2000</mat-hint
                                >
                                @if (form.controls.notes.errors; as errors) {
                                    <mat-error data-testid="vehicle-notes-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="vehicle-dataSheet-card"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">article</mat-icon>
                            <mat-card-title>{{
                                i18n.t('vehicles.section.dataSheet')
                            }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields one">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('vehicles.section.dataSheet') }}</mat-label>
                                <textarea
                                    matInput
                                    rows="12"
                                    wrap="off"
                                    class="data-sheet"
                                    formControlName="dataSheet"
                                    [maxlength]="dataSheetMaxLength"
                                    data-testid="vehicle-dataSheet"
                                ></textarea>
                                <mat-hint align="end"
                                    >{{ form.controls.dataSheet.value.length }} /
                                    {{ dataSheetMaxLength }}</mat-hint
                                >
                                @if (form.controls.dataSheet.errors; as errors) {
                                    <mat-error data-testid="vehicle-dataSheet-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>
                </div>

                <div class="action-bar">
                    <button
                        mat-button
                        type="button"
                        data-testid="vehicle-cancel"
                        (click)="cancel()"
                    >
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button
                        mat-flat-button
                        type="submit"
                        data-testid="vehicle-save"
                        [disabled]="busy()"
                    >
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: [
        `
            .page {
                max-width: 1200px;
                margin: 0 auto;
                display: flex;
                flex-direction: column;
                gap: 16px;
            }

            .page-header {
                display: flex;
                align-items: flex-start;
                justify-content: space-between;
                gap: 16px;
                flex-wrap: wrap;
            }

            .title-row {
                display: flex;
                align-items: center;
                gap: 12px;
                flex-wrap: wrap;
            }

            h1 {
                font: var(--mat-sys-headline-medium);
                margin: 0;
            }

            .audit,
            .hint {
                display: flex;
                align-items: center;
                gap: 6px;
                margin: 6px 0 0;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .audit mat-icon,
            .hint mat-icon {
                font-size: 18px;
                width: 18px;
                height: 18px;
            }

            .status-actions {
                display: flex;
                align-items: center;
                gap: 8px;
                flex-wrap: wrap;
            }

            .status-actions .hint {
                margin: 0;
            }

            .danger {
                --mat-button-outlined-label-text-color: var(--mat-sys-error);
                --mat-button-outlined-outline-color: var(--mat-sys-error);
            }

            .chip {
                --mat-chip-container-height: 28px;
                --mat-chip-outline-width: 0;
                --mat-chip-label-text-weight: 500;
            }

            .chip[data-tone='success'] {
                --mat-chip-elevated-container-color: #d7f3dc;
                --mat-chip-label-text-color: #0f5223;
            }

            .chip[data-tone='warning'] {
                --mat-chip-elevated-container-color: #ffe4bc;
                --mat-chip-label-text-color: #6d3d00;
            }

            .chip[data-tone='neutral'] {
                --mat-chip-elevated-container-color: var(--mat-sys-surface-container-highest);
                --mat-chip-label-text-color: var(--mat-sys-on-surface-variant);
            }

            .error-banner {
                display: flex;
                align-items: center;
                gap: 8px;
                margin: 0;
                padding: 12px 16px;
                border-radius: var(--mat-sys-corner-medium);
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }

            .cards {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(min(420px, 100%), 1fr));
                gap: 16px;
                align-items: start;
            }

            .full-width {
                grid-column: 1 / -1;
            }

            mat-card-header {
                align-items: center;
                margin-bottom: 12px;
            }

            .avatar {
                display: flex;
                align-items: center;
                justify-content: center;
                width: 40px;
                height: 40px;
                font-size: 22px;
                border-radius: 50%;
                background: var(--mat-sys-primary-container);
                color: var(--mat-sys-on-primary-container);
            }

            mat-card-title {
                font: var(--mat-sys-title-medium);
            }

            .fields {
                display: grid;
                grid-template-columns: repeat(2, minmax(0, 1fr));
                column-gap: 16px;
                row-gap: 4px;
            }

            .fields.three {
                grid-template-columns: repeat(3, minmax(0, 1fr));
            }

            .fields.one {
                grid-template-columns: minmax(0, 1fr);
            }

            .fields mat-form-field {
                width: 100%;
            }

            .checkbox-cell {
                display: flex;
                align-items: center;
                min-height: 56px;
                padding-bottom: 20px;
            }

            .equipment-groups {
                display: grid;
                grid-template-columns: repeat(4, minmax(0, 1fr));
                gap: 8px 24px;
            }

            .group {
                display: flex;
                flex-direction: column;
                min-width: 0;
            }

            .group mat-form-field {
                width: 100%;
            }

            .group-title {
                margin: 0 0 12px;
                font: var(--mat-sys-title-small);
                color: var(--mat-sys-on-surface-variant);
            }

            .radio-label {
                font: var(--mat-sys-body-medium);
                color: var(--mat-sys-on-surface-variant);
            }

            .radio-group {
                display: flex;
                flex-direction: column;
            }

            .radio-error {
                font: var(--mat-sys-body-small);
                padding-left: 16px;
            }

            .uppercase {
                text-transform: uppercase;
            }

            .chip[data-tone='error'] {
                --mat-chip-elevated-container-color: var(--mat-sys-error-container);
                --mat-chip-label-text-color: var(--mat-sys-on-error-container);
            }

            .insurance-groups {
                display: flex;
                flex-direction: column;
                gap: 8px;
            }

            .card-action {
                margin-left: auto;
            }

            .table-scroll {
                overflow-x: auto;
            }

            .table-scroll table {
                width: 100%;
            }

            .document-type {
                font-weight: 500;
            }

            .remark {
                white-space: pre-line;
            }

            .modified {
                color: var(--mat-sys-on-surface-variant);
                white-space: nowrap;
            }

            .actions-cell {
                text-align: right;
                white-space: nowrap;
            }

            .delete {
                color: var(--mat-sys-error);
            }

            .data-sheet {
                font-family: 'Roboto Mono', Consolas, 'Courier New', monospace;
                white-space: pre;
                overflow-x: auto;
                overflow-wrap: normal;
            }

            .action-bar {
                position: sticky;
                bottom: 0;
                z-index: 1;
                display: flex;
                justify-content: flex-end;
                gap: 8px;
                margin-top: 16px;
                padding: 12px 16px;
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-large);
                background: var(--mat-sys-surface-container-low);
            }

            @media (max-width: 959px) {
                .equipment-groups {
                    grid-template-columns: repeat(2, minmax(0, 1fr));
                }
            }

            @media (max-width: 599px) {
                .fields,
                .fields.three,
                .equipment-groups {
                    grid-template-columns: minmax(0, 1fr);
                }
            }
        `,
    ],
})
export class VehicleFormComponent {
    private readonly service = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    private readonly catalogService = inject(VehicleCatalogService);
    private readonly documentService = inject(VehicleDocumentService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly dialog = inject(MatDialog);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly vehicleTypes = VEHICLE_TYPES;
    protected readonly equipmentFlags = EQUIPMENT_FLAGS;
    protected readonly loadingSpaceFields = LOADING_SPACE_FIELDS;
    protected readonly loadSecuringFields = LOAD_SECURING_FIELDS;
    protected readonly marbleRackCountField = MARBLE_RACK_COUNT_FIELD;
    protected readonly marbleRackMaterials = MARBLE_RACK_MATERIALS;
    protected readonly dataSheetMaxLength = DATA_SHEET_MAX_LENGTH;
    protected readonly vehicleId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.vehicleId !== null;
    protected readonly formError = signal<string | null>(null);
    protected readonly busy = signal(false);
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly driverError = signal(false);
    // The loaded vehicle as last answered by the server - drives the status chip, the status
    // buttons and the audit line, which a status change updates without touching the form.
    protected readonly vehicle = signal<Vehicle | null>(null);

    private readonly vehicleClasses = signal<VehicleCatalogEntry[]>([]);
    private readonly carrierGroups = signal<VehicleCatalogEntry[]>([]);
    private readonly insuranceCompanies = signal<VehicleCatalogEntry[]>([]);
    private readonly documentTypes = signal<VehicleCatalogEntry[]>([]);
    private readonly documentTypeIndex = computed(() => catalogIndex(this.documentTypes()));
    // Active entries only, plus the code the vehicle is SAVED with if that one has since been
    // deactivated - the server accepts keeping it, but rejects choosing any other inactive code.
    protected readonly vehicleClassOptions = computed(() =>
        catalogOptions(this.vehicleClasses(), this.vehicle()?.vehicleClass ?? null),
    );
    protected readonly carrierGroupOptions = computed(() =>
        catalogOptions(this.carrierGroups(), this.vehicle()?.carrierGroup ?? null),
    );
    protected readonly vehicleInsurerOptions = computed(() =>
        catalogOptions(
            this.insuranceCompanies(),
            this.vehicle()?.commercial?.vehicleInsurer ?? null,
        ),
    );
    protected readonly transportInsurerOptions = computed(() =>
        catalogOptions(
            this.insuranceCompanies(),
            this.vehicle()?.commercial?.transportInsurer ?? null,
        ),
    );

    protected readonly documents = signal<VehicleDocument[]>([]);
    protected readonly documentsLoaded = signal(false);
    protected readonly documentError = signal<string | null>(null);
    protected readonly documentColumns = computed(() =>
        this.session.can('masterData.write')
            ? [...BASE_DOCUMENT_COLUMNS, 'actions']
            : BASE_DOCUMENT_COLUMNS,
    );

    protected readonly modifiedText = computed(() => {
        const vehicle = this.vehicle();
        if (!vehicle?.modifiedAt) {
            return null;
        }
        return this.i18n.t('vehicles.modified', {
            date: formatDateTime(vehicle.modifiedAt, this.i18n.locale()),
            user: vehicle.modifiedBy ?? '–',
        });
    });

    protected readonly form = this.fb.nonNullable.group({
        licensePlate: ['', Validators.required],
        isLicensePlateConfidential: [false],
        type: ['Tractor' as VehicleType, Validators.required],
        vehicleClass: this.fb.control<string | null>(null),
        make: [''],
        model: [''],
        carrierGroup: this.fb.control<string | null>(null),
        defaultDriverId: this.fb.control<string | null>(null),
        // "YYYY-MM" as produced by <input type="month">; the API wants "YYYY-MM-01".
        manufacturedOn: [''],
        powerKw: this.fb.control<number | null>(null, positiveOrEmpty),
        chassisNumber: [''],
        engineNumber: [''],
        nextInspectionDue: ['', Validators.required],
        lengthMm: this.fb.control<number | null>(null, positiveOrEmpty),
        widthMm: this.fb.control<number | null>(null, positiveOrEmpty),
        heightMm: this.fb.control<number | null>(null, positiveOrEmpty),
        loadMeters: [0, Validators.required],
        payloadKg: [0, Validators.required],
        curbWeightKg: this.fb.control<number | null>(null, positiveOrEmpty),
        grossWeightKg: this.fb.control<number | null>(null, positiveOrEmpty),
        maxFifthWheelLoadKg: this.fb.control<number | null>(null, positiveOrEmpty),
        notes: [''],
        dataSheet: [''],
        equipment: this.fb.nonNullable.group({
            hasPartitionWall: [false],
            hasPalletBasket: [false],
            hasDangerousGoodsEquipment: [false],
            isDangerousGoodsApproved: [false],
            hasLiftAxle: [false],
            hasDoubleDeck: [false],
            hasDoubleDeckBeams: [false],
            hasSmallLadder: [false],
            hasLargeLadder: [false],
            tankVolumeLiters: this.equipmentNumber('tankVolumeLiters'),
            interiorLengthMm: this.equipmentNumber('interiorLengthMm'),
            interiorWidthMm: this.equipmentNumber('interiorWidthMm'),
            interiorHeightMm: this.equipmentNumber('interiorHeightMm'),
            supportPoleCount: this.equipmentNumber('supportPoleCount'),
            loadBarCount: this.equipmentNumber('loadBarCount'),
            lashingStrapCount: this.equipmentNumber('lashingStrapCount'),
            palletCount: this.equipmentNumber('palletCount'),
            chainTensionerCount: this.equipmentNumber('chainTensionerCount'),
            marbleRackCount: this.equipmentNumber('marbleRackCount'),
            marbleRackMaterial: this.fb.control<MarbleRackMaterial | null>(null),
        }),
        commercial: this.fb.nonNullable.group({
            purchasePrice: this.fb.control<number | null>(null, amountInRange(0, 10000000)),
            purchasedOn: ['', dateFromYear(MIN_YEAR)],
            purchaseOdometerKm: this.fb.control<number | null>(
                null,
                wholeNumberInRange(0, 9999999),
            ),
            firstRegisteredOn: ['', dateFromYear(MIN_YEAR)],
            usefulLifeYears: this.fb.control<number | null>(null, wholeNumberInRange(1, 50)),
            usefulLifeKm: this.fb.control<number | null>(null, wholeNumberInRange(1, 9999999)),
            costCenter: [''],
            vehicleInsurer: this.fb.control<string | null>(null),
            vehicleInsurancePolicyNumbers: [''],
            transportInsurer: this.fb.control<string | null>(null),
            transportInsurancePolicyNumbers: [''],
        }),
    });

    protected readonly equipment = this.form.controls.equipment;
    protected readonly commercial = this.form.controls.commercial;

    constructor() {
        this.session.ready
            .pipe(switchMap(() => this.driverService.list(DRIVER_PAGE_SIZE)))
            .subscribe({
                next: (page) =>
                    this.drivers.set(
                        [...page.items].sort(
                            (a, b) =>
                                a.lastName.localeCompare(b.lastName) ||
                                a.firstName.localeCompare(b.firstName),
                        ),
                    ),
                error: () => this.driverError.set(true),
            });

        this.session.ready
            .pipe(
                switchMap(() =>
                    forkJoin([
                        this.catalogService.list('VehicleClass'),
                        this.catalogService.list('CarrierGroup'),
                        this.catalogService.list('InsuranceCompany'),
                        this.catalogService.list('DocumentType'),
                    ]),
                ),
            )
            .subscribe({
                next: ([vehicleClasses, carrierGroups, insuranceCompanies, documentTypes]) => {
                    this.vehicleClasses.set(vehicleClasses);
                    this.carrierGroups.set(carrierGroups);
                    this.insuranceCompanies.set(insuranceCompanies);
                    this.documentTypes.set(documentTypes);
                },
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        `${this.i18n.t('vehicles.error.catalogs')} (HTTP ${error.status})`,
                    ),
            });

        if (this.isEditMode) {
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.vehicleId!)))
                .subscribe({
                    next: (vehicle: Vehicle) => {
                        this.vehicle.set(vehicle);
                        this.form.patchValue({
                            licensePlate: vehicle.licensePlate,
                            isLicensePlateConfidential: vehicle.isLicensePlateConfidential ?? false,
                            type: vehicle.type,
                            vehicleClass: vehicle.vehicleClass ?? null,
                            make: vehicle.make ?? '',
                            model: vehicle.model ?? '',
                            carrierGroup: vehicle.carrierGroup ?? null,
                            defaultDriverId: vehicle.defaultDriverId ?? null,
                            manufacturedOn: vehicle.manufacturedOn?.slice(0, 7) ?? '',
                            powerKw: vehicle.powerKw ?? null,
                            chassisNumber: vehicle.chassisNumber ?? '',
                            engineNumber: vehicle.engineNumber ?? '',
                            nextInspectionDue: vehicle.nextInspectionDue,
                            lengthMm: vehicle.lengthMm ?? null,
                            widthMm: vehicle.widthMm ?? null,
                            heightMm: vehicle.heightMm ?? null,
                            loadMeters: vehicle.loadMeters,
                            payloadKg: vehicle.payloadKg,
                            curbWeightKg: vehicle.curbWeightKg ?? null,
                            grossWeightKg: vehicle.grossWeightKg ?? null,
                            maxFifthWheelLoadKg: vehicle.maxFifthWheelLoadKg ?? null,
                            notes: vehicle.notes ?? '',
                            dataSheet: vehicle.dataSheet ?? '',
                            equipment: { ...EMPTY_VEHICLE_EQUIPMENT, ...vehicle.equipment },
                            commercial: this.toCommercialFormValue(vehicle),
                        });
                    },
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            this.describeFailure(error, this.i18n.t('vehicles.error.load')),
                        ),
                });
            this.loadDocuments();
        }
    }

    protected statusKey(status: VehicleStatus) {
        return `vehicles.status.${status}` as const;
    }

    protected typeKey(type: VehicleType) {
        return `vehicles.typeOption.${type}` as const;
    }

    protected equipmentKey(field: EquipmentFlag | EquipmentNumber) {
        return `vehicles.equipment.${field}` as const;
    }

    protected materialKey(material: MarbleRackMaterial) {
        return `vehicles.equipment.marbleRackMaterialOption.${material}` as const;
    }

    protected numberControl(field: EquipmentNumberField): FormControl<number | null> {
        return this.equipment.controls[field.name];
    }

    protected optionLabel(option: VehicleCatalogOption): string {
        return option.isActive
            ? option.name
            : this.i18n.t('vehicles.catalogInactive', { name: option.name });
    }

    protected statusTone(status: VehicleStatus): string {
        switch (status) {
            case 'Available':
                return 'success';
            case 'InWorkshop':
                return 'warning';
            default:
                return 'neutral';
        }
    }

    // Renders whichever error is active on a control: a server-mapped message (set via
    // `setErrors({ server })` in applyServerErrors below) takes priority since it is the more
    // specific, API-sourced text; a bare client-side `required` falls back to a generic
    // message, since Validators.required carries no message of its own.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['positive']) {
            return this.i18n.t('common.fieldPositive');
        }
        if (errors['range']) {
            return this.i18n.t(
                'common.fieldRange',
                errors['range'] as { min: number; max: number },
            );
        }
        if (errors['amountRange']) {
            return this.i18n.t(
                'common.fieldAmountRange',
                errors['amountRange'] as { min: number; max: number },
            );
        }
        if (errors['minYear']) {
            return this.i18n.t('common.fieldMinYear', errors['minYear'] as { year: number });
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected documentTypeName(document: VehicleDocument): string {
        return catalogName(this.documentTypeIndex(), document.documentType) ?? '–';
    }

    protected date(iso: string | null): string {
        return formatDate(iso, this.i18n.locale());
    }

    protected documentStatus(validUntil: string): { tone: string; key: TranslationKey } {
        return DOCUMENT_STATUS[inspectionUrgency(validUntil)];
    }

    protected documentModified(document: VehicleDocument): string {
        return this.i18n.t('vehicles.documents.modifiedValue', {
            date: formatDateTime(document.modifiedAt, this.i18n.locale()),
            user: document.modifiedBy ?? '–',
        });
    }

    // Special documents are saved on their own endpoints right from the dialog, independent of
    // the vehicle's Save button - unsaved edits in the form are neither sent nor lost.
    protected openDocumentDialog(document: VehicleDocument | null): void {
        this.documentError.set(null);
        this.dialog
            .open<VehicleDocumentDialogComponent, VehicleDocumentDialogData, VehicleDocument>(
                VehicleDocumentDialogComponent,
                {
                    data: {
                        vehicleId: this.vehicleId!,
                        document,
                        documentTypes: this.documentTypes(),
                    },
                    width: '520px',
                    autoFocus: 'first-tabbable',
                },
            )
            .afterClosed()
            .pipe(filter((saved) => !!saved))
            .subscribe(() => this.loadDocuments());
    }

    protected deleteDocument(document: VehicleDocument): void {
        this.documentError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('vehicles.documents.deleteTitle'),
            message: this.i18n.t('vehicles.documents.deleteConfirm', {
                type: this.documentTypeName(document),
            }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'vehicle-document-delete-confirm',
            cancelTestId: 'vehicle-document-delete-cancel',
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
                switchMap(() => this.session.ready),
                switchMap(() => this.documentService.remove(this.vehicleId!, document.id)),
            )
            .subscribe({
                next: () => this.loadDocuments(),
                error: (error: HttpErrorResponse) =>
                    this.documentError.set(
                        this.describeFailure(error, this.i18n.t('vehicles.documents.error.delete')),
                    ),
            });
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request = this.toRequest();
        const save$ = this.session.ready.pipe(
            switchMap(() =>
                this.isEditMode
                    ? this.service.update(this.vehicleId!, request)
                    : this.service.create(request),
            ),
        );

        this.busy.set(true);
        save$.subscribe({
            next: () => this.router.navigateByUrl('/vehicles'),
            error: (error: HttpErrorResponse) => {
                this.busy.set(false);
                this.applyServerErrors(error);
            },
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/vehicles');
    }

    protected decommission(vehicle: Vehicle): void {
        const data: ConfirmDialogData = {
            title: this.i18n.t('vehicles.decommissionTitle'),
            message: this.i18n.t('vehicles.decommissionConfirm', { plate: vehicle.licensePlate }),
            confirmLabel: this.i18n.t('vehicles.action.decommission'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'vehicle-decommission-confirm',
            cancelTestId: 'vehicle-decommission-cancel',
            icon: 'block',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(filter((confirmed) => confirmed === true))
            .subscribe(() => this.changeStatus('Decommissioned'));
    }

    // Sent immediately rather than with Save: the status endpoint is separate from PUT, and
    // the form may hold unrelated, still-unsaved edits that must not be submitted with it.
    protected changeStatus(status: VehicleStatus): void {
        this.formError.set(null);
        this.busy.set(true);
        this.session.ready
            .pipe(switchMap(() => this.service.changeStatus(this.vehicleId!, status)))
            .subscribe({
                next: (vehicle) => {
                    this.vehicle.set(vehicle);
                    this.busy.set(false);
                },
                error: (error: HttpErrorResponse) => {
                    this.busy.set(false);
                    this.formError.set(
                        this.describeFailure(error, this.i18n.t('vehicles.error.status')),
                    );
                },
            });
    }

    private toRequest(): VehicleWriteRequest {
        const raw = this.form.getRawValue();
        return {
            licensePlate: raw.licensePlate,
            isLicensePlateConfidential: raw.isLicensePlateConfidential,
            type: raw.type,
            payloadKg: raw.payloadKg,
            loadMeters: raw.loadMeters,
            nextInspectionDue: raw.nextInspectionDue,
            make: textOrNull(raw.make),
            model: textOrNull(raw.model),
            manufacturedOn: raw.manufacturedOn ? `${raw.manufacturedOn.slice(0, 7)}-01` : null,
            powerKw: numberOrNull(raw.powerKw),
            vehicleClass: raw.vehicleClass,
            carrierGroup: raw.carrierGroup,
            defaultDriverId: raw.defaultDriverId,
            chassisNumber: textOrNull(raw.chassisNumber),
            engineNumber: textOrNull(raw.engineNumber),
            lengthMm: numberOrNull(raw.lengthMm),
            widthMm: numberOrNull(raw.widthMm),
            heightMm: numberOrNull(raw.heightMm),
            curbWeightKg: numberOrNull(raw.curbWeightKg),
            grossWeightKg: numberOrNull(raw.grossWeightKg),
            maxFifthWheelLoadKg: numberOrNull(raw.maxFifthWheelLoadKg),
            notes: textOrNull(raw.notes),
            equipment: {
                ...raw.equipment,
                tankVolumeLiters: numberOrNull(raw.equipment.tankVolumeLiters),
                interiorLengthMm: numberOrNull(raw.equipment.interiorLengthMm),
                interiorWidthMm: numberOrNull(raw.equipment.interiorWidthMm),
                interiorHeightMm: numberOrNull(raw.equipment.interiorHeightMm),
                supportPoleCount: numberOrNull(raw.equipment.supportPoleCount),
                loadBarCount: numberOrNull(raw.equipment.loadBarCount),
                lashingStrapCount: numberOrNull(raw.equipment.lashingStrapCount),
                palletCount: numberOrNull(raw.equipment.palletCount),
                chainTensionerCount: numberOrNull(raw.equipment.chainTensionerCount),
                marbleRackCount: numberOrNull(raw.equipment.marbleRackCount),
            },
            dataSheet: raw.dataSheet.trim() === '' ? null : raw.dataSheet,
            commercial: {
                purchasePrice: numberOrNull(raw.commercial.purchasePrice),
                purchasedOn: raw.commercial.purchasedOn || null,
                purchaseOdometerKm: numberOrNull(raw.commercial.purchaseOdometerKm),
                firstRegisteredOn: raw.commercial.firstRegisteredOn || null,
                usefulLifeYears: numberOrNull(raw.commercial.usefulLifeYears),
                usefulLifeKm: numberOrNull(raw.commercial.usefulLifeKm),
                costCenter: textOrNull(raw.commercial.costCenter),
                vehicleInsurer: raw.commercial.vehicleInsurer,
                vehicleInsurancePolicyNumbers: textOrNull(
                    raw.commercial.vehicleInsurancePolicyNumbers,
                ),
                transportInsurer: raw.commercial.transportInsurer,
                transportInsurancePolicyNumbers: textOrNull(
                    raw.commercial.transportInsurancePolicyNumbers,
                ),
            },
        };
    }

    private toCommercialFormValue(vehicle: Vehicle) {
        const commercial = { ...EMPTY_VEHICLE_COMMERCIAL, ...vehicle.commercial };
        return {
            ...commercial,
            purchasedOn: commercial.purchasedOn?.slice(0, 10) ?? '',
            firstRegisteredOn: commercial.firstRegisteredOn?.slice(0, 10) ?? '',
            costCenter: commercial.costCenter ?? '',
            vehicleInsurancePolicyNumbers: commercial.vehicleInsurancePolicyNumbers ?? '',
            transportInsurancePolicyNumbers: commercial.transportInsurancePolicyNumbers ?? '',
        };
    }

    private loadDocuments(): void {
        this.session.ready
            .pipe(switchMap(() => this.documentService.list(this.vehicleId!)))
            .subscribe({
                next: (documents) => {
                    this.documents.set(documents);
                    this.documentsLoaded.set(true);
                },
                error: (error: HttpErrorResponse) =>
                    this.documentError.set(
                        this.describeFailure(error, this.i18n.t('vehicles.documents.error.list')),
                    ),
            });
    }

    private equipmentNumber(name: EquipmentNumber): FormControl<number | null> {
        const field = EQUIPMENT_NUMBER_FIELDS.find((candidate) => candidate.name === name)!;
        return this.fb.control<number | null>(null, wholeNumberInRange(field.min, field.max));
    }

    // Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed
    // by error code (e.g. "Vehicle.PayloadKgNotPositive") rather than by the field name a form
    // control could match against. Task 1 of the 2026-08-29 master-data-completion phase fixed
    // the API to group every failure under its real field name instead, so binding it is now
    // correct - this supersedes that earlier rule. The dictionary key is still the .NET
    // property name (PascalCase, e.g. "LicensePlate"), while this form's controls use the
    // camelCase names the rest of the wire format uses, so each key is lowercased on its first
    // character before the control lookup. Nested fields come keyed by their dotted path
    // ("Equipment.TankVolumeLiters"), so every segment is converted the same way.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const control = this.form.get(toControlPath(field));
                const message = messages.join(' ');
                if (control instanceof FormControl) {
                    control.setErrors({ server: message });
                } else {
                    // A failure keyed to something this form has no field control for still needs
                    // to reach the user - fall back to the form-level message.
                    this.formError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error, this.i18n.t('vehicles.error.save')));
    }

    // No `errors` dictionary means this was not a per-field validation failure but a
    // domain-level one (404 not found, 409 conflict on a duplicate plate, or a domain
    // invariant rejected before any field-level validator ran) - `detail` is the stable,
    // human-readable message for all of those. `errorCode` (present only on the domain-
    // invariant 400 case) is available for callers that want to branch on it; none of the
    // vehicle failures need a friendlier message than `detail` already gives. `fallback` is
    // supplied per call site (loading, saving or a status change) - a bodyless failure (e.g. a
    // policy-rejected 403 with no ProblemDetails at all) must not report "could not be saved"
    // for what was actually a failed load, or vice versa.
    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
