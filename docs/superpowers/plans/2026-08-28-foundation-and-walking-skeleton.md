# TransBrain Foundation & Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the TransBrain solution foundation and prove one complete vertical slice — Keycloak login → API → PostgreSQL → vehicle list — visible in both the Angular and the Vue frontend.

**Architecture:** Clean Architecture in four layers (`Domain → Application → Infrastructure → Api`) with vertical feature slices inside Application, a hand-rolled CQRS mediator, and the Result pattern instead of exceptions for control flow. .NET Aspire orchestrates PostgreSQL, Redis, Keycloak, the API and both frontends. Only the `Vehicles` aggregate is built here, with `Create` and `List`; every later domain repeats this proven shape.

**Tech Stack:** .NET 10 / C# 14, ASP.NET Minimal APIs, EF Core 10 + Npgsql, FluentValidation, Aspire 13.5.3, Keycloak, Redis, xUnit v2 + AwesomeAssertions + Testcontainers, Angular 22 + Material, Vue 3 + Vuetify, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-28-transbrain-dispatch-design.md`

## Global Constraints

- **Scope of this plan:** Spec phases 0 and 1 only. `Vehicles` gets `Create` and `List`. Drivers, TransportOrders, Tours, Redis caching, and vehicle Update/Delete belong to later plans and MUST NOT be built here.
- **.NET target framework:** `net10.0`. Nullable reference types enabled everywhere. File-scoped namespaces. 4-space indentation.
- **Package versions** (pinned centrally in `Directory.Packages.props`, exact values):
  - `Aspire.Hosting.PostgreSQL` `13.5.3`, `Aspire.Hosting.Redis` `13.5.3`, `Aspire.Hosting.JavaScript` `13.5.3`
  - `Aspire.Hosting.Keycloak` `13.5.3-preview.1.26425.3`, `Aspire.Keycloak.Authentication` `13.5.3-preview.1.26425.3` (preview, deliberately accepted)
  - `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` `13.5.3`, `Aspire.StackExchange.Redis.DistributedCaching` `13.5.3`
  - `Microsoft.EntityFrameworkCore.Design` `10.0.11`, `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3`
  - `FluentValidation` `12.1.1`, `FluentValidation.DependencyInjectionExtensions` `12.1.1`
  - `Testcontainers.PostgreSql` `4.14.0`, `AwesomeAssertions` `9.6.0`, `xunit` `2.9.3`, `Microsoft.NET.Test.Sdk`
- **Aspire package names verified 2026-08-28:** the Node.js hosting package is `Aspire.Hosting.JavaScript` — `Aspire.Hosting.NodeJs` stopped at 9.5.2 and does NOT work with Aspire 13. The API is `AddViteApp(name, appDirectory, runScriptName)` plus `.WithNpm()`; there is no `AddNpmApp` in Aspire 13.
- **Naming:** English for code and identifiers. Test names follow `Method_Scenario_ExpectedResult`.
- **Result pattern:** no exceptions for business outcomes. Handlers return `Result<T>`.
- **Ports:** API from Aspire, Angular `4200`, Vue `4300`, Keycloak `8080`. CORS allows exactly the two frontend origins.
- **Node.js:** `>= 26.4.0` required (26.7.0 present).
- **Commit style:** Conventional Commits. Commit at the end of every task.

---

### Task 1: Solution scaffold and central package management

Creates the solution, all project shells, and version pinning. Nothing here is testable behaviour, so the deliverable is "everything builds".

**Files:**
- Create: `TransBrain.slnx`, `Directory.Build.props`, `Directory.Packages.props`
- Create: `src/TransBrain.Domain/TransBrain.Domain.csproj`, `src/TransBrain.Application/TransBrain.Application.csproj`, `src/TransBrain.Infrastructure/TransBrain.Infrastructure.csproj`, `src/TransBrain.Api/TransBrain.Api.csproj`
- Create: `tests/TransBrain.Domain.Tests/`, `tests/TransBrain.Application.Tests/`, `tests/TransBrain.Api.IntegrationTests/`

**Interfaces:**
- Consumes: nothing.
- Produces: project layout and `Directory.Packages.props` that every later task adds `PackageVersion` entries to.

**Note on `nuget.config`:** this task deliberately does not create one — Task 10 Step 1 generates it from the Aspire template at the repository root, and two creators would conflict. The machine's default NuGet configuration restores everything Tasks 1-9 need.

- [ ] **Step 1: Create the solution and library projects**

```bash
dotnet new sln -n TransBrain -f slnx
dotnet new classlib -n TransBrain.Domain -o src/TransBrain.Domain -f net10.0
dotnet new classlib -n TransBrain.Application -o src/TransBrain.Application -f net10.0
dotnet new classlib -n TransBrain.Infrastructure -o src/TransBrain.Infrastructure -f net10.0
dotnet new web -n TransBrain.Api -o src/TransBrain.Api -f net10.0
rm src/TransBrain.Domain/Class1.cs src/TransBrain.Application/Class1.cs src/TransBrain.Infrastructure/Class1.cs
```

- [ ] **Step 2: Create the test projects**

```bash
dotnet new xunit -n TransBrain.Domain.Tests -o tests/TransBrain.Domain.Tests -f net10.0
dotnet new xunit -n TransBrain.Application.Tests -o tests/TransBrain.Application.Tests -f net10.0
dotnet new xunit -n TransBrain.Api.IntegrationTests -o tests/TransBrain.Api.IntegrationTests -f net10.0
rm tests/TransBrain.Domain.Tests/UnitTest1.cs tests/TransBrain.Application.Tests/UnitTest1.cs tests/TransBrain.Api.IntegrationTests/UnitTest1.cs
```

- [ ] **Step 3: Wire project references**

Dependencies point inward only. The Api references Infrastructure solely so it can call `AddInfrastructure` in DI.

```bash
dotnet add src/TransBrain.Application reference src/TransBrain.Domain
dotnet add src/TransBrain.Infrastructure reference src/TransBrain.Application
dotnet add src/TransBrain.Api reference src/TransBrain.Infrastructure
dotnet add tests/TransBrain.Domain.Tests reference src/TransBrain.Domain
dotnet add tests/TransBrain.Application.Tests reference src/TransBrain.Application
dotnet add tests/TransBrain.Api.IntegrationTests reference src/TransBrain.Api
dotnet sln TransBrain.slnx add src/**/*.csproj tests/**/*.csproj
```

- [ ] **Step 4: Write `Directory.Build.props`**

Sets the shared compiler settings from the Global Constraints once, for every project.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Write `Directory.Packages.props`**

Central Package Management: projects reference packages without versions; versions live only here.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Aspire.Hosting.PostgreSQL" Version="13.5.3" />
    <PackageVersion Include="Aspire.Hosting.Redis" Version="13.5.3" />
    <PackageVersion Include="Aspire.Hosting.JavaScript" Version="13.5.3" />
    <PackageVersion Include="Aspire.Hosting.Keycloak" Version="13.5.3-preview.1.26425.3" />
    <PackageVersion Include="Aspire.Keycloak.Authentication" Version="13.5.3-preview.1.26425.3" />
    <PackageVersion Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="13.5.3" />
    <PackageVersion Include="Aspire.StackExchange.Redis.DistributedCaching" Version="13.5.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.9.4" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.14.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
  </ItemGroup>
</Project>
```

Note: the xUnit `PackageVersion` entries are added by moving whatever versions `dotnet new xunit` generated in the test `.csproj` files into this file, then stripping the `Version` attributes from the `PackageReference` elements. Central Package Management fails the build if any `PackageReference` still carries a version.

**Resolved during execution (2026-08-28):** `dotnet new xunit` on this SDK produces **xUnit v2 (`xunit` 2.9.3)**, and emits no assertion-library reference at all. The assertion library is therefore added deliberately in Task 2, not inherited from the template — see Task 2 Step 0.

- [ ] **Step 6: Verify the whole solution builds**

Run: `dotnet build TransBrain.slnx`
Expected: `Build succeeded`, 0 errors, 0 warnings (warnings are errors).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with clean architecture layout and central package management"
```

---

### Task 2: Result pattern and Error type

The spine of every handler in the codebase. Built first because everything else returns it.

**Files:**
- Create: `src/TransBrain.Domain/Common/Error.cs`, `src/TransBrain.Domain/Common/Result.cs`
- Test: `tests/TransBrain.Domain.Tests/Common/ResultTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ErrorType` enum (`Validation`, `NotFound`, `Conflict`, `Forbidden`); `sealed record Error(string Code, string Message, ErrorType Type)` with static factories `Error.Validation/NotFound/Conflict/Forbidden(string code, string message)`; `readonly record struct Result<T>` with `IsSuccess`, `Value`, `Error`, `Result<T>.Success(T)`, `Result<T>.Failure(Error)`, and implicit conversions from `T` and from `Error`.

- [ ] **Step 0: Add the assertion library to all three test projects**

`dotnet new xunit` emits no assertion library. **AwesomeAssertions** is used rather than FluentAssertions: FluentAssertions 8.x is proprietary (Xceed Software Inc.) and this is commercial software, while AwesomeAssertions is an Apache-2.0 fork of FluentAssertions 7 with an identical API — every assertion in this plan compiles unchanged, only the `using` differs.

```bash
dotnet add tests/TransBrain.Domain.Tests package AwesomeAssertions --version 9.6.0
dotnet add tests/TransBrain.Application.Tests package AwesomeAssertions --version 9.6.0
dotnet add tests/TransBrain.Api.IntegrationTests package AwesomeAssertions --version 9.6.0
```

Then move the version into `Directory.Packages.props` as `<PackageVersion Include="AwesomeAssertions" Version="9.6.0" />` and strip the `Version` attributes from the three `PackageReference` elements — Central Package Management fails the build otherwise.

- [ ] **Step 1: Write the failing tests**

`tests/TransBrain.Domain.Tests/Common/ResultTests.cs`:

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Value_SuccessfulResult_ReturnsValue()
    {
        Result<int> result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Value_FailedResult_ThrowsInvalidOperationException()
    {
        Result<int> result = Result<int>.Failure(Error.NotFound("X.NotFound", "missing"));

        result.IsSuccess.Should().BeFalse();
        FluentActions.Invoking(() => result.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        Result<string> result = Error.Conflict("X.Conflict", "clash");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("X.Conflict");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~ResultTests`
Expected: compile error — `Result<>` and `Error` do not exist.

- [ ] **Step 3: Implement `Error`**

`src/TransBrain.Domain/Common/Error.cs`:

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
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
```

- [ ] **Step 4: Implement `Result<T>`**

`src/TransBrain.Domain/Common/Result.cs`:

```csharp
namespace TransBrain.Domain.Common;

public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = null;
    }

    private Result(Error error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~ResultTests`
Expected: 4 passed.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Domain/Common tests/TransBrain.Domain.Tests/Common
git commit -m "feat(domain): add Result pattern and Error type"
```

---

### Task 3: LicensePlate value object

First value object; establishes the `Create` → `Result<T>` factory shape every other VO and entity copies.

**Files:**
- Create: `src/TransBrain.Domain/Vehicles/LicensePlate.cs`
- Test: `tests/TransBrain.Domain.Tests/Vehicles/LicensePlateTests.cs`

**Interfaces:**
- Consumes: `Result<T>`, `Error` from Task 2.
- Produces: `sealed record LicensePlate` with `string Value`, `static Result<LicensePlate> Create(string? input)`, `override string ToString()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class LicensePlateTests
{
    [Fact]
    public void Create_ValidPlate_ReturnsNormalizedUppercaseValue()
    {
        Result<LicensePlate> result = LicensePlate.Create("  m-ab 1234 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("M-AB 1234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyPlate_ReturnsValidationError(string? input)
    {
        Result<LicensePlate> result = LicensePlate.Create(input);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_PlateLongerThan15Characters_ReturnsValidationError()
    {
        Result<LicensePlate> result = LicensePlate.Create(new string('A', 16));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.TooLong");
    }

    [Fact]
    public void Equals_SamePlateDifferentCasing_ReturnsTrue()
    {
        LicensePlate first = LicensePlate.Create("m-ab 1234").Value;
        LicensePlate second = LicensePlate.Create("M-AB 1234").Value;

        first.Should().Be(second);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~LicensePlateTests`
Expected: compile error — `LicensePlate` does not exist.

- [ ] **Step 3: Implement `LicensePlate`**

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

public sealed record LicensePlate
{
    private const int MaxLength = 15;

    private LicensePlate(string value) => Value = value;

    public string Value { get; }

    public static Result<LicensePlate> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("LicensePlate.Empty", "License plate must not be empty.");
        }

        string normalized = input.Trim().ToUpperInvariant();

        if (normalized.Length > MaxLength)
        {
            return Error.Validation("LicensePlate.TooLong", $"License plate must not exceed {MaxLength} characters.");
        }

        return new LicensePlate(normalized);
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~LicensePlateTests`
Expected: 6 passed (the `[Theory]` contributes 3).

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Domain/Vehicles tests/TransBrain.Domain.Tests/Vehicles
git commit -m "feat(domain): add LicensePlate value object"
```

---

### Task 4: Vehicle entity

**Files:**
- Create: `src/TransBrain.Domain/Vehicles/Vehicle.cs`, `src/TransBrain.Domain/Vehicles/VehicleType.cs`, `src/TransBrain.Domain/Vehicles/VehicleStatus.cs`
- Test: `tests/TransBrain.Domain.Tests/Vehicles/VehicleTests.cs`

**Interfaces:**
- Consumes: `LicensePlate`, `Result<T>`, `Error`.
- Produces: `enum VehicleType { Tractor, RigidTruck, Van }`; `enum VehicleStatus { Available, InWorkshop, Decommissioned }`; `sealed class Vehicle` with read-only properties `Guid Id`, `LicensePlate LicensePlate`, `VehicleType Type`, `int PayloadKg`, `decimal LoadMeters`, `DateOnly NextInspectionDue`, `VehicleStatus Status`, and `static Result<Vehicle> Create(LicensePlate licensePlate, VehicleType type, int payloadKg, decimal loadMeters, DateOnly nextInspectionDue)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleTests
{
    private static readonly LicensePlate Plate = LicensePlate.Create("M-AB 1234").Value;
    private static readonly DateOnly Inspection = new(2027, 3, 31);

    [Fact]
    public void Create_ValidArguments_ReturnsAvailableVehicleWithIdentity()
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Tractor, 24_000, 13.6m, Inspection);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.LicensePlate.Should().Be(Plate);
        result.Value.Type.Should().Be(VehicleType.Tractor);
        result.Value.PayloadKg.Should().Be(24_000);
        result.Value.LoadMeters.Should().Be(13.6m);
        result.Value.NextInspectionDue.Should().Be(Inspection);
        result.Value.Status.Should().Be(VehicleStatus.Available);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePayload_ReturnsValidationError(int payloadKg)
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Van, payloadKg, 4.0m, Inspection);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_NonPositiveLoadMeters_ReturnsValidationError()
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Van, 3_000, 0m, Inspection);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.LoadMetersNotPositive");
    }

    [Fact]
    public void Create_TwoVehicles_AssignsDistinctIdentities()
    {
        Vehicle first = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        Vehicle second = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        first.Id.Should().NotBe(second.Id);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~VehicleTests`
Expected: compile error — `Vehicle` does not exist.

- [ ] **Step 3: Implement the enums**

`src/TransBrain.Domain/Vehicles/VehicleType.cs`:

```csharp
namespace TransBrain.Domain.Vehicles;

public enum VehicleType
{
    Tractor,
    RigidTruck,
    Van
}
```

`src/TransBrain.Domain/Vehicles/VehicleStatus.cs`:

```csharp
namespace TransBrain.Domain.Vehicles;

public enum VehicleStatus
{
    Available,
    InWorkshop,
    Decommissioned
}
```

- [ ] **Step 4: Implement `Vehicle`**

The private parameterless constructor exists so EF Core can materialize the entity without a public setter surface. `Guid.CreateVersion7` gives time-ordered identifiers, which keeps the primary-key index compact in PostgreSQL.

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

public sealed class Vehicle
{
    private Vehicle()
    {
        LicensePlate = null!;
    }

    private Vehicle(
        Guid id,
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue,
        VehicleStatus status)
    {
        Id = id;
        LicensePlate = licensePlate;
        Type = type;
        PayloadKg = payloadKg;
        LoadMeters = loadMeters;
        NextInspectionDue = nextInspectionDue;
        Status = status;
    }

    public Guid Id { get; private set; }

    public LicensePlate LicensePlate { get; private set; }

    public VehicleType Type { get; private set; }

    public int PayloadKg { get; private set; }

    public decimal LoadMeters { get; private set; }

    public DateOnly NextInspectionDue { get; private set; }

    public VehicleStatus Status { get; private set; }

    public static Result<Vehicle> Create(
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

        return new Vehicle(
            Guid.CreateVersion7(),
            licensePlate,
            type,
            payloadKg,
            loadMeters,
            nextInspectionDue,
            VehicleStatus.Available);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Domain.Tests`
Expected: all Domain tests pass (Result, LicensePlate, Vehicle).

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Domain/Vehicles tests/TransBrain.Domain.Tests/Vehicles
git commit -m "feat(domain): add Vehicle entity with creation invariants"
```

---

### Task 5: CQRS messaging contracts and sender

The hand-rolled mediator. Deliberately not MediatR (commercially licensed from v13).

**Files:**
- Create: `src/TransBrain.Application/Common/Messaging/IMessage.cs`, `src/TransBrain.Application/Common/Messaging/ISender.cs`, `src/TransBrain.Application/Common/Messaging/IPipelineBehavior.cs`, `src/TransBrain.Application/Common/Messaging/Sender.cs`
- Test: `tests/TransBrain.Application.Tests/Common/Messaging/SenderTests.cs`

**Interfaces:**
- Consumes: `Result<T>` from Task 2.
- Produces:
  - `interface ICommand<TResponse>`, `interface IQuery<TResponse>`
  - `interface ICommandHandler<in TCommand, TResponse>` with `Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)`
  - `interface IQueryHandler<in TQuery, TResponse>` with `Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)`
  - `delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>()`
  - `interface IPipelineBehavior<in TRequest, TResponse>` with `Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)`
  - `interface ISender` with `Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct)` and the same for `IQuery<TResponse>`

- [ ] **Step 1: Write the failing tests**

The fake handler and behaviour in this test also document how production slices are shaped.

```csharp
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Messaging;

public class SenderTests
{
    private sealed record EchoCommand(string Text) : ICommand<string>;

    private sealed class EchoCommandHandler : ICommandHandler<EchoCommand, string>
    {
        public Task<Result<string>> Handle(EchoCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Success(command.Text));
    }

    private sealed record FailingQuery : IQuery<string>;

    private sealed class FailingQueryHandler : IQueryHandler<FailingQuery, string>
    {
        public Task<Result<string>> Handle(FailingQuery query, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Failure(Error.NotFound("Q.NotFound", "nothing here")));
    }

    private sealed class SuffixBehavior : IPipelineBehavior<EchoCommand, string>
    {
        public async Task<Result<string>> Handle(
            EchoCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            Result<string> result = await next();
            return result.IsSuccess ? Result<string>.Success(result.Value + "!") : result;
        }
    }

    private static ISender BuildSender(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddScoped<ISender, Sender>();
        services.AddScoped<ICommandHandler<EchoCommand, string>, EchoCommandHandler>();
        services.AddScoped<IQueryHandler<FailingQuery, string>, FailingQueryHandler>();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_CommandWithRegisteredHandler_ReturnsHandlerResult()
    {
        ISender sender = BuildSender();

        Result<string> result = await sender.Send(new EchoCommand("hello"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task Send_QueryWithFailingHandler_PropagatesError()
    {
        ISender sender = BuildSender();

        Result<string> result = await sender.Send(new FailingQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Send_CommandWithBehavior_RunsBehaviorAroundHandler()
    {
        ISender sender = BuildSender(services =>
            services.AddScoped<IPipelineBehavior<EchoCommand, string>, SuffixBehavior>());

        Result<string> result = await sender.Send(new EchoCommand("hello"), CancellationToken.None);

        result.Value.Should().Be("hello!");
    }

    [Fact]
    public async Task Send_CommandWithoutRegisteredHandler_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        services.AddScoped<ISender, Sender>();
        ISender sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        await FluentActions
            .Awaiting(() => sender.Send(new EchoCommand("hello"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Add the DI package to the Application project**

```bash
dotnet add src/TransBrain.Application package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add tests/TransBrain.Application.Tests package Microsoft.Extensions.DependencyInjection
```

Then move the emitted versions into `Directory.Packages.props` and strip them from the `.csproj` files.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~SenderTests`
Expected: compile error — `ISender` and `Sender` do not exist.

- [ ] **Step 4: Implement the message contracts**

`src/TransBrain.Application/Common/Messaging/IMessage.cs`:

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

public interface ICommand<TResponse>;

public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
```

`src/TransBrain.Application/Common/Messaging/IPipelineBehavior.cs`:

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

`src/TransBrain.Application/Common/Messaging/ISender.cs`:

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

public interface ISender
{
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);

    Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement `Sender`**

The runtime type of the request decides which closed generic handler to resolve, so resolution is reflective; `dynamic` then dispatches to the strongly typed `Handle` without hand-written expression trees. Behaviours are applied in reverse registration order so the first registered behaviour is the outermost.

```csharp
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
        => Dispatch<TResponse>(command, typeof(ICommandHandler<,>), cancellationToken);

    public Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        => Dispatch<TResponse>(query, typeof(IQueryHandler<,>), cancellationToken);

    private Task<Result<TResponse>> Dispatch<TResponse>(
        object request,
        Type openHandlerType,
        CancellationToken cancellationToken)
    {
        Type requestType = request.GetType();
        Type handlerType = openHandlerType.MakeGenericType(requestType, typeof(TResponse));

        object? handler = serviceProvider.GetService(handlerType);
        if (handler is null)
        {
            throw new InvalidOperationException($"No handler registered for {requestType.Name}.");
        }

        RequestHandlerDelegate<TResponse> pipeline = () =>
            (Task<Result<TResponse>>)((dynamic)handler).Handle((dynamic)request, cancellationToken);

        Type behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        object?[] behaviors = serviceProvider.GetServices(behaviorType).ToArray();

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            object behavior = behaviors[i]!;
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = () => (Task<Result<TResponse>>)((dynamic)behavior).Handle((dynamic)request, next, cancellationToken);
        }

        return pipeline();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~SenderTests`
Expected: 4 passed.

If the build fails with "Compiler dynamic dispatch requires Microsoft.CSharp", add `<PackageReference Include="Microsoft.CSharp" />` to the Application project and a matching `PackageVersion`.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Application/Common tests/TransBrain.Application.Tests/Common
git commit -m "feat(application): add hand-rolled CQRS sender with pipeline behaviors"
```

---

### Task 6: Validation and logging pipeline behaviors, plus Application DI registration

**Files:**
- Create: `src/TransBrain.Application/Common/Behaviors/ValidationBehavior.cs`, `src/TransBrain.Application/Common/Behaviors/LoggingBehavior.cs`, `src/TransBrain.Application/DependencyInjection.cs`
- Test: `tests/TransBrain.Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs`

**Interfaces:**
- Consumes: `IPipelineBehavior<,>`, `RequestHandlerDelegate<>`, `Result<T>`, `Error`.
- Produces: `ValidationBehavior<TRequest, TResponse>`; `LoggingBehavior<TRequest, TResponse>`; `public static IServiceCollection AddApplication(this IServiceCollection services)` which registers `ISender`, both behaviours (logging first, validation second), all `ICommandHandler<,>`/`IQueryHandler<,>` implementations, and all FluentValidation validators from the Application assembly.

- [ ] **Step 1: Write the failing tests**

Validation failures must surface as `Result` failures with `ErrorType.Validation`, never as thrown `ValidationException`s.

```csharp
using AwesomeAssertions;
using FluentValidation;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() => RuleFor(c => c.Name).NotEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNextAndReturnsItsResult()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([new SampleCommandValidator()]);
        bool nextCalled = false;

        Result<string> result = await behavior.Handle(
            new SampleCommand("ok"),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Result<string>.Success("done"));
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.Value.Should().Be("done");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ReturnsValidationErrorWithoutCallingNext()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([new SampleCommandValidator()]);
        bool nextCalled = false;

        Result<string> result = await behavior.Handle(
            new SampleCommand(string.Empty),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Result<string>.Success("done"));
            },
            CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Name");
    }

    [Fact]
    public async Task Handle_NoValidatorsRegistered_CallsNext()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([]);

        Result<string> result = await behavior.Handle(
            new SampleCommand(string.Empty),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.Value.Should().Be("done");
    }
}
```

- [ ] **Step 2: Add FluentValidation and logging packages**

```bash
dotnet add src/TransBrain.Application package FluentValidation
dotnet add src/TransBrain.Application package FluentValidation.DependencyInjectionExtensions
dotnet add src/TransBrain.Application package Microsoft.Extensions.Logging.Abstractions
dotnet add tests/TransBrain.Application.Tests package FluentValidation
```

Move versions into `Directory.Packages.props`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~ValidationBehaviorTests`
Expected: compile error — `ValidationBehavior` does not exist.

- [ ] **Step 4: Implement `ValidationBehavior`**

Only the first failure is reported as the `Error`; the full list travels in nothing else because the API layer re-runs nothing — keeping one error keeps `Result<T>` a single-error type, and the field name is carried in `Error.Code` so the API can build a ProblemDetails entry.

```csharp
using FluentValidation;
using FluentValidation.Results;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] applicable = validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next();
        }

        ValidationContext<TRequest> context = new(request);
        ValidationFailure[] failures = (await Task.WhenAll(
                applicable.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length == 0)
        {
            return await next();
        }

        ValidationFailure first = failures[0];
        return Error.Validation(first.PropertyName, first.ErrorMessage);
    }
}
```

- [ ] **Step 5: Implement `LoggingBehavior`**

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        long start = Stopwatch.GetTimestamp();

        Result<TResponse> result = await next();

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        if (result.IsSuccess)
        {
            logger.LogInformation("{Request} succeeded in {ElapsedMs} ms", requestName, elapsed.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                "{Request} failed in {ElapsedMs} ms with {ErrorCode} ({ErrorType})",
                requestName,
                elapsed.TotalMilliseconds,
                result.Error!.Code,
                result.Error.Type);
        }

        return result;
    }
}
```

- [ ] **Step 6: Implement `AddApplication`**

Registration order matters: logging is registered first, so it wraps validation and records rejected requests too.

```csharp
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ISender, Sender>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        Type[] handlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

        foreach (Type implementation in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (Type service in implementation.GetInterfaces()
                         .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(service, implementation);
            }
        }

        return services;
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests`
Expected: Sender and ValidationBehavior tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add validation and logging behaviors with DI registration"
```

---

### Task 7: Vehicle repository abstraction and CreateVehicle slice

**Files:**
- Create: `src/TransBrain.Application/Abstractions/IVehicleRepository.cs`
- Create: `src/TransBrain.Application/Features/Vehicles/VehicleResponse.cs`
- Create: `src/TransBrain.Application/Features/Vehicles/CreateVehicle/CreateVehicleCommand.cs`, `CreateVehicleCommandValidator.cs`, `CreateVehicleCommandHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Vehicles/CreateVehicleCommandHandlerTests.cs`
- Test: `tests/TransBrain.Application.Tests/Fakes/InMemoryVehicleRepository.cs`

**Interfaces:**
- Consumes: `Vehicle`, `LicensePlate`, `Result<T>`, `Error`, `ICommand<>`, `ICommandHandler<,>`.
- Produces:
  - `interface IVehicleRepository` with `Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken ct)`, `Task AddAsync(Vehicle vehicle, CancellationToken ct)`, `Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken ct)`, `Task<int> CountAsync(CancellationToken ct)`
  - `sealed record VehicleResponse(Guid Id, string LicensePlate, string Type, int PayloadKg, decimal LoadMeters, DateOnly NextInspectionDue, string Status)`
  - `sealed record CreateVehicleCommand(string LicensePlate, string Type, int PayloadKg, decimal LoadMeters, DateOnly NextInspectionDue) : ICommand<VehicleResponse>`

- [ ] **Step 1: Write the in-memory fake**

`tests/TransBrain.Application.Tests/Fakes/InMemoryVehicleRepository.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly List<Vehicle> _vehicles = [];

    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    public void Seed(params Vehicle[] vehicles) => _vehicles.AddRange(vehicles);

    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken)
        => Task.FromResult(_vehicles.Any(v => v.LicensePlate == plate));

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        _vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Vehicle>>(
            _vehicles.OrderBy(v => v.LicensePlate.Value).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_vehicles.Count);
}
```

- [ ] **Step 2: Write the failing handler tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class CreateVehicleCommandHandlerTests
{
    private static CreateVehicleCommand ValidCommand => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    [Fact]
    public async Task Handle_ValidCommand_PersistsVehicleAndReturnsResponse()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        result.Value.Status.Should().Be("Available");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DuplicateLicensePlate_ReturnsConflictError()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value);
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.DuplicateLicensePlate");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownVehicleType_ReturnsValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = "Spaceship" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
    }

    [Fact]
    public async Task Handle_InvalidLicensePlate_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { LicensePlate = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
    }

    [Fact]
    public async Task Handle_NonPositivePayload_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { PayloadKg = 0 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~CreateVehicleCommandHandlerTests`
Expected: compile error — the command, handler, response and repository interface do not exist.

- [ ] **Step 4: Implement the repository abstraction and response record**

`src/TransBrain.Application/Abstractions/IVehicleRepository.cs`:

```csharp
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Abstractions;

public interface IVehicleRepository
{
    Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken);

    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
```

`src/TransBrain.Application/Features/Vehicles/VehicleResponse.cs`:

```csharp
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

public sealed record VehicleResponse(
    Guid Id,
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue,
    string Status)
{
    public static VehicleResponse From(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.LicensePlate.Value,
        vehicle.Type.ToString(),
        vehicle.PayloadKg,
        vehicle.LoadMeters,
        vehicle.NextInspectionDue,
        vehicle.Status.ToString());
}
```

- [ ] **Step 5: Implement the command, validator and handler**

`CreateVehicleCommand.cs`:

```csharp
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

public sealed record CreateVehicleCommand(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue) : ICommand<VehicleResponse>;
```

`CreateVehicleCommandValidator.cs` — shape checks only; business invariants stay in the domain:

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(c => c.LicensePlate).NotEmpty().MaximumLength(15);
        RuleFor(c => c.Type).NotEmpty();
        RuleFor(c => c.PayloadKg).GreaterThan(0);
        RuleFor(c => c.LoadMeters).GreaterThan(0m);
        RuleFor(c => c.NextInspectionDue).NotEmpty();
    }
}
```

`CreateVehicleCommandHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

internal sealed class CreateVehicleCommandHandler(IVehicleRepository repository)
    : ICommandHandler<CreateVehicleCommand, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        CreateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        Result<LicensePlate> plate = LicensePlate.Create(command.LicensePlate);
        if (!plate.IsSuccess)
        {
            return plate.Error!;
        }

        if (!Enum.TryParse(command.Type, ignoreCase: true, out VehicleType type))
        {
            return Error.Validation("Vehicle.UnknownType", $"'{command.Type}' is not a known vehicle type.");
        }

        if (await repository.ExistsByLicensePlateAsync(plate.Value, cancellationToken))
        {
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{plate.Value}' already exists.");
        }

        Result<Vehicle> vehicle = Vehicle.Create(
            plate.Value,
            type,
            command.PayloadKg,
            command.LoadMeters,
            command.NextInspectionDue);

        if (!vehicle.IsSuccess)
        {
            return vehicle.Error!;
        }

        await repository.AddAsync(vehicle.Value, cancellationToken);

        return VehicleResponse.From(vehicle.Value);
    }
}
```

The handler is `internal`; the assembly scan in `AddApplication` picks it up regardless, and nothing outside the Application layer should construct it. The test project needs `[assembly: InternalsVisibleTo("TransBrain.Application.Tests")]` — add it to `src/TransBrain.Application/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TransBrain.Application.Tests")]
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~CreateVehicleCommandHandlerTests`
Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add CreateVehicle slice with duplicate plate detection"
```

---

### Task 8: ListVehicles slice

**Files:**
- Create: `src/TransBrain.Application/Common/Pagination/PagedResult.cs`
- Create: `src/TransBrain.Application/Features/Vehicles/ListVehicles/ListVehiclesQuery.cs`, `ListVehiclesQueryValidator.cs`, `ListVehiclesQueryHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Vehicles/ListVehiclesQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IVehicleRepository`, `VehicleResponse`, `IQuery<>`, `IQueryHandler<,>`.
- Produces: `sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`; `sealed record ListVehiclesQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<VehicleResponse>>`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class ListVehiclesQueryHandlerTests
{
    private static Vehicle VehicleWithPlate(string plate) => Vehicle.Create(
        LicensePlate.Create(plate).Value,
        VehicleType.Van,
        3_000,
        4.0m,
        new DateOnly(2027, 1, 1)).Value;

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository());

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(VehicleWithPlate("M-AA 1"), VehicleWithPlate("M-BB 2"), VehicleWithPlate("M-CC 3"));
        ListVehiclesQueryHandler handler = new(repository);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(
            new ListVehiclesQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LicensePlate.Should().Be("M-CC 3");
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FirstPage_ReturnsItemsOrderedByLicensePlate()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(VehicleWithPlate("M-CC 3"), VehicleWithPlate("M-AA 1"));
        ListVehiclesQueryHandler handler = new(repository);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        result.Value.Items.Select(i => i.LicensePlate).Should().ContainInOrder("M-AA 1", "M-CC 3");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~ListVehiclesQueryHandlerTests`
Expected: compile error — query, handler and `PagedResult` do not exist.

- [ ] **Step 3: Implement `PagedResult`, query, validator and handler**

`src/TransBrain.Application/Common/Pagination/PagedResult.cs`:

```csharp
namespace TransBrain.Application.Common.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

`ListVehiclesQuery.cs`:

```csharp
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

public sealed record ListVehiclesQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<VehicleResponse>>;
```

`ListVehiclesQueryValidator.cs`:

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

public sealed class ListVehiclesQueryValidator : AbstractValidator<ListVehiclesQuery>
{
    public ListVehiclesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
```

`ListVehiclesQueryHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

internal sealed class ListVehiclesQueryHandler(IVehicleRepository repository)
    : IQueryHandler<ListVehiclesQuery, PagedResult<VehicleResponse>>
{
    public async Task<Result<PagedResult<VehicleResponse>>> Handle(
        ListVehiclesQuery query,
        CancellationToken cancellationToken)
    {
        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Vehicle> vehicles = await repository.ListAsync(skip, query.PageSize, cancellationToken);
        int totalCount = await repository.CountAsync(cancellationToken);

        VehicleResponse[] items = vehicles.Select(VehicleResponse.From).ToArray();

        return new PagedResult<VehicleResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests`
Expected: all Application tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add ListVehicles query with pagination"
```

---

### Task 9: Infrastructure — DbContext, EF configuration, repository, migration

**Files:**
- Create: `src/TransBrain.Infrastructure/Persistence/TransBrainDbContext.cs`
- Create: `src/TransBrain.Infrastructure/Persistence/Configurations/VehicleConfiguration.cs`
- Create: `src/TransBrain.Infrastructure/Persistence/Repositories/VehicleRepository.cs`
- Create: `src/TransBrain.Infrastructure/DependencyInjection.cs`
- Create: `src/TransBrain.Infrastructure/Migrations/` (generated)

**Interfaces:**
- Consumes: `IVehicleRepository`, `Vehicle`, `LicensePlate`.
- Produces: `TransBrainDbContext` with `DbSet<Vehicle> Vehicles`; `public static IServiceCollection AddInfrastructure(this IServiceCollection services)` registering `IVehicleRepository → VehicleRepository`. The DbContext itself is registered by the API via `AddNpgsqlDbContext<TransBrainDbContext>("transbraindb")`, so `AddInfrastructure` must NOT register it.

- [ ] **Step 1: Add EF packages**

```bash
dotnet add src/TransBrain.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/TransBrain.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef --version 10.0.11
```

- [ ] **Step 2: Implement the DbContext and configuration**

`TransBrainDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence;

public sealed class TransBrainDbContext(DbContextOptions<TransBrainDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransBrainDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

`VehicleConfiguration.cs` — the license plate is stored as plain text with a unique index; the converter round-trips through the domain factory, and `.Value` is safe there because only already-validated plates ever reach the database.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.LicensePlate)
            .HasConversion(plate => plate.Value, value => LicensePlate.Create(value).Value)
            .HasMaxLength(15)
            .IsRequired();

        builder.HasIndex(v => v.LicensePlate).IsUnique();

        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.PayloadKg).IsRequired();
        builder.Property(v => v.LoadMeters).HasPrecision(6, 2).IsRequired();
        builder.Property(v => v.NextInspectionDue).IsRequired();
    }
}
```

- [ ] **Step 3: Implement the repository and DI registration**

`VehicleRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class VehicleRepository(TransBrainDbContext context) : IVehicleRepository
{
    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken)
        => context.Vehicles.AnyAsync(v => v.LicensePlate == plate, cancellationToken);

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        await context.Vehicles.AddAsync(vehicle, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => await context.Vehicles
            .OrderBy(v => v.LicensePlate)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => context.Vehicles.CountAsync(cancellationToken);
}
```

`DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Persistence.Repositories;

namespace TransBrain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        return services;
    }
}
```

- [ ] **Step 4: Create the initial migration**

The Api is the startup project because it owns the DbContext registration and connection string.

```bash
dotnet ef migrations add InitialCreate \
  --project src/TransBrain.Infrastructure \
  --startup-project src/TransBrain.Api \
  --output-dir Migrations
```

Expected: a `Migrations/` folder containing `*_InitialCreate.cs` with a `vehicles` table and a unique index on `LicensePlate`.

**Ordering note — read before running this step.** `dotnet ef` builds the startup project and needs the DbContext to be registered there, which happens in Task 10 Step 5 (`Program.cs`). Execute **Task 10 Steps 1 through 6 first**, then come back and run this step, then continue with Task 10 Step 7. The alternative — a design-time `IDesignTimeDbContextFactory` with a hard-coded connection string — is deliberately avoided: it duplicates connection configuration that Aspire already owns.

- [ ] **Step 5: Verify the build**

Run: `dotnet build TransBrain.slnx`
Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Infrastructure
git commit -m "feat(infrastructure): add EF Core persistence for vehicles with initial migration"
```

---

### Task 10: Api — ServiceDefaults, endpoints, Result-to-HTTP mapping, OpenAPI

**Files:**
- Create: `src/TransBrain.ServiceDefaults/TransBrain.ServiceDefaults.csproj`, `src/TransBrain.ServiceDefaults/Extensions.cs`
- Create: `src/TransBrain.Api/Endpoints/IEndpointGroup.cs`, `src/TransBrain.Api/Endpoints/VehicleEndpoints.cs`
- Create: `src/TransBrain.Api/Common/ResultExtensions.cs`
- Modify: `src/TransBrain.Api/Program.cs`
- Create: `src/TransBrain.Api/appsettings.json` CORS section

**Interfaces:**
- Consumes: `ISender`, `CreateVehicleCommand`, `ListVehiclesQuery`, `Result<T>`, `ErrorType`.
- Produces: `interface IEndpointGroup { void Map(IEndpointRouteBuilder app); }`; `static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)`; routes `POST /api/vehicles` and `GET /api/vehicles`.

- [ ] **Step 1: Create the ServiceDefaults project**

Generate it from the Aspire starter template, then keep only this project:

```bash
aspire new aspire-starter --name TransBrainTemp --output .aspire-temp --non-interactive
cp -r .aspire-temp/TransBrainTemp.ServiceDefaults src/TransBrain.ServiceDefaults
rm -rf .aspire-temp
```

Rename the project file to `TransBrain.ServiceDefaults.csproj`, change the namespace in `Extensions.cs` to `TransBrain.ServiceDefaults`, then:

```bash
dotnet sln TransBrain.slnx add src/TransBrain.ServiceDefaults/TransBrain.ServiceDefaults.csproj
dotnet add src/TransBrain.Api reference src/TransBrain.ServiceDefaults
```

Note: `aspire new` writes a `nuget.config` and an `aspire.config.json` at the output root and installs Aspire agent skills into `~/.agents/skills`. Keep the generated `nuget.config` at the repository root; point `aspire.config.json` at the AppHost created in Task 11.

- [ ] **Step 2: Add Api packages**

```bash
dotnet add src/TransBrain.Api package Aspire.Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/TransBrain.Api package Aspire.StackExchange.Redis.DistributedCaching
dotnet add src/TransBrain.Api package Aspire.Keycloak.Authentication
dotnet add src/TransBrain.Api package Scalar.AspNetCore
```

- [ ] **Step 3: Implement the Result-to-HTTP mapping**

`src/TransBrain.Api/Common/ResultExtensions.cs`:

```csharp
using TransBrain.Domain.Common;

namespace TransBrain.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? Results.Ok(result.Value) : onSuccess(result.Value);
        }

        Error error = result.Error!;

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                new Dictionary<string, string[]> { [error.Code] = [error.Message] },
                title: "Validation failed"),
            ErrorType.NotFound => Results.Problem(title: error.Code, detail: error.Message, statusCode: 404),
            ErrorType.Conflict => Results.Problem(title: error.Code, detail: error.Message, statusCode: 409),
            ErrorType.Forbidden => Results.Problem(title: error.Code, detail: error.Message, statusCode: 403),
            _ => Results.Problem(title: error.Code, detail: error.Message, statusCode: 500)
        };
    }
}
```

- [ ] **Step 4: Implement the endpoint group contract and vehicle endpoints**

`IEndpointGroup.cs`:

```csharp
namespace TransBrain.Api.Endpoints;

public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
```

`VehicleEndpoints.cs` — the policies referenced here are defined in Task 12; until then both routes are reachable anonymously because authorization is not yet added.

```csharp
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class VehicleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/vehicles").WithTags("Vehicles");

        group.MapPost("/", async (
                CreateVehicleCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(command, cancellationToken);
                return result.ToHttpResult(vehicle => Results.Created($"/api/vehicles/{vehicle.Id}", vehicle));
            })
            .WithName("CreateVehicle")
            .Produces<VehicleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                Result<PagedResult<VehicleResponse>> result =
                    await sender.Send(new ListVehiclesQuery(page, pageSize), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("ListVehicles")
            .Produces<PagedResult<VehicleResponse>>()
            .ProducesValidationProblem();
    }
}
```

- [ ] **Step 5: Write `Program.cs`**

```csharp
using System.Reflection;
using Scalar.AspNetCore;
using TransBrain.Api.Endpoints;
using TransBrain.Application;
using TransBrain.Infrastructure;
using TransBrain.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<TransBrainDbContext>("transbraindb");

// Redis is registered only when Aspire supplied a connection string. The integration
// tests run without a Redis container and fall through to the in-memory cache.
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("cache")))
{
    builder.AddRedisDistributedCache("cache");
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TransBrainDbContext>().Database.MigrateAsync();
}

foreach (IEndpointGroup group in Assembly.GetExecutingAssembly().GetTypes()
             .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
             .Select(Activator.CreateInstance)
             .Cast<IEndpointGroup>())
{
    group.Map(app);
}

await app.RunAsync();

public partial class Program;
```

`public partial class Program;` at the end is what makes `WebApplicationFactory<Program>` work in Task 13.

- [ ] **Step 6: Configure CORS origins**

Add to `src/TransBrain.Api/appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4200", "http://localhost:4300" ]
  }
}
```

- [ ] **Step 7: Verify the build**

Run: `dotnet build TransBrain.slnx`
Expected: `Build succeeded`. Now return to Task 9 Step 4 and generate the migration if it was deferred.

- [ ] **Step 8: Commit**

```bash
git add src/TransBrain.Api src/TransBrain.ServiceDefaults nuget.config
git commit -m "feat(api): add vehicle endpoints with Result-to-ProblemDetails mapping and OpenAPI"
```

---

### Task 11: Aspire AppHost with PostgreSQL, Redis and Keycloak realm import

**Files:**
- Create: `src/TransBrain.AppHost/TransBrain.AppHost.csproj`, `src/TransBrain.AppHost/AppHost.cs`
- Create: `src/TransBrain.AppHost/realms/transbrain-realm.json`
- Modify: `aspire.config.json`

**Interfaces:**
- Consumes: the Api project.
- Produces: Aspire resource names that every other component binds to by name — `postgres`, database `transbraindb`, `cache`, `keycloak` (host port `8080`, realm `transbrain`), `api`, `web` (4200), `vueweb` (4300).

- [ ] **Step 1: Create the AppHost project**

```bash
dotnet new classlib -n TransBrain.AppHost -o src/TransBrain.AppHost -f net10.0
rm src/TransBrain.AppHost/Class1.cs
dotnet sln TransBrain.slnx add src/TransBrain.AppHost/TransBrain.AppHost.csproj
```

Replace `src/TransBrain.AppHost/TransBrain.AppHost.csproj` with:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AspireUseCliBundle>true</AspireUseCliBundle>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TransBrain.Api\TransBrain.Api.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.PostgreSQL" />
    <PackageReference Include="Aspire.Hosting.Redis" />
    <PackageReference Include="Aspire.Hosting.Keycloak" />
    <PackageReference Include="Aspire.Hosting.JavaScript" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the Keycloak realm export**

`src/TransBrain.AppHost/realms/transbrain-realm.json`. Test users exist for local development only; the passwords are intentionally trivial and this file must never be used for a deployed environment.

```json
{
  "realm": "transbrain",
  "enabled": true,
  "sslRequired": "none",
  "registrationAllowed": false,
  "roles": {
    "realm": [
      { "name": "admin", "description": "Full access including master data" },
      { "name": "disponent", "description": "Dispatcher: orders and tours" },
      { "name": "fahrer", "description": "Driver: own tours, status reporting" },
      { "name": "viewer", "description": "Read-only access" }
    ]
  },
  "clients": [
    {
      "clientId": "transbrain-api",
      "enabled": true,
      "bearerOnly": true,
      "publicClient": false,
      "serviceAccountsEnabled": false
    },
    {
      "clientId": "transbrain-spa",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "redirectUris": [
        "http://localhost:4200/*",
        "http://localhost:4300/*"
      ],
      "webOrigins": [
        "http://localhost:4200",
        "http://localhost:4300"
      ],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "http://localhost:4200/*##http://localhost:4300/*"
      },
      "protocolMappers": [
        {
          "name": "transbrain-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": {
            "included.client.audience": "transbrain-api",
            "id.token.claim": "false",
            "access.token.claim": "true"
          }
        }
      ]
    }
  ],
  "users": [
    {
      "username": "admin.user",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Anna",
      "lastName": "Admin",
      "credentials": [ { "type": "password", "value": "admin", "temporary": false } ],
      "realmRoles": [ "admin" ]
    },
    {
      "username": "dispo.user",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Dirk",
      "lastName": "Disponent",
      "credentials": [ { "type": "password", "value": "dispo", "temporary": false } ],
      "realmRoles": [ "disponent" ]
    },
    {
      "username": "fahrer.user",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Frank",
      "lastName": "Fahrer",
      "credentials": [ { "type": "password", "value": "fahrer", "temporary": false } ],
      "realmRoles": [ "fahrer" ]
    },
    {
      "username": "viewer.user",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Vera",
      "lastName": "Viewer",
      "credentials": [ { "type": "password", "value": "viewer", "temporary": false } ],
      "realmRoles": [ "viewer" ]
    }
  ]
}
```

- [ ] **Step 3: Write `AppHost.cs`**

This exact shape was compile-verified against Aspire 13.5.3 on 2026-08-28. The frontend directories do not exist until Tasks 14 and 15 — comment those two blocks out until then.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("transbraindb");

var cache = builder.AddRedis("cache");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithRealmImport("./realms");

var api = builder.AddProject<Projects.TransBrain_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(cache).WaitFor(cache)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.AddViteApp("web", "../TransBrain.Web", "start")
    .WithNpm()
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithExternalHttpEndpoints();

builder.AddViteApp("vueweb", "../TransBrain.VueWeb")
    .WithNpm()
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4300, targetPort: 4300, isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

- [ ] **Step 4: Point `aspire.config.json` at the AppHost**

```json
{
  "appHost": {
    "path": "src/TransBrain.AppHost/TransBrain.AppHost.csproj"
  },
  "channel": "stable"
}
```

- [ ] **Step 5: Run the AppHost and verify all resources start**

Run: `aspire run`
Expected: the dashboard lists `postgres`, `pgadmin`, `transbraindb`, `cache`, `keycloak`, `api` — all healthy. Open the Keycloak admin console at `http://localhost:8080`, confirm the `transbrain` realm exists with four roles and four users. Open the API's Scalar page and confirm `GET /api/vehicles` returns an empty page and `POST /api/vehicles` creates one.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.AppHost aspire.config.json
git commit -m "feat(apphost): orchestrate postgres, redis and keycloak with realm import"
```

---

### Task 12: Api authentication and authorization policies

**Files:**
- Create: `src/TransBrain.Api/Authorization/Policies.cs`
- Modify: `src/TransBrain.Api/Program.cs`, `src/TransBrain.Api/Endpoints/VehicleEndpoints.cs`

**Interfaces:**
- Consumes: `AddKeycloakJwtBearer` from `Aspire.Keycloak.Authentication` (namespace `Microsoft.Extensions.DependencyInjection`, extension on `AuthenticationBuilder`, signature `AddKeycloakJwtBearer(string serviceName, string realm, Action<JwtBearerOptions>? configure)`).
- Produces: `static class Policies` with `const string MasterDataWrite = "MasterDataWrite"`, `DispatchWrite`, `TourStatusWrite`, `Read`; realm-role claim mapping.

- [ ] **Step 1: Define the policy names**

```csharp
namespace TransBrain.Api.Authorization;

public static class Policies
{
    public const string MasterDataWrite = "MasterDataWrite";
    public const string DispatchWrite = "DispatchWrite";
    public const string TourStatusWrite = "TourStatusWrite";
    public const string Read = "Read";
}
```

- [ ] **Step 2: Add authentication and authorization to `Program.cs`**

Two Keycloak specifics drive this code. First, Keycloak puts realm roles in a nested `realm_access.roles` claim, which ASP.NET does not map to role claims on its own — the token-validated event below does that, otherwise every role check silently fails. Second, `options.Audience` is `transbrain-api`, which only validates because Task 11's realm gives the `transbrain-spa` client an `oidc-audience-mapper` that writes `transbrain-api` into the access token's `aud` claim. If you see `401` with an audience-validation failure, that mapper is missing or misspelled — fix the realm, do not weaken the audience check to `account`, which would accept any token the realm ever issued.

Insert before `WebApplication app = builder.Build();`:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer("keycloak", realm: "transbrain", options =>
    {
        options.Audience = "transbrain-api";
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                string? realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                if (string.IsNullOrWhiteSpace(realmAccess))
                {
                    return Task.CompletedTask;
                }

                using JsonDocument document = JsonDocument.Parse(realmAccess);
                if (document.RootElement.TryGetProperty("roles", out JsonElement roles))
                {
                    foreach (JsonElement role in roles.EnumerateArray())
                    {
                        string? value = role.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, value));
                        }
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.MasterDataWrite, policy => policy.RequireRole("admin"))
    .AddPolicy(Policies.DispatchWrite, policy => policy.RequireRole("admin", "disponent"))
    .AddPolicy(Policies.TourStatusWrite, policy => policy.RequireRole("admin", "disponent", "fahrer"))
    .AddPolicy(Policies.Read, policy => policy.RequireRole("admin", "disponent", "fahrer", "viewer"));
```

Required usings at the top of `Program.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TransBrain.Api.Authorization;
```

Insert after `app.UseCors();`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 3: Apply the policies to the vehicle endpoints**

In `VehicleEndpoints.Map`, add to the `POST` route:

```csharp
            .RequireAuthorization(Policies.MasterDataWrite)
```

and to the `GET` route:

```csharp
            .RequireAuthorization(Policies.Read)
```

- [ ] **Step 4: Verify manually against the running stack**

Run: `aspire run`, then from the Scalar page call `GET /api/vehicles` without a token.
Expected: `401 Unauthorized`.

Obtain a token for `dispo.user` and repeat:

```bash
curl -s -X POST "http://localhost:8080/realms/transbrain/protocol/openid-connect/token" \
  -d "client_id=transbrain-spa" -d "grant_type=password" \
  -d "username=dispo.user" -d "password=dispo"
```

Note: this password grant only works if `directAccessGrantsEnabled` is temporarily set to `true` on the SPA client. Set it back to `false` afterwards — the browsers use Authorization Code + PKCE, and leaving the password grant on weakens the realm.

Expected: `GET /api/vehicles` with that bearer token returns `200`; `POST /api/vehicles` with it returns `403` (dispatcher may not write master data); the same `POST` with an `admin.user` token returns `201`.

- [ ] **Step 5: Commit**

```bash
git add src/TransBrain.Api
git commit -m "feat(api): add keycloak jwt authentication with role-based policies"
```

---

### Task 13: API integration tests with Testcontainers

**Files:**
- Create: `tests/TransBrain.Api.IntegrationTests/TestAuthHandler.cs`
- Create: `tests/TransBrain.Api.IntegrationTests/TransBrainApiFactory.cs`
- Create: `tests/TransBrain.Api.IntegrationTests/VehicleEndpointsTests.cs`

**Interfaces:**
- Consumes: `Program`, `TransBrainDbContext`, `VehicleResponse`, `PagedResult<T>`.
- Produces: `TransBrainApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` exposing `HttpClient CreateClientAs(params string[] roles)`.

- [ ] **Step 1: Add test packages**

```bash
dotnet add tests/TransBrain.Api.IntegrationTests package Testcontainers.PostgreSql
dotnet add tests/TransBrain.Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/TransBrain.Api.IntegrationTests package AwesomeAssertions
dotnet add tests/TransBrain.Api.IntegrationTests reference src/TransBrain.Infrastructure
```

- [ ] **Step 2: Write the test authentication handler**

Real Keycloak is deliberately not started for these tests — they verify endpoint wiring, persistence and authorization decisions, not token issuance. The genuine OIDC flow is covered by the Playwright tests in Tasks 14 and 15.

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TransBrain.Api.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues roles))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Name, "test-user"),
            .. roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role))
        ];

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
```

- [ ] **Step 3: Write the factory**

**xUnit version — resolved during execution.** Task 1 pinned **xUnit v2 (`xunit` 2.9.3)**, so `Xunit.IAsyncLifetime` requires `Task InitializeAsync()` and `Task DisposeAsync()` — **not** the `ValueTask` signatures of xUnit v3. Write the factory as shown below, which already uses the v2 shape. Under v2, `IAsyncLifetime.DisposeAsync()` returns `Task` and is a plain interface implementation, so it does not override `WebApplicationFactory.DisposeAsync()`; dispose the container there and let the base class clean itself up via `Dispose(bool)`.

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using TransBrain.Infrastructure.Persistence;

namespace TransBrain.Api.IntegrationTests;

public sealed class TransBrainApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    public HttpClient CreateClientAs(params string[] roles)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:transbraindb", _postgres.GetConnectionString());

        // No `cache` connection string on purpose: Program.cs falls back to the
        // in-memory distributed cache when Aspire has not supplied one.

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
```

This works because Task 10's `Program.cs` guards the Redis registration on the presence of a `cache` connection string. If that guard is missing, add it there rather than pointing the tests at an unreachable `localhost:6379` — test startup must depend on configuration, not on connection timing.

- [ ] **Step 4: Write the failing endpoint tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;

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
    public async Task GetVehicles_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/TransBrain.Api.IntegrationTests`
Expected: 6 passed. Docker must be running; Testcontainers pulls `postgres:17-alpine` on first run.

- [ ] **Step 6: Commit**

```bash
git add tests/TransBrain.Api.IntegrationTests
git commit -m "test(api): add integration tests with testcontainers and test auth scheme"
```

---

### Task 14: Angular frontend shell with OIDC login and vehicle list

**Files:**
- Create: `src/TransBrain.Web/` (Angular workspace)
- Create: `src/TransBrain.Web/src/app/auth/auth.config.ts`, `src/app/auth/auth.interceptor.ts`
- Create: `src/TransBrain.Web/src/app/vehicles/vehicle.service.ts`, `src/app/vehicles/vehicle-list.component.ts`
- Create: `src/TransBrain.Web/e2e/vehicles.spec.ts`, `playwright.config.ts`

**Interfaces:**
- Consumes: `GET /api/vehicles` returning `{ items, page, pageSize, totalCount }`; Keycloak realm `transbrain`, client `transbrain-spa`.
- Produces: an Angular dev server on port 4200 whose `npm start` script Aspire invokes.

- [ ] **Step 1: Create the workspace**

```bash
npx --yes @angular/cli@22 new TransBrain.Web \
  --directory src/TransBrain.Web --style=scss --ssr=false --routing --skip-git --package-manager=npm
cd src/TransBrain.Web && npx --yes ng add @angular/material --skip-confirmation && npm install angular-auth-oidc-client
```

- [ ] **Step 2: Pin the dev-server port to 4200**

In `src/TransBrain.Web/angular.json`, under `projects.TransBrain.Web.architect.serve.options`, add `"port": 4200`. This is what lets the AppHost use a fixed, non-proxied endpoint.

- [ ] **Step 3: Configure OIDC**

`src/app/auth/auth.config.ts`:

```typescript
import { PassedInitialConfig } from 'angular-auth-oidc-client';

export const authConfig: PassedInitialConfig = {
    config: {
        authority: 'http://localhost:8080/realms/transbrain',
        redirectUrl: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
        clientId: 'transbrain-spa',
        scope: 'openid profile email',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        secureRoutes: ['/api'],
    },
};
```

`src/app/app.config.ts` — add to `providers`:

```typescript
provideAuth(authConfig),
provideHttpClient(withInterceptors([authInterceptor()])),
```

with imports `import { provideAuth, authInterceptor } from 'angular-auth-oidc-client';` and `import { provideHttpClient, withInterceptors } from '@angular/common/http';`.

- [ ] **Step 4: Write the vehicle service and list component**

`src/app/vehicles/vehicle.service.ts`:

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Vehicle {
    id: string;
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    status: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class VehicleService {
    private readonly http = inject(HttpClient);

    list(): Observable<PagedResult<Vehicle>> {
        return this.http.get<PagedResult<Vehicle>>('/api/vehicles');
    }
}
```

`src/app/vehicles/vehicle-list.component.ts`:

```typescript
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Vehicle, VehicleService } from './vehicle.service';

@Component({
    selector: 'app-vehicle-list',
    standalone: true,
    imports: [MatTableModule, MatButtonModule],
    template: `
        @if (isAuthenticated()) {
            <h1>Vehicles</h1>
            <table mat-table [dataSource]="vehicles()">
                <ng-container matColumnDef="licensePlate">
                    <th mat-header-cell *matHeaderCellDef>License plate</th>
                    <td mat-cell *matCellDef="let v" data-testid="vehicle-plate">{{ v.licensePlate }}</td>
                </ng-container>
                <ng-container matColumnDef="type">
                    <th mat-header-cell *matHeaderCellDef>Type</th>
                    <td mat-cell *matCellDef="let v">{{ v.type }}</td>
                </ng-container>
                <ng-container matColumnDef="payloadKg">
                    <th mat-header-cell *matHeaderCellDef>Payload (kg)</th>
                    <td mat-cell *matCellDef="let v">{{ v.payloadKg }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
        } @else {
            <button mat-raised-button data-testid="login" (click)="login()">Sign in</button>
        }
    `,
})
export class VehicleListComponent {
    private readonly service = inject(VehicleService);
    private readonly oidc = inject(OidcSecurityService);

    protected readonly columns = ['licensePlate', 'type', 'payloadKg'];
    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly isAuthenticated = signal(false);

    constructor() {
        this.oidc.checkAuth().subscribe(({ isAuthenticated }) => {
            this.isAuthenticated.set(isAuthenticated);
            if (isAuthenticated) {
                this.service.list().subscribe((page) => this.vehicles.set(page.items));
            }
        });
    }

    protected login(): void {
        this.oidc.authorize();
    }
}
```

Register the route in `src/app/app.routes.ts`:

```typescript
export const routes: Routes = [
    { path: '', redirectTo: 'vehicles', pathMatch: 'full' },
    { path: 'vehicles', loadComponent: () => import('./vehicles/vehicle-list.component').then(m => m.VehicleListComponent) },
];
```

- [ ] **Step 5: Proxy `/api` to the Aspire-provided API URL**

Create `src/TransBrain.Web/proxy.conf.js`:

```javascript
module.exports = {
    '/api': {
        target: process.env['services__api__https__0'] ?? process.env['services__api__http__0'] ?? 'http://localhost:5000',
        secure: false,
        changeOrigin: true,
    },
};
```

In `angular.json`, add `"proxyConfig": "proxy.conf.js"` to the same `serve.options` block as the port.

- [ ] **Step 6: Add the Playwright smoke test**

```bash
cd src/TransBrain.Web && npm install --save-dev @playwright/test && npx playwright install chromium
```

`src/TransBrain.Web/e2e/vehicles.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';

test('unauthenticated_visitor_seesSignInButton', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
});

test('adminUser_afterKeycloakLogin_seesVehicleList', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('login').click();
    await page.getByLabel('Username or email').fill('admin.user');
    await page.getByLabel('Password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
});
```

`src/TransBrain.Web/playwright.config.ts`:

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    use: { baseURL: 'http://localhost:4200' },
    reporter: 'list',
});
```

Add to `package.json` scripts: `"e2e": "playwright test"`.

- [ ] **Step 7: Run and verify against the live stack**

Run: `aspire run` (uncomment the `web` block in `AppHost.cs` first), then in a second terminal `cd src/TransBrain.Web && npm run e2e`.
Expected: both tests pass. The second test proves the full chain — Keycloak login, token attached by the interceptor, API call, PostgreSQL read, list rendered.

- [ ] **Step 8: Commit**

```bash
git add src/TransBrain.Web src/TransBrain.AppHost/AppHost.cs
git commit -m "feat(web): add angular shell with keycloak login and vehicle list"
```

---

### Task 15: Vue frontend shell with OIDC login and vehicle list

**Files:**
- Create: `src/TransBrain.VueWeb/` (Vite + Vue workspace)
- Create: `src/TransBrain.VueWeb/src/auth/userManager.ts`, `src/stores/auth.ts`, `src/api/vehicles.ts`, `src/views/VehicleList.vue`
- Create: `src/TransBrain.VueWeb/e2e/vehicles.spec.ts`, `playwright.config.ts`

**Interfaces:**
- Consumes: the same API contract and the same Keycloak client as Task 14.
- Produces: a Vite dev server on port 4300.

- [ ] **Step 1: Create the workspace**

```bash
npm create vite@latest src/TransBrain.VueWeb -- --template vue-ts
cd src/TransBrain.VueWeb
npm install
npm install vuetify@4 pinia vue-router axios oidc-client-ts
npm install --save-dev @playwright/test
npx playwright install chromium
```

- [ ] **Step 2: Pin the dev-server port and proxy `/api`**

`src/TransBrain.VueWeb/vite.config.ts`:

```typescript
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [vue()],
    server: {
        port: 4300,
        strictPort: true,
        proxy: {
            '/api': {
                target: process.env['services__api__https__0'] ?? process.env['services__api__http__0'] ?? 'http://localhost:5000',
                changeOrigin: true,
                secure: false,
            },
        },
    },
});
```

- [ ] **Step 3: Configure OIDC and the auth store**

`src/auth/userManager.ts`:

```typescript
import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

export const userManager = new UserManager({
    authority: 'http://localhost:8080/realms/transbrain',
    client_id: 'transbrain-spa',
    redirect_uri: `${window.location.origin}/callback`,
    post_logout_redirect_uri: window.location.origin,
    response_type: 'code',
    scope: 'openid profile email',
    userStore: new WebStorageStateStore({ store: window.localStorage }),
});
```

`src/stores/auth.ts`:

```typescript
import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { User } from 'oidc-client-ts';
import { userManager } from '../auth/userManager';

export const useAuthStore = defineStore('auth', () => {
    const user = ref<User | null>(null);
    const isAuthenticated = ref(false);

    async function load(): Promise<void> {
        user.value = await userManager.getUser();
        isAuthenticated.value = user.value !== null && !user.value.expired;
    }

    async function login(): Promise<void> {
        await userManager.signinRedirect();
    }

    async function completeLogin(): Promise<void> {
        user.value = await userManager.signinRedirectCallback();
        isAuthenticated.value = true;
    }

    return { user, isAuthenticated, load, login, completeLogin };
});
```

- [ ] **Step 4: Write the API client with the bearer interceptor**

`src/api/vehicles.ts`:

```typescript
import axios from 'axios';
import { userManager } from '../auth/userManager';

export interface Vehicle {
    id: string;
    licensePlate: string;
    type: string;
    payloadKg: number;
    loadMeters: number;
    nextInspectionDue: string;
    status: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

export async function listVehicles(): Promise<PagedResult<Vehicle>> {
    const response = await client.get<PagedResult<Vehicle>>('/vehicles');
    return response.data;
}
```

- [ ] **Step 5: Write the list view**

`src/views/VehicleList.vue`:

```vue
<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { listVehicles, type Vehicle } from '../api/vehicles';
import { useAuthStore } from '../stores/auth';

const auth = useAuthStore();
const vehicles = ref<Vehicle[]>([]);

const headers = [
    { title: 'License plate', key: 'licensePlate' },
    { title: 'Type', key: 'type' },
    { title: 'Payload (kg)', key: 'payloadKg' },
];

onMounted(async () => {
    await auth.load();
    if (auth.isAuthenticated) {
        vehicles.value = (await listVehicles()).items;
    }
});
</script>

<template>
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1>Vehicles</h1>
            <v-data-table :headers="headers" :items="vehicles" item-value="id" data-testid="vehicle-table" />
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
</template>
```

`src/views/AuthCallback.vue`:

```vue
<script setup lang="ts">
import { onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const auth = useAuthStore();
const router = useRouter();

onMounted(async () => {
    await auth.completeLogin();
    await router.replace('/');
});
</script>

<template>
    <v-progress-circular indeterminate />
</template>
```

`src/main.ts`:

```typescript
import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createWebHistory } from 'vue-router';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import 'vuetify/styles';
import App from './App.vue';
import VehicleList from './views/VehicleList.vue';
import AuthCallback from './views/AuthCallback.vue';

const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: '/', component: VehicleList },
        { path: '/callback', component: AuthCallback },
    ],
});

createApp(App)
    .use(createPinia())
    .use(router)
    .use(createVuetify({ components, directives }))
    .mount('#app');
```

`src/App.vue` must render the router outlet inside Vuetify's application shell:

```vue
<template>
    <v-app>
        <v-main>
            <router-view />
        </v-main>
    </v-app>
</template>
```

- [ ] **Step 6: Add the Playwright smoke test**

`src/TransBrain.VueWeb/e2e/vehicles.spec.ts`:

```typescript
import { expect, test } from '@playwright/test';

test('unauthenticated_visitor_seesSignInButton', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
});

test('adminUser_afterKeycloakLogin_seesVehicleList', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('login').click();
    await page.getByLabel('Username or email').fill('admin.user');
    await page.getByLabel('Password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
});
```

`playwright.config.ts` is identical to Task 14's except `baseURL: 'http://localhost:4300'`. Add `"e2e": "playwright test"` to `package.json` scripts.

- [ ] **Step 7: Run and verify**

Run: `aspire run` (uncomment the `vueweb` block), then `cd src/TransBrain.VueWeb && npm run e2e`.
Expected: both tests pass. This is the Phase 1 acceptance criterion — the same login, the same API, the same data, in the second frontend.

- [ ] **Step 8: Commit**

```bash
git add src/TransBrain.VueWeb src/TransBrain.AppHost/AppHost.cs
git commit -m "feat(vueweb): add vue shell with keycloak login and vehicle list"
```

---

### Task 16: CI workflow and documentation

**Files:**
- Create: `.github/workflows/ci.yml`, `CHANGELOG.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: a CI pipeline that builds the solution, runs all .NET tests, and builds both frontends.

- [ ] **Step 1: Write the CI workflow**

Playwright E2E needs the full Aspire stack including Keycloak, which is not started here — those tests run locally in this phase and get a CI job once a headless startup path exists. This is a deliberate gap, recorded so nobody assumes E2E is covered in CI yet.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore TransBrain.slnx
      - run: dotnet build TransBrain.slnx --no-restore
      - run: dotnet test tests/TransBrain.Domain.Tests --no-build
      - run: dotnet test tests/TransBrain.Application.Tests --no-build
      - run: dotnet test tests/TransBrain.Api.IntegrationTests --no-build

  frontends:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        project: [TransBrain.Web, TransBrain.VueWeb]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '26'
      - run: npm ci
        working-directory: src/${{ matrix.project }}
      - run: npm run build
        working-directory: src/${{ matrix.project }}
```

- [ ] **Step 2: Write `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Clean Architecture solution layout with Domain, Application, Infrastructure and Api layers
- Hand-rolled CQRS mediator with validation and logging pipeline behaviors
- Result pattern with `Validation`, `NotFound`, `Conflict` and `Forbidden` error types
- Vehicle aggregate with `Create` and `List` use cases
- PostgreSQL persistence via EF Core 10 with an initial migration
- .NET Aspire orchestration for PostgreSQL, Redis and Keycloak with realm import
- Keycloak authentication with the realm roles `admin`, `disponent`, `fahrer` and `viewer`
- Angular 22 frontend with Material, OIDC login and vehicle list
- Vue 3 frontend with Vuetify, OIDC login and vehicle list
- Integration tests using Testcontainers and a test authentication scheme
- CI workflow building the solution, running .NET tests and building both frontends
```

- [ ] **Step 3: Write `README.md`**

Cover: what TransBrain is, prerequisites (.NET 10 SDK, Node >= 26.4.0, Docker, Aspire CLI), `aspire run` as the single command to start everything, the four test users and their roles with an explicit warning that these credentials are for local development only, the ports (API from the dashboard, Angular 4200, Vue 4300, Keycloak 8080), and how to run each test suite.

- [ ] **Step 4: Verify CI passes**

Push the branch and confirm both jobs are green. If the integration-test job fails on Docker availability, confirm `ubuntu-latest` provides a Docker daemon for Testcontainers; it does.

- [ ] **Step 5: Commit**

```bash
git add .github CHANGELOG.md README.md
git commit -m "docs: add CI workflow, changelog and readme"
```

---

## Out of scope for this plan

Recorded so the next plan picks them up rather than anyone assuming they were forgotten:

- Redis caching of master-data lists with write-invalidation (spec §7) — Phase 2
- Vehicle `Update`, `Delete`, `GetById` and status/type filters — Phase 2
- The entire `Driver` aggregate — Phase 2
- `TransportOrder` with its status transitions — Phase 3
- `Tour` with capacity, licence and double-booking invariants, and the `Driver.ExternalUserId` claim check behind `TourStatusWrite` — Phase 4
- The 80 % Application-layer coverage gate in CI — Phase 2, once there is enough surface to measure meaningfully
- Playwright E2E in CI — needs a headless Aspire startup path
- Operator guides `docs/BEDIENUNG_TRANSBRAIN_WEB.md` and `_VUEWEB.md` with screenshots, and the AGENTS.md correction (spec §13) — Phase 5
