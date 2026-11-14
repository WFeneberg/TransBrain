using AwesomeAssertions;
using TransBrain.Application.Features.Exports;
using TransBrain.Application.Features.Exports.GetExportTargets;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Exports;

public class GetExportTargetsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEveryImportTargetWithItsRowCount()
    {
        var customers = new InMemoryCustomerRepository();
        customers.Seed(
            Customer.Create(
                "Eins GmbH", Address.Create("Eins GmbH", "Weg 1", "4020", "Linz", "AT").Value,
                null, null, null, null, 30).Value,
            Customer.Create(
                "Zwei KG", Address.Create("Zwei KG", "Weg 2", "1010", "Wien", "AT").Value,
                null, null, null, null, 14).Value);
        var drivers = new InMemoryDriverRepository();
        drivers.Seed(Driver.Create("Franz", "Huber", [LicenseClass.CE], new DateOnly(2028, 1, 1), null).Value);

        var handler = new GetExportTargetsQueryHandler(customers, drivers, new InMemoryVehicleRepository());

        Result<IReadOnlyList<ExportTargetResponse>> result =
            await handler.Handle(new GetExportTargetsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(
        [
            new ExportTargetResponse("Customers", "Kunden", 2),
            new ExportTargetResponse("Drivers", "Fahrer", 1),
            new ExportTargetResponse("Vehicles", "Fahrzeuge", 0)
        ]);
    }
}
