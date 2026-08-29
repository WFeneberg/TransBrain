# TransBrain Phases 4 & 5 — Tours and Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `Tour` aggregate end to end — its capacity, licence and double-booking invariants, the seven use cases, the driver-scoped authorization the spec has promised since Phase 0, both frontends — and then the documentation that closes the project out.

**Architecture:** The same shape the three previous aggregates proved: a folder per use case under `Features/Tours/<Action>/`, handlers returning `Result<T>`, invariants owned by the domain, endpoints mapping `Result` to HTTP through `ResultExtensions`. Three things are new. Tour invariants span aggregates, so the domain methods take the already-loaded `Vehicle`, `Driver` and `TransportOrder` objects as parameters rather than reaching for a repository — the handler does the loading, the domain does the deciding. Double-booking is enforced by a database unique index rather than a pre-flight query, because a read-then-write check lets two concurrent requests both through. And the Application layer learns who is calling, through a new `ICurrentUser`, so a driver can only start and complete their own tours.

**Tech Stack:** .NET 10 / C# 14, ASP.NET Minimal APIs, EF Core 10 + Npgsql, FluentValidation, xUnit v2 + AwesomeAssertions + Testcontainers, Angular 22 + Material, Vue 3 + Vuetify, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-28-transbrain-dispatch-design.md` — the aggregate in §5.5, the order status machine it drives in §5.4, use cases in §6.4, caching policy in §7, the driver-scoping rule in §9, the phase table in §12.

**Predecessors, all merged:** `2026-08-28-foundation-and-walking-skeleton.md` (Phases 0–1), `2026-08-29-master-data-completion.md` (Phase 2), `2026-08-29-transport-orders.md` (Phase 3). Read their Global Constraints; they still bind.

## Global Constraints

Everything the predecessor plans require still applies. Repeated here because it is binding:

- `net10.0`, nullable enabled, file-scoped namespaces, 4-space indentation, English identifiers.
- Records for DTOs and value objects. Primary constructors where they fit.
- Result pattern throughout — **never throw for control flow.** The one sanctioned exception is a guard against a programming error (see `OrderNumber.From`).
- A domain invariant lives in the domain and is **never** duplicated into a FluentValidation validator. Validators exist only to report several *field-shaped* problems at once.
- xUnit + **AwesomeAssertions** (not FluentAssertions). Test naming `Method_Scenario_ExpectedResult`.
- Application-layer line coverage must stay at or above 80% — CI gate in `.github/workflows/ci.yml`.
- `dotnet build TransBrain.slnx` must end with **0 warnings, 0 errors**. No MSB3277.
- Conventional Commits. Run the tests before every commit.
- Add a `CHANGELOG.md` entry under `[Unreleased]` for each notable change.
- Playwright specs live in `<project>/e2e/*.spec.ts`, run via `npm run e2e` against a stack started with `dotnet run --project src/TransBrain.AppHost`. They do **not** run in CI. `workers: 1` stays pinned in both configs.

## Decisions this plan locks in

Recorded here because a reader of the spec alone would not find them:

1. **The order status machine is driven by tour operations.** Spec §5.4 draws it explicitly: `Draft ──(Tourzuordnung)──> Planned ──(Tourstart)──> InTransit ──(Zustellung)──> Delivered`. Phase 3 implemented and tested `MarkPlanned`, `MarkInTransit` and `MarkDelivered` but deliberately gave them no endpoint. This phase is what calls them. `AssignOrder` → `MarkPlanned`, `Start` → `MarkInTransit` on every assigned order, `Complete` → `MarkDelivered`.

2. **`TransportOrder.ReturnToDraft()` is new, and it fills a gap in the spec.** §6.4 requires a `RemoveOrder` slice, but §5.4's diagram has no arrow from `Planned` back to `Draft`. Without one, an order removed from a tour would stay `Planned` forever with no tour to belong to — un-assignable and un-cancellable-by-edit. The transition is added, guarded to `Planned` only.

3. **"An order belongs to at most one active tour" (§5.4) is enforced by the order's own status, not by a query.** `AssignOrder` requires the order to be `Draft` and moves it to `Planned`; a second tour trying to assign it gets a `Conflict` from `MarkPlanned`. No cross-tour lookup is needed, and none is written.

4. **Double-booking (§5.5) is enforced by the database.** Unique indexes on `(TourDate, VehicleId)` and `(TourDate, DriverId)`; the repository maps PostgreSQL `23505` onto a `Conflict`, exactly as `DriverRepository` already does for `ExternalUserId`. A pre-flight "is this vehicle free?" query would be a read-then-write race: two concurrent requests would both read "free" and both insert. The index is the only thing that actually serialises them.

5. **Cross-aggregate invariants take their inputs as parameters.** `Tour.Create` receives the loaded `Vehicle` and `Driver`; `AssignOrder` receives the `TransportOrder`, the `Vehicle` and the list of orders already on the tour. The domain stays free of I/O and the arithmetic stays unit-testable. The cost — handlers must load more before they can decide — is the deliberate trade.

6. **Tours are not cached.** Spec §7 excludes them along with orders. Do not inject `ICacheService` into any tour handler, and do not invalidate anything from a tour write.

7. **A completed tour still occupies its vehicle and driver for that date.** §5.5 says "at most one tour per `TourDate`" with no exception for finished ones, so the unique index is unconditional.

## File Structure

```
src/TransBrain.Domain/Tours/
    TourStatus.cs            — Planned / InProgress / Completed
    StopType.cs              — Pickup / Delivery
    TourStop.cs              — Sequence, TransportOrderId, StopType
    Tour.cs                  — the aggregate: Create, AssignOrder, RemoveOrder, Start, Complete
src/TransBrain.Domain/Orders/
    TransportOrder.cs        — MODIFY: add ReturnToDraft()

src/TransBrain.Application/Abstractions/
    ITourRepository.cs
    ICurrentUser.cs
src/TransBrain.Application/Features/Tours/
    TourResponse.cs          — TourResponse, TourStopResponse
    CreateTour/              — Command, Validator, Handler
    AssignOrder/             — Command, Handler
    RemoveOrder/             — Command, Handler
    StartTour/               — Command, Handler
    CompleteTour/            — Command, Handler
    ListTours/               — Query, Validator, Handler
    GetTourById/             — Query, Handler
src/TransBrain.Application/Features/Tours/TourAccess.cs
                             — the one place the driver-scoping rule is written

src/TransBrain.Infrastructure/Persistence/
    Configurations/TourConfiguration.cs
    Repositories/TourRepository.cs
    Migrations/<timestamp>_AddTours.cs
src/TransBrain.Api/
    Authorization/HttpContextCurrentUser.cs
    Endpoints/TourEndpoints.cs

src/TransBrain.Web/src/app/tours/     — tour.service.ts, tour-list/-form/-detail.component.ts
src/TransBrain.VueWeb/src/api/tours.ts, src/views/TourList.vue, TourForm.vue, TourDetail.vue
```

`TourAccess.cs` is a single static class rather than a rule copied into four handlers. Four copies of an authorization check is four places for it to drift, and the one that drifts is the one that stops refusing.

---

### Task 1: The `Tour` aggregate and its invariants

**Files:**
- Create: `src/TransBrain.Domain/Tours/TourStatus.cs`, `StopType.cs`, `TourStop.cs`, `Tour.cs`
- Modify: `src/TransBrain.Domain/Orders/TransportOrder.cs`
- Test: `tests/TransBrain.Domain.Tests/Tours/TourTests.cs`, `tests/TransBrain.Domain.Tests/Orders/TransportOrderTests.cs` (extend)

**Interfaces:**
- Consumes: `Result<T>`, `Error`, `Unit`, `Vehicle` (`Status`, `PayloadKg`, `LoadMeters`, `LicensePlate.Value`, `SendToWorkshop()`), `Driver` (`CanDriveOn(DateOnly)`, `Status`, `LicenseValidUntil`, `MarkAbsent()`), `TransportOrder` (`Cargo`, `MarkPlanned()`), `LicensePlate.Create`.
- Produces:
  - `enum TourStatus { Planned, InProgress, Completed }`
  - `enum StopType { Pickup, Delivery }`
  - `sealed record TourStop` with `int Sequence`, `Guid TransportOrderId`, `StopType StopType`
  - `sealed class Tour` with `Guid Id`, `DateOnly TourDate`, `Guid VehicleId`, `Guid DriverId`, `TourStatus Status`, `IReadOnlyList<TourStop> Stops`
  - `static Result<Tour> Tour.Create(DateOnly tourDate, Vehicle vehicle, Driver driver)`
  - `Result<Unit> Tour.AssignOrder(TransportOrder order, Vehicle vehicle, IReadOnlyList<TransportOrder> alreadyAssigned)`
  - `Result<Unit> Tour.RemoveOrder(TransportOrder order)`
  - `Result<Unit> Tour.Start()`
  - `Result<Unit> Tour.Complete()`
  - `Result<Unit> TransportOrder.ReturnToDraft()`

**Templates to open:** `src/TransBrain.Domain/Orders/TransportOrder.cs` for the status-machine shape and the `Transition`/`InvalidTransition` helpers; `tests/TransBrain.Domain.Tests/Orders/TransportOrderTests.cs` for the test style and its existing `AnOrder()` helper.

**Do not re-implement the licence rule.** `Driver.CanDriveOn(DateOnly)` already exists and already reads `Status == Available && LicenseValidUntil >= date` — Phase 2 wrote it for exactly this moment. A second copy of that condition in `Tour` is the kind of duplicate that stays correct until someone changes one of them.

**The capacity check is the reason `AssignOrder` has three parameters.** A tour stores order *ids*, not orders, so it cannot sum the cargo it already carries. Passing the loaded orders in keeps the arithmetic in the domain where it is unit-testable, instead of pushing it into a handler where it would be tested only through a fake repository.

- [ ] **Step 1: Write the failing `Tour` tests**

Create `tests/TransBrain.Domain.Tests/Tours/TourTests.cs`:

```csharp
using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Tours;

public class TourTests
{
    private static readonly DateOnly TourDate = new(2027, 3, 1);

    private static Vehicle AVehicle(
        int payloadKg = 18_000,
        decimal loadMeters = 13.6m,
        bool inWorkshop = false)
    {
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value,
            VehicleType.RigidTruck,
            payloadKg,
            loadMeters,
            new DateOnly(2028, 1, 1)).Value;

        if (inWorkshop)
        {
            vehicle.SendToWorkshop();
        }

        return vehicle;
    }

    private static Driver ADriver(DateOnly? licenceUntil = null, bool available = true)
    {
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.CE],
            licenceUntil ?? new DateOnly(2028, 6, 30), null).Value;

        if (!available)
        {
            driver.MarkAbsent();
        }

        return driver;
    }

    private static TransportOrder AnOrder(int weightKg = 5_000, decimal loadMeters = 4.0m)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, 1),
            address,
            address,
            Cargo.Create("Palettenware", weightKg, loadMeters).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
    }

    private static Tour ATour(Vehicle? vehicle = null, Driver? driver = null) =>
        Tour.Create(TourDate, vehicle ?? AVehicle(), driver ?? ADriver()).Value;

    [Fact]
    public void Create_AvailableVehicleAndDriver_StartsPlannedWithNoStops()
    {
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();

        Result<Tour> result = Tour.Create(TourDate, vehicle, driver);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TourStatus.Planned);
        result.Value.TourDate.Should().Be(TourDate);
        result.Value.VehicleId.Should().Be(vehicle.Id);
        result.Value.DriverId.Should().Be(driver.Id);
        result.Value.Stops.Should().BeEmpty();
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_VehicleInWorkshop_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(inWorkshop: true), ADriver());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tour.VehicleNotAvailable");
    }

    [Fact]
    public void Create_DriverNotAvailable_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(available: false));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.DriverNotAvailable");
    }

    [Fact]
    public void Create_LicenceExpiresBeforeTourDate_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(licenceUntil: TourDate.AddDays(-1)));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LicenceExpired");
    }

    // The boundary the spec words as "LicenseValidUntil >= Tourdatum": a licence expiring ON the
    // tour date is still valid that day. Off by one here silently grounds a legal driver.
    [Fact]
    public void Create_LicenceExpiresExactlyOnTourDate_Succeeds()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(licenceUntil: TourDate));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignOrder_FirstOrder_AddsPickupThenDeliveryAndPlansTheOrder()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();

        Result<Unit> result = tour.AssignOrder(order, AVehicle(), []);

        result.IsSuccess.Should().BeTrue();
        tour.Stops.Should().HaveCount(2);
        tour.Stops[0].Sequence.Should().Be(1);
        tour.Stops[0].StopType.Should().Be(StopType.Pickup);
        tour.Stops[0].TransportOrderId.Should().Be(order.Id);
        tour.Stops[1].Sequence.Should().Be(2);
        tour.Stops[1].StopType.Should().Be(StopType.Delivery);
        tour.Stops[1].TransportOrderId.Should().Be(order.Id);
        order.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void AssignOrder_SecondOrder_AppendsAfterTheFirstOrdersStops()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder first = AnOrder();
        TransportOrder second = AnOrder();
        tour.AssignOrder(first, vehicle, []);

        tour.AssignOrder(second, vehicle, [first]);

        tour.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2, 3, 4);
        tour.Stops[2].TransportOrderId.Should().Be(second.Id);
        tour.Stops[2].StopType.Should().Be(StopType.Pickup);
        tour.Stops[3].StopType.Should().Be(StopType.Delivery);
    }

    [Fact]
    public void AssignOrder_ExceedingPayload_ReturnsConflictAndAddsNoStops()
    {
        Vehicle vehicle = AVehicle(payloadKg: 10_000);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(weightKg: 6_000);
        tour.AssignOrder(assigned, vehicle, []);
        TransportOrder tooHeavy = AnOrder(weightKg: 5_000);

        Result<Unit> result = tour.AssignOrder(tooHeavy, vehicle, [assigned]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tour.PayloadExceeded");
        tour.Stops.Should().HaveCount(2);
        tooHeavy.Status.Should().Be(OrderStatus.Draft);
    }

    // The boundary: filling the vehicle exactly to its rated payload is legal.
    [Fact]
    public void AssignOrder_FillingPayloadExactly_Succeeds()
    {
        Vehicle vehicle = AVehicle(payloadKg: 10_000);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(weightKg: 6_000);
        tour.AssignOrder(assigned, vehicle, []);

        Result<Unit> result = tour.AssignOrder(AnOrder(weightKg: 4_000), vehicle, [assigned]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignOrder_ExceedingLoadMeters_ReturnsConflict()
    {
        Vehicle vehicle = AVehicle(loadMeters: 8.0m);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(loadMeters: 5.0m);
        tour.AssignOrder(assigned, vehicle, []);

        Result<Unit> result = tour.AssignOrder(AnOrder(loadMeters: 3.5m), vehicle, [assigned]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LoadMetersExceeded");
    }

    [Fact]
    public void AssignOrder_OrderAlreadyOnThisTour_ReturnsConflict()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder order = AnOrder();
        tour.AssignOrder(order, vehicle, []);

        Result<Unit> result = tour.AssignOrder(order, vehicle, [order]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.OrderAlreadyAssigned");
    }

    // Spec 5.4: an order belongs to at most one active tour. A second tour gets the refusal
    // from the order's own status machine, so no cross-tour lookup exists anywhere.
    [Fact]
    public void AssignOrder_OrderAlreadyPlannedOnAnotherTour_ReturnsConflict()
    {
        Vehicle vehicle = AVehicle();
        Tour first = ATour(vehicle);
        Tour second = ATour(vehicle);
        TransportOrder order = AnOrder();
        first.AssignOrder(order, vehicle, []);

        Result<Unit> result = second.AssignOrder(order, vehicle, []);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        second.Stops.Should().BeEmpty();
    }

    [Fact]
    public void AssignOrder_CancelledOrder_ReturnsConflictAndAddsNoStops()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();
        order.Cancel();

        Result<Unit> result = tour.AssignOrder(order, AVehicle(), []);

        result.IsSuccess.Should().BeFalse();
        tour.Stops.Should().BeEmpty();
    }

    [Fact]
    public void AssignOrder_TourInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        tour.AssignOrder(AnOrder(), vehicle, []);
        tour.Start();

        Result<Unit> result = tour.AssignOrder(AnOrder(), vehicle, []);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }

    [Fact]
    public void RemoveOrder_AssignedOrder_DropsBothStopsRenumbersAndReturnsTheOrderToDraft()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder first = AnOrder();
        TransportOrder second = AnOrder();
        tour.AssignOrder(first, vehicle, []);
        tour.AssignOrder(second, vehicle, [first]);

        Result<Unit> result = tour.RemoveOrder(first);

        result.IsSuccess.Should().BeTrue();
        tour.Stops.Should().HaveCount(2);
        tour.Stops.Should().OnlyContain(s => s.TransportOrderId == second.Id);
        // Renumbered contiguously - a gap would break the "pickup before delivery" ordering
        // the next assignment relies on.
        tour.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2);
        first.Status.Should().Be(OrderStatus.Draft);
        second.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void RemoveOrder_OrderNotOnTheTour_ReturnsNotFound()
    {
        Tour tour = ATour();

        Result<Unit> result = tour.RemoveOrder(AnOrder());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.OrderNotAssigned");
    }

    [Fact]
    public void RemoveOrder_TourInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();
        tour.AssignOrder(order, AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.RemoveOrder(order);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }

    [Fact]
    public void Start_PlannedTourWithStops_BecomesInProgress()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeTrue();
        tour.Status.Should().Be(TourStatus.InProgress);
    }

    // An empty tour is a planning mistake, not a journey. Starting one would move a vehicle and
    // a driver into InProgress for the day while carrying nothing.
    [Fact]
    public void Start_TourWithoutStops_ReturnsConflict()
    {
        Tour tour = ATour();

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NoStops");
        tour.Status.Should().Be(TourStatus.Planned);
    }

    [Fact]
    public void Start_AlreadyInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
    }

    [Fact]
    public void Complete_InProgressTour_BecomesCompleted()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeTrue();
        tour.Status.Should().Be(TourStatus.Completed);
    }

    [Fact]
    public void Complete_PlannedTour_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
        tour.Status.Should().Be(TourStatus.Planned);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();
        tour.Complete();

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
    }
}
```

Then extend `tests/TransBrain.Domain.Tests/Orders/TransportOrderTests.cs` with the new transition. Add these three facts (keep the file's existing helper for building an order — do not duplicate it):

```csharp
    [Fact]
    public void ReturnToDraft_PlannedOrder_BecomesDraftAgain()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<Unit> result = order.ReturnToDraft();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void ReturnToDraft_DraftOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();

        Result<Unit> result = order.ReturnToDraft();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    // Once the goods are moving, taking the order off a tour cannot un-move them.
    [Fact]
    public void ReturnToDraft_InTransitOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        order.MarkInTransit();

        Result<Unit> result = order.ReturnToDraft();

        result.IsSuccess.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.InTransit);
    }
```

Before writing these, open `TransportOrderTests.cs` and confirm the exact name of its order-building helper; use that name rather than `AnOrder()` if it differs.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Domain.Tests --filter FullyQualifiedName~Tour`
Expected: compile errors — `TransBrain.Domain.Tours` does not exist.

- [ ] **Step 3: Implement the three small types**

`src/TransBrain.Domain/Tours/TourStatus.cs`:

```csharp
namespace TransBrain.Domain.Tours;

public enum TourStatus
{
    Planned,
    InProgress,
    Completed
}
```

`src/TransBrain.Domain/Tours/StopType.cs`:

```csharp
namespace TransBrain.Domain.Tours;

public enum StopType
{
    Pickup,
    Delivery
}
```

`src/TransBrain.Domain/Tours/TourStop.cs`:

```csharp
namespace TransBrain.Domain.Tours;

/// <summary>
/// One call on a tour. Two of these exist per assigned order — a <see cref="StopType.Pickup"/>
/// and a <see cref="StopType.Delivery"/> — and the pickup always carries the lower sequence.
/// </summary>
/// <remarks>
/// Created only by <see cref="Tour"/>, which is what keeps the pickup-before-delivery and
/// contiguous-sequence invariants true: nothing outside the aggregate can add a stop.
/// </remarks>
public sealed record TourStop
{
    private TourStop(int sequence, Guid transportOrderId, StopType stopType)
    {
        Sequence = sequence;
        TransportOrderId = transportOrderId;
        StopType = stopType;
    }

    public int Sequence { get; }

    public Guid TransportOrderId { get; }

    public StopType StopType { get; }

    internal static TourStop Create(int sequence, Guid transportOrderId, StopType stopType) =>
        new(sequence, transportOrderId, stopType);

    internal TourStop WithSequence(int sequence) => new(sequence, TransportOrderId, StopType);
}
```

- [ ] **Step 4: Implement `Tour`**

`src/TransBrain.Domain/Tours/Tour.cs`:

```csharp
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tours;

/// <summary>
/// A day's work for one vehicle and one driver: an ordered list of stops serving a set of
/// transport orders.
/// </summary>
/// <remarks>
/// Several of this aggregate's invariants span other aggregates — capacity needs the vehicle's
/// rating and the assigned orders' cargo, the licence rule needs the driver. Those objects are
/// passed IN rather than fetched, so the domain stays free of I/O and the rules stay unit-
/// testable. The one invariant that is not here is "one tour per vehicle and driver per date":
/// that is a uniqueness question, and uniqueness cannot be decided by an object that can only
/// see itself. It lives in a database unique index (see TourConfiguration).
/// </remarks>
public sealed class Tour
{
    private readonly List<TourStop> _stops = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Tour()
    {
    }

    private Tour(Guid id, DateOnly tourDate, Guid vehicleId, Guid driverId)
    {
        Id = id;
        TourDate = tourDate;
        VehicleId = vehicleId;
        DriverId = driverId;
        Status = TourStatus.Planned;
    }

    public Guid Id { get; private set; }

    public DateOnly TourDate { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid DriverId { get; private set; }

    public TourStatus Status { get; private set; }

    public IReadOnlyList<TourStop> Stops => _stops;

    public static Result<Tour> Create(DateOnly tourDate, Vehicle vehicle, Driver driver)
    {
        if (vehicle.Status != VehicleStatus.Available)
        {
            return Error.Conflict(
                "Tour.VehicleNotAvailable",
                $"Vehicle '{vehicle.LicensePlate.Value}' is '{vehicle.Status}' and cannot be assigned to a tour.");
        }

        // Driver.CanDriveOn already encodes spec 5.3's rule in full - "Status == Available and
        // LicenseValidUntil >= Tourdatum" - so it is asked once and is the only judge here.
        // The branch below does not re-decide anything; it only picks which of the two reasons
        // to name, because "this driver cannot be assigned" without saying why sends a
        // dispatcher hunting through two screens.
        if (!driver.CanDriveOn(tourDate))
        {
            return driver.Status != DriverStatus.Available
                ? Error.Conflict(
                    "Tour.DriverNotAvailable",
                    $"Driver '{driver.LastName}' is '{driver.Status}' and cannot be assigned to a tour.")
                : Error.Conflict(
                    "Tour.LicenceExpired",
                    $"The driver's licence expires on {driver.LicenseValidUntil:yyyy-MM-dd}, before the tour date {tourDate:yyyy-MM-dd}.");
        }

        return new Tour(Guid.CreateVersion7(), tourDate, vehicle.Id, driver.Id);
    }

    /// <param name="alreadyAssigned">
    /// The orders this tour already carries. Required because a tour stores order ids, not
    /// orders, and so cannot sum its own load.
    /// </param>
    public Result<Unit> AssignOrder(
        TransportOrder order,
        Vehicle vehicle,
        IReadOnlyList<TransportOrder> alreadyAssigned)
    {
        if (Status != TourStatus.Planned)
        {
            return NotEditable();
        }

        if (_stops.Any(stop => stop.TransportOrderId == order.Id))
        {
            return Error.Conflict(
                "Tour.OrderAlreadyAssigned",
                $"Order '{order.OrderNumber.Value}' is already on this tour.");
        }

        int totalWeight = alreadyAssigned.Sum(o => o.Cargo.WeightKg) + order.Cargo.WeightKg;
        if (totalWeight > vehicle.PayloadKg)
        {
            return Error.Conflict(
                "Tour.PayloadExceeded",
                $"Adding this order would load {totalWeight} kg onto a vehicle rated for {vehicle.PayloadKg} kg.");
        }

        decimal totalLoadMeters = alreadyAssigned.Sum(o => o.Cargo.LoadMeters) + order.Cargo.LoadMeters;
        if (totalLoadMeters > vehicle.LoadMeters)
        {
            return Error.Conflict(
                "Tour.LoadMetersExceeded",
                $"Adding this order would need {totalLoadMeters} load meters on a vehicle offering {vehicle.LoadMeters}.");
        }

        // Last, and deliberately: this MUTATES the order, so every cheap refusal above must
        // already have run. It also carries the spec 5.4 rule that an order belongs to at most
        // one active tour — an order another tour has planned is no longer Draft and refuses.
        Result<Unit> planned = order.MarkPlanned();
        if (!planned.IsSuccess)
        {
            return planned.Error!;
        }

        _stops.Add(TourStop.Create(_stops.Count + 1, order.Id, StopType.Pickup));
        _stops.Add(TourStop.Create(_stops.Count + 1, order.Id, StopType.Delivery));

        return Unit.Value;
    }

    public Result<Unit> RemoveOrder(TransportOrder order)
    {
        if (Status != TourStatus.Planned)
        {
            return NotEditable();
        }

        if (_stops.All(stop => stop.TransportOrderId != order.Id))
        {
            return Error.NotFound(
                "Tour.OrderNotAssigned",
                $"Order '{order.OrderNumber.Value}' is not on this tour.");
        }

        Result<Unit> returned = order.ReturnToDraft();
        if (!returned.IsSuccess)
        {
            return returned.Error!;
        }

        _stops.RemoveAll(stop => stop.TransportOrderId == order.Id);
        Renumber();

        return Unit.Value;
    }

    /// <remarks>
    /// Moves only the tour. The assigned orders are a different aggregate, so the handler
    /// transitions them — see StartTourCommandHandler for why it validates every order before
    /// moving any of them.
    /// </remarks>
    public Result<Unit> Start()
    {
        if (Status != TourStatus.Planned)
        {
            return InvalidTransition(TourStatus.InProgress);
        }

        // An empty tour is a planning mistake, not a journey: starting one would occupy a
        // vehicle and a driver for the day while carrying nothing.
        if (_stops.Count == 0)
        {
            return Error.Conflict("Tour.NoStops", "A tour without stops cannot be started.");
        }

        Status = TourStatus.InProgress;
        return Unit.Value;
    }

    public Result<Unit> Complete()
    {
        if (Status != TourStatus.InProgress)
        {
            return InvalidTransition(TourStatus.Completed);
        }

        Status = TourStatus.Completed;
        return Unit.Value;
    }

    /// <summary>The distinct order ids on this tour, in the order they are first called at.</summary>
    public IReadOnlyList<Guid> AssignedOrderIds() =>
        _stops.OrderBy(stop => stop.Sequence).Select(stop => stop.TransportOrderId).Distinct().ToList();

    // Sequences stay contiguous from 1. A gap would not break any single check, but it would
    // make "the next stop is Count + 1" wrong the moment anything relied on it.
    private void Renumber()
    {
        List<TourStop> renumbered = _stops
            .OrderBy(stop => stop.Sequence)
            .Select((stop, index) => stop.WithSequence(index + 1))
            .ToList();

        _stops.Clear();
        _stops.AddRange(renumbered);
    }

    private Result<Unit> NotEditable() => Error.Conflict(
        "Tour.NotEditable",
        $"A tour in status '{Status}' no longer accepts changes to its stops.");

    private Result<Unit> InvalidTransition(TourStatus to) => Error.Conflict(
        "Tour.InvalidTransition",
        $"A tour in status '{Status}' cannot move to '{to}'.");
}
```

- [ ] **Step 5: Add `TransportOrder.ReturnToDraft`**

In `src/TransBrain.Domain/Orders/TransportOrder.cs`, directly below `MarkPlanned`:

```csharp
    /// <remarks>
    /// The reverse of <see cref="MarkPlanned"/>, for an order taken off a tour before that tour
    /// started. Spec §5.4's diagram does not draw this arrow, but §6.4 requires a RemoveOrder
    /// slice: without a way back, a removed order would be stranded in Planned with no tour —
    /// neither assignable to another tour nor editable. Deliberately NOT reachable from
    /// InTransit: once the goods are moving, taking the order off a tour cannot un-move them.
    /// </remarks>
    public Result<Unit> ReturnToDraft() => Transition(OrderStatus.Planned, OrderStatus.Draft);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Domain.Tests`
Expected: every test passes, including the pre-existing ones.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Domain tests/TransBrain.Domain.Tests
git commit -m "feat(domain): add the Tour aggregate with its capacity and licence invariants"
```

---

### Task 2: Abstractions, the response shape, and the `CreateTour` slice

**Files:**
- Create: `src/TransBrain.Application/Abstractions/ITourRepository.cs`, `ICurrentUser.cs`
- Create: `src/TransBrain.Application/Features/Tours/TourResponse.cs`
- Create: `src/TransBrain.Application/Features/Tours/CreateTour/CreateTourCommand.cs`, `CreateTourCommandValidator.cs`, `CreateTourCommandHandler.cs`
- Create: `tests/TransBrain.Application.Tests/Fakes/InMemoryTourRepository.cs`, `StubCurrentUser.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Tours/CreateTourCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IVehicleRepository`, `IDriverRepository`, `Tour.Create`.
- Produces:
  - `ITourRepository` — `Task<Result<Tour>> AddAsync(Tour, CancellationToken)`, `Task<Tour?> GetByIdAsync(Guid, CancellationToken)`, `Task<IReadOnlyList<Tour>> ListAsync(int skip, int take, DateOnly? tourDate, Guid? vehicleId, Guid? driverId, CancellationToken)`, `Task<int> CountAsync(DateOnly? tourDate, Guid? vehicleId, Guid? driverId, CancellationToken)`, `Task SaveChangesAsync(CancellationToken)`
  - `ICurrentUser` — `string? UserId { get; }`, `bool IsInRole(string role)`
  - `sealed record TourStopResponse(int Sequence, Guid TransportOrderId, string OrderNumber, string StopType)`
  - `sealed record TourResponse(Guid Id, DateOnly TourDate, Guid VehicleId, string VehicleLicensePlate, Guid DriverId, string DriverName, string Status, int TotalWeightKg, decimal TotalLoadMeters, int VehiclePayloadKg, decimal VehicleLoadMeters, IReadOnlyList<TourStopResponse> Stops)`
  - `static TourResponse TourResponse.From(Tour, Vehicle, Driver, IReadOnlyList<TransportOrder>)`
  - `sealed record CreateTourCommand(DateOnly TourDate, Guid VehicleId, Guid DriverId) : ICommand<TourResponse>`

**Templates to open:** `src/TransBrain.Application/Features/Orders/CreateOrder/CreateOrderCommandHandler.cs`, `src/TransBrain.Application/Abstractions/ITransportOrderRepository.cs`, `tests/TransBrain.Application.Tests/Fakes/InMemoryTransportOrderRepository.cs`.

**`TourResponse` carries the vehicle's rating alongside the tour's current load** — `TotalWeightKg` against `VehiclePayloadKg`, `TotalLoadMeters` against `VehicleLoadMeters`. A dispatcher deciding whether one more order fits needs both numbers on the screen; making the frontend fetch the vehicle separately to render a capacity bar would be a second round trip for data the server already had in hand.

- [ ] **Step 1: Write the two fakes**

`tests/TransBrain.Application.Tests/Fakes/InMemoryTourRepository.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryTourRepository : ITourRepository
{
    private readonly List<Tour> _tours = [];

    public IReadOnlyList<Tour> Tours => _tours;

    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Set to make AddAsync answer the Conflict the real repository produces when the
    /// (TourDate, VehicleId) or (TourDate, DriverId) unique index rejects a double booking.
    /// The fake cannot enforce an index, so the handler test says which outcome it wants.
    /// </summary>
    public Error? AddConflict { get; set; }

    public void Seed(params Tour[] tours) => _tours.AddRange(tours);

    public Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken)
    {
        if (AddConflict is not null)
        {
            return Task.FromResult(Result<Tour>.Failure(AddConflict));
        }

        _tours.Add(tour);
        return Task.FromResult(Result<Tour>.Success(tour));
    }

    public Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_tours.SingleOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Tour>>(
            Filter(tourDate, vehicleId, driverId)
                .OrderBy(t => t.TourDate)
                .ThenBy(t => t.Id)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Task.FromResult(Filter(tourDate, vehicleId, driverId).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    private IEnumerable<Tour> Filter(DateOnly? tourDate, Guid? vehicleId, Guid? driverId)
    {
        IEnumerable<Tour> query = _tours;

        if (tourDate is not null)
        {
            query = query.Where(t => t.TourDate == tourDate);
        }

        if (vehicleId is not null)
        {
            query = query.Where(t => t.VehicleId == vehicleId);
        }

        if (driverId is not null)
        {
            query = query.Where(t => t.DriverId == driverId);
        }

        return query;
    }
}
```

`tests/TransBrain.Application.Tests/Fakes/StubCurrentUser.cs`:

```csharp
using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

public sealed class StubCurrentUser(string? userId, params string[] roles) : ICurrentUser
{
    public string? UserId { get; } = userId;

    public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static StubCurrentUser Dispatcher() => new("dispatcher-sub", "disponent");

    public static StubCurrentUser Admin() => new("admin-sub", "admin");

    public static StubCurrentUser DriverWith(string externalUserId) => new(externalUserId, "fahrer");
}
```

- [ ] **Step 2: Write the failing handler tests**

`tests/TransBrain.Application.Tests/Features/Tours/CreateTourCommandHandlerTests.cs`:

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.CreateTour;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Tours;

public class CreateTourCommandHandlerTests
{
    private static readonly DateOnly TourDate = new(2027, 3, 1);

    private static Vehicle AVehicle() => Vehicle.Create(
        LicensePlate.Create("M-AB 1234").Value,
        VehicleType.RigidTruck,
        18_000,
        13.6m,
        new DateOnly(2028, 1, 1)).Value;

    private static Driver ADriver() => Driver.Create(
        "Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 6, 30), null).Value;

    [Fact]
    public async Task Handle_AvailableVehicleAndDriver_PersistsTourAndReturnsResponse()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Planned");
        result.Value.TourDate.Should().Be(TourDate);
        result.Value.VehicleLicensePlate.Should().Be(vehicle.LicensePlate.Value);
        result.Value.DriverName.Should().Be("Fahrer, Frank");
        result.Value.Stops.Should().BeEmpty();
        // The capacity headroom a dispatcher needs before assigning anything.
        result.Value.VehiclePayloadKg.Should().Be(18_000);
        result.Value.TotalWeightKg.Should().Be(0);
        tours.Tours.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsNotFoundAndPersistsNothing()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Driver driver = ADriver();
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, Guid.CreateVersion7(), driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Vehicle.NotFound");
        tours.Tours.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndPersistsNothing()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        vehicles.Seed(vehicle);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.NotFound");
        tours.Tours.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DriverLicenceExpiredBeforeTourDate_ReturnsDomainConflict()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        Driver driver = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.CE], TourDate.AddDays(-1), null).Value;
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LicenceExpired");
        tours.Tours.Should().BeEmpty();
    }

    // The double-booking rule lives in a database unique index, so the handler's only job is to
    // pass the repository's Conflict through unchanged rather than swallow or reword it.
    [Fact]
    public async Task Handle_RepositoryReportsDoubleBooking_ReturnsThatConflict()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        InMemoryTourRepository tours = new()
        {
            AddConflict = Error.Conflict("Tour.VehicleAlreadyBooked", "already booked")
        };
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.VehicleAlreadyBooked");
    }
}
```

Also add `tests/TransBrain.Application.Tests/Features/Tours/CreateTourCommandValidatorTests.cs`:

```csharp
using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Tours.CreateTour;

namespace TransBrain.Application.Tests.Features.Tours;

public class CreateTourCommandValidatorTests
{
    private readonly CreateTourCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyVehicleId_IsInvalid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.Empty, Guid.CreateVersion7()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTourCommand.VehicleId));
    }

    [Fact]
    public void Validate_EmptyDriverId_IsInvalid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.CreateVersion7(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTourCommand.DriverId));
    }

    [Fact]
    public void Validate_BothIdsPresent_IsValid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.CreateVersion7(), Guid.CreateVersion7()));

        result.IsValid.Should().BeTrue();
    }
}
```

Before writing these, open `InMemoryVehicleRepository.cs` and `InMemoryDriverRepository.cs` and confirm their `Seed` signatures match the usage above.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Tours`
Expected: compile errors — the abstractions, the response and the slice do not exist.

- [ ] **Step 4: Implement the abstractions and the response**

`src/TransBrain.Application/Abstractions/ITourRepository.cs`:

```csharp
using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Abstractions;

public interface ITourRepository
{
    /// <summary>
    /// Persists a new tour. Returns a <see cref="ErrorType.Conflict"/> when the database's
    /// unique index rejects a second tour for the same vehicle or driver on the same date —
    /// that rule cannot live in the domain, because uniqueness is not something one object
    /// can see.
    /// </summary>
    Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken);

    Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

`src/TransBrain.Application/Abstractions/ICurrentUser.cs`:

```csharp
namespace TransBrain.Application.Abstractions;

/// <summary>
/// The authenticated caller, as far as the Application layer needs to know them.
/// </summary>
/// <remarks>
/// Spec §9 restricts a driver to their own tours, and that rule has to be checked where the
/// tour and the driver are both in hand — in a handler. Handlers must not reference
/// HttpContext or ClaimsPrincipal, so this is the seam. <see cref="UserId"/> is the Keycloak
/// "sub" claim, which is what a driver's <c>ExternalUserId</c> stores.
/// </remarks>
public interface ICurrentUser
{
    string? UserId { get; }

    bool IsInRole(string role);
}
```

`src/TransBrain.Application/Features/Tours/TourResponse.cs`:

```csharp
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours;

public sealed record TourStopResponse(int Sequence, Guid TransportOrderId, string OrderNumber, string StopType);

public sealed record TourResponse(
    Guid Id,
    DateOnly TourDate,
    Guid VehicleId,
    string VehicleLicensePlate,
    Guid DriverId,
    string DriverName,
    string Status,
    int TotalWeightKg,
    decimal TotalLoadMeters,
    int VehiclePayloadKg,
    decimal VehicleLoadMeters,
    IReadOnlyList<TourStopResponse> Stops)
{
    /// <param name="assignedOrders">
    /// Every order this tour's stops refer to. Carried so the response can report the tour's
    /// load against the vehicle's rating and name each stop's order — a dispatcher deciding
    /// whether one more order fits needs both numbers, and fetching the vehicle separately to
    /// draw a capacity bar would be a round trip for data the server already held.
    /// </param>
    public static TourResponse From(
        Tour tour,
        Vehicle vehicle,
        Driver driver,
        IReadOnlyList<TransportOrder> assignedOrders)
    {
        Dictionary<Guid, TransportOrder> byId = assignedOrders.ToDictionary(order => order.Id);

        TourStopResponse[] stops = tour.Stops
            .OrderBy(stop => stop.Sequence)
            .Select(stop => new TourStopResponse(
                stop.Sequence,
                stop.TransportOrderId,
                // An id with no order behind it means the two were loaded inconsistently.
                // Showing the raw id is more useful to whoever debugs that than an exception.
                byId.TryGetValue(stop.TransportOrderId, out TransportOrder? order)
                    ? order.OrderNumber.Value
                    : stop.TransportOrderId.ToString(),
                stop.StopType.ToString()))
            .ToArray();

        return new TourResponse(
            tour.Id,
            tour.TourDate,
            tour.VehicleId,
            vehicle.LicensePlate.Value,
            tour.DriverId,
            $"{driver.LastName}, {driver.FirstName}",
            tour.Status.ToString(),
            assignedOrders.Sum(order => order.Cargo.WeightKg),
            assignedOrders.Sum(order => order.Cargo.LoadMeters),
            vehicle.PayloadKg,
            vehicle.LoadMeters,
            stops);
    }
}
```

- [ ] **Step 5: Implement the `CreateTour` slice**

`CreateTourCommand.cs`:

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.CreateTour;

public sealed record CreateTourCommand(DateOnly TourDate, Guid VehicleId, Guid DriverId)
    : ICommand<TourResponse>;
```

`CreateTourCommandValidator.cs`:

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Tours.CreateTour;

/// <remarks>
/// Shape only. Whether the vehicle is available, whether the driver's licence covers the tour
/// date, and whether either is already booked that day are domain and database questions —
/// see Tour.Create and TourConfiguration. Restating them here would be a second copy that
/// eventually disagrees with the first.
/// </remarks>
public sealed class CreateTourCommandValidator : AbstractValidator<CreateTourCommand>
{
    public CreateTourCommandValidator()
    {
        RuleFor(c => c.VehicleId).NotEmpty();
        RuleFor(c => c.DriverId).NotEmpty();
    }
}
```

`CreateTourCommandHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours.CreateTour;

internal sealed class CreateTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers)
    : ICommandHandler<CreateTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        CreateTourCommand command,
        CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await vehicles.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.VehicleId}'.");
        }

        Driver? driver = await drivers.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.DriverId}'.");
        }

        // Availability and the licence rule are decided here, by the domain, with both objects
        // in hand. Double-booking is decided by the database inside AddAsync below.
        Result<Tour> tour = Tour.Create(command.TourDate, vehicle, driver);
        if (!tour.IsSuccess)
        {
            return tour.Error!;
        }

        Result<Tour> added = await tours.AddAsync(tour.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return TourResponse.From(added.Value, vehicle, driver, []);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TransBrain.Application.Tests`
Expected: every test passes.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add the tour repository abstraction and the CreateTour slice"
```

---

### Task 3: Persistence, and a double booking the database refuses

**Files:**
- Create: `src/TransBrain.Infrastructure/Persistence/Configurations/TourConfiguration.cs`
- Create: `src/TransBrain.Infrastructure/Persistence/Repositories/TourRepository.cs`
- Modify: `src/TransBrain.Infrastructure/Persistence/TransBrainDbContext.cs`, `src/TransBrain.Infrastructure/DependencyInjection.cs`
- Create: a migration under `src/TransBrain.Infrastructure/Persistence/Migrations/`
- Test: `tests/TransBrain.Api.IntegrationTests/TourDoubleBookingTests.cs`

**Interfaces:**
- Consumes: `ITourRepository`, `Tour`, `TourStop`.
- Produces: `DbSet<Tour> Tours`; `AddInfrastructure()` additionally registers `ITourRepository`.

**The double-booking index is the point of this task.** `SELECT ... WHERE TourDate = @d AND VehicleId = @v` followed by an insert is a read-then-write race: two dispatchers assigning the same lorry at the same moment both read "free" and both insert. A unique index is the only construct that actually serialises them, and it is the same mechanism Phase 2 used for `ExternalUserId`.

- [ ] **Step 1: Add the DbSet and the configuration**

Add to `TransBrainDbContext`, below `TransportOrders`:

```csharp
    public DbSet<Tour> Tours => Set<Tour>();
```

with `using TransBrain.Domain.Tours;` at the top.

`TourConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Tours;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("tours");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TourDate).HasColumnName("tour_date").IsRequired();
        builder.Property(t => t.VehicleId).HasColumnName("vehicle_id").IsRequired();
        builder.Property(t => t.DriverId).HasColumnName("driver_id").IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The two invariants no single object can check. Spec §5.5 allows a vehicle and a
        // driver at most one tour per date, with no exception for a completed one, so these are
        // unconditional. Enforcing them here rather than with a pre-flight query is what makes
        // them hold under concurrency: an index serialises, a SELECT does not.
        builder.HasIndex(t => new { t.TourDate, t.VehicleId })
            .IsUnique()
            .HasDatabaseName("ix_tours_date_vehicle_unique");

        builder.HasIndex(t => new { t.TourDate, t.DriverId })
            .IsUnique()
            .HasDatabaseName("ix_tours_date_driver_unique");

        builder.OwnsMany(t => t.Stops, stop =>
        {
            stop.ToTable("tour_stops");
            stop.WithOwner().HasForeignKey("tour_id");
            stop.Property(s => s.Sequence).HasColumnName("sequence").IsRequired();
            stop.Property(s => s.TransportOrderId).HasColumnName("transport_order_id").IsRequired();
            stop.Property(s => s.StopType).HasColumnName("stop_type").HasConversion<string>()
                .HasMaxLength(20).IsRequired();
            stop.HasKey("tour_id", "Sequence");
        });

        // The backing field, not the IReadOnlyList property: the aggregate exposes its stops
        // read-only on purpose, and EF must write through the field to respect that.
        builder.Navigation(t => t.Stops).HasField("_stops").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

If `HasKey("tour_id", "Sequence")` fails because the shadow foreign key is not yet defined at that point, drop the explicit `HasKey` and let EF create its own shadow key — say so in your report, and confirm the generated migration still creates `tour_stops` with a primary key.

- [ ] **Step 2: Implement the repository**

`TourRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class TourRepository(TransBrainDbContext context) : ITourRepository
{
    // PostgreSQL error code for unique_violation.
    private const string UniqueViolation = "23505";

    private const string VehicleIndex = "ix_tours_date_vehicle_unique";

    public async Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken)
    {
        await context.Tours.AddAsync(tour, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return tour;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: UniqueViolation } postgres)
        {
            context.Entry(tour).State = EntityState.Detached;

            // Naming which of the two is double-booked matters: "this tour conflicts" sends a
            // dispatcher looking at both the lorry and the driver.
            return postgres.ConstraintName == VehicleIndex
                ? Error.Conflict(
                    "Tour.VehicleAlreadyBooked",
                    $"That vehicle already has a tour on {tour.TourDate:yyyy-MM-dd}.")
                : Error.Conflict(
                    "Tour.DriverAlreadyBooked",
                    $"That driver already has a tour on {tour.TourDate:yyyy-MM-dd}.");
        }
    }

    // Stops are an owned collection, so they load with the tour; no Include is needed. They are
    // tracked deliberately - AssignOrder and RemoveOrder mutate them.
    public Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Tours.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => await Filter(tourDate, vehicleId, driverId)
            .OrderBy(t => t.TourDate)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Filter(tourDate, vehicleId, driverId).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    private IQueryable<Tour> Filter(DateOnly? tourDate, Guid? vehicleId, Guid? driverId)
    {
        IQueryable<Tour> query = context.Tours;

        if (tourDate is not null)
        {
            query = query.Where(t => t.TourDate == tourDate);
        }

        if (vehicleId is not null)
        {
            query = query.Where(t => t.VehicleId == vehicleId);
        }

        if (driverId is not null)
        {
            query = query.Where(t => t.DriverId == driverId);
        }

        return query;
    }
}
```

Register it in `AddInfrastructure`, next to the other repositories:

```csharp
        services.AddScoped<ITourRepository, TourRepository>();
```

- [ ] **Step 3: Generate the migration**

```bash
dotnet ef migrations add AddTours \
  --project src/TransBrain.Infrastructure \
  --startup-project src/TransBrain.Api \
  --output-dir Persistence/Migrations
```

Verify the generated migration creates `tours` and `tour_stops`, and contains both unique indexes by the names `ix_tours_date_vehicle_unique` and `ix_tours_date_driver_unique`. The repository above branches on those exact strings; if EF emitted different names, fix the configuration rather than the repository, so the name stays declared in one place.

- [ ] **Step 4: Prove the database actually refuses a double booking**

`tests/TransBrain.Api.IntegrationTests/TourDoubleBookingTests.cs`:

```csharp
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Api.IntegrationTests;

public class TourDoubleBookingTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static Vehicle AVehicle(string plate) => Vehicle.Create(
        LicensePlate.Create(plate).Value, VehicleType.RigidTruck, 18_000, 13.6m, new DateOnly(2028, 1, 1)).Value;

    private static Driver ADriver(string lastName) => Driver.Create(
        "Frank", lastName, [LicenseClass.CE], new DateOnly(2028, 6, 30), null).Value;

    [Fact]
    public async Task AddAsync_SecondTourForTheSameVehicleAndDate_ReturnsConflict()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        DateOnly date = new(2097, 5, 1);
        Vehicle vehicle = AVehicle("M-DB 1001");
        Driver firstDriver = ADriver("DoppeltEins");
        Driver secondDriver = ADriver("DoppeltZwei");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(firstDriver, CancellationToken.None);
        await drivers.AddAsync(secondDriver, CancellationToken.None);

        await tours.AddAsync(Tour.Create(date, vehicle, firstDriver).Value, CancellationToken.None);

        // Same vehicle, same date, a different driver: the vehicle index must be what refuses.
        Result<Tour> second = await tours.AddAsync(
            Tour.Create(date, vehicle, secondDriver).Value, CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error!.Type.Should().Be(ErrorType.Conflict);
        second.Error.Code.Should().Be("Tour.VehicleAlreadyBooked");
    }

    [Fact]
    public async Task AddAsync_SecondTourForTheSameDriverAndDate_ReturnsConflict()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        DateOnly date = new(2097, 6, 1);
        Vehicle firstVehicle = AVehicle("M-DB 2001");
        Vehicle secondVehicle = AVehicle("M-DB 2002");
        Driver driver = ADriver("DoppeltDrei");
        await vehicles.AddAsync(firstVehicle, CancellationToken.None);
        await vehicles.AddAsync(secondVehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        await tours.AddAsync(Tour.Create(date, firstVehicle, driver).Value, CancellationToken.None);

        Result<Tour> second = await tours.AddAsync(
            Tour.Create(date, secondVehicle, driver).Value, CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be("Tour.DriverAlreadyBooked");
    }

    [Fact]
    public async Task AddAsync_SameVehicleOnADifferentDate_Succeeds()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        Vehicle vehicle = AVehicle("M-DB 3001");
        Driver driver = ADriver("DoppeltVier");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        await tours.AddAsync(
            Tour.Create(new DateOnly(2097, 7, 1), vehicle, driver).Value, CancellationToken.None);

        Result<Tour> next = await tours.AddAsync(
            Tour.Create(new DateOnly(2097, 7, 2), vehicle, driver).Value, CancellationToken.None);

        next.IsSuccess.Should().BeTrue();
    }

    // Proves the owned stop collection round-trips: sequence, type and order all survive, and
    // the aggregate rebuilds them through its private backing field rather than a public setter.
    [Fact]
    public async Task GetByIdAsync_AfterAssigningAnOrder_ReloadsBothStopsInSequence()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITransportOrderRepository orders = scope.ServiceProvider.GetRequiredService<ITransportOrderRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        Vehicle vehicle = AVehicle("M-DB 4001");
        Driver driver = ADriver("DoppeltFuenf");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        // A real, persisted order: TransportOrder.Create assigns its own id, and widening the
        // domain's API just so a test could choose one would be the tail wagging the dog.
        DateTimeOffset pickup = new(2097, 8, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2097, 41),
            address,
            address,
            Cargo.Create("Palettenware", 5_000, 4.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
        await orders.AddAsync(order, CancellationToken.None);

        Tour tour = Tour.Create(new DateOnly(2097, 8, 1), vehicle, driver).Value;
        tour.AssignOrder(order, vehicle, []);
        await tours.AddAsync(tour, CancellationToken.None);

        Tour? reloaded = await tours.GetByIdAsync(tour.Id, CancellationToken.None);

        reloaded!.Stops.Should().HaveCount(2);
        reloaded.Stops[0].Sequence.Should().Be(1);
        reloaded.Stops[0].StopType.Should().Be(StopType.Pickup);
        reloaded.Stops[0].TransportOrderId.Should().Be(order.Id);
        reloaded.Stops[1].Sequence.Should().Be(2);
        reloaded.Stops[1].StopType.Should().Be(StopType.Delivery);
    }
}
```

Add `using TransBrain.Domain.Common;` for `Address`, `Cargo` and `TimeWindow`, and `using TransBrain.Domain.Orders;` for `TransportOrder` and `OrderNumber`.

- [ ] **Step 5: Verify the whole suite is green**

Run: `dotnet build TransBrain.slnx` then `dotnet test TransBrain.slnx`
Expected: 0 warnings, 0 errors; every test passes.

- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.Infrastructure tests/TransBrain.Api.IntegrationTests
git commit -m "feat(infrastructure): persist tours with database-enforced double-booking rules"
```

---

### Task 4: `AssignOrder` and `RemoveOrder`

**Files:**
- Create: `src/TransBrain.Application/Features/Tours/AssignOrder/AssignOrderCommand.cs`, `AssignOrderCommandHandler.cs`
- Create: `src/TransBrain.Application/Features/Tours/RemoveOrder/RemoveOrderCommand.cs`, `RemoveOrderCommandHandler.cs`
- Create: `src/TransBrain.Application/Features/Tours/TourLoader.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Tours/AssignOrderCommandHandlerTests.cs`, `RemoveOrderCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ITourRepository`, `IVehicleRepository`, `IDriverRepository`, `ITransportOrderRepository`, `Tour.AssignOrder`, `Tour.RemoveOrder`, `TourResponse.From`.
- Produces:
  - `sealed record AssignOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>`
  - `sealed record RemoveOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>`
  - `internal static class TourLoader` with
    `static Task<Result<TourContext>> LoadAsync(Guid tourId, ITourRepository, IVehicleRepository, IDriverRepository, ITransportOrderRepository, CancellationToken)`
    and `internal sealed record TourContext(Tour Tour, Vehicle Vehicle, Driver Driver, IReadOnlyList<TransportOrder> AssignedOrders)`

**Every tour handler needs the same four things** — the tour, its vehicle, its driver and its assigned orders — because `TourResponse.From` needs all four and the domain methods need three of them. `TourLoader` is that load written once. Five handlers each repeating four lookups and four not-found branches is twenty places for one of them to be forgotten.

- [ ] **Step 1: Write `TourLoader`**

`src/TransBrain.Application/Features/Tours/TourLoader.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours;

/// <summary>A tour together with everything needed to decide about it and to render it.</summary>
internal sealed record TourContext(
    Tour Tour,
    Vehicle Vehicle,
    Driver Driver,
    IReadOnlyList<TransportOrder> AssignedOrders);

/// <remarks>
/// Every tour handler needs the same four loads: the tour, its vehicle, its driver, and the
/// orders its stops point at. Written once here rather than five times, because five copies of
/// four not-found branches is where one branch quietly goes missing.
/// </remarks>
internal static class TourLoader
{
    public static async Task<Result<TourContext>> LoadAsync(
        Guid tourId,
        ITourRepository tours,
        IVehicleRepository vehicles,
        IDriverRepository drivers,
        ITransportOrderRepository orders,
        CancellationToken cancellationToken)
    {
        Tour? tour = await tours.GetByIdAsync(tourId, cancellationToken);
        if (tour is null)
        {
            return Error.NotFound("Tour.NotFound", $"No tour with id '{tourId}'.");
        }

        Vehicle? vehicle = await vehicles.GetByIdAsync(tour.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            // Only reachable if a vehicle was deleted out from under a tour. Reported rather
            // than dereferenced, so the cause is legible instead of a NullReferenceException.
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{tour.VehicleId}'.");
        }

        Driver? driver = await drivers.GetByIdAsync(tour.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{tour.DriverId}'.");
        }

        List<TransportOrder> assigned = [];
        foreach (Guid orderId in tour.AssignedOrderIds())
        {
            TransportOrder? order = await orders.GetByIdAsync(orderId, cancellationToken);
            if (order is not null)
            {
                assigned.Add(order);
            }
        }

        return new TourContext(tour, vehicle, driver, assigned);
    }
}
```

- [ ] **Step 2: Write the failing handler tests**

`tests/TransBrain.Application.Tests/Features/Tours/AssignOrderCommandHandlerTests.cs`:

```csharp
using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.AssignOrder;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Tours;

public class AssignOrderCommandHandlerTests
{
    private static readonly DateOnly TourDate = new(2027, 3, 1);

    private sealed record Fixture(
        InMemoryTourRepository Tours,
        InMemoryVehicleRepository Vehicles,
        InMemoryDriverRepository Drivers,
        InMemoryTransportOrderRepository Orders,
        Tour Tour,
        Vehicle Vehicle)
    {
        public AssignOrderCommandHandler Handler() => new(Tours, Vehicles, Drivers, Orders);
    }

    private static Vehicle AVehicle(int payloadKg = 18_000, decimal loadMeters = 13.6m) =>
        Vehicle.Create(LicensePlate.Create("M-AB 1234").Value, VehicleType.RigidTruck, payloadKg,
            loadMeters, new DateOnly(2028, 1, 1)).Value;

    private static Driver ADriver() =>
        Driver.Create("Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 6, 30), null).Value;

    private static TransportOrder AnOrder(int weightKg = 5_000, decimal loadMeters = 4.0m, int sequence = 1)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            address,
            address,
            Cargo.Create("Palettenware", weightKg, loadMeters).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
    }

    private static Fixture ATourFixture(int payloadKg = 18_000, decimal loadMeters = 13.6m)
    {
        InMemoryTourRepository tours = new();
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTransportOrderRepository orders = new();

        Vehicle vehicle = AVehicle(payloadKg, loadMeters);
        Driver driver = ADriver();
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        Tour tour = Tour.Create(TourDate, vehicle, driver).Value;
        tours.Seed(tour);

        return new Fixture(tours, vehicles, drivers, orders, tour, vehicle);
    }

    [Fact]
    public async Task Handle_DraftOrder_AddsTwoStopsPlansTheOrderAndSavesOnce()
    {
        Fixture f = ATourFixture();
        TransportOrder order = AnOrder();
        f.Orders.Seed(order);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops[0].StopType.Should().Be("Pickup");
        result.Value.Stops[0].OrderNumber.Should().Be(order.OrderNumber.Value);
        result.Value.Stops[1].StopType.Should().Be("Delivery");
        result.Value.TotalWeightKg.Should().Be(5_000);
        order.Status.Should().Be(OrderStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFoundAndDoesNotSave()
    {
        Fixture f = ATourFixture();
        TransportOrder order = AnOrder();
        f.Orders.Seed(order);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(Guid.CreateVersion7(), order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFoundAndDoesNotSave()
    {
        Fixture f = ATourFixture();

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.NotFound");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OrderExceedingPayload_ReturnsConflictAndDoesNotSave()
    {
        Fixture f = ATourFixture(payloadKg: 6_000);
        TransportOrder order = AnOrder(weightKg: 7_000);
        f.Orders.Seed(order);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.PayloadExceeded");
        order.Status.Should().Be(OrderStatus.Draft);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    // The capacity sum must count what the tour already carries, not just the incoming order.
    // A handler that passed an empty list to the domain would pass every other test in this
    // file and still let a dispatcher overload a lorry one order at a time.
    [Fact]
    public async Task Handle_SecondOrderTippingItOverThePayload_ReturnsConflict()
    {
        Fixture f = ATourFixture(payloadKg: 10_000);
        TransportOrder first = AnOrder(weightKg: 6_000, sequence: 1);
        TransportOrder second = AnOrder(weightKg: 5_000, sequence: 2);
        f.Orders.Seed(first, second);
        await f.Handler().Handle(new AssignOrderCommand(f.Tour.Id, first.Id), CancellationToken.None);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.PayloadExceeded");
        second.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Handle_OrderAlreadyPlanned_ReturnsConflict()
    {
        Fixture f = ATourFixture();
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        f.Orders.Seed(order);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TourAlreadyInProgress_ReturnsConflict()
    {
        Fixture f = ATourFixture();
        TransportOrder onTour = AnOrder(sequence: 1);
        f.Orders.Seed(onTour);
        await f.Handler().Handle(new AssignOrderCommand(f.Tour.Id, onTour.Id), CancellationToken.None);
        f.Tour.Start();
        TransportOrder late = AnOrder(sequence: 2);
        f.Orders.Seed(late);

        Result<TourResponse> result = await f.Handler().Handle(
            new AssignOrderCommand(f.Tour.Id, late.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }
}
```

`tests/TransBrain.Application.Tests/Features/Tours/RemoveOrderCommandHandlerTests.cs` — reuse the same fixture shape (copy the private helpers; do not share them across test classes) with these cases:

```csharp
    [Fact]
    public async Task Handle_AssignedOrder_DropsItsStopsReturnsItToDraftAndSavesOnce()
    {
        Fixture f = ATourFixture();
        TransportOrder order = AnOrder();
        f.Orders.Seed(order);
        await new AssignOrderCommandHandler(f.Tours, f.Vehicles, f.Drivers, f.Orders)
            .Handle(new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await f.Handler().Handle(
            new RemoveOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().BeEmpty();
        result.Value.TotalWeightKg.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Draft);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OrderNotOnTheTour_ReturnsNotFoundAndDoesNotSave() { /* Tour.OrderNotAssigned */ }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFoundAndDoesNotSave() { /* Tour.NotFound */ }

    [Fact]
    public async Task Handle_TourInProgress_ReturnsConflictAndDoesNotSave() { /* Tour.NotEditable */ }
```

Fill each stubbed body following the shape of the first — arrange with the fixture, act through `f.Handler()`, assert the code named in the comment plus `SaveChangesCallCount`. Add `ResetSaveCount()` to `InMemoryTourRepository`:

```csharp
    public void ResetSaveCount() => SaveChangesCallCount = 0;
```

- [ ] **Step 3: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Tours`
Expected: compile errors.

- [ ] **Step 4: Implement both slices**

`AssignOrderCommand.cs`:

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.AssignOrder;

public sealed record AssignOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>;
```

`AssignOrderCommandHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.AssignOrder;

internal sealed class AssignOrderCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders)
    : ICommandHandler<AssignOrderCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        AssignOrderCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        TransportOrder? order = await orders.GetByIdAsync(command.TransportOrderId, cancellationToken);
        if (order is null)
        {
            return Error.NotFound(
                "TransportOrder.NotFound", $"No transport order with id '{command.TransportOrderId}'.");
        }

        // AssignedOrders is what makes the capacity sum count the whole tour rather than just
        // this one order - the difference between a full lorry and an overloaded one.
        Result<Unit> assigned = tour.Tour.AssignOrder(order, tour.Vehicle, tour.AssignedOrders);
        if (!assigned.IsSuccess)
        {
            return assigned.Error!;
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, [.. tour.AssignedOrders, order]);
    }
}
```

`RemoveOrderCommand.cs`:

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.RemoveOrder;

public sealed record RemoveOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>;
```

`RemoveOrderCommandHandler.cs`:

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.RemoveOrder;

internal sealed class RemoveOrderCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders)
    : ICommandHandler<RemoveOrderCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        RemoveOrderCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        TransportOrder? order = tour.AssignedOrders
            .SingleOrDefault(o => o.Id == command.TransportOrderId);

        if (order is null)
        {
            return Error.NotFound(
                "Tour.OrderNotAssigned", $"Order '{command.TransportOrderId}' is not on this tour.");
        }

        Result<Unit> removed = tour.Tour.RemoveOrder(order);
        if (!removed.IsSuccess)
        {
            return removed.Error!;
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(
            tour.Tour,
            tour.Vehicle,
            tour.Driver,
            tour.AssignedOrders.Where(o => o.Id != order.Id).ToList());
    }
}
```

Note the ordering difference: `RemoveOrder` looks the order up in the tour's own assigned set, so an order that exists but is not on this tour answers `Tour.OrderNotAssigned` rather than a bare `TransportOrder.NotFound`. `AssignOrder` looks it up in the repository, because the whole point is that it is not on the tour yet.

- [ ] **Step 5: Run the whole suite and commit**

```bash
dotnet test TransBrain.slnx
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add AssignOrder and RemoveOrder tour slices"
```

---

### Task 5: `StartTour`, `CompleteTour`, and the driver-scoping rule

**Files:**
- Create: `src/TransBrain.Application/Features/Tours/TourAccess.cs`
- Create: `src/TransBrain.Application/Features/Tours/StartTour/StartTourCommand.cs`, `StartTourCommandHandler.cs`
- Create: `src/TransBrain.Application/Features/Tours/CompleteTour/CompleteTourCommand.cs`, `CompleteTourCommandHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Tours/StartTourCommandHandlerTests.cs`, `CompleteTourCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `TourLoader`, `Tour.Start`, `Tour.Complete`, `TransportOrder.MarkInTransit`, `TransportOrder.MarkDelivered`.
- Produces:
  - `internal static class TourAccess` with `static Result<Unit> EnsureMayChangeStatus(TourContext, ICurrentUser)` and `static bool MaySee(Tour, Driver, ICurrentUser)`
  - `sealed record StartTourCommand(Guid TourId) : ICommand<TourResponse>`
  - `sealed record CompleteTourCommand(Guid TourId) : ICommand<TourResponse>`

**This is the task that finally implements spec §9's "nur eigene".** A `fahrer` may start and complete only tours whose driver is them; an `admin` or `disponent` may act on any. The rule is written once, in `TourAccess`, and called from both handlers.

**Both handlers validate every order before moving any of them.** Nothing is persisted until `SaveChangesAsync`, so a mid-loop failure would not reach the database — but it would leave half the in-memory orders mutated, and a later handler on the same scope would see a state that never existed. Checking first costs one extra pass and removes the whole question.

- [ ] **Step 1: Write `TourAccess`**

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours;

/// <summary>
/// Spec §9's "nur eigene" rule for drivers, written exactly once.
/// </summary>
/// <remarks>
/// A driver's identity is the Keycloak "sub" claim, which is stored on the driver record as
/// ExternalUserId. A tour whose driver has no ExternalUserId therefore belongs to nobody who
/// can sign in, and a fahrer is refused it — treating a missing link as "matches everyone"
/// would hand every unlinked driver's tour to whoever asked first.
/// </remarks>
internal static class TourAccess
{
    public const string AdminRole = "admin";
    public const string DispatcherRole = "disponent";
    public const string DriverRole = "fahrer";

    public static Result<Unit> EnsureMayChangeStatus(TourContext context, ICurrentUser currentUser)
    {
        if (currentUser.IsInRole(AdminRole) || currentUser.IsInRole(DispatcherRole))
        {
            return Unit.Value;
        }

        if (MaySee(context.Tour, context.Driver, currentUser))
        {
            return Unit.Value;
        }

        return Error.Forbidden(
            "Tour.NotYours",
            "A driver may only start or complete their own tours.");
    }

    public static bool MaySee(Tour tour, Driver driver, ICurrentUser currentUser)
    {
        if (currentUser.IsInRole(AdminRole) || currentUser.IsInRole(DispatcherRole))
        {
            return true;
        }

        return driver.Id == tour.DriverId
               && !string.IsNullOrWhiteSpace(driver.ExternalUserId)
               && !string.IsNullOrWhiteSpace(currentUser.UserId)
               && string.Equals(driver.ExternalUserId, currentUser.UserId, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Write the failing tests**

`StartTourCommandHandlerTests.cs` — build the same fixture as Task 4 (copy the helpers) plus a `StubCurrentUser`, and cover:

- `Handle_PlannedTourAsDispatcher_StartsItAndMovesEveryOrderToInTransit` — two assigned orders; assert `result.Value.Status == "InProgress"`, both orders `OrderStatus.InTransit`, `SaveChangesCallCount == 1`
- `Handle_TourWithoutStops_ReturnsConflictAndDoesNotSave` — `Tour.NoStops`
- `Handle_AlreadyInProgress_ReturnsConflict` — `Tour.InvalidTransition`
- `Handle_UnknownTour_ReturnsNotFound` — `Tour.NotFound`
- `Handle_AsTheAssignedDriver_Succeeds` — driver seeded with `ExternalUserId = "driver-sub"`, `StubCurrentUser.DriverWith("driver-sub")`
- `Handle_AsADifferentDriver_ReturnsForbiddenAndDoesNotSave` — `StubCurrentUser.DriverWith("someone-else")`, assert `ErrorType.Forbidden`, code `Tour.NotYours`, tour still `Planned`, `SaveChangesCallCount == 0`
- `Handle_AsADriverWhenTheTourDriverHasNoExternalUserId_ReturnsForbidden` — the unlinked-driver case the remark above warns about

Write the first and the two driver cases in full; here is the driver-refusal one, which is the one that must not be got wrong:

```csharp
    [Fact]
    public async Task Handle_AsADifferentDriver_ReturnsForbiddenAndDoesNotSave()
    {
        Fixture f = ATourFixture(driverExternalUserId: "driver-sub");
        TransportOrder order = AnOrder();
        f.Orders.Seed(order);
        await new AssignOrderCommandHandler(f.Tours, f.Vehicles, f.Drivers, f.Orders)
            .Handle(new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);
        f.Tours.ResetSaveCount();

        StartTourCommandHandler handler = new(
            f.Tours, f.Vehicles, f.Drivers, f.Orders, StubCurrentUser.DriverWith("someone-else"));

        Result<TourResponse> result = await handler.Handle(
            new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Tour.NotYours");
        f.Tour.Status.Should().Be(TourStatus.Planned);
        order.Status.Should().Be(OrderStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }
```

`ATourFixture` takes an optional `string? driverExternalUserId = null` and passes it to `Driver.Create`'s last parameter.

`CompleteTourCommandHandlerTests.cs` mirrors it: `Handle_InProgressTourAsDispatcher_CompletesItAndDeliversEveryOrder`, `Handle_PlannedTour_ReturnsConflict`, `Handle_AlreadyCompleted_ReturnsConflict`, `Handle_AsADifferentDriver_ReturnsForbidden`, `Handle_AsTheAssignedDriver_Succeeds`.

- [ ] **Step 3: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Tour`
Expected: compile errors.

- [ ] **Step 4: Implement `StartTour`**

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.StartTour;

public sealed record StartTourCommand(Guid TourId) : ICommand<TourResponse>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.StartTour;

internal sealed class StartTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : ICommandHandler<StartTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        StartTourCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        Result<Unit> allowed = TourAccess.EnsureMayChangeStatus(tour, currentUser);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        // Checked before anything moves. Nothing reaches the database until SaveChangesAsync,
        // so a mid-loop failure could not corrupt storage - but it would leave half the loaded
        // orders mutated in a state that never existed, which the next handler on this scope
        // would then read. One extra pass removes the question entirely.
        TransportOrder? notPlanned = tour.AssignedOrders
            .FirstOrDefault(order => order.Status != OrderStatus.Planned);

        if (notPlanned is not null)
        {
            return Error.Conflict(
                "Tour.OrderNotPlanned",
                $"Order '{notPlanned.OrderNumber.Value}' is '{notPlanned.Status}' and cannot go in transit.");
        }

        Result<Unit> started = tour.Tour.Start();
        if (!started.IsSuccess)
        {
            return started.Error!;
        }

        foreach (TransportOrder order in tour.AssignedOrders)
        {
            order.MarkInTransit();
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
```

- [ ] **Step 5: Implement `CompleteTour`**

Identical shape, with `OrderStatus.InTransit` as the required precondition, `Tour.OrderNotInTransit` as the code, `tour.Tour.Complete()` and `order.MarkDelivered()`:

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.CompleteTour;

public sealed record CompleteTourCommand(Guid TourId) : ICommand<TourResponse>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.CompleteTour;

internal sealed class CompleteTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : ICommandHandler<CompleteTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        CompleteTourCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        Result<Unit> allowed = TourAccess.EnsureMayChangeStatus(tour, currentUser);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        // Same two-pass reasoning as StartTourCommandHandler.
        TransportOrder? notInTransit = tour.AssignedOrders
            .FirstOrDefault(order => order.Status != OrderStatus.InTransit);

        if (notInTransit is not null)
        {
            return Error.Conflict(
                "Tour.OrderNotInTransit",
                $"Order '{notInTransit.OrderNumber.Value}' is '{notInTransit.Status}' and cannot be delivered.");
        }

        Result<Unit> completed = tour.Tour.Complete();
        if (!completed.IsSuccess)
        {
            return completed.Error!;
        }

        foreach (TransportOrder order in tour.AssignedOrders)
        {
            order.MarkDelivered();
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
```

- [ ] **Step 6: Run the whole suite and commit**

```bash
dotnet test TransBrain.slnx
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add StartTour and CompleteTour with driver-scoped access"
```

---

### Task 6: `ListTours` and `GetTourById`

**Files:**
- Create: `src/TransBrain.Application/Features/Tours/ListTours/ListToursQuery.cs`, `ListToursQueryValidator.cs`, `ListToursQueryHandler.cs`
- Create: `src/TransBrain.Application/Features/Tours/GetTourById/GetTourByIdQuery.cs`, `GetTourByIdQueryHandler.cs`
- Test: `tests/TransBrain.Application.Tests/Features/Tours/ListToursQueryHandlerTests.cs`, `GetTourByIdQueryHandlerTests.cs`, `ListToursQueryValidatorTests.cs`

**Interfaces:**
- Consumes: `ITourRepository`, `ICurrentUser`, `TourAccess.MaySee`, `TourResponse`, `PagedResult<T>`.
- Produces:
  - `sealed record ListToursQuery(int Page = 1, int PageSize = 20, DateOnly? TourDate = null, Guid? VehicleId = null, Guid? DriverId = null) : IQuery<PagedResult<TourResponse>>`
  - `sealed record GetTourByIdQuery(Guid Id) : IQuery<TourResponse>`

**Reads are driver-scoped too.** Spec §9's `Read` row says a `fahrer` sees "nur eigene Touren". `GetTourById` answers `Forbidden` for someone else's tour; `ListTours` narrows the driver filter to the caller rather than refusing, because a list that 403s would be useless to a driver opening the screen.

**Tours are not cached** (§7). Do not inject `ICacheService`.

- [ ] **Step 1: Write the failing tests**

`ListToursQueryHandlerTests` cases:
- `Handle_EmptyRepository_ReturnsEmptyPage`
- `Handle_FirstPage_OrdersByTourDate` — three tours on different dates, assert ascending
- `Handle_SecondPage_ReturnsRequestedSliceAndTotalCount`
- `Handle_TourDateFilter_ReturnsOnlyThatDay`
- `Handle_VehicleFilter_ReturnsOnlyThatVehiclesTours`
- `Handle_DriverFilter_ReturnsOnlyThatDriversTours`
- `Handle_AsADriver_NarrowsToTheirOwnToursEvenWithoutAFilter` — two tours for two drivers, caller is `StubCurrentUser.DriverWith("driver-sub")` matching one of them; assert exactly one item and `TotalCount == 1`
- `Handle_AsADriverAskingForSomeoneElsesDriverId_StillOnlySeesTheirOwn` — pass another driver's `DriverId` in the query and assert the caller's own scope still wins

Write that last one in full; it is the one that proves the narrowing cannot be argued away by a crafted query string:

```csharp
    [Fact]
    public async Task Handle_AsADriverAskingForSomeoneElsesDriverId_StillOnlySeesTheirOwn()
    {
        // A driver who edits the query string must not be able to widen their own scope.
        Fixture f = TwoDriversWithATourEach(mineExternalUserId: "driver-sub");
        ListToursQueryHandler handler = new(f.Tours, f.Vehicles, f.Drivers, f.Orders,
            StubCurrentUser.DriverWith("driver-sub"));

        Result<PagedResult<TourResponse>> result = await handler.Handle(
            new ListToursQuery(DriverId: f.OtherDriver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
```

`GetTourByIdQueryHandlerTests` cases: `Handle_KnownId_ReturnsTour`, `Handle_UnknownId_ReturnsNotFound` (`Tour.NotFound`), `Handle_AsTheAssignedDriver_ReturnsTour`, `Handle_AsADifferentDriver_ReturnsForbidden` (`Tour.NotYours`).

`ListToursQueryValidatorTests`: page at the cap valid, page beyond the cap invalid, page zero invalid, page size beyond 100 invalid — mirroring `ListOrdersQueryValidatorTests`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Application.Tests --filter FullyQualifiedName~Tour`
Expected: compile errors.

- [ ] **Step 3: Implement `ListTours`**

```csharp
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Tours.ListTours;

public sealed record ListToursQuery(
    int Page = 1,
    int PageSize = 20,
    DateOnly? TourDate = null,
    Guid? VehicleId = null,
    Guid? DriverId = null) : IQuery<PagedResult<TourResponse>>;
```

```csharp
using FluentValidation;

namespace TransBrain.Application.Features.Tours.ListTours;

public sealed class ListToursQueryValidator : AbstractValidator<ListToursQuery>
{
    public ListToursQueryValidator()
    {
        // The Page cap mirrors the other list validators: without it every distinct page is a
        // distinct query and an authenticated caller can walk the number space freely.
        RuleFor(q => q.Page).InclusiveBetween(1, 10_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours.ListTours;

/// <remarks>
/// Deliberately not cached: spec §7 excludes tours along with orders, as too volatile for the
/// invalidation cost.
/// </remarks>
internal sealed class ListToursQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : IQueryHandler<ListToursQuery, PagedResult<TourResponse>>
{
    public async Task<Result<PagedResult<TourResponse>>> Handle(
        ListToursQuery query,
        CancellationToken cancellationToken)
    {
        Guid? driverFilter = query.DriverId;

        // Spec §9: a fahrer sees only their own tours. Narrowed rather than refused - a list
        // endpoint that 403s would be useless to a driver opening the screen. Applied by
        // OVERWRITING the requested filter, not by combining with it, so editing the query
        // string cannot widen the scope.
        if (!currentUser.IsInRole(TourAccess.AdminRole) && !currentUser.IsInRole(TourAccess.DispatcherRole))
        {
            Driver? me = await FindDriverForCallerAsync(cancellationToken);
            if (me is null)
            {
                return new PagedResult<TourResponse>([], query.Page, query.PageSize, 0);
            }

            driverFilter = me.Id;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Tour> page = await tours.ListAsync(
            skip, query.PageSize, query.TourDate, query.VehicleId, driverFilter, cancellationToken);

        int totalCount = await tours.CountAsync(
            query.TourDate, query.VehicleId, driverFilter, cancellationToken);

        List<TourResponse> items = [];
        foreach (Tour tour in page)
        {
            Result<TourContext> context = await TourLoader.LoadAsync(
                tour.Id, tours, vehicles, drivers, orders, cancellationToken);

            if (context.IsSuccess)
            {
                items.Add(TourResponse.From(
                    context.Value.Tour, context.Value.Vehicle, context.Value.Driver,
                    context.Value.AssignedOrders));
            }
        }

        return new PagedResult<TourResponse>(items, query.Page, query.PageSize, totalCount);
    }

    private async Task<Driver?> FindDriverForCallerAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        return await drivers.GetByExternalUserIdAsync(currentUser.UserId, cancellationToken);
    }
}
```

**`IDriverRepository` does not have `GetByExternalUserIdAsync` yet — add it in this task**, in three places:

```csharp
// src/TransBrain.Application/Abstractions/IDriverRepository.cs
    Task<Driver?> GetByExternalUserIdAsync(string externalUserId, CancellationToken cancellationToken);
```

```csharp
// src/TransBrain.Infrastructure/Persistence/Repositories/DriverRepository.cs
    // Backed by the unique filtered index Phase 2 added on ExternalUserId, so this is an index
    // seek and can match at most one driver - which is what makes SingleOrDefault safe here.
    public Task<Driver?> GetByExternalUserIdAsync(string externalUserId, CancellationToken cancellationToken)
        => context.Drivers.SingleOrDefaultAsync(d => d.ExternalUserId == externalUserId, cancellationToken);
```

```csharp
// tests/TransBrain.Application.Tests/Fakes/InMemoryDriverRepository.cs
    public Task<Driver?> GetByExternalUserIdAsync(string externalUserId, CancellationToken cancellationToken)
        => Task.FromResult(_drivers.SingleOrDefault(d => d.ExternalUserId == externalUserId));
```

Check `InMemoryDriverRepository`'s backing field name before pasting the third one; if it is not `_drivers`, use whatever it is called.

- [ ] **Step 4: Implement `GetTourById`**

```csharp
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.GetTourById;

public sealed record GetTourByIdQuery(Guid Id) : IQuery<TourResponse>;
```

```csharp
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Tours.GetTourById;

internal sealed class GetTourByIdQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : IQueryHandler<GetTourByIdQuery, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        GetTourByIdQuery query,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            query.Id, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        // Unlike the list, a single-tour read refuses rather than narrows: the caller asked for
        // one specific tour, and silently answering about a different one would be worse.
        if (!TourAccess.MaySee(tour.Tour, tour.Driver, currentUser))
        {
            return Error.Forbidden("Tour.NotYours", "A driver may only see their own tours.");
        }

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
```

- [ ] **Step 5: Run the whole suite and commit**

```bash
dotnet test TransBrain.slnx
git add src/TransBrain.Application tests/TransBrain.Application.Tests
git commit -m "feat(application): add ListTours and GetTourById with driver-scoped reads"
```

---

### Task 7: Tour endpoints, `ICurrentUser` wiring, and integration tests

**Files:**
- Create: `src/TransBrain.Api/Authorization/HttpContextCurrentUser.cs`
- Create: `src/TransBrain.Api/Endpoints/TourEndpoints.cs`
- Modify: `src/TransBrain.Api/Program.cs`
- Modify: `tests/TransBrain.Api.IntegrationTests/TestAuthHandler.cs`
- Test: `tests/TransBrain.Api.IntegrationTests/TourEndpointsTests.cs`

**Interfaces:**
- Consumes: `ISender`, the seven tour slices, `ResultExtensions`, `Policies`.
- Produces: `POST/GET /api/tours`, `GET /api/tours/{id}`, `POST /api/tours/{id}/orders`, `DELETE /api/tours/{id}/orders/{orderId}`, `POST /api/tours/{id}/start`, `POST /api/tours/{id}/complete`.

**Policies:** planning is `DispatchWrite` (admin, disponent). Starting and completing is `TourStatusWrite` (admin, disponent, fahrer) — the "only their own" half of that policy is the handler's job, because a policy cannot see which tour is being addressed. Reads are `Read`.

- [ ] **Step 1: Implement `ICurrentUser` for the Api**

```csharp
using System.Security.Claims;
using TransBrain.Application.Abstractions;

namespace TransBrain.Api.Authorization;

/// <summary>
/// Reads the caller out of the current request. Registered scoped, because HttpContext is.
/// </summary>
/// <remarks>
/// "sub" is Keycloak's subject claim and is what a driver's ExternalUserId stores. ASP.NET maps
/// "sub" onto ClaimTypes.NameIdentifier by default, so both are read — relying on only one of
/// them breaks the moment the inbound-claim mapping changes.
/// </remarks>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId =>
        accessor.HttpContext?.User.FindFirstValue("sub")
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
}
```

Register in `Program.cs`, next to `AddApplication()`:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
```

with `using TransBrain.Application.Abstractions;` present.

- [ ] **Step 2: Let the test auth handler carry a subject**

`TestAuthHandler` currently supplies roles from a header and hard-codes the subject as `"test-user"`. The driver-scoping tests need to act as a *specific* driver, so make the subject settable. It builds a `Claim[]` with a collection expression, so add the subject as an element rather than calling `.Add` on a list:

```csharp
    public const string SchemeName = "TestScheme";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubjectHeader = "X-Test-Subject";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues roles))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Defaults to the old constant so every existing test keeps its current identity; only a
        // test that sets the header gets a different subject.
        string subject = Request.Headers.TryGetValue(
                             SubjectHeader, out Microsoft.Extensions.Primitives.StringValues header)
                         && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : "test-user";

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject),
            // The Api's HttpContextCurrentUser reads "sub" first and only falls back to
            // NameIdentifier, so the driver-scoping path is exercised through the same claim
            // Keycloak actually issues rather than through the fallback.
            new("sub", subject),
            .. roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role))
        ];

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
```

Then add to `TransBrainApiFactory`. Note the name: an overload called `CreateClientAs(string, params string[])` would be ambiguous with the existing `CreateClientAs(params string[])` at any single-string call site, so it gets a distinct name instead:

```csharp
    public HttpClient CreateClientAsSubject(string subject, params string[] roles)
    {
        HttpClient client = CreateClientAs(roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject);
        return client;
    }
```

- [ ] **Step 3: Write the failing integration tests**

`TourEndpointsTests` — cover:
- `PostTour_WithoutToken_ReturnsUnauthorized`
- `PostTour_AsViewer_ReturnsForbidden`
- `PostTour_AsDisponent_ReturnsCreatedAndIsListable`
- `PostTour_SameVehicleAndDateTwice_ReturnsConflict` — the double booking, over HTTP
- `GetTourById_UnknownId_ReturnsNotFound`
- `PostOrders_AssignsAnOrderAndReportsCapacity` — assert the body's `totalWeightKg` and `vehiclePayloadKg`
- `PostOrders_OrderTooHeavyForTheVehicle_ReturnsConflict`
- `DeleteOrder_RemovesTheStopsAndReturnsTheOrderToDraft` — then `GET /api/orders/{id}` and assert `"Draft"`
- `PostStart_AsDisponent_MovesTheTourAndItsOrders` — assert the tour body is `InProgress` and `GET /api/orders/{id}` is `InTransit`
- `PostComplete_AfterStart_DeliversTheOrders`
- `PostStart_AsAForeignDriver_ReturnsForbidden` — a driver whose `externalUserId` is not the tour's; `CreateClientAsSubject("someone-else", "fahrer")`
- `PostStart_AsTheAssignedDriver_Succeeds` — create the driver with `externalUserId = "driver-sub"`, call with `CreateClientAsSubject("driver-sub", "fahrer")`
- `GetTours_AsADriver_ListsOnlyTheirOwn`

Build fixtures through the API (`POST /api/vehicles`, `/api/drivers`, `/api/orders` as `admin`), the way `OrderEndpointsTests` does. Use tour dates far in the future (2098, 2099) and distinct per test, so the unique index cannot make two unrelated tests collide.

- [ ] **Step 4: Run them to verify they fail**

Run: `dotnet test tests/TransBrain.Api.IntegrationTests --filter FullyQualifiedName~TourEndpointsTests`
Expected: 404s — the routes do not exist.

- [ ] **Step 5: Implement the endpoints**

```csharp
using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.AssignOrder;
using TransBrain.Application.Features.Tours.CompleteTour;
using TransBrain.Application.Features.Tours.CreateTour;
using TransBrain.Application.Features.Tours.GetTourById;
using TransBrain.Application.Features.Tours.ListTours;
using TransBrain.Application.Features.Tours.RemoveOrder;
using TransBrain.Application.Features.Tours.StartTour;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// Planning is DispatchWrite (admin, disponent). Starting and completing is TourStatusWrite,
/// which additionally admits fahrer — but only for their own tours, and that half of spec §9's
/// rule cannot live in a policy: a policy sees the request, not which tour it addresses. It is
/// enforced in the handlers, via TourAccess.
/// </remarks>
public sealed class TourEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tours").WithTags("Tours");

        group.MapPost("/", async (CreateTourCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(tour => Results.Created($"/api/tours/{tour.Id}", tour));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CreateTour")
            .Produces<TourResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                DateOnly? tourDate = null,
                Guid? vehicleId = null,
                Guid? driverId = null) =>
            {
                Result<PagedResult<TourResponse>> result = await sender.Send(
                    new ListToursQuery(page, pageSize, tourDate, vehicleId, driverId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListTours")
            .Produces<PagedResult<TourResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new GetTourByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetTourById")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/orders", async (
                Guid id, AssignOrderRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(
                    new AssignOrderCommand(id, request.TransportOrderId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("AssignOrderToTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE, unlike cancelling an order: a stop really is removed, and the order goes back
        // to Draft as if it had never been planned. Nothing is archived, so nothing is lost.
        group.MapDelete("/{id:guid}/orders/{orderId:guid}", async (
                Guid id, Guid orderId, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new RemoveOrderCommand(id, orderId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("RemoveOrderFromTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/start", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new StartTourCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.TourStatusWrite)
            .WithName("StartTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new CompleteTourCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.TourStatusWrite)
            .WithName("CompleteTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

/// <summary>Body of an order assignment. The tour id comes from the route, not the payload.</summary>
public sealed record AssignOrderRequest(Guid TransportOrderId);
```

- [ ] **Step 6: Run the whole suite and commit**

```bash
dotnet build TransBrain.slnx
dotnet test TransBrain.slnx
git add src/TransBrain.Api tests/TransBrain.Api.IntegrationTests
git commit -m "feat(api): add tour endpoints with driver-scoped status transitions"
```

---

### Task 8: Angular tour screens

**Files:**
- Create: `src/TransBrain.Web/src/app/tours/tour.service.ts`, `tour-list.component.ts`, `tour-form.component.ts`, `tour-detail.component.ts`
- Modify: `src/TransBrain.Web/src/app/app.routes.ts`
- Test: `src/TransBrain.Web/e2e/tours.spec.ts`

**Templates to open:** `src/TransBrain.Web/src/app/orders/order-list.component.ts`, `order-form.component.ts`, `order.service.ts`, and `e2e/orders.spec.ts`. The tour screens are the same shape plus a detail page.

Everything the order screens learned applies unchanged, and all of it cost a fix round to discover:

- **Render field errors, do not merely map them.** `setErrors` without a `<mat-error>` means the message never reaches the user.
- **The load path needs its own failure message**, distinct from the save path.
- **A 403 from the authorization middleware carries no ProblemDetails body**, so the error helper needs an action-specific fallback per call site. This matters more here than anywhere else: a driver acting on someone else's tour gets a 403 that *does* carry a body (`Tour.NotYours` from the handler), while a viewer gets a bodyless one from the middleware. Both must read sensibly.
- **Bind the ProblemDetails `errors` dictionary** — it is field-keyed.
- **Playwright targets `#username` and `#password`**; `getByLabel('Password')` matches two elements.
- **Login always returns to `/`** — authenticate first, then navigate.
- **`workers: 1` stays.**
- **A form opened by direct navigation must still carry a token.** `angular-auth-oidc-client` only hydrates its stored session when `checkAuth()` runs. `OrderFormComponent` pipes its load and save through a shared `checkAuth()` for exactly this reason — copy that `session` field into both `TourFormComponent` and `TourDetailComponent`, and pin it with a direct-navigation e2e case. Without it, a bookmarked `/tours/{id}` answers 401 to a signed-in dispatcher.

Three things are specific to tours:

- **The detail page is where the work happens.** The form only picks a date, a vehicle and a driver; assigning orders, removing them, and starting/completing the tour all live on `/tours/{id}`.
- **Show the capacity, because the API already sends it.** Render `totalWeightKg` / `vehiclePayloadKg` and `totalLoadMeters` / `vehicleLoadMeters` as text plus a `<progress>`; a dispatcher choosing the next order needs to see the headroom before the server refuses.
- **The assignable-order picker lists only `Draft` orders** — `GET /api/orders?status=Draft`. Any other status will be refused by the domain, and offering a choice the server will reject is a worse experience than not offering it.

- [ ] **Step 1: Write the API client**

`tour.service.ts` exports `TourStop`, `Tour`, `PagedResult<T>` and a `TourService` with `list(filters)`, `getById(id)`, `create(request)`, `assignOrder(tourId, transportOrderId)`, `removeOrder(tourId, orderId)`, `start(id)`, `complete(id)`. Build the query string with `HttpParams`, appending only the filters that are set — an omitted `tourDate` must not become the string `"null"`, which the API would reject with a 400.

- [ ] **Step 2: Build the list and the form**

List columns: Date, Vehicle (`vehicleLicensePlate`), Driver (`driverName`), Stops (`stops.length`), Status, Actions (`Open`). Filters: a date input, a vehicle select and a driver select, both populated from `VehicleService.list()` / `DriverService.list()`. `data-testid`s: `tour-add`, `tour-date-filter`, `tour-vehicle-filter`, `tour-driver-filter`, `tour-date`, `tour-vehicle`, `tour-driver`, `tour-status`, `tour-open`, `tour-list-error`, `tour-action-error`.

Form (`/tours/new`): a required `datetime`-free date input plus vehicle and driver selects. `data-testid`s: `tour-tourDate`, `tour-vehicleId`, `tour-driverId`, `tour-save`, `tour-cancel`, `tour-form-error`, and `<mat-error>` children keyed `tour-<field>-error`. On success navigate to `/tours/{id}` — the detail page, not the list, because the dispatcher's next action is always to assign orders.

- [ ] **Step 3: Build the detail page with the assignment and status actions**

`/tours/{id}` shows: the header (date, plate, driver, status), the capacity readouts, the ordered stop table (`tour-stop-sequence`, `tour-stop-order`, `tour-stop-type`), a `Draft`-order select plus an `Assign` button (`tour-assign-select`, `tour-assign`), a `Remove` button per assigned order (`tour-remove`), and `Start` / `Complete` buttons (`tour-start`, `tour-complete`). Errors from any action go to `tour-action-error` — a failed assignment must not blank the page the way a failed load does.

Hide `Start` unless the status is `Planned` and `Complete` unless it is `InProgress`; a button that can only ever 409 is noise. Keep `Assign`/`Remove` visible but let the server refuse, matching how the order screens treat `Cancel` — the refusal message teaches the rule.

- [ ] **Step 4: Register the routes**

In `app.routes.ts`, after the order routes and with `tours/new` **before** `tours/:id`:

```typescript
    { path: 'tours', loadComponent: loadTourList },
    { path: 'tours/new', loadComponent: loadTourForm },
    { path: 'tours/:id', loadComponent: loadTourDetail },
```

- [ ] **Step 5: Write `tours.spec.ts`**

Log in as `dispo.user`. Cases:
1. `dispatcher_planATourAssignAnOrderAndRunIt_throughTheUi` — create a vehicle and a driver through their screens (or reuse existing rows), create an order, create a tour, assign the order, assert the capacity readout and the two stops, start it, assert the status is `InProgress`, complete it, assert `Completed`, then open `/orders` and assert that order shows `Delivered`.
2. `removingAnOrderFromATour_returnsItToDraft` — assign, remove, assert the stops are gone and the order is `Draft` again on the orders list.
3. `doubleBookingAVehicle_showsTheConflict` — create a second tour for the same vehicle and date; assert `tour-form-error` contains `409`.
4. `blankRequiredFields_showVisibleFieldErrorsOnSave` — submit the empty form, assert the messages are **visible**. After it passes, temporarily delete one `<mat-error>`, confirm this test goes red, restore it, and record both runs in your report.
5. `directNavigationToTheTourDetail_canStillAssign` — `page.goto('/tours/{id}')` straight after login and assign an order; this is the 401 regression the order form already carries.

- [ ] **Step 6: Verify**

`npm run build`, then `npm run e2e` against a stack started with `dotnet run --project src/TransBrain.AppHost`. Every spec in the suite must pass, not only the new file.

- [ ] **Step 7: Commit**

```bash
git add src/TransBrain.Web
git commit -m "feat(web): add tour planning screens"
```

---

### Task 9: Vue tour screens

**Files:**
- Create: `src/TransBrain.VueWeb/src/api/tours.ts`, `src/views/TourList.vue`, `TourForm.vue`, `TourDetail.vue`
- Modify: `src/TransBrain.VueWeb/src/main.ts`
- Test: `src/TransBrain.VueWeb/e2e/tours.spec.ts`

**Templates to open:** the Angular tour screens from Task 8, plus `src/TransBrain.VueWeb/src/views/OrderList.vue`, `OrderForm.vue` for how this codebase writes Vue.

The two frontends must behave equivalently for a user — the same fields, the same messages, the same empty and error states. They differ only in framework idiom.

Everything listed in Task 8 applies. Four additional points, all learned the hard way in Phases 2 and 3:

- **Vue forms need `novalidate`.** Angular's `[formGroup]` adds it automatically; without it a native input constraint silently blocks submission and the submit handler never runs.
- **Do NOT use `<v-text-field>` or `<v-select>` for anything Playwright must drive.** A `data-testid` on a Vuetify control lands on its wrapper `<div>`, and `fill()`/`selectOption()` then fail. Use plain `<input>` and `<select>` with labels, as `OrderList.vue` and `OrderForm.vue` already do.
- **Do NOT change the `/callback` route or its comments.** That divergence from Angular is deliberate.
- **Match Angular's non-admin behaviour** rather than building role decoding in only one frontend.

- [ ] **Step 1: Write the API client** — mirror `src/api/orders.ts`, including the axios interceptor that reads the token from `userManager.getUser()` per request.
- [ ] **Step 2: Build the list, the form and the detail page** — same `data-testid`s as Task 8, so the two suites read alike.
- [ ] **Step 3: Register the routes** in `main.ts`, `'/tours/new'` before `'/tours/:id'`.
- [ ] **Step 4: Write `tours.spec.ts`** mirroring Task 8's five cases.
- [ ] **Step 5: Verify** with `npm run build` (which runs `vue-tsc`) and `npm run e2e`; the whole suite, not only the new file.
- [ ] **Step 6: Commit**

```bash
git add src/TransBrain.VueWeb
git commit -m "feat(vueweb): add tour planning screens"
```

---

### Task 10: Phase 5 — documentation and screenshots

**Files:**
- Modify: `CHANGELOG.md`, `README.md`, `docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`, `AGENTS.md`
- Create: `docs/img/web/*.png`, `docs/img/vueweb/*.png`

Spec §12 gives Phase 5 as "CHANGELOG, Bedienhandbücher inkl. Screenshots, korrigierte AGENTS.md", accepted when "Doku deckt den Stand vollständig ab". Two of those are already partly done, so this task is smaller than the spec implies — **verify each claim below before acting on it rather than trusting this paragraph.**

- [ ] **Step 1: Check what §13's AGENTS.md correction still needs**

Run:

```bash
grep -in "fewobrain\|guestinfo\|blazor\|drei Frontends\|three frontends" AGENTS.md
```

Spec §13 lists leftovers from a predecessor project (`FeWoBrain.Web`, `FeWoBrain.BlazorWeb`, `FeWoBrain.Api`, `GuestInfo`, "alle drei Frontends"). As of the start of this phase that grep returns nothing — the file already names `TransBrain.Web`, `TransBrain.VueWeb` and `TransBrain.Api`, and the guide filenames already match §13's required names. **If the grep is still empty, change nothing and record in your report that §13 was already satisfied.** Do not invent edits to make a checklist item feel done.

Then read AGENTS.md once for genuine drift against the finished system — for example, whether its Tests section should now mention the tour e2e specs.

- [ ] **Step 2: Capture screenshots for every screen, in both frontends**

The guides currently illustrate only the order screens. Bring the rest up to the same standard.

Start a stack with a **fresh database** (`dotnet run --project src/TransBrain.AppHost`; the AppHost deliberately keeps no data volume, so every restart starts empty). Then drive the screens with a throwaway Playwright spec that logs in, seeds one clean-looking row per aggregate, and screenshots each page — leftovers from the e2e suite make a guide screenshot unreadable, which is why the fresh database matters.

Capture, per frontend, into `docs/img/<web|vueweb>/`:

| File | Screen |
|---|---|
| `fahrzeugliste.png` | `/vehicles` |
| `fahrzeugformular.png` | `/vehicles/new`, filled |
| `fahrerliste.png` | `/drivers` |
| `fahrerformular.png` | `/drivers/new`, filled |
| `auftragsliste.png` | `/orders` — **re-capture**, so all images share one dataset |
| `auftragsformular.png` | `/orders/new`, filled |
| `tourenliste.png` | `/tours` |
| `tourenformular.png` | `/tours/new`, filled |
| `tourendetail.png` | `/tours/{id}` with one assigned order, capacity visible |

Use `page.setViewportSize({ width: 1280, height: 1000 })` and `fullPage: true`, matching the existing order screenshots. **Delete the throwaway spec afterwards** — it must not land in the committed e2e suite, and `test-results/` must not be committed either.

- [ ] **Step 3: Extend both operator guides**

In German, in both files, add `## Tourenliste`, `## Tourenformular`, `## Tourendetail` and `## Tourstatus: welche Schritte abgelehnt werden`, mirroring the structure the order sections already use. Then embed every screenshot from Step 2 in its matching section.

Cover, and do not stop at the happy path — the refusals are what a dispatcher actually hits:

- Which vehicle/driver combinations are refused when planning: a vehicle in the workshop, an absent driver, a licence that expires before the tour date, and a vehicle or driver already booked that day (with the exact message and `409`).
- That capacity is checked per assignment, that the screen shows the headroom, and what an over-capacity assignment looks like.
- That removing an order puts it back to `Draft` and it becomes assignable again.
- That starting a tour moves every assigned order to `InTransit`, and completing it to `Delivered` — so a dispatcher understands why the order list changes without anyone touching it.
- **That a driver (`fahrer`) can start and complete only their own tours, and sees only their own in the list.** Include what the refusal looks like. This is the first place in the product where two signed-in users see different data, and a guide that omits it will generate support questions.

Extend the roles table in both guides with a Touren column: `admin` full, `disponent` full, `fahrer` only own tours (start/complete), `viewer` read only.

- [ ] **Step 4: Update the CHANGELOG and the README**

`CHANGELOG.md`, under `[Unreleased]`: the `Tour` aggregate, its capacity/licence/double-booking invariants, the seven use cases, the order status machine now being driven end to end, `TransportOrder.ReturnToDraft`, `ICurrentUser` and the driver-scoped authorization, and the tour screens in both frontends.

`README.md`: add the tour endpoints to the endpoint tables with their policies, note that starting and completing use `TourStatusWrite` and are additionally narrowed to the driver's own tours in the handler, that removing an order from a tour is a `DELETE` (unlike cancelling an order, and say why the two differ), and that tours — like orders — are deliberately not cached.

- [ ] **Step 5: Verify every documented route exists**

The realm's `transbrain-spa` client has `directAccessGrantsEnabled: false`, so a password-grant token is not available and the endpoints cannot simply be curled. Verify against the running API's OpenAPI document instead:

```bash
curl -s http://localhost:<api-port>/openapi/v1.json | python -c "
import sys,json
d=json.load(sys.stdin)
for path in sorted(d['paths']):
    if 'tour' in path.lower():
        for verb in d['paths'][path]:
            op=d['paths'][path][verb]
            print(verb.upper(), path, op.get('operationId'), sorted(op.get('responses',{})))
"
```

Find the API port from the Aspire dashboard or the AppHost log. Confirm every route, verb, query parameter and status code you documented matches, and that nothing documented is missing.

- [ ] **Step 6: Full verification and commit**

```bash
dotnet build TransBrain.slnx    # 0 warnings, 0 errors
dotnet test TransBrain.slnx     # all green
(cd src/TransBrain.Web && npm run build && npm run e2e)
(cd src/TransBrain.VueWeb && npm run build && npm run e2e)
```

Also re-measure the Application coverage gate, because this phase added a lot of Application code:

```bash
dotnet test tests/TransBrain.Application.Tests --collect:"XPlat Code Coverage" \
  --results-directory ./coverage/application \
  --settings tests/TransBrain.Application.Tests/coverage.runsettings
```

Read the root `line-rate` from the cobertura report; it must be at least `0.80`. If it has fallen below, add the missing handler tests rather than lowering the gate. Delete `./coverage` afterwards.

```bash
git add CHANGELOG.md README.md AGENTS.md docs/
git commit -m "docs: document tours and complete the operator guides with screenshots"
```

---

## Out of scope for this plan

- Route optimisation, telematics/GPS tracking, freight billing, multi-tenancy and a driver mobile app — `ToDo.txt` lists these as future work, and none is in the spec's phase table.
- A drag-and-drop planning board. The list/form/detail shape was chosen deliberately for consistency with the three existing aggregates; revisit only if dispatchers ask for it.
- Reassigning a tour's vehicle or driver after creation. §6.4 lists no `UpdateTour` slice; a mis-planned tour is deleted and re-planned. Note that §6.4 also lists no tour deletion, so in practice a wrong tour is emptied of its orders and left. **Flag this to the user rather than inventing a `DeleteTour` slice** — it is a real gap in the spec, but filling it is a product decision, not an implementation detail.
