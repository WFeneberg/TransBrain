# TransBrain Phase 2 — Master Data Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete master-data management — the full `Driver` aggregate, the missing `Vehicle` operations, Redis caching of the two list queries — and close the two debts Phase 1 deliberately deferred, both of whose gates fire in this phase.

**Architecture:** Every slice follows the shape Phase 1 proved: a folder per use case under `Features/<Aggregate>/<Action>/`, a handler returning `Result<T>`, invariants owned by the domain and never duplicated into a validator, and an endpoint mapping `Result` to HTTP through the shared `ResultExtensions`. Two cross-cutting changes come first because everything after them depends on their shape: the validation pipeline is widened to carry per-field failures, and a fallback authorization policy makes a forgotten `RequireAuthorization` fail closed instead of silently opening an endpoint.

**Tech Stack:** .NET 10 / C# 14, ASP.NET Minimal APIs, EF Core 10 + Npgsql, FluentValidation, Redis via `Aspire.StackExchange.Redis.DistributedCaching`, xUnit v2 + AwesomeAssertions + Testcontainers, Angular 22 + Material, Vue 3 + Vuetify, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-28-transbrain-dispatch-design.md` (Phase 2 row in §12; `Driver` in §5.3; caching in §7; policies in §9)

**Predecessor:** `docs/superpowers/plans/2026-08-28-foundation-and-walking-skeleton.md` — Phases 0 and 1, complete and merged. Read its Global Constraints; they still bind.

## Global Constraints

Everything in the predecessor plan's Global Constraints still applies. Repeated here because they are binding, plus what Phase 1's execution added:

- `net10.0`, nullable enabled, file-scoped namespaces, 4-space indentation, English identifiers.
- `TreatWarningsAsErrors` **and** `MSBuildTreatWarningsAsErrors` are both on. The build must show 0 warnings, 0 errors and 0 MSB3277.
- Central Package Management: no `PackageReference` carries a `Version`; every version is a `PackageVersion` in `Directory.Packages.props`, which must keep its trailing newline. `dotnet add package` has stripped that newline three times — check it every time.
- Assertion library is **AwesomeAssertions** (`using AwesomeAssertions;`). FluentAssertions is proprietary from 8.x and must never appear. FluentValidation is a different library and is required.
- Tests are xUnit **v2**: `IAsyncLifetime` uses `Task`, not `ValueTask`.
- Test naming `Method_Scenario_ExpectedResult`. Test fakes are `public`, never `private` nested — `dynamic` dispatch in `Sender` respects accessibility.
- Business failures never throw. Handlers return `Result<T>`; genuine exceptional conditions still propagate.
- **A validator may only carry rules the domain cannot express.** Duplicating a domain invariant into a validator silently overrides the layer that should decide, because `ValidationBehavior` short-circuits before the handler runs. Phase 1 shipped that bug once and deleted the validator to fix it.
- Every aggregate gets at least one test that dispatches through `ISender` with the real `AddApplication()` registrations. Handler-only tests bypass the pipeline and cannot see this class of defect.
- Start the stack with `dotnet run --project src/TransBrain.AppHost`. **Not** `aspire run` — it times out here and force-kills succeeding orchestrations.
- Neither Postgres nor Keycloak has a data volume. The database starts empty every run and migrations re-apply; the realm re-imports from `transbrain-realm.json`.
- The Keycloak authority is `https://localhost:8080/realms/transbrain` — HTTPS, with a development certificate that must be trusted (`dotnet dev-certs https --trust`).
- Conventional Commits.
- **Environment:** this machine hosts unrelated Docker resources — `dapr_*` containers, and `fewobrain.apphost-*` / `truckingweb.apphost-*` volumes belonging to other projects. Never run `docker volume prune` or `docker system prune`, never delete a volume, and stop only containers you start.

---

## File Structure

**New domain files** — `src/TransBrain.Domain/Drivers/`: `Driver.cs`, `DriverStatus.cs`, `LicenseClass.cs`. One type per file, matching the `Vehicles/` folder's shape.

**New application files** — `src/TransBrain.Application/`:
- `Abstractions/IDriverRepository.cs`, `Abstractions/ICacheService.cs`
- `Features/Drivers/DriverResponse.cs` and one folder per use case: `CreateDriver/`, `ListDrivers/`, `GetDriverById/`, `UpdateDriver/`, `DeleteDriver/`
- `Features/Vehicles/GetVehicleById/`, `UpdateVehicle/`, `DeleteVehicle/`

**Modified application files**: `Common/Behaviors/ValidationBehavior.cs` (group all failures), `Features/Vehicles/ListVehicles/` (filters), `Abstractions/IVehicleRepository.cs` (the operations the new slices need).

**Modified domain file**: `Common/Error.cs` — an optional per-field failure collection.

**New infrastructure files** — `src/TransBrain.Infrastructure/Persistence/`: `Configurations/DriverConfiguration.cs`, `Repositories/DriverRepository.cs`, `Caching/RedisCacheService.cs`, plus a generated migration.

**New API files**: `Endpoints/DriverEndpoints.cs`. Modified: `Common/ResultExtensions.cs`, `Endpoints/VehicleEndpoints.cs`, `Program.cs`.

**Frontends**: a drivers list and form, and vehicle edit/delete, in each of `src/TransBrain.Web` and `src/TransBrain.VueWeb`.

---

### Task 1: Widen the validation pipeline to carry per-field failures

Phase 1 shipped a validation shape that no client can bind to: `ValidationBehavior` discarded every failure but the first, and the API keyed its ProblemDetails dictionary by `Error.Code` — which for a domain failure is a code like `Vehicle.PayloadKgNotPositive`, not a field name. Both frontends were forbidden from binding those keys precisely so this debt would acquire no dependents. Phase 2 adds forms, so the gate fires now.

**Files:**
- Modify: `src/TransBrain.Domain/Common/Error.cs`
- Modify: `src/TransBrain.Application/Common/Behaviors/ValidationBehavior.cs`
- Modify: `src/TransBrain.Api/Common/ResultExtensions.cs`
- Test: `tests/TransBrain.Domain.Tests/Common/ErrorTests.cs` (create)
- Test: `tests/TransBrain.Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs` (extend)

**Interfaces:**
- Consumes: `Error`, `ErrorType`, `Result<T>`, `IPipelineBehavior<,>`.
- Produces: `Error.Failures` — an `IReadOnlyDictionary<string, string[]>?`, null for every error except one produced from validator failures; `Error.ValidationFailures(IReadOnlyDictionary<string, string[]> failures)` static factory. `ResultExtensions.ToHttpResult` emits a field-keyed `ValidationProblem` when `Failures` is present and a `ProblemDetails` carrying an `errorCode` extension member when it is not.

- [ ] **Step 1: Write the failing tests**

`tests/TransBrain.Domain.Tests/Common/ErrorTests.cs`:

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class ErrorTests
{
    [Fact]
    public void Validation_SingleCodeAndMessage_LeavesFailuresNull()
    {
        Error error = Error.Validation("Vehicle.PayloadKgNotPositive", "Payload must be greater than zero.");

        error.Type.Should().Be(ErrorType.Validation);
        error.Failures.Should().BeNull();
    }

    [Fact]
    public void ValidationFailures_WithFieldErrors_ExposesThemKeyedByFieldName()
    {
        Dictionary<string, string[]> failures = new()
        {
            ["FirstName"] = ["'First Name' must not be empty."],
            ["LicenseValidUntil"] = ["'License Valid Until' must not be empty."]
        };

        Error error = Error.ValidationFailures(failures);

        error.Type.Should().Be(ErrorType.Validation);
        error.Failures.Should().NotBeNull();
        error.Failures!.Should().ContainKey("FirstName");
        error.Failures.Should().ContainKey("LicenseValidUntil");
    }

    [Fact]
    public void NotFound_Always_LeavesFailuresNull()
    {
        Error error = Error.NotFound("Driver.NotFound", "No driver with that id.");

        error.Failures.Should().BeNull();
    }
}
```

Append to `tests/TransBrain.Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs` — note the existing tests use a `SampleCommand` with a `Name` property and a validator requiring it non-empty; add a second property so more than one field can fail at once:

```csharp
    public sealed record TwoFieldCommand(string Name, string City) : ICommand<string>;

    public sealed class TwoFieldCommandValidator : AbstractValidator<TwoFieldCommand>
    {
        public TwoFieldCommandValidator()
        {
            RuleFor(c => c.Name).NotEmpty();
            RuleFor(c => c.City).NotEmpty();
        }
    }

    [Fact]
    public async Task Handle_TwoInvalidFields_ReportsBothKeyedByFieldName()
    {
        ValidationBehavior<TwoFieldCommand, string> behavior = new([new TwoFieldCommandValidator()]);

        Result<string> result = await behavior.Handle(
            new TwoFieldCommand(string.Empty, string.Empty),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Failures.Should().NotBeNull();
        result.Error.Failures!.Keys.Should().BeEquivalentTo(["Name", "City"]);
    }

    [Fact]
    public async Task Handle_OneFieldWithTwoRuleFailures_GroupsBothMessagesUnderThatField()
    {
        ValidationBehavior<TwoFieldCommand, string> behavior = new([new TwoRuleValidator()]);

        Result<string> result = await behavior.Handle(
            new TwoFieldCommand("x", "ok"),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.Error!.Failures!["Name"].Should().HaveCount(2);
    }

    public sealed class TwoRuleValidator : AbstractValidator<TwoFieldCommand>
    {
        public TwoRuleValidator()
        {
            RuleFor(c => c.Name).MinimumLength(3);
            RuleFor(c => c.Name).Matches("^[A-Z]");
        }
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~ErrorTests` and `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~ValidationBehaviorTests`
Expected: compile errors — `Error.Failures` and `Error.ValidationFailures` do not exist.

- [ ] **Step 3: Widen `Error`**

The property is `init`-only and defaults to null, so every existing construction site keeps compiling and keeps producing exactly what it produced before.

```csharp
namespace TransBrain.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>
    /// Per-field validation messages, keyed by field name. Populated only by
    /// <c>ValidationBehavior</c> from validator failures. A domain invariant produces a
    /// coded error with no field to attach to, so this stays null there — and the API
    /// must not pretend otherwise by inventing a field key from the code.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Failures { get; init; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error ValidationFailures(IReadOnlyDictionary<string, string[]> failures) =>
        new("Validation.Failed", "One or more fields are invalid.", ErrorType.Validation)
        {
            Failures = failures
        };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
```

- [ ] **Step 4: Group every failure in `ValidationBehavior`**

Replace the `failures[0]` discard with grouping. Everything above that line stays as it is.

```csharp
        if (failures.Length == 0)
        {
            return await next();
        }

        Dictionary<string, string[]> grouped = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Error.ValidationFailures(grouped);
```

- [ ] **Step 5: Make the HTTP mapping honest**

`ResultExtensions.ToHttpResult`'s `Validation` arm becomes two cases: a real field-keyed problem when field failures exist, and a coded problem when the failure came from a domain invariant. The domain code moves into an `errorCode` extension member, where a client can branch on it without the dictionary pretending its key is a field.

```csharp
        return error.Type switch
        {
            ErrorType.Validation => error.Failures is { Count: > 0 }
                ? Results.ValidationProblem(
                    error.Failures.ToDictionary(kv => kv.Key, kv => kv.Value),
                    title: "Validation failed")
                : Results.Problem(
                    title: "Validation failed",
                    detail: error.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),
            ErrorType.NotFound => Results.Problem(title: error.Code, detail: error.Message, statusCode: 404),
            ErrorType.Conflict => Results.Problem(title: error.Code, detail: error.Message, statusCode: 409),
            ErrorType.Forbidden => Results.Problem(title: error.Code, detail: error.Message, statusCode: 403),
            _ => Results.Problem(title: error.Code, detail: error.Message, statusCode: 500)
        };
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test TransBrain.slnx`
Expected: all pass. The existing integration test asserting 400 for a non-positive payload still passes — that failure is a domain invariant, so it now returns a coded problem rather than a field dictionary, and the status is unchanged.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Domain/Common/Error.cs src/TransBrain.Application/Common/Behaviors/ValidationBehavior.cs src/TransBrain.Api/Common/ResultExtensions.cs tests/
git commit -m "feat(api): report every validation failure keyed by field name"
```

---

### Task 2: Fallback authorization policy

Phase 1 deferred this with a recorded reason: a blanket fallback would also capture the health endpoints, the OpenAPI document and the Scalar UI, so it needed its own design rather than being appended to an auth fix round. This phase adds a second endpoint group, which is the point at which forgetting a `RequireAuthorization` becomes likely — so the gate fires now.

**Files:**
- Modify: `src/TransBrain.Api/Program.cs`
- Test: `tests/TransBrain.Api.IntegrationTests/AuthorizationFallbackTests.cs` (create)

**Interfaces:**
- Consumes: the existing `AddAuthorizationBuilder()` chain and the endpoint-group registration loop.
- Produces: a fallback policy requiring an authenticated user, with the infrastructure endpoints explicitly anonymous.

- [ ] **Step 1: Write the failing test**

The test proves both halves: a route with no explicit policy is refused, and the infrastructure routes stay open.

```csharp
using System.Net;
using AwesomeAssertions;

namespace TransBrain.Api.IntegrationTests;

public class AuthorizationFallbackTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    [Fact]
    public async Task GetHealth_WithoutToken_ReturnsSuccess()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAlive_WithoutToken_ReturnsSuccess()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUnmappedRoute_WithoutToken_ReturnsNotFoundNotUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run the test to verify the health cases fail**

Run: `dotnet test tests/TransBrain.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationFallbackTests`
Expected: all three PASS before the change — that is the point. Record the output. They are the regression net for Step 3: after adding the fallback, `/health` and `/alive` would start returning 401 unless explicitly allowed anonymous, and these tests are what catches it.

- [ ] **Step 3: Add the fallback policy and exempt the infrastructure endpoints**

In `Program.cs`, extend the authorization registration:

```csharp
builder.Services.AddAuthorizationBuilder()
    // Fail closed: an endpoint that forgets RequireAuthorization is refused rather than
    // silently public. The infrastructure endpoints below opt out explicitly.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.MasterDataWrite, policy => policy.RequireRole("admin"))
    .AddPolicy(Policies.DispatchWrite, policy => policy.RequireRole("admin", "disponent"))
    .AddPolicy(Policies.TourStatusWrite, policy => policy.RequireRole("admin", "disponent", "fahrer"))
    .AddPolicy(Policies.Read, policy => policy.RequireRole("admin", "disponent", "fahrer", "viewer"));
```

`AuthorizationPolicyBuilder` needs `using Microsoft.AspNetCore.Authorization;`.

Then mark the infrastructure endpoints anonymous. `MapDefaultEndpoints()` lives in ServiceDefaults and maps `/health` and `/alive`; rather than editing that shared project, chain the exemption where they are mapped in `Program.cs`:

```csharp
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
    // ... existing migration block unchanged
}
```

If `MapDefaultEndpoints()` returns void and cannot be chained, add `.AllowAnonymous()` inside `src/TransBrain.ServiceDefaults/Extensions.cs` on the two health-check maps and say so in your report — that is a shared-project edit and I want it visible, not silent.

- [ ] **Step 4: Run the tests to verify they still pass**

Run: `dotnet test TransBrain.slnx`
Expected: all pass, including the three fallback tests. A 401 from `/health` means the exemption did not take.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Api tests/TransBrain.Api.IntegrationTests
git commit -m "feat(api): fail closed with a fallback authorization policy"
```

---

### Task 3: Driver domain

**Files:**
- Create: `src/TransBrain.Domain/Drivers/LicenseClass.cs`, `DriverStatus.cs`, `Driver.cs`
- Test: `tests/TransBrain.Domain.Tests/Drivers/DriverTests.cs`

**Interfaces:**
- Consumes: `Result<T>`, `Error`.
- Produces: `enum LicenseClass { B, C1, C, CE }`; `enum DriverStatus { Available, Absent, Inactive }`; `sealed class Driver` with `Guid Id`, `string FirstName`, `string LastName`, `IReadOnlyCollection<LicenseClass> LicenseClasses`, `DateOnly LicenseValidUntil`, `DriverStatus Status`, `string? ExternalUserId`; `static Result<Driver> Create(string firstName, string lastName, IReadOnlyCollection<LicenseClass> licenseClasses, DateOnly licenseValidUntil, string? externalUserId)`; `Result<Driver> Update(...)` with the same parameters; `void MarkAbsent()`, `void MarkAvailable()`, `void Deactivate()`; `bool CanDriveOn(DateOnly date)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Domain.Tests.Drivers;

public class DriverTests
{
    private static readonly DateOnly ValidUntil = new(2028, 6, 30);
    private static readonly LicenseClass[] Classes = [LicenseClass.C, LicenseClass.CE];

    [Fact]
    public void Create_ValidArguments_ReturnsAvailableDriver()
    {
        Result<Driver> result = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.FirstName.Should().Be("Frank");
        result.Value.LastName.Should().Be("Fahrer");
        result.Value.LicenseClasses.Should().BeEquivalentTo(Classes);
        result.Value.Status.Should().Be(DriverStatus.Available);
        result.Value.ExternalUserId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankFirstName_ReturnsValidationError(string firstName)
    {
        Result<Driver> result = Driver.Create(firstName, "Fahrer", Classes, ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankLastName_ReturnsValidationError(string lastName)
    {
        Result<Driver> result = Driver.Create("Frank", lastName, Classes, ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LastNameRequired");
    }

    [Fact]
    public void Create_NoLicenseClasses_ReturnsValidationError()
    {
        Result<Driver> result = Driver.Create("Frank", "Fahrer", [], ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseClassRequired");
    }

    [Fact]
    public void Create_DuplicateLicenseClasses_StoresEachOnce()
    {
        Result<Driver> result = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.C, LicenseClass.C, LicenseClass.CE], ValidUntil, null);

        result.Value.LicenseClasses.Should().BeEquivalentTo([LicenseClass.C, LicenseClass.CE]);
    }

    [Fact]
    public void Create_NamesWithSurroundingWhitespace_StoresThemTrimmed()
    {
        Result<Driver> result = Driver.Create("  Frank  ", "  Fahrer ", Classes, ValidUntil, null);

        result.Value.FirstName.Should().Be("Frank");
        result.Value.LastName.Should().Be("Fahrer");
    }

    [Fact]
    public void CanDriveOn_AvailableAndLicenceStillValid_ReturnsTrue()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        driver.CanDriveOn(new DateOnly(2028, 6, 30)).Should().BeTrue();
    }

    [Fact]
    public void CanDriveOn_LicenceExpiredBeforeThatDate_ReturnsFalse()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        driver.CanDriveOn(new DateOnly(2028, 7, 1)).Should().BeFalse();
    }

    [Fact]
    public void CanDriveOn_DriverAbsent_ReturnsFalse()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.MarkAbsent();

        driver.CanDriveOn(new DateOnly(2027, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public void MarkAvailable_AfterBeingAbsent_RestoresAvailability()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.MarkAbsent();

        driver.MarkAvailable();

        driver.Status.Should().Be(DriverStatus.Available);
    }

    [Fact]
    public void MarkAvailable_AfterDeactivation_LeavesDriverInactive()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.Deactivate();

        driver.MarkAvailable();

        driver.Status.Should().Be(DriverStatus.Inactive);
    }

    [Fact]
    public void Update_ValidArguments_ReplacesNamesAndLicence()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        Result<Driver> result = driver.Update("Franz", "Fahrer", [LicenseClass.B], new DateOnly(2030, 1, 1), "sub-123");

        result.IsSuccess.Should().BeTrue();
        driver.FirstName.Should().Be("Franz");
        driver.LicenseClasses.Should().BeEquivalentTo([LicenseClass.B]);
        driver.LicenseValidUntil.Should().Be(new DateOnly(2030, 1, 1));
        driver.ExternalUserId.Should().Be("sub-123");
    }

    [Fact]
    public void Update_NoLicenseClasses_ReturnsValidationErrorAndLeavesDriverUnchanged()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        Result<Driver> result = driver.Update("Franz", "Fahrer", [], ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        driver.FirstName.Should().Be("Frank");
        driver.LicenseClasses.Should().BeEquivalentTo(Classes);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~DriverTests`
Expected: compile error — `Driver` does not exist.

- [ ] **Step 3: Implement the enums**

`src/TransBrain.Domain/Drivers/LicenseClass.cs`:

```csharp
namespace TransBrain.Domain.Drivers;

public enum LicenseClass
{
    B,
    C1,
    C,
    CE
}
```

`src/TransBrain.Domain/Drivers/DriverStatus.cs`:

```csharp
namespace TransBrain.Domain.Drivers;

public enum DriverStatus
{
    Available,
    Absent,
    Inactive
}
```

- [ ] **Step 4: Implement `Driver`**

`Update` validates before mutating, so a rejected update leaves the entity exactly as it was — the last test pins that. `MarkAvailable` deliberately refuses to resurrect a deactivated driver; deactivation is an administrative decision that an availability toggle must not undo.

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

public sealed class Driver
{
    private readonly HashSet<LicenseClass> _licenseClasses = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Driver()
    {
        FirstName = null!;
        LastName = null!;
    }

    private Driver(
        Guid id,
        string firstName,
        string lastName,
        IEnumerable<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        _licenseClasses = [.. licenseClasses];
        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = externalUserId;
        Status = DriverStatus.Available;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public IReadOnlyCollection<LicenseClass> LicenseClasses => _licenseClasses;

    public DateOnly LicenseValidUntil { get; private set; }

    public DriverStatus Status { get; private set; }

    /// <summary>Keycloak's <c>sub</c> claim, set when the driver has a login.</summary>
    public string? ExternalUserId { get; private set; }

    public static Result<Driver> Create(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Result<Unit> validation = Validate(firstName, lastName, licenseClasses);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        return new Driver(
            Guid.CreateVersion7(),
            firstName.Trim(),
            lastName.Trim(),
            licenseClasses,
            licenseValidUntil,
            NormalizeExternalUserId(externalUserId));
    }

    public Result<Driver> Update(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Result<Unit> validation = Validate(firstName, lastName, licenseClasses);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        _licenseClasses.Clear();
        foreach (LicenseClass licenseClass in licenseClasses)
        {
            _licenseClasses.Add(licenseClass);
        }

        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = NormalizeExternalUserId(externalUserId);

        return this;
    }

    public void MarkAbsent()
    {
        if (Status == DriverStatus.Available)
        {
            Status = DriverStatus.Absent;
        }
    }

    /// <remarks>
    /// Deliberately refuses to revive an inactive driver: deactivation is an administrative
    /// decision, and an availability toggle must not silently undo it.
    /// </remarks>
    public void MarkAvailable()
    {
        if (Status == DriverStatus.Absent)
        {
            Status = DriverStatus.Available;
        }
    }

    public void Deactivate() => Status = DriverStatus.Inactive;

    public bool CanDriveOn(DateOnly date) =>
        Status == DriverStatus.Available && LicenseValidUntil >= date;

    private static Result<Unit> Validate(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Error.Validation("Driver.FirstNameRequired", "First name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Error.Validation("Driver.LastNameRequired", "Last name must not be empty.");
        }

        if (licenseClasses.Count == 0)
        {
            return Error.Validation("Driver.LicenseClassRequired", "At least one licence class is required.");
        }

        return Unit.Value;
    }

    private static string? NormalizeExternalUserId(string? externalUserId) =>
        string.IsNullOrWhiteSpace(externalUserId) ? null : externalUserId.Trim();
}
```

This needs a `Unit` type for the private validation helper, since `Result<T>` has no non-generic form. Add `src/TransBrain.Domain/Common/Unit.cs`:

```csharp
namespace TransBrain.Domain.Common;

/// <summary>A result that carries no value. Used where an operation can only succeed or fail.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Domain.Tests`
Expected: all Domain tests pass, including the 15 new `DriverTests`.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Domain tests/TransBrain.Domain.Tests
git commit -m "feat(domain): add Driver aggregate with licence and availability rules"
```

---

### Task 4: Driver repository abstraction and the CreateDriver slice

**Files:**
- Create: `src/TransBrain.Application/Abstractions/IDriverRepository.cs`
- Create: `src/TransBrain.Application/Features/Drivers/DriverResponse.cs`
- Create: `src/TransBrain.Application/Features/Drivers/CreateDriver/CreateDriverCommand.cs`, `CreateDriverCommandHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Fakes/InMemoryDriverRepository.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Drivers/CreateDriverCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Driver`, `LicenseClass`, `Result<T>`, `Error`, `ICommand<>`, `ICommandHandler<,>`.
- Produces:
  - `interface IDriverRepository` with `Task<Result<Driver>> AddAsync(Driver driver, CancellationToken ct)`, `Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct)`, `Task<IReadOnlyList<Driver>> ListAsync(int skip, int take, DriverStatus? status, CancellationToken ct)`, `Task<int> CountAsync(DriverStatus? status, CancellationToken ct)`, `Task SaveChangesAsync(CancellationToken ct)`, `Task RemoveAsync(Driver driver, CancellationToken ct)`
  - `sealed record DriverResponse(Guid Id, string FirstName, string LastName, string[] LicenseClasses, DateOnly LicenseValidUntil, string Status, string? ExternalUserId)` with `static DriverResponse From(Driver driver)`
  - `sealed record CreateDriverCommand(string FirstName, string LastName, string[] LicenseClasses, DateOnly LicenseValidUntil, string? ExternalUserId) : ICommand<DriverResponse>`

- [ ] **Step 1: Write the in-memory fake**

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryDriverRepository : IDriverRepository
{
    private readonly List<Driver> _drivers = [];

    public IReadOnlyList<Driver> Drivers => _drivers;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(params Driver[] drivers) => _drivers.AddRange(drivers);

    public Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken)
    {
        _drivers.Add(driver);
        return Task.FromResult(Result<Driver>.Success(driver));
    }

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_drivers.SingleOrDefault(d => d.Id == id));

    // Ordinal ordering, matching the EF repository's column collation. The fake must not
    // define a different notion of "sorted" from the one production uses.
    public Task<IReadOnlyList<Driver>> ListAsync(
        int skip, int take, DriverStatus? status, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Driver>>(
            Filter(status)
                .OrderBy(d => d.LastName, StringComparer.Ordinal)
                .ThenBy(d => d.FirstName, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken)
        => Task.FromResult(Filter(status).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Driver driver, CancellationToken cancellationToken)
    {
        _drivers.Remove(driver);
        return Task.CompletedTask;
    }

    private IEnumerable<Driver> Filter(DriverStatus? status)
        => status is null ? _drivers : _drivers.Where(d => d.Status == status);
}
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Drivers;

public class CreateDriverCommandHandlerTests
{
    private static CreateDriverCommand ValidCommand => new(
        "Frank", "Fahrer", ["C", "CE"], new DateOnly(2028, 6, 30), null);

    [Fact]
    public async Task Handle_ValidCommand_PersistsDriverAndReturnsResponse()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Frank");
        result.Value.LicenseClasses.Should().BeEquivalentTo(["C", "CE"]);
        result.Value.Status.Should().Be("Available");
        repository.Drivers.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownLicenseClass_ReturnsValidationError()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = ["C", "Rocket"] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
        repository.Drivers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NumericLicenseClass_ReturnsValidationError()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = ["99"] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
    }

    [Fact]
    public async Task Handle_BlankFirstName_ReturnsDomainValidationError()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { FirstName = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
    }

    [Fact]
    public async Task Handle_NoLicenseClasses_ReturnsDomainValidationError()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = [] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseClassRequired");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~CreateDriverCommandHandlerTests`
Expected: compile errors — none of these types exist yet.

- [ ] **Step 4: Implement the abstraction and the response record**

`src/TransBrain.Application/Abstractions/IDriverRepository.cs`:

```csharp
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Abstractions;

public interface IDriverRepository
{
    Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken);

    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Driver>> ListAsync(int skip, int take, DriverStatus? status, CancellationToken cancellationToken);

    Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Driver driver, CancellationToken cancellationToken);
}
```

`src/TransBrain.Application/Features/Drivers/DriverResponse.cs`:

```csharp
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

public sealed record DriverResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string Status,
    string? ExternalUserId)
{
    public static DriverResponse From(Driver driver) => new(
        driver.Id,
        driver.FirstName,
        driver.LastName,
        driver.LicenseClasses.Select(c => c.ToString()).ToArray(),
        driver.LicenseValidUntil,
        driver.Status.ToString(),
        driver.ExternalUserId);
}
```

- [ ] **Step 5: Implement the command and handler**

Note the `Enum.IsDefined` guard. Phase 1 shipped a handler without it and `"99"` parsed into an undefined enum value that reached the database — the whole-branch review caught it. Do not repeat that here.

`CreateDriverCommand.cs`:

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

public sealed record CreateDriverCommand(
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId) : ICommand<DriverResponse>;
```

`CreateDriverCommandHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

internal sealed class CreateDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<CreateDriverCommand, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        CreateDriverCommand command,
        CancellationToken cancellationToken)
    {
        Result<LicenseClass[]> classes = LicenseClassParser.Parse(command.LicenseClasses);
        if (!classes.IsSuccess)
        {
            return classes.Error!;
        }

        Result<Driver> driver = Driver.Create(
            command.FirstName,
            command.LastName,
            classes.Value,
            command.LicenseValidUntil,
            command.ExternalUserId);

        if (!driver.IsSuccess)
        {
            return driver.Error!;
        }

        Result<Driver> added = await repository.AddAsync(driver.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return DriverResponse.From(added.Value);
    }
}
```

The parser is shared by Create and Update, so it lives in its own file, `src/TransBrain.Application/Features/Drivers/LicenseClassParser.cs`:

```csharp
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

internal static class LicenseClassParser
{
    public static Result<LicenseClass[]> Parse(IReadOnlyCollection<string> values)
    {
        List<LicenseClass> parsed = new(values.Count);

        foreach (string value in values)
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise become an
            // undefined enum member and reach the database. IsDefined closes that gap.
            if (!Enum.TryParse(value, ignoreCase: true, out LicenseClass licenseClass)
                || !Enum.IsDefined(licenseClass))
            {
                return Error.Validation(
                    "Driver.UnknownLicenseClass",
                    $"'{value}' is not a known licence class.");
            }

            parsed.Add(licenseClass);
        }

        return parsed.ToArray();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~CreateDriverCommandHandlerTests`
Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add CreateDriver slice with licence class parsing"
```

---

### Task 5: ListDrivers and GetDriverById slices

**Files:**
- Create: `src/TransBrain.Application/Features/Drivers/ListDrivers/ListDriversQuery.cs`, `ListDriversQueryValidator.cs`, `ListDriversQueryHandler.cs`
- Create: `src/TransBrain.Application/Features/Drivers/GetDriverById/GetDriverByIdQuery.cs`, `GetDriverByIdQueryHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Drivers/ListDriversQueryHandlerTests.cs`, `GetDriverByIdQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IDriverRepository`, `DriverResponse`, `PagedResult<T>`, `IQuery<>`, `IQueryHandler<,>`.
- Produces: `sealed record ListDriversQuery(int Page = 1, int PageSize = 20, string? Status = null) : IQuery<PagedResult<DriverResponse>>`; `sealed record GetDriverByIdQuery(Guid Id) : IQuery<DriverResponse>`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.ListDrivers;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class ListDriversQueryHandlerTests
{
    private static Driver DriverNamed(string firstName, string lastName) =>
        Driver.Create(firstName, lastName, [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        ListDriversQueryHandler handler = new(new InMemoryDriverRepository());

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FirstPage_OrdersByLastNameThenFirstName()
    {
        InMemoryDriverRepository repository = new();
        repository.Seed(DriverNamed("Bea", "Zimmer"), DriverNamed("Anton", "Meier"), DriverNamed("Zoe", "Meier"));
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(), CancellationToken.None);

        result.Value.Items.Select(d => d.LastName + "," + d.FirstName)
            .Should().ContainInOrder("Meier,Anton", "Meier,Zoe", "Zimmer,Bea");
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        InMemoryDriverRepository repository = new();
        repository.Seed(DriverNamed("A", "Aa"), DriverNamed("B", "Bb"), DriverNamed("C", "Cc"));
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Cc");
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingDriversAndCountsOnlyThose()
    {
        InMemoryDriverRepository repository = new();
        Driver absent = DriverNamed("Abs", "Ent");
        absent.MarkAbsent();
        repository.Seed(DriverNamed("Ava", "Ilable"), absent);
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Status: "Absent"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Status.Should().Be("Absent");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownStatusFilter_ReturnsValidationError()
    {
        ListDriversQueryHandler handler = new(new InMemoryDriverRepository());

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Status: "Sleeping"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownStatus");
    }
}
```

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.GetDriverById;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class GetDriverByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_KnownId_ReturnsDriver()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;
        repository.Seed(driver);
        GetDriverByIdQueryHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new GetDriverByIdQuery(driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(driver.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        GetDriverByIdQueryHandler handler = new(new InMemoryDriverRepository());

        Result<DriverResponse> result = await handler.Handle(
            new GetDriverByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Driver.NotFound");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Drivers`
Expected: compile errors — the queries and handlers do not exist.

- [ ] **Step 3: Implement `ListDrivers`**

`ListDriversQuery.cs`:

```csharp
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

public sealed record ListDriversQuery(int Page = 1, int PageSize = 20, string? Status = null)
    : IQuery<PagedResult<DriverResponse>>;
```

`ListDriversQueryValidator.cs` — paging bounds only. The status string is parsed in the handler, because an unknown value is a domain-vocabulary failure with its own code, not a shape problem:

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

public sealed class ListDriversQueryValidator : AbstractValidator<ListDriversQuery>
{
    public ListDriversQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
```

`ListDriversQueryHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

internal sealed class ListDriversQueryHandler(IDriverRepository repository)
    : IQueryHandler<ListDriversQuery, PagedResult<DriverResponse>>
{
    public async Task<Result<PagedResult<DriverResponse>>> Handle(
        ListDriversQuery query,
        CancellationToken cancellationToken)
    {
        DriverStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out DriverStatus parsed)
                || !Enum.IsDefined(parsed))
            {
                return Error.Validation("Driver.UnknownStatus", $"'{query.Status}' is not a known driver status.");
            }

            status = parsed;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Driver> drivers = await repository.ListAsync(skip, query.PageSize, status, cancellationToken);
        int totalCount = await repository.CountAsync(status, cancellationToken);

        DriverResponse[] items = drivers.Select(DriverResponse.From).ToArray();

        return new PagedResult<DriverResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
```

- [ ] **Step 4: Implement `GetDriverById`**

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.GetDriverById;

public sealed record GetDriverByIdQuery(Guid Id) : IQuery<DriverResponse>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.GetDriverById;

internal sealed class GetDriverByIdQueryHandler(IDriverRepository repository)
    : IQueryHandler<GetDriverByIdQuery, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        GetDriverByIdQuery query,
        CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(query.Id, cancellationToken);

        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{query.Id}'.");
        }

        return DriverResponse.From(driver);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add ListDrivers and GetDriverById slices"
```

---

### Task 6: UpdateDriver and DeleteDriver slices

**Files:**
- Create: `src/TransBrain.Application/Features/Drivers/UpdateDriver/UpdateDriverCommand.cs`, `UpdateDriverCommandHandler.cs`
- Create: `src/TransBrain.Application/Features/Drivers/DeleteDriver/DeleteDriverCommand.cs`, `DeleteDriverCommandHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Drivers/UpdateDriverCommandHandlerTests.cs`, `DeleteDriverCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IDriverRepository`, `Driver.Update`, `LicenseClassParser`, `DriverResponse`.
- Produces: `sealed record UpdateDriverCommand(Guid Id, string FirstName, string LastName, string[] LicenseClasses, DateOnly LicenseValidUntil, string? ExternalUserId) : ICommand<DriverResponse>`; `sealed record DeleteDriverCommand(Guid Id) : ICommand<Unit>`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class UpdateDriverCommandHandlerTests
{
    private static Driver ExistingDriver() =>
        Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;

    [Fact]
    public async Task Handle_KnownDriver_UpdatesFieldsAndSavesOnce()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        UpdateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "Franz", "Fahrer", ["B"], new DateOnly(2030, 1, 1), "sub-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Franz");
        result.Value.LicenseClasses.Should().BeEquivalentTo(["B"]);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        UpdateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(Guid.CreateVersion7(), "A", "B", ["B"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidUpdate_LeavesDriverUnchangedAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        UpdateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "   ", "Fahrer", ["B"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
        driver.FirstName.Should().Be("Frank");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownLicenseClass_ReturnsValidationErrorAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        UpdateDriverCommandHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "Franz", "Fahrer", ["Rocket"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
```

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Drivers.DeleteDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class DeleteDriverCommandHandlerTests
{
    [Fact]
    public async Task Handle_KnownDriver_RemovesItAndSaves()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;
        repository.Seed(driver);
        DeleteDriverCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(new DeleteDriverCommand(driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Drivers.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        DeleteDriverCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteDriverCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Driver`
Expected: compile errors.

- [ ] **Step 3: Implement `UpdateDriver`**

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

public sealed record UpdateDriverCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId) : ICommand<DriverResponse>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

internal sealed class UpdateDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<UpdateDriverCommand, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        UpdateDriverCommand command,
        CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.Id}'.");
        }

        Result<LicenseClass[]> classes = LicenseClassParser.Parse(command.LicenseClasses);
        if (!classes.IsSuccess)
        {
            return classes.Error!;
        }

        Result<Driver> updated = driver.Update(
            command.FirstName,
            command.LastName,
            classes.Value,
            command.LicenseValidUntil,
            command.ExternalUserId);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return DriverResponse.From(updated.Value);
    }
}
```

- [ ] **Step 4: Implement `DeleteDriver`**

```csharp
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Drivers.DeleteDriver;

public sealed record DeleteDriverCommand(Guid Id) : ICommand<Unit>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.DeleteDriver;

internal sealed class DeleteDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<DeleteDriverCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteDriverCommand command, CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.Id}'.");
        }

        await repository.RemoveAsync(driver, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add UpdateDriver and DeleteDriver slices"
```

---

### Task 7: Driver persistence and migration

**Files:**
- Create: `src/TransBrain.Infrastructure/Persistence/Configurations/DriverConfiguration.cs`
- Create: `src/TransBrain.Infrastructure/Persistence/Repositories/DriverRepository.cs`
- Modify: `src/TransBrain.Infrastructure/Persistence/TransBrainDbContext.cs`, `src/TransBrain.Infrastructure/DependencyInjection.cs`
- Create: a generated migration under `src/TransBrain.Infrastructure/Persistence/Migrations/`

**Interfaces:**
- Consumes: `IDriverRepository`, `Driver`, `LicenseClass`, `DriverStatus`.
- Produces: `DbSet<Driver> Drivers` on the context; `AddInfrastructure()` additionally registers `IDriverRepository → DriverRepository`.

- [ ] **Step 1: Add the DbSet and the configuration**

Add to `TransBrainDbContext`:

```csharp
    public DbSet<Driver> Drivers => Set<Driver>();
```

`DriverConfiguration.cs` — the licence classes are a small fixed set, so they are stored as a sorted comma-separated string rather than a child table. Ordering the values on write keeps the column stable, so an unchanged driver does not produce a spurious update.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Drivers;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);

        // Ordered ordinally so this repository and InMemoryDriverRepository, which uses
        // StringComparer.Ordinal, cannot disagree about what "sorted by name" means.
        builder.Property(d => d.LastName).HasMaxLength(100).UseCollation("C").IsRequired();
        builder.Property(d => d.FirstName).HasMaxLength(100).UseCollation("C").IsRequired();

        builder.Property(d => d.LicenseValidUntil).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.ExternalUserId).HasMaxLength(200);

        builder.HasIndex(d => new { d.LastName, d.FirstName });

        builder.Property<string>("LicenseClassesRaw")
            .HasColumnName("license_classes")
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(d => d.LicenseClasses);
    }
}
```

The backing field and its mapping need the entity to expose something EF can write. Add to `Driver`, below the existing members:

```csharp
    // EF Core maps this shadow-ish projection rather than the collection itself: the licence
    // classes are a small fixed set, so a comma-separated column is cheaper than a child table.
    // Sorted on write so an unchanged driver never produces a spurious UPDATE.
    private string LicenseClassesRaw
    {
        get => string.Join(',', _licenseClasses.Select(c => c.ToString()).OrderBy(c => c, StringComparer.Ordinal));
        set
        {
            _licenseClasses.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(part, out LicenseClass parsed) && Enum.IsDefined(parsed))
                {
                    _licenseClasses.Add(parsed);
                }
            }
        }
    }
```

- [ ] **Step 2: Implement the repository**

```csharp
using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class DriverRepository(TransBrainDbContext context) : IDriverRepository
{
    public async Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken)
    {
        await context.Drivers.AddAsync(driver, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return driver;
    }

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Drivers.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Driver>> ListAsync(
        int skip, int take, DriverStatus? status, CancellationToken cancellationToken)
        => await Filter(status)
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken)
        => Filter(status).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(Driver driver, CancellationToken cancellationToken)
    {
        context.Drivers.Remove(driver);
        return Task.CompletedTask;
    }

    private IQueryable<Driver> Filter(DriverStatus? status)
        => status is null ? context.Drivers : context.Drivers.Where(d => d.Status == status);
}
```

Register it in `AddInfrastructure`:

```csharp
        services.AddScoped<IDriverRepository, DriverRepository>();
```

- [ ] **Step 3: Generate the migration**

```bash
dotnet ef migrations add AddDrivers \
  --project src/TransBrain.Infrastructure \
  --startup-project src/TransBrain.Api \
  --output-dir Persistence/Migrations
```

Expected: a `drivers` table with `license_classes` as `character varying(50)`, `LastName`/`FirstName` carrying `collation: "C"`, and a composite index on the two name columns.

- [ ] **Step 4: Verify the build and the suite**

Run: `dotnet build TransBrain.slnx` then `dotnet test TransBrain.slnx`
Expected: 0 warnings, 0 errors, 0 MSB3277; all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Infrastructure src/TransBrain.Domain
git commit -m "feat(infrastructure): persist drivers with an ordinal-collated name index"
```

---

### Task 8: Driver endpoints and integration tests

**Files:**
- Create: `src/TransBrain.Api/Endpoints/DriverEndpoints.cs`
- Test: `tests/TransBrain.Api.IntegrationTests/DriverEndpointsTests.cs`

**Interfaces:**
- Consumes: `ISender`, the five driver slices, `ResultExtensions`, `Policies`.
- Produces: `POST/GET/PUT/DELETE /api/drivers` and `GET /api/drivers/{id}`, discovered by the existing `IEndpointGroup` assembly scan.

- [ ] **Step 1: Write the failing integration tests**

Master data is admin-only for writes and readable by every role, exactly as vehicles are.

```csharp
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;

namespace TransBrain.Api.IntegrationTests;

public class DriverEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object NewDriver(string lastName) => new
    {
        firstName = "Frank",
        lastName,
        licenseClasses = new[] { "C", "CE" },
        licenseValidUntil = "2028-06-30",
        externalUserId = (string?)null
    };

    [Fact]
    public async Task PostDriver_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/drivers", NewDriver("Anon"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostDriver_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/drivers", NewDriver("Dispo"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostDriver_AsAdmin_ReturnsCreatedAndIsListable()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/drivers", NewDriver("Createable"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/drivers");
        PagedResult<DriverResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<DriverResponse>>();
        page!.Items.Should().Contain(d => d.LastName == "Createable");
    }

    [Fact]
    public async Task GetDriverById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/drivers/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutDriver_AsAdmin_UpdatesAndReturnsNewValues()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Updatable"));
        DriverResponse? driver = await created.Content.ReadFromJsonAsync<DriverResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/drivers/{driver!.Id}", new
        {
            firstName = "Franz",
            lastName = "Updatable",
            licenseClasses = new[] { "B" },
            licenseValidUntil = "2030-01-01",
            externalUserId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        DriverResponse? updated = await response.Content.ReadFromJsonAsync<DriverResponse>();
        updated!.FirstName.Should().Be("Franz");
        updated.LicenseClasses.Should().BeEquivalentTo(["B"]);
    }

    [Fact]
    public async Task DeleteDriver_AsAdmin_RemovesIt()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Deletable"));
        DriverResponse? driver = await created.Content.ReadFromJsonAsync<DriverResponse>();

        HttpResponseMessage response = await admin.DeleteAsync($"/api/drivers/{driver!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage after = await admin.GetAsync($"/api/drivers/{driver.Id}");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostDriver_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "",
            lastName = "",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("FirstName").And.Contain("LastName");
    }
}
```

The last test is the acceptance criterion for Task 1: it requires a validator on `CreateDriverCommand` that checks the two names are present as request shape. That is NOT a duplicate of the domain rule — it exists so the client receives both field errors at once, which the domain (returning one coded error) cannot express. Add `src/TransBrain.Application/Features/Drivers/CreateDriver/CreateDriverCommandValidator.cs`:

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks: if these two ever disagree, the domain wins and this is the copy
/// to delete.
/// </remarks>
public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty();
        RuleFor(c => c.LastName).NotEmpty();
        RuleFor(c => c.LicenseClasses).NotEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Api.IntegrationTests --filter FullyQualifiedName~DriverEndpointsTests`
Expected: 404s — the routes do not exist.

- [ ] **Step 3: Implement the endpoints**

```csharp
using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Features.Drivers.DeleteDriver;
using TransBrain.Application.Features.Drivers.GetDriverById;
using TransBrain.Application.Features.Drivers.ListDrivers;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class DriverEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/drivers").WithTags("Drivers");

        group.MapPost("/", async (CreateDriverCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(driver => Results.Created($"/api/drivers/{driver.Id}", driver));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("CreateDriver")
            .Produces<DriverResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (
                ISender sender, CancellationToken ct, int page = 1, int pageSize = 20, string? status = null) =>
            {
                Result<PagedResult<DriverResponse>> result =
                    await sender.Send(new ListDriversQuery(page, pageSize, status), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListDrivers")
            .Produces<PagedResult<DriverResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(new GetDriverByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetDriverById")
            .Produces<DriverResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateDriverRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(
                    new UpdateDriverCommand(
                        id,
                        request.FirstName,
                        request.LastName,
                        request.LicenseClasses,
                        request.LicenseValidUntil,
                        request.ExternalUserId),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateDriver")
            .Produces<DriverResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteDriverCommand(id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteDriver")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>Body of a driver update. The id comes from the route, not the payload.</summary>
public sealed record UpdateDriverRequest(
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TransBrain.slnx`
Expected: all pass, including the seven driver endpoint tests.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Api src/TransBrain.Application tests/TransBrain.Api.IntegrationTests
git commit -m "feat(api): add driver endpoints with master-data authorization"
```

---

### Task 9: Vehicle GetById, Update and Delete slices

**Files:**
- Modify: `src/TransBrain.Application/Abstractions/IVehicleRepository.cs`
- Modify: `src/TransBrain.Domain/Vehicles/Vehicle.cs` (add `Update`, `SendToWorkshop`, `ReturnToService`, `Decommission`)
- Create: `src/TransBrain.Application/Features/Vehicles/GetVehicleById/`, `UpdateVehicle/`, `DeleteVehicle/`
- Modify: `tests/TransBrain.Application.Tests/Fakes/InMemoryVehicleRepository.cs`
- Test: `tests/TransBrain.Domain.Tests/Vehicles/VehicleTests.cs` (extend), three new handler test files

**Interfaces:**
- Consumes: `Vehicle`, `LicensePlate`, `IVehicleRepository`.
- Produces: `Vehicle.Update(LicensePlate, VehicleType, int, decimal, DateOnly) : Result<Vehicle>`, `Vehicle.SendToWorkshop()`, `Vehicle.ReturnToService()`, `Vehicle.Decommission()`; `IVehicleRepository` gains `GetByIdAsync`, `SaveChangesAsync`, `RemoveAsync`, and `ExistsByLicensePlateAsync` gains an `excludingId` parameter; `GetVehicleByIdQuery(Guid Id)`, `UpdateVehicleCommand(Guid Id, string LicensePlate, string Type, int PayloadKg, decimal LoadMeters, DateOnly NextInspectionDue)`, `DeleteVehicleCommand(Guid Id)`.

- [ ] **Step 1: Write the failing domain tests**

Append to `VehicleTests`:

```csharp
    [Fact]
    public void Update_ValidArguments_ReplacesFields()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        LicensePlate newPlate = LicensePlate.Create("M-ZZ 9999").Value;

        Result<Vehicle> result = vehicle.Update(newPlate, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2029, 1, 1));

        result.IsSuccess.Should().BeTrue();
        vehicle.LicensePlate.Should().Be(newPlate);
        vehicle.Type.Should().Be(VehicleType.Tractor);
        vehicle.PayloadKg.Should().Be(24_000);
    }

    [Fact]
    public void Update_NonPositivePayload_LeavesVehicleUnchanged()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        Result<Vehicle> result = vehicle.Update(Plate, VehicleType.Van, 0, 4.0m, Inspection);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
        vehicle.PayloadKg.Should().Be(3_000);
    }

    [Fact]
    public void SendToWorkshop_AvailableVehicle_ChangesStatus()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        vehicle.SendToWorkshop();

        vehicle.Status.Should().Be(VehicleStatus.InWorkshop);
    }

    [Fact]
    public void ReturnToService_DecommissionedVehicle_StaysDecommissioned()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        vehicle.Decommission();

        vehicle.ReturnToService();

        vehicle.Status.Should().Be(VehicleStatus.Decommissioned);
    }
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~VehicleTests`
Expected: compile errors — the four methods do not exist.

- [ ] **Step 3: Extend `Vehicle`**

Mirrors `Driver`: validate before mutating, and a decommissioned vehicle cannot be revived by a status toggle.

```csharp
    public Result<Vehicle> Update(
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue)
    {
        if (payloadKg <= 0)
        {
            return Error.Validation("Vehicle.PayloadKgNotPositive", "Payload must be greater than zero.");
        }

        if (loadMeters <= 0m)
        {
            return Error.Validation("Vehicle.LoadMetersNotPositive", "Load meters must be greater than zero.");
        }

        LicensePlate = licensePlate;
        Type = type;
        PayloadKg = payloadKg;
        LoadMeters = loadMeters;
        NextInspectionDue = nextInspectionDue;

        return this;
    }

    public void SendToWorkshop()
    {
        if (Status == VehicleStatus.Available)
        {
            Status = VehicleStatus.InWorkshop;
        }
    }

    /// <remarks>Deliberately refuses to revive a decommissioned vehicle.</remarks>
    public void ReturnToService()
    {
        if (Status == VehicleStatus.InWorkshop)
        {
            Status = VehicleStatus.Available;
        }
    }

    public void Decommission() => Status = VehicleStatus.Decommissioned;
```

- [ ] **Step 4: Extend the repository abstraction and the fake**

`IVehicleRepository` becomes:

```csharp
public interface IVehicleRepository
{
    /// <param name="excludingId">
    /// Ignore this vehicle when checking uniqueness, so updating a vehicle without changing
    /// its plate does not collide with itself.
    /// </param>
    Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, Guid? excludingId, CancellationToken cancellationToken);

    Task<Result<Vehicle>> AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(
        int skip, int take, VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken);

    Task<int> CountAsync(VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken);
}
```

Update `InMemoryVehicleRepository` to match, keeping `StringComparer.Ordinal` ordering, adding a `SaveChangesCallCount`, and honouring `excludingId` and the two filters. Update `CreateVehicleCommandHandler`'s existing call to pass `excludingId: null`.

- [ ] **Step 5: Write the failing handler tests**

**Templates to open, not tasks to recall:** the driver equivalents are committed files you can read — `src/TransBrain.Application/Features/Drivers/GetDriverById/GetDriverByIdQueryHandler.cs`, `UpdateDriver/UpdateDriverCommandHandler.cs`, `DeleteDriver/DeleteDriverCommandHandler.cs`, and their tests under `tests/TransBrain.Application.Tests/Features/Drivers/`. Read those; the vehicle versions are the same shape with different types.

Write exactly these tests, in `tests/TransBrain.Application.Tests/Features/Vehicles/`:

`GetVehicleByIdQueryHandlerTests`
- `Handle_KnownId_ReturnsVehicle` — seeded vehicle, asserts `result.Value.Id`
- `Handle_UnknownId_ReturnsNotFound` — asserts `ErrorType.NotFound` and code `Vehicle.NotFound`

`UpdateVehicleCommandHandlerTests`
- `Handle_KnownVehicle_UpdatesFieldsAndSavesOnce` — asserts new values and `SaveChangesCallCount == 1`
- `Handle_UnknownVehicle_ReturnsNotFoundAndDoesNotSave`
- `Handle_NonPositivePayload_LeavesVehicleUnchangedAndDoesNotSave` — asserts code `Vehicle.PayloadKgNotPositive` and that the stored payload is untouched
- `Handle_PlateTakenByAnotherVehicle_ReturnsConflict` — seed two vehicles, update the first to the second's plate, assert `ErrorType.Conflict` and code `Vehicle.DuplicateLicensePlate`
- `Handle_UnchangedPlateOnSameVehicle_Succeeds` — **this is the test `excludingId` exists for**: updating a vehicle without changing its plate must not collide with itself

`DeleteVehicleCommandHandlerTests`
- `Handle_KnownVehicle_RemovesItAndSaves`
- `Handle_UnknownVehicle_ReturnsNotFoundAndDoesNotSave`

- [ ] **Step 6: Implement the three handlers**

`GetVehicleByIdQueryHandler` and `DeleteVehicleCommandHandler` are structurally identical to their driver counterparts — read those files and substitute the type. `UpdateVehicleCommandHandler` has one step the driver version does not, because a vehicle's plate is unique:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

internal sealed class UpdateVehicleCommandHandler(IVehicleRepository repository)
    : ICommandHandler<UpdateVehicleCommand, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        UpdateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.Id}'.");
        }

        Result<LicensePlate> plate = LicensePlate.Create(command.LicensePlate);
        if (!plate.IsSuccess)
        {
            return plate.Error!;
        }

        if (!Enum.TryParse(command.Type, ignoreCase: true, out VehicleType type) || !Enum.IsDefined(type))
        {
            return Error.Validation("Vehicle.UnknownType", $"'{command.Type}' is not a known vehicle type.");
        }

        // excludingId is what stops a vehicle colliding with its own plate on an update that
        // leaves the plate alone.
        if (await repository.ExistsByLicensePlateAsync(plate.Value, command.Id, cancellationToken))
        {
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{plate.Value}' already exists.");
        }

        Result<Vehicle> updated = vehicle.Update(
            plate.Value, type, command.PayloadKg, command.LoadMeters, command.NextInspectionDue);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return VehicleResponse.From(updated.Value);
    }
}
```

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test TransBrain.slnx`
Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat(application): add vehicle get, update and delete slices"
```

---

### Task 10: Vehicle list filters and endpoint completion

**Files:**
- Modify: `src/TransBrain.Application/Features/Vehicles/ListVehicles/ListVehiclesQuery.cs`, `ListVehiclesQueryHandler.cs`
- Modify: `src/TransBrain.Infrastructure/Persistence/Repositories/VehicleRepository.cs`
- Modify: `src/TransBrain.Api/Endpoints/VehicleEndpoints.cs`
- Test: extend `ListVehiclesQueryHandlerTests` and `VehicleEndpointsTests`

**Interfaces:**
- Consumes: the widened `IVehicleRepository`.
- Produces: `ListVehiclesQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Type = null)`; routes `GET /api/vehicles/{id}`, `PUT /api/vehicles/{id}`, `DELETE /api/vehicles/{id}`.

- [ ] **Step 1: Extend the query and its tests**

**Template to open:** `src/TransBrain.Application/Features/Drivers/ListDrivers/ListDriversQueryHandler.cs` already does exactly this for one filter. Read it; this task does the same for two.

Add these tests to `ListVehiclesQueryHandlerTests`:
- `Handle_StatusFilter_ReturnsOnlyMatchingVehiclesAndCountsOnlyThose` — seed one `Available` and one `InWorkshop`, filter on `"InWorkshop"`, assert one item and `TotalCount == 1`
- `Handle_TypeFilter_ReturnsOnlyMatchingVehicles` — seed a `Van` and a `Tractor`, filter on `"Tractor"`
- `Handle_StatusAndTypeFilter_AppliesBoth` — three vehicles, only one matching both
- `Handle_UnknownStatus_ReturnsValidationError` — asserts code `Vehicle.UnknownStatus`
- `Handle_UnknownType_ReturnsValidationError` — asserts code `Vehicle.UnknownType`
- `Handle_NumericStatus_ReturnsValidationError` — passes `"99"`; without `Enum.IsDefined` this parses and silently filters on an undefined value

Then widen the record to `ListVehiclesQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Type = null)` and give the handler two parse blocks in the shape `ListDriversQueryHandler` uses — `Enum.TryParse` plus `Enum.IsDefined`, returning `Error.Validation` with the code above, before any repository call.

- [ ] **Step 2: Implement the EF filtering**

`VehicleRepository.ListAsync` and `CountAsync` gain the same `Filter(status, type)` shape the driver repository uses. `ExistsByLicensePlateAsync` gains the `excludingId` parameter:

```csharp
    public Task<bool> ExistsByLicensePlateAsync(
        LicensePlate plate, Guid? excludingId, CancellationToken cancellationToken)
        => context.Vehicles.AnyAsync(
            v => v.LicensePlate == plate && (excludingId == null || v.Id != excludingId),
            cancellationToken);
```

- [ ] **Step 3: Add the three routes**

**Template to open:** `src/TransBrain.Api/Endpoints/DriverEndpoints.cs`. Add the same three route shapes to `VehicleEndpoints`, keeping the existing two untouched:

- `GET /{id:guid}` → `GetVehicleByIdQuery`, `.RequireAuthorization(Policies.Read)`, produces `VehicleResponse` or 404
- `PUT /{id:guid}` → `UpdateVehicleCommand`, `.RequireAuthorization(Policies.MasterDataWrite)`, produces `VehicleResponse`, validation problem, 404 or 409
- `DELETE /{id:guid}` → `DeleteVehicleCommand`, `.RequireAuthorization(Policies.MasterDataWrite)`, produces 204 or 404

Add the PUT body record alongside the endpoint class, taking the id from the route rather than the payload:

```csharp
/// <summary>Body of a vehicle update. The id comes from the route, not the payload.</summary>
public sealed record UpdateVehicleRequest(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue);
```

- [ ] **Step 4: Extend the integration tests**

Add to `VehicleEndpointsTests`:
- `GetVehicleById_KnownId_ReturnsVehicle`
- `GetVehicleById_UnknownId_ReturnsNotFound`
- `PutVehicle_AsAdmin_UpdatesAndReturnsNewValues`
- `PutVehicle_AsDisponent_ReturnsForbidden`
- `PutVehicle_PlateTakenByAnotherVehicle_ReturnsConflict`
- `PutVehicle_UnchangedPlate_ReturnsOk` — the round-trip proof for `excludingId`
- `DeleteVehicle_AsAdmin_RemovesIt`
- `DeleteVehicle_AsViewer_ReturnsForbidden`

Give every vehicle in these tests a distinct license plate; the container is shared across the class and the plate column is unique.

- [ ] **Step 5: Run the whole suite and commit**

```bash
dotnet test TransBrain.slnx
git add src tests
git commit -m "feat(api): complete vehicle master data with filters and full CRUD"
```

---

### Task 11: Redis cache for the two list queries

Spec §7: the master-data lists are cached with explicit invalidation on every write to the aggregate. Orders and tours are deliberately not cached.

**Files:**
- Create: `src/TransBrain.Application/Abstractions/ICacheService.cs`
- Create: `src/TransBrain.Infrastructure/Persistence/Caching/RedisCacheService.cs`
- Modify: the two list handlers and the six write handlers
- Modify: `src/TransBrain.Infrastructure/DependencyInjection.cs`
- Test: `tests/TransBrain.Application.Tests/Fakes/InMemoryCacheService.cs`, `tests/TransBrain.Application.Tests/Features/Vehicles/ListVehiclesCachingTests.cs`

**Interfaces:**
- Produces: `interface ICacheService` with `Task<T?> GetAsync<T>(string key, CancellationToken ct)`, `Task SetAsync<T>(string key, T value, CancellationToken ct)`, `Task RemoveByPrefixAsync(string prefix, CancellationToken ct)`; cache keys `vehicles:list:{page}:{size}:{status}:{type}` and `drivers:list:{page}:{size}:{status}`; prefixes `vehicles:` and `drivers:`.

- [ ] **Step 1: Write the failing caching tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class ListVehiclesCachingTests
{
    [Fact]
    public async Task Handle_CalledTwice_HitsTheRepositoryOnlyOnce()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        repository.ListCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentPage_DoesNotServeTheFirstPagesCachedResult()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(Page: 1), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(Page: 2), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateVehicle_AfterAListWasCached_InvalidatesIt()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler list = new(repository, cache);
        CreateVehicleCommandHandler create = new(repository, cache);

        await list.Handle(new ListVehiclesQuery(), CancellationToken.None);
        await create.Handle(
            new CreateVehicleCommand("M-NEW 1", "Van", 3_000, 4.0m, new DateOnly(2028, 1, 1)),
            CancellationToken.None);

        Result<PagedResult<VehicleResponse>> after = await list.Handle(new ListVehiclesQuery(), CancellationToken.None);

        after.Value.Items.Should().ContainSingle();
        cache.RemoveByPrefixCallCount.Should().Be(1);
    }
}
```

`InMemoryCacheService` stores objects in a dictionary, counts `RemoveByPrefixAsync` calls, and removes every key starting with the prefix. `CountingVehicleRepository` wraps `InMemoryVehicleRepository` and counts `ListAsync` calls.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Caching`
Expected: compile errors — `ICacheService` does not exist and the handlers take one constructor argument.

- [ ] **Step 3: Define the abstraction**

```csharp
namespace TransBrain.Application.Abstractions;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Drops every entry whose key starts with <paramref name="prefix"/>. Write handlers call
    /// this with the aggregate's prefix, because a single write can invalidate every page and
    /// every filter combination, not merely the page it touched.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement the Redis service**

`IDistributedCache` cannot enumerate keys, so prefix invalidation is done through the StackExchange.Redis connection Aspire already registers. Track the keys written under each prefix in a Redis set, so invalidation deletes exactly what was cached.

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Persistence.Caching;

internal sealed class RedisCacheService(IDistributedCache cache, IConnectionMultiplexer? connection)
    : ICacheService
{
    private static readonly DistributedCacheEntryOptions Options = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), Options, cancellationToken);

        if (connection is not null)
        {
            await connection.GetDatabase().SetAddAsync(IndexKey(Prefix(key)), key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            return;
        }

        IDatabase database = connection.GetDatabase();
        RedisValue[] keys = await database.SetMembersAsync(IndexKey(prefix));

        foreach (RedisValue key in keys)
        {
            await cache.RemoveAsync(key!, cancellationToken);
        }

        await database.KeyDeleteAsync(IndexKey(prefix));
    }

    private static string Prefix(string key) => key[..(key.IndexOf(':') + 1)];

    private static string IndexKey(string prefix) => $"__index:{prefix}";
}
```

`IConnectionMultiplexer` is nullable because the integration tests run with the in-memory distributed cache and no Redis. When it is absent, entries still cache and expire on their own; only prefix invalidation degrades, and the tests that care use the fake.

Register in `AddInfrastructure`: `services.AddScoped<ICacheService, RedisCacheService>();`

- [ ] **Step 5: Wire the handlers**

Both list handlers gain an `ICacheService` parameter, build their key from every query parameter, return a cache hit when present, and store the result otherwise. All six write handlers — create, update and delete for each aggregate — call `RemoveByPrefixAsync` with their aggregate's prefix after a successful write, and only after.

- [ ] **Step 6: Run the whole suite and commit**

```bash
dotnet test TransBrain.slnx
git add src tests
git commit -m "feat(infrastructure): cache master-data lists with prefix invalidation"
```

---

### Task 12: Angular master data screens

**Files:**
- Create: `src/TransBrain.Web/src/app/drivers/driver.service.ts`, `driver-list.component.ts`, `driver-form.component.ts`
- Modify: `src/TransBrain.Web/src/app/app.routes.ts`, `vehicles/vehicle.service.ts`, `vehicles/vehicle-list.component.ts`
- Create: `src/TransBrain.Web/src/app/vehicles/vehicle-form.component.ts`
- Test: `src/TransBrain.Web/e2e/drivers.spec.ts`

**Interfaces:**
- Consumes: `/api/drivers` and `/api/vehicles` as defined in Tasks 8 and 10.
- Produces: routes `/drivers`, `/drivers/new`, `/drivers/:id`, `/vehicles/new`, `/vehicles/:id`.

- [ ] **Step 1: Extend the API clients**

`vehicle.service.ts` gains `getById`, `create`, `update`, `remove`. `driver.service.ts` mirrors it with a `Driver` interface matching `DriverResponse` — note `licenseClasses` is `string[]`.

- [ ] **Step 2: Build the list and form components**

The list mirrors the existing vehicle list, including its error handling. The form is a reactive form; on a failed save it reads the ProblemDetails body and, **now that Task 1 makes the `errors` dictionary field-keyed**, maps each entry onto the matching control via `setErrors`. When the body carries no `errors` (a domain failure), display `detail` at form level and use the `errorCode` extension member if a specific message is warranted.

This supersedes the Phase 1 instruction that forbade binding those keys. Record that in a comment where the mapping happens, so the change of rule is visible at the place it applies.

- [ ] **Step 3: Add an e2e spec**

`drivers.spec.ts` logs in as `admin.user`, creates a driver through the UI, sees it in the list, edits it, and deletes it. Use `#username` / `#password` for the Keycloak form — `getByLabel('Password')` matches two elements and throws under strict mode.

- [ ] **Step 4: Verify**

`npm run build`, then `npm run e2e` against a stack started with `dotnet run --project src/TransBrain.AppHost`.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Web
git commit -m "feat(web): add driver management and vehicle editing"
```

---

### Task 13: Vue master data screens

**Files:**
- Create: `src/TransBrain.VueWeb/src/api/drivers.ts`, `src/views/DriverList.vue`, `src/views/DriverForm.vue`, `src/views/VehicleForm.vue`
- Modify: `src/TransBrain.VueWeb/src/main.ts` (routes), `src/api/vehicles.ts`
- Test: `src/TransBrain.VueWeb/e2e/drivers.spec.ts`

**Templates to open:** the Angular versions are committed files — `src/TransBrain.Web/src/app/drivers/driver.service.ts`, `driver-list.component.ts`, `driver-form.component.ts`, and `e2e/drivers.spec.ts`. Read them. This task produces the same behaviour in Vuetify idiom, and the existing `src/TransBrain.VueWeb/src/api/vehicles.ts` shows how this codebase writes an axios client with the bearer interceptor.

The two frontends must behave equivalently for a user: the same fields, the same validation messages, the same empty-list and error states. They differ only in framework idiom.

Do NOT change the `/callback` route or its comments. That divergence from Angular is deliberate and documented, and collapsing it once already discarded an authorization code silently.

- [ ] **Step 1: Extend the API client**

`src/TransBrain.VueWeb/src/api/drivers.ts` exporting a `Driver` interface matching `DriverResponse` — `id`, `firstName`, `lastName`, `licenseClasses: string[]`, `licenseValidUntil`, `status`, `externalUserId: string | null` — plus `listDrivers`, `getDriver`, `createDriver`, `updateDriver`, `deleteDriver`, all on the existing `/api` axios instance. Extend `vehicles.ts` with `getVehicle`, `createVehicle`, `updateVehicle`, `deleteVehicle`.

- [ ] **Step 2: Add the views and register the routes**

`DriverList.vue`, `DriverForm.vue`, `VehicleForm.vue`. Routes `/drivers`, `/drivers/new`, `/drivers/:id`, `/vehicles/new`, `/vehicles/:id` in `main.ts`. The forms map the ProblemDetails `errors` dictionary onto per-field messages — field-keyed since Task 1 — and fall back to `detail` at form level when the body carries no `errors`.

- [ ] **Step 3: Add the e2e spec**

`e2e/drivers.spec.ts`: log in as `admin.user`, create a driver through the UI, see it listed, edit it, delete it. Use `#username` and `#password` for the Keycloak form — `getByLabel('Password')` matches two elements and throws under strict mode.

- [ ] **Step 4: Verify with `npm run build` and `npm run e2e`**

Start the stack with `dotnet run --project src/TransBrain.AppHost`, never `aspire run`.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.VueWeb
git commit -m "feat(vueweb): add driver management and vehicle editing"
```

---

### Task 14: Coverage gate and documentation

Spec §11 sets a minimum of 80 % line coverage in the Application layer. Phase 1 deferred the gate because there was too little surface to measure meaningfully; with two full aggregates there now is.

**Files:**
- Modify: `.github/workflows/ci.yml`, `CHANGELOG.md`, `README.md`
- Create: `docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`

- [ ] **Step 1: Measure current Application-layer coverage**

```bash
dotnet test tests/TransBrain.Application.Tests --collect:"XPlat Code Coverage"
```

Report the figure. If it is already at or above 80 %, add the gate. **If it is below, do NOT lower the threshold to match** — report the number and name the untested paths, so the gap is a decision rather than a silently weakened rule.

- [ ] **Step 2: Add the coverage gate to CI**

Fail the backend job when Application-layer line coverage drops below 80 %.

- [ ] **Step 3: Write the operator guides**

`docs/BEDIENUNG_TRANSBRAIN_WEB.md` and `_VUEWEB.md`, in German, covering login, the vehicle list and form, the driver list and form, and what each role may do. Spec §13 requires these to be updated in the same change as any user-facing UI change.

- [ ] **Step 4: Update CHANGELOG and README**

CHANGELOG under `[Unreleased]`: the Driver aggregate, vehicle CRUD completion, list filters, Redis caching, per-field validation errors, and the fallback authorization policy. README: the new endpoints and the coverage gate.

- [ ] **Step 5: Correct AGENTS.md**

It still says three frontends and `FeWoBrain` in places, still names FluentAssertions, and claims Playwright runs in CI — which it does not. Fix all four. There is a pre-existing uncommitted partial edit in the working tree; incorporate it rather than reverting it, and check with the human partner if its intent is unclear.

- [ ] **Step 6: Commit**

```bash
git add .github CHANGELOG.md README.md AGENTS.md docs/
git commit -m "docs: add operator guides and enforce the application coverage gate"
```

---

## Out of scope for this plan

- `TransportOrder` with its status transitions — Phase 3
- `Tour` with capacity, licence and double-booking invariants, and the `Driver.ExternalUserId` claim check behind `TourStatusWrite` — Phase 4
- Playwright in CI — still needs a headless Aspire startup path
- Telematics, freight billing, multi-tenancy, route optimisation — not in this product's first release
