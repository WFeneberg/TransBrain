using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Features.Imports;

/// <summary>Shared wiring for import tests: a sender with every fake the import pipeline can reach.</summary>
internal static class ImportTestSetup
{
    public sealed record TestContext(
        ISender Sender,
        InMemoryImportSessionRepository Sessions,
        FakeImportSourceParser Parser,
        InMemoryCustomerRepository Customers,
        InMemoryDriverRepository Drivers,
        InMemoryVehicleRepository Vehicles);

    public static TestContext Build()
    {
        var sessions = new InMemoryImportSessionRepository();
        var parser = new FakeImportSourceParser();
        var customers = new InMemoryCustomerRepository();
        var drivers = new InMemoryDriverRepository();
        var vehicles = new InMemoryVehicleRepository();

        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton<IImportSessionRepository>(sessions);
        services.AddSingleton<IImportSourceParser>(parser);
        services.AddSingleton<ICustomerRepository>(customers);
        services.AddSingleton<IDriverRepository>(drivers);
        services.AddSingleton<IVehicleRepository>(vehicles);
        services.AddSingleton<IVehicleCatalogRepository>(InMemoryVehicleCatalogRepository.WithStandardEntries());
        services.AddSingleton<ICacheService>(new InMemoryCacheService());
        services.AddSingleton<ICurrentUser>(new StubCurrentUser("importer-admin", "admin"));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        ISender sender = services.BuildServiceProvider().GetRequiredService<ISender>();
        return new TestContext(sender, sessions, parser, customers, drivers, vehicles);
    }

    /// <summary>One customers-shaped source table with German headers, as a CSV export would produce.</summary>
    public static ImportTableSet CustomerTables(params IReadOnlyList<string?>[] rows) => new([
        new ImportTable(
            "Kunden",
            ["Firma", "Strasse", "PLZ", "Ort", "Land", "Zahlungsziel"],
            rows)
    ]);

    public static IReadOnlyList<ImportFieldMapping> CustomerMappings =>
    [
        new("name", "Firma", null),
        new("street", "Strasse", null),
        new("postalCode", "PLZ", null),
        new("city", "Ort", null),
        new("country", "Land", "DE"),
        new("paymentTermDays", "Zahlungsziel", null)
    ];

    public static ImportSession CreateSession(
        ImportSourceType sourceType = ImportSourceType.Csv, string sourceName = "kunden.csv")
        => ImportSession.Create(sourceType, sourceName, "importer-admin", FixedTimeProvider.Default).Value;
}
