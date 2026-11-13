using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Customers.CreateCustomer;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports.Targets;

internal sealed class CustomerImportTarget : IImportTargetDefinition
{
    public ImportTargetEntity Entity => ImportTargetEntity.Customers;

    public string Label => "Kunden";

    public IReadOnlyList<ImportTargetField> Fields { get; } =
    [
        new("name", "Name", ImportFieldKind.Text, Required: true),
        new("street", "Straße", ImportFieldKind.Text, Required: true),
        new("postalCode", "PLZ", ImportFieldKind.Text, Required: true),
        new("city", "Ort", ImportFieldKind.Text, Required: true),
        new("country", "Land", ImportFieldKind.Text, Required: true,
            Description: "ISO 3166-1 alpha-2, z. B. DE"),
        new("contactPerson", "Ansprechpartner", ImportFieldKind.Text, Required: false),
        new("email", "E-Mail", ImportFieldKind.Text, Required: false),
        new("phone", "Telefon", ImportFieldKind.Text, Required: false),
        new("vatId", "UID-Nummer", ImportFieldKind.Text, Required: false),
        new("paymentTermDays", "Zahlungsziel (Tage)", ImportFieldKind.Integer, Required: false,
            Description: "0 bis 365; leer bedeutet sofort fällig")
    ];

    public async Task<Result<Unit>> ImportRowAsync(
        ISender sender, ImportRowValues values, CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(
            Name: values.GetRequiredText("name"),
            Street: values.GetRequiredText("street"),
            PostalCode: values.GetRequiredText("postalCode"),
            City: values.GetRequiredText("city"),
            Country: values.GetRequiredText("country"),
            ContactPerson: values.GetText("contactPerson"),
            Email: values.GetText("email"),
            Phone: values.GetText("phone"),
            VatId: values.GetText("vatId"),
            PaymentTermDays: values.GetInt("paymentTermDays") ?? 0);

        Result<CustomerResponse> result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Unit.Value : result.Error!;
    }
}
