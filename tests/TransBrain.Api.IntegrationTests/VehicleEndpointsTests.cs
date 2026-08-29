using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence;

namespace TransBrain.Api.IntegrationTests;

public class VehicleEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static readonly object ValidVehicle = new
    {
        licensePlate = "M-AB 1234",
        type = "Tractor",
        payloadKg = 24_000,
        loadMeters = 13.6m,
        nextInspectionDue = "2027-03-31"
    };

    [Fact]
    public async Task PostVehicle_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsJsonAsync("/api/vehicles", ValidVehicle);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostVehicle_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/vehicles", ValidVehicle);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostVehicle_AsAdmin_ReturnsCreatedAndPersistsVehicle()
    {
        HttpClient client = factory.CreateClientAs("admin");
        object vehicle = new
        {
            licensePlate = "M-CR 8080",
            type = "Tractor",
            payloadKg = 24_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2027-03-31"
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/vehicles", vehicle);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleResponse? created = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        created!.LicensePlate.Should().Be("M-CR 8080");

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/vehicles");
        PagedResult<VehicleResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<VehicleResponse>>();
        page!.Items.Should().Contain(v => v.LicensePlate == "M-CR 8080");
    }

    [Fact]
    public async Task PostVehicle_DuplicateLicensePlate_ReturnsConflict()
    {
        HttpClient client = factory.CreateClientAs("admin");
        object vehicle = new
        {
            licensePlate = "M-DUP 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        };

        await client.PostAsJsonAsync("/api/vehicles", vehicle);
        HttpResponseMessage second = await client.PostAsJsonAsync("/api/vehicles", vehicle);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostVehicle_NonPositivePayload_ReturnsBadRequest()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicles", new
            {
                licensePlate = "M-BAD 1",
                type = "Van",
                payloadKg = 0,
                loadMeters = 4.0m,
                nextInspectionDue = "2027-03-31"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostVehicle_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicles", new
            {
                licensePlate = "M-2F 1",
                type = "Van",
                payloadKg = 0,
                loadMeters = 0m,
                nextInspectionDue = "2027-03-31"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("PayloadKg").And.Contain("LoadMeters");
    }

    [Fact]
    public async Task PutVehicle_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-2F 2",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/vehicles/{vehicle!.Id}", new
        {
            licensePlate = "",
            type = "Van",
            payloadKg = 0,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("LicensePlate").And.Contain("PayloadKg");
    }

    [Fact]
    public async Task GetVehicles_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVehicleById_KnownId_ReturnsVehicle()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-GV 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/vehicles/{vehicle!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleResponse? fetched = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        fetched!.LicensePlate.Should().Be("M-GV 1");
    }

    [Fact]
    public async Task GetVehicleById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/vehicles/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutVehicle_AsAdmin_UpdatesAndReturnsNewValues()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-PV 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/vehicles/{vehicle!.Id}", new
        {
            licensePlate = "M-PV 2",
            type = "Tractor",
            payloadKg = 24_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2029-01-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleResponse? updated = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        updated!.LicensePlate.Should().Be("M-PV 2");
        updated.Type.Should().Be("Tractor");
        updated.PayloadKg.Should().Be(24_000);
    }

    [Fact]
    public async Task PutVehicle_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PutAsJsonAsync($"/api/vehicles/{Guid.CreateVersion7()}", new
            {
                licensePlate = "M-PD 1",
                type = "Van",
                payloadKg = 3_000,
                loadMeters = 4.0m,
                nextInspectionDue = "2027-03-31"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutVehicle_PlateTakenByAnotherVehicle_ReturnsConflict()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage firstCreated = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-PC 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? first = await firstCreated.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage secondCreated = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-PC 2",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? second = await secondCreated.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/vehicles/{first!.Id}", new
        {
            licensePlate = second!.LicensePlate,
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // The round-trip proof for excludingId: updating a vehicle without changing its own plate
    // must not collide with itself in ExistsByLicensePlateAsync.
    [Fact]
    public async Task PutVehicle_UnchangedPlate_ReturnsOk()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-UP 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/vehicles/{vehicle!.Id}", new
        {
            licensePlate = "M-UP 1",
            type = "Tractor",
            payloadKg = 24_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2029-01-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleResponse? updated = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        updated!.LicensePlate.Should().Be("M-UP 1");
        updated.Type.Should().Be("Tractor");
    }

    [Fact]
    public async Task DeleteVehicle_AsAdmin_RemovesIt()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-DV 1",
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        });
        VehicleResponse? vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        HttpResponseMessage response = await admin.DeleteAsync($"/api/vehicles/{vehicle!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage after = await admin.GetAsync($"/api/vehicles/{vehicle.Id}");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteVehicle_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .DeleteAsync($"/api/vehicles/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---
    // Extra test beyond the Task 13 brief (which specifies only the six tests above). The
    // duplicate-plate handling has two branches: CreateVehicleCommandHandler's own
    // ExistsByLicensePlateAsync pre-check, and VehicleRepository.AddAsync's catch of PostgreSQL's
    // 23505 unique-violation. PostVehicle_DuplicateLicensePlate_ReturnsConflict above only ever
    // exercises the first, because by the time the second POST runs, the first request has already
    // committed - so the pre-check itself finds the row.
    //
    // To force the second (database) branch, a vehicle with the same plate is inserted here inside
    // a transaction that is deliberately left open. Under PostgreSQL's read-committed isolation, a
    // plain SELECT (the pre-check's AnyAsync) never blocks on, and never observes, another
    // transaction's uncommitted writes - so the API's pre-check reports no existing vehicle. Only a
    // concurrent INSERT of the same key blocks on the unique index until the held transaction
    // resolves. So the API's own INSERT (from AddAsync) stalls behind ours; once we commit, it comes
    // back as a 23505 unique violation, which AddAsync converts to the same Conflict result the
    // pre-check would have produced. The test polls pg_stat_activity to know the API's request has
    // actually reached that blocked INSERT before releasing the held transaction, rather than
    // relying on a fixed delay.
    [Fact]
    public async Task PostVehicle_DuplicatePlateInsertedOutsideApi_ReturnsConflict()
    {
        const string plate = "M-RACE 1";

        using IServiceScope scope = factory.Services.CreateScope();
        TransBrainDbContext context = scope.ServiceProvider.GetRequiredService<TransBrainDbContext>();

        // A second, independent context/connection is used to poll for the API's blocked insert so
        // the poll itself never runs inside the held transaction above.
        using IServiceScope pollScope = factory.Services.CreateScope();
        TransBrainDbContext pollContext = pollScope.ServiceProvider.GetRequiredService<TransBrainDbContext>();

        HttpClient client = factory.CreateClientAs("admin");
        object vehicle = new
        {
            licensePlate = plate,
            type = "Van",
            payloadKg = 3_000,
            loadMeters = 4.0m,
            nextInspectionDue = "2027-03-31"
        };

        // Aspire's AddNpgsqlDbContext enables a retrying execution strategy, which refuses to run
        // SaveChangesAsync under a manually-started transaction unless that begin+operation unit
        // executes through CreateExecutionStrategy().ExecuteAsync (otherwise a retried operation
        // could replay part of the transaction). Only the begin+insert step is wrapped in the
        // ExecuteAsync call below: the HTTP POST, the lock-wait poll, and the eventual commit all
        // run outside it, so a transient retry of the insert can never re-issue the POST or double
        // it - see the comment above the CommitAsync call below for why commit does not need (and
        // in fact cannot use) its own wrapping.
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        IDbContextTransaction transaction = await strategy.ExecuteAsync(async () =>
        {
            IDbContextTransaction tx = await context.Database.BeginTransactionAsync();

            Vehicle existing = Vehicle.Create(
                LicensePlate.Create(plate).Value,
                VehicleType.Van,
                3_000,
                4.0m,
                new DateOnly(2027, 3, 31)).Value;
            context.Vehicles.Add(existing);
            await context.SaveChangesAsync();

            return tx;
        });

        try
        {
            Task<HttpResponseMessage> postTask = client.PostAsJsonAsync("/api/vehicles", vehicle);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (await CountBlockedOnVehicleInsertAsync(pollContext) == 0)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException(
                        "Timed out waiting for the API's insert to block behind the held transaction; " +
                        "the database-conflict race could not be arranged.");
                }

                await Task.Delay(25);
            }

            // Not wrapped in the execution strategy: CommitAsync talks directly to the ADO
            // transaction and never routes through DbContext.SaveChangesAsync's own
            // ExecutionStrategy.ExecuteAsync call, so it does not trip the "does not support
            // user-initiated transactions" guard the way a second, separate ExecuteAsync(...)
            // call around it would (verified: wrapping it that way throws, because that guard
            // fires at the start of every top-level ExecuteAsync call whenever a user transaction
            // is already open, regardless of which call originally started it).
            await transaction.CommitAsync();

            HttpResponseMessage response = await postTask;

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    // Matches specifically a blocked INSERT into vehicles, not any blocked statement touching the
    // table (a plain SELECT never blocks on another transaction's uncommitted write under
    // read-committed isolation, so this only catches writers). It cannot narrow further to this
    // test's specific license plate: Npgsql submits parameterized SQL over the PostgreSQL extended
    // query protocol, so pg_stat_activity.query shows the placeholder text ("VALUES (@p0, ...)"),
    // never the bound parameter values. The remaining assumption is that no other test in this
    // fixture is concurrently blocked on an INSERT into vehicles when this poll runs - true today
    // because IClassFixture tests in this class execute sequentially and this is the only test
    // here that holds a transaction open, but not something a future test added to this class
    // should assume without checking.
    private static async Task<int> CountBlockedOnVehicleInsertAsync(TransBrainDbContext context)
        => await context.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM pg_stat_activity
                 WHERE wait_event_type = 'Lock' AND query ILIKE '%INSERT INTO vehicles%'
                 """)
            .SingleAsync();
}
