using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Imports.Targets;

internal sealed class DriverImportTarget : IImportTargetDefinition
{
    public ImportTargetEntity Entity => ImportTargetEntity.Drivers;

    public string Label => "Fahrer";

    public IReadOnlyList<ImportTargetField> Fields { get; } =
    [
        new("firstName", "Vorname", ImportFieldKind.Text, Required: true),
        new("lastName", "Nachname", ImportFieldKind.Text, Required: true),
        new("licenseClasses", "Führerscheinklassen", ImportFieldKind.TextList, Required: true,
            Description: "Mit ; oder , getrennt, z. B. \"C1;CE\"",
            AllowedValues: Enum.GetNames<LicenseClass>()),
        new("licenseValidUntil", "Führerschein gültig bis", ImportFieldKind.Date, Required: true),
        new("externalUserId", "Login-Benutzer-ID", ImportFieldKind.Text, Required: false,
            Description: "Keycloak-Benutzer-ID, falls der Fahrer einen Login hat")
    ];

    public async Task<Result<Unit>> ImportRowAsync(
        ISender sender, ImportRowValues values, CancellationToken cancellationToken)
    {
        var command = new CreateDriverCommand(
            FirstName: values.GetRequiredText("firstName"),
            LastName: values.GetRequiredText("lastName"),
            LicenseClasses: values.GetTextList("licenseClasses"),
            LicenseValidUntil: values.GetRequiredDate("licenseValidUntil"),
            ExternalUserId: values.GetText("externalUserId"));

        Result<DriverResponse> result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Unit.Value : result.Error!;
    }
}
