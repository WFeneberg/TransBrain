using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Features.Exports;
using TransBrain.Application.Features.Exports.ExportData;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Exports;

public class ExportDataQueryHandlerTests
{
    private static Customer ACustomer(
        string name = "Spedition Huber GmbH",
        string street = "Hafenstraße 12",
        string? contactPerson = null,
        int paymentTermDays = 30) =>
        Customer.Create(
            name,
            Address.Create(name, street, "20095", "Hamburg", "DE").Value,
            contactPerson, null, null, null, paymentTermDays).Value;

    private static Driver ADriver() =>
        Driver.Create("Franz", "Huber", [LicenseClass.C1, LicenseClass.CE], new DateOnly(2028, 6, 30), null).Value;

    private static Vehicle AVehicle() =>
        Vehicle.Create(
            LicensePlate.Create("M-TB 1001").Value,
            VehicleType.Tractor,
            24_000,
            13.6m,
            new DateOnly(2027, 5, 1),
            VehicleDetails.Create(make: "MAN", model: "TGX 18.510", powerKw: 375).Value).Value;

    private static ExportDataQueryHandler Handler(
        InMemoryCustomerRepository? customers = null,
        InMemoryDriverRepository? drivers = null,
        InMemoryVehicleRepository? vehicles = null) =>
        new(
            customers ?? new InMemoryCustomerRepository(),
            drivers ?? new InMemoryDriverRepository(),
            vehicles ?? new InMemoryVehicleRepository(),
            new FixedTimeProvider());

    [Fact]
    public async Task Handle_CustomersAsCsv_WritesBomGermanHeadersAndSemicolonRows()
    {
        var customers = new InMemoryCustomerRepository();
        customers.Seed(
            ACustomer(contactPerson: "Anna Admin"),
            ACustomer(name: "Mit;Strichpunkt KG", street: "Ring \"B\" 2", paymentTermDays: 14));

        Result<ExportFileResult> result = await Handler(customers)
            .Handle(new ExportDataQuery("Customers", "Csv"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ExportFileResult file = result.Value!;
        file.ContentType.Should().Be("text/csv");
        file.FileName.Should().MatchRegex(@"^transbrain-export-kunden-\d{8}-\d{6}\.csv$");

        file.Content.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        string[] lines = Encoding.UTF8.GetString(file.Content).TrimStart('\uFEFF')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be(
            "Name;Straße;PLZ;Ort;Land;Ansprechpartner;E-Mail;Telefon;UID-Nummer;Zahlungsziel (Tage)");
        lines.Should().Contain("Spedition Huber GmbH;Hafenstraße 12;20095;Hamburg;DE;Anna Admin;;;;30");
        lines.Should().Contain("\"Mit;Strichpunkt KG\";\"Ring \"\"B\"\" 2\";20095;Hamburg;DE;;;;;14");
    }

    [Fact]
    public async Task Handle_DriversAsJson_UsesFieldNamesIsoDatesAndJoinedLists()
    {
        var drivers = new InMemoryDriverRepository();
        drivers.Seed(ADriver());

        Result<ExportFileResult> result = await Handler(drivers: drivers)
            .Handle(new ExportDataQuery("Drivers", "Json"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should().Be("application/json");
        string json = Encoding.UTF8.GetString(result.Value.Content);
        json.Should().Contain("\"firstName\": \"Franz\"");
        json.Should().Contain("\"licenseClasses\": \"C1; CE\"");
        json.Should().Contain("\"licenseValidUntil\": \"2028-06-30\"");
        json.Should().Contain("\"externalUserId\": null");
    }

    [Fact]
    public async Task Handle_VehiclesAsXlsx_WritesAWorkbookWithInlineStringsAndNumbers()
    {
        var vehicles = new InMemoryVehicleRepository();
        vehicles.Seed(AVehicle());

        Result<ExportFileResult> result = await Handler(vehicles: vehicles)
            .Handle(new ExportDataQuery("Vehicles", "Xlsx"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ContentType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Value.FileName.Should().EndWith(".xlsx");

        using var archive = new System.IO.Compression.ZipArchive(
            new MemoryStream(result.Value.Content), System.IO.Compression.ZipArchiveMode.Read);
        archive.GetEntry("xl/workbook.xml").Should().NotBeNull();
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        string sheet = await reader.ReadToEndAsync(CancellationToken.None);
        sheet.Should().Contain("<t>Kennzeichen</t>");
        sheet.Should().Contain("<t>M-TB 1001</t>");
        sheet.Should().Contain("<v>24000</v>");
        sheet.Should().Contain("<v>13.6</v>");
        sheet.Should().Contain("<t>2027-05-01</t>");

        using var workbookReader = new StreamReader(archive.GetEntry("xl/workbook.xml")!.Open());
        (await workbookReader.ReadToEndAsync(CancellationToken.None)).Should().Contain("name=\"Fahrzeuge\"");
    }

    [Fact]
    public async Task Handle_EntityAndFormatNamesAreCaseInsensitive()
    {
        var customers = new InMemoryCustomerRepository();
        customers.Seed(ACustomer());

        Result<ExportFileResult> result = await Handler(customers)
            .Handle(new ExportDataQuery("customers", "json"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task Handle_WithoutAnyRows_StillReturnsTheHeaderLine()
    {
        Result<ExportFileResult> result = await Handler()
            .Handle(new ExportDataQuery("Customers", "Csv"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Encoding.UTF8.GetString(result.Value!.Content).TrimStart('\uFEFF')
            .Should().Be("Name;Straße;PLZ;Ort;Land;Ansprechpartner;E-Mail;Telefon;UID-Nummer;Zahlungsziel (Tage)\r\n");
    }

    [Fact]
    public async Task Handle_UnknownTarget_ReturnsValidationError()
    {
        Result<ExportFileResult> result = await Handler()
            .Handle(new ExportDataQuery("Invoices", "Csv"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Export.UnknownTarget");
    }

    [Fact]
    public async Task Handle_UnknownFormat_ReturnsValidationError()
    {
        Result<ExportFileResult> result = await Handler()
            .Handle(new ExportDataQuery("Customers", "pdf"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Export.UnknownFormat");
    }
}
