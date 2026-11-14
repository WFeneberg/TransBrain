using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Exports.ExportData;

/// <summary>
/// Loads every row of the chosen entity (tenant-scoped through the repositories) and writes it
/// into the chosen format. The columns are exactly the import target's fields, so the file is
/// both a human-readable extract and a re-importable exchange file.
/// </summary>
internal sealed class ExportDataQueryHandler(
    ICustomerRepository customers,
    IDriverRepository drivers,
    IVehicleRepository vehicles,
    TimeProvider timeProvider)
    : IQueryHandler<ExportDataQuery, ExportFileResult>
{
    public async Task<Result<ExportFileResult>> Handle(ExportDataQuery query, CancellationToken cancellationToken)
    {
        IImportTargetDefinition? target = ImportTargetRegistry.Find(query.TargetEntity);
        if (target is null)
        {
            return Error.Validation(
                "Export.UnknownTarget",
                $"'{query.TargetEntity}' is not an exportable entity.");
        }

        if (!Enum.TryParse(query.Format, ignoreCase: true, out ExportFormat format))
        {
            return Error.Validation(
                "Export.UnknownFormat",
                $"'{query.Format}' is not a supported export format (Csv, Json, Xlsx).");
        }

        IReadOnlyList<object?[]> rows = target.Entity switch
        {
            ImportTargetEntity.Customers => (await customers.ListAsync(0, int.MaxValue, cancellationToken))
                .Select(CustomerRow).ToArray(),
            ImportTargetEntity.Drivers => (await drivers.ListAsync(0, int.MaxValue, null, cancellationToken))
                .Select(DriverRow).ToArray(),
            ImportTargetEntity.Vehicles => (await vehicles.ListAsync(0, int.MaxValue, null, null, cancellationToken))
                .Select(VehicleRow).ToArray(),
            _ => []
        };

        (string contentType, string extension, byte[] content) =
            ExportFileWriter.Write(format, target.Label, target.Fields, rows);

        string fileName = FormattableString.Invariant(
            $"transbrain-export-{target.Label.ToLowerInvariant()}-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.{extension}");

        return new ExportFileResult(fileName, contentType, content);
    }

    /// <summary>Order matches <see cref="CustomerImportTarget"/>.Fields.</summary>
    private static object?[] CustomerRow(Customer customer) =>
    [
        customer.Name,
        customer.Address.Street,
        customer.Address.PostalCode,
        customer.Address.City,
        customer.Address.Country,
        customer.ContactPerson,
        customer.Email,
        customer.Phone,
        customer.VatId,
        customer.PaymentTermDays
    ];

    /// <summary>Order matches <see cref="DriverImportTarget"/>.Fields.</summary>
    private static object?[] DriverRow(Driver driver) =>
    [
        driver.FirstName,
        driver.LastName,
        driver.LicenseClasses.Select(c => c.ToString()).ToArray(),
        driver.LicenseValidUntil,
        driver.ExternalUserId
    ];

    /// <summary>Order matches <see cref="VehicleImportTarget"/>.Fields.</summary>
    private static object?[] VehicleRow(Vehicle vehicle) =>
    [
        vehicle.LicensePlate.Value,
        vehicle.Type.ToString(),
        vehicle.PayloadKg,
        vehicle.LoadMeters,
        vehicle.NextInspectionDue,
        vehicle.Details.Make,
        vehicle.Details.Model,
        vehicle.Details.ManufacturedOn,
        vehicle.Details.PowerKw,
        vehicle.Details.ChassisNumber,
        vehicle.Details.EngineNumber,
        vehicle.Details.Notes
    ];
}
