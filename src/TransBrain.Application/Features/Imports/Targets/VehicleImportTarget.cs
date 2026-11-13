using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Imports.Targets;

internal sealed class VehicleImportTarget : IImportTargetDefinition
{
    public ImportTargetEntity Entity => ImportTargetEntity.Vehicles;

    public string Label => "Fahrzeuge";

    public IReadOnlyList<ImportTargetField> Fields { get; } =
    [
        new("licensePlate", "Kennzeichen", ImportFieldKind.Text, Required: true),
        new("type", "Typ", ImportFieldKind.Text, Required: true,
            Description: "Tractor = Zugmaschine, SemiTrailer = Auflieger",
            AllowedValues: Enum.GetNames<VehicleType>()),
        new("payloadKg", "Nutzlast (kg)", ImportFieldKind.Integer, Required: true),
        new("loadMeters", "Lademeter", ImportFieldKind.Decimal, Required: true),
        new("nextInspectionDue", "Nächste HU", ImportFieldKind.Date, Required: true),
        new("make", "Hersteller", ImportFieldKind.Text, Required: false),
        new("model", "Modell", ImportFieldKind.Text, Required: false),
        new("manufacturedOn", "Baujahr/-datum", ImportFieldKind.Date, Required: false),
        new("powerKw", "Leistung (kW)", ImportFieldKind.Integer, Required: false),
        new("chassisNumber", "Fahrgestellnummer", ImportFieldKind.Text, Required: false),
        new("engineNumber", "Motornummer", ImportFieldKind.Text, Required: false),
        new("notes", "Bemerkungen", ImportFieldKind.Text, Required: false)
    ];

    public async Task<Result<Unit>> ImportRowAsync(
        ISender sender, ImportRowValues values, CancellationToken cancellationToken)
    {
        var command = new CreateVehicleCommand(
            LicensePlate: values.GetRequiredText("licensePlate"),
            Type: values.GetRequiredText("type"),
            PayloadKg: values.GetRequiredInt("payloadKg"),
            LoadMeters: values.GetRequiredDecimal("loadMeters"),
            NextInspectionDue: values.GetRequiredDate("nextInspectionDue"),
            Make: values.GetText("make"),
            Model: values.GetText("model"),
            ManufacturedOn: values.GetDate("manufacturedOn"),
            PowerKw: values.GetInt("powerKw"),
            ChassisNumber: values.GetText("chassisNumber"),
            EngineNumber: values.GetText("engineNumber"),
            Notes: values.GetText("notes"));

        Result<VehicleResponse> result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Unit.Value : result.Error!;
    }
}
