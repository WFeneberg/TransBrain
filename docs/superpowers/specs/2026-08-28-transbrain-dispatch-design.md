# TransBrain — Dispositionssystem: Design

**Datum:** 2026-08-28
**Status:** Genehmigt
**Umfang:** Erste Anwendung — Stammdaten, Transportaufträge, Tourenplanung; API plus zwei Frontends

## 1. Zweck

TransBrain ist die Dispositionssoftware einer Spedition. Die erste Anwendung deckt die
Kernkette ab: Fahrzeuge und Fahrer verwalten, Transportaufträge erfassen, Aufträge zu
Touren bündeln und deren Ausführung verfolgen.

Nicht Teil dieser Spec: Telematik/GPS-Tracking, Frachtabrechnung, Mandantenfähigkeit,
Fahrer-Mobile-App, Routenoptimierung.

## 2. Vorgehen

Walking Skeleton zuerst. Das Risiko dieses Projekts liegt in der Verkabelung
(Keycloak-Realm, OIDC-Flow durch zwei SPAs, Aspire-Orchestrierung), nicht in der
Fachlichkeit — drei CRUD-nahe Domänen sind handwerklich. Deshalb wird ein dünner,
vollständiger Durchstich (Login → API → Postgres → Liste in beiden Frontends) bewiesen,
bevor Fachlichkeit in die Breite geht.

## 3. Technologie

Alle Versionen gegen nuget.org bzw. npm verifiziert am 2026-08-28.

| Baustein | Version | Anmerkung |
|---|---|---|
| .NET SDK | 10.0.400 | lokal vorhanden |
| Aspire CLI | 13.4.6 | lokal vorhanden |
| `Aspire.Hosting.PostgreSQL` | 13.5.3 | stabil |
| `Aspire.Hosting.Redis` | 13.5.3 | stabil |
| `Aspire.Hosting.Keycloak` | 13.5.3-preview.1.26425.3 | Preview, bewusst akzeptiert, Version gepinnt |
| `Aspire.Keycloak.Authentication` | 13.5.3-preview.1.26425.3 | Preview, bewusst akzeptiert, Version gepinnt |
| `Microsoft.EntityFrameworkCore` | 10.0.11 | stabil |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | stabil |
| `FluentValidation` | 12.1.1 | stabil |
| `Testcontainers.PostgreSql` | 4.14.0 | stabil |
| Angular CLI / `@angular/material` | 22.1.6 / 22.1.4 | stabil |
| Vue / Vuetify | 3.5.42 / 4.1.12 | stabil |
| Node.js | 26.7.0 | erfüllt Mindestanforderung >= 26.4.0 |

Alle NuGet-Versionen werden zentral in `Directory.Packages.props` gepinnt
(Central Package Management).

## 4. Solution-Layout

```
TransBrain.slnx
src/
  TransBrain.Domain/           Entities, Value Objects, DomainErrors — keine Abhängigkeiten
  TransBrain.Application/      CQRS-Mediator, Vertical Slices, Abstraktionen
  TransBrain.Infrastructure/   EF Core + Npgsql, Redis-Cache, Migrations
  TransBrain.Api/              Minimal APIs, Endpoint-Gruppen, Auth, OpenAPI
  TransBrain.AppHost/          Aspire-Orchestrierung
  TransBrain.ServiceDefaults/  OpenTelemetry, Health Checks, Resilience
  TransBrain.Web/              Angular 22 + Material (Port 4200)
  TransBrain.VueWeb/           Vue 3 + Vuetify (Port 4300)
tests/
  TransBrain.Domain.Tests/
  TransBrain.Application.Tests/
  TransBrain.Api.IntegrationTests/
```

Abhängigkeitsrichtung strikt einwärts: `Api → Infrastructure → Application → Domain`.
Die Api referenziert Infrastructure ausschließlich in der DI-Registrierung
(`AddInfrastructure(...)`); Endpoints kennen nur Application-Typen.

## 5. Domänenmodell

Vier Aggregate. Value Objects durchgängig als `record`, Entities mit privatem
Konstruktor und statischer `Create`-Factory, die `Result<T>` liefert.

### 5.1 Value Objects

| VO | Felder | Validierung |
|---|---|---|
| `LicensePlate` | `Value` | nicht leer, normalisiert (Großbuchstaben, Bindestrich-Format), max. 15 Zeichen |
| `Address` | `Name`, `Street`, `PostalCode`, `City`, `Country` | alle Pflichtfelder nicht leer; `Country` ISO-3166-Alpha-2 |
| `Cargo` | `Description`, `WeightKg`, `LoadMeters` | Beschreibung nicht leer; Gewicht > 0; Lademeter > 0 |
| `TimeWindow` | `From`, `To` | `From < To`; beide als `DateTimeOffset` (UTC in der DB) |

### 5.2 Vehicle

Felder: `Id`, `LicensePlate`, `Type` (`Tractor` / `RigidTruck` / `Van`), `PayloadKg`,
`LoadMeters`, `NextInspectionDue` (`DateOnly`), `Status` (`Available` / `InWorkshop` /
`Decommissioned`).

Invarianten: Kennzeichen ist eindeutig; `PayloadKg` und `LoadMeters` > 0;
ein `Decommissioned`-Fahrzeug ist keiner Tour zuordenbar.

### 5.3 Driver

Felder: `Id`, `FirstName`, `LastName`, `LicenseClasses` (Menge aus `B` / `C1` / `C` /
`CE`), `LicenseValidUntil` (`DateOnly`), `Status` (`Available` / `Absent` / `Inactive`),
`ExternalUserId` (Keycloak-`sub`, optional).

Invarianten: mindestens eine Führerscheinklasse; ein Fahrer ist nur zuordenbar, wenn
`Status == Available` und `LicenseValidUntil >= Tourdatum`.

### 5.4 TransportOrder

Felder: `Id`, `OrderNumber` (generiert, Format `TB-{yyyy}-{laufende Nummer:D5}`),
`Consignor` (`Address`), `Consignee` (`Address`), `Cargo`, `PickupWindow` (`TimeWindow`),
`DeliveryWindow` (`TimeWindow`), `Status`, `CreatedAt`.

Statusübergänge — jeder andere Übergang liefert `Error` vom Typ `Conflict`:

```
Draft ──(Tourzuordnung)──> Planned ──(Tourstart)──> InTransit ──(Zustellung)──> Delivered
  │                           │
  └──────────(Storno)─────────┴──> Cancelled
```

Invarianten: `PickupWindow.To <= DeliveryWindow.From`; ein Auftrag ist zu höchstens einer
aktiven Tour zugeordnet; `Delivered` ist final; ab `InTransit` ist kein Storno mehr möglich.

### 5.5 Tour

Felder: `Id`, `TourDate` (`DateOnly`), `VehicleId`, `DriverId`, `Status` (`Planned` /
`InProgress` / `Completed`), `Stops` (geordnete Liste `TourStop`).

`TourStop`: `Sequence`, `TransportOrderId`, `StopType` (`Pickup` / `Delivery`).

Invarianten:

- Summe `Cargo.WeightKg` der zugeordneten Aufträge <= `Vehicle.PayloadKg`
- Summe `Cargo.LoadMeters` <= `Vehicle.LoadMeters`
- Fahrzeug und Fahrer sind pro `TourDate` höchstens einer Tour zugeordnet
- Fahrer erfüllt die Führerscheinbedingung aus 5.3 am `TourDate`
- Fahrzeug hat `Status == Available`
- Je Auftrag existieren genau ein `Pickup`- und ein `Delivery`-Stop; `Pickup.Sequence < Delivery.Sequence`
- Eine Tour im Status `InProgress` oder `Completed` nimmt keine neuen Stops auf

Diese Invarianten leben im Domain-Layer und sind der Gegenstand der Domain-Unit-Tests.

## 6. Application-Layer

### 6.1 Mediator

Hand-gerollt unter `Common/Messaging/`, kein MediatR:

```csharp
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}
public interface ISender
{
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct);
    Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct);
}
```

Handler-Auflösung über DI (Assembly-Scan bei der Registrierung). Zwei Pipeline-Verhalten,
in dieser Reihenfolge: **Logging** (Slice-Name, Dauer, Ergebnisart), dann **Validation**
(FluentValidation; Fehler werden zu `Result.Failure` mit `ErrorType.Validation`, keine
Exception).

### 6.2 Result Pattern

```csharp
public enum ErrorType { Validation, NotFound, Conflict, Forbidden }
public sealed record Error(string Code, string Message, ErrorType Type);
public readonly record struct Result<T>;  // IsSuccess, Value, Error
```

Fachliche Fehler werfen nie. Exceptions bleiben echten Ausnahmen vorbehalten
(DB nicht erreichbar, Programmierfehler) und landen im globalen Exception-Handler als 500.

### 6.3 Slice-Struktur

Ein Ordner je Anwendungsfall unter `Features/<Aggregat>/<Aktion>/` mit Command bzw. Query,
Handler, Validator und Response-Record nebeneinander. Beispiel:

```
Features/Vehicles/CreateVehicle/
  CreateVehicleCommand.cs
  CreateVehicleCommandHandler.cs
  CreateVehicleCommandValidator.cs
  VehicleResponse.cs
```

### 6.4 Anwendungsfälle

| Aggregat | Slices |
|---|---|
| Vehicles | Create, Update, Delete, GetById, List (Paging + Filter Status/Typ) |
| Drivers | Create, Update, Delete, GetById, List (Paging + Filter Status) |
| TransportOrders | Create, Update (nur `Draft`), Cancel, GetById, List (Paging + Filter Status/Zeitraum) |
| Tours | Create, AssignOrder, RemoveOrder, Start, Complete, GetById, List (Filter Datum/Fahrzeug/Fahrer) |

## 7. Infrastructure

EF Core 10 mit Npgsql. Value Objects über Complex Types bzw. Owned Entities abgebildet,
`LicensePlate` als Konverter auf `text` mit Unique-Index. Zeitstempel durchgängig
`timestamptz` in UTC. Migrations im Infrastructure-Projekt; sie werden beim Start der Api
im Entwicklungsmodus angewendet (kein `EnsureCreated`).

Redis cacht die Stammdatenlisten (`Vehicles.List`, `Drivers.List`) hinter einer
`ICacheService`-Abstraktion mit expliziter Invalidierung bei jedem Schreibvorgang auf dem
jeweiligen Aggregat. Aufträge und Touren werden nicht gecacht — zu volatil, der
Invalidierungsaufwand überstiege den Nutzen.

## 8. Api

Minimal APIs, keine Controller. Ein `IEndpointGroup` je Aggregat, registriert per
Assembly-Scan, gemappt unter `/api/vehicles`, `/api/drivers`, `/api/orders`, `/api/tours`.

`Result<T>` wird zentral auf HTTP gemappt:

| `ErrorType` | HTTP |
|---|---|
| `Validation` | 400 + ProblemDetails (RFC 9457) mit Feldfehlern |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Forbidden` | 403 |

OpenAPI über `Microsoft.AspNetCore.OpenApi`, interaktive Doku über Scalar. CORS erlaubt
`http://localhost:4200` (Angular) und `http://localhost:4300` (Vue).

## 9. Authentifizierung und Autorisierung

Keycloak läuft als Aspire-Ressource. Der Realm `transbrain` liegt als
`realm-export.json` im Repository und wird beim Start importiert — dadurch ist die
Konfiguration reproduzierbar und ohne manuelle Klickarbeit.

Realm-Inhalt:

- Client `transbrain-api` — Bearer-only, Audience für die Api
- Client `transbrain-spa` — Public, Authorization Code + PKCE, Redirect-URIs für 4200 und 4300
- Realm-Rollen: `admin`, `disponent`, `fahrer`, `viewer`
- Je ein Testbenutzer pro Rolle mit dokumentiertem Kennwort (nur für lokale Entwicklung)

Die Api validiert JWT-Bearer-Token gegen den Realm und bildet Realm-Rollen auf benannte
Policies ab:

| Policy | Rechte | admin | disponent | fahrer | viewer |
|---|---|---|---|---|---|
| `MasterDataWrite` | Fahrzeuge/Fahrer anlegen, ändern, löschen | ja | nein | nein | nein |
| `DispatchWrite` | Aufträge und Touren anlegen, ändern, stornieren | ja | ja | nein | nein |
| `TourStatusWrite` | Tour starten/abschließen | ja | ja | nur eigene | nein |
| `Read` | Lesezugriff | ja | ja | nur eigene Touren | ja |

Die Einschränkung "nur eigene" für Fahrer wird im Handler geprüft: die Fahrer-Identität
kommt aus dem `sub`-Claim, der beim Anlegen eines Fahrers als `ExternalUserId` hinterlegt
wird. Passt der Claim nicht zum `DriverId` der Tour, liefert der Handler
`ErrorType.Forbidden`.

**Entschieden während der Umsetzung (Phase 2): nicht gefundene Routen antworten 401, nicht 404.**
Eine Fallback-Policy verlangt für jeden Endpoint einen authentifizierten Benutzer, damit ein
vergessenes `RequireAuthorization` fail-closed ist. ASP.NET wendet diese Policy auf jede Anfrage
ohne Endpoint-Metadaten an — also auch auf Routen, die auf nichts passen. Ein `MapFallback` mit
`AllowAnonymous` könnte das gewohnte 404 wiederherstellen; das wurde bewusst verworfen, weil ein
404 einem nicht authentifizierten Aufrufer verrät, welche Routen existieren. Ein einheitliches
401 verhindert dieses Abklopfen der API-Oberfläche. Der Preis: Ein Tippfehler in der URL
beantwortet sich mit 401. Ausgenommen bleiben `/health`, `/alive`, das OpenAPI-Dokument und die
Scalar-UI — Aspires Health-Probes tragen kein Token, und ein 401 dort würde die Ressource als
ungesund melden und den gesamten Stack hängen lassen.

Beide SPAs verwenden denselben Public Client mit Authorization Code + PKCE:
Angular über `angular-auth-oidc-client`, Vue über `oidc-client-ts`. Das Access Token wird
per HTTP-Interceptor an die Api gehängt.

## 10. Aspire-Orchestrierung

Der AppHost verdrahtet: Postgres (mit persistentem Volume und pgAdmin im
Entwicklungsmodus), Redis, Keycloak (mit Realm-Import), die Api (referenziert alle drei)
sowie beide Frontends als npm-Ressourcen mit weitergereichten Endpoint-URLs. `aspire run`
startet damit die vollständige Umgebung inklusive Dashboard und Telemetrie.

## 11. Tests

| Ebene | Werkzeug | Inhalt |
|---|---|---|
| Domain | xUnit + FluentAssertions | Invarianten aus Abschnitt 5, insbesondere Kapazität, Führerscheingültigkeit, Statusübergänge |
| Application | xUnit + FluentAssertions, In-Memory-Fakes der Repositories | ein Testfall je Handler für Erfolg und je Fehlerpfad; Ziel >= 80 % Zeilenabdeckung |
| Api | `WebApplicationFactory` + Testcontainers-Postgres | echte Migrationen, echte Endpunkte; Auth über ein Test-Authentication-Scheme statt echtem Keycloak, damit Tests ohne Container-Keycloak laufen |
| E2E | Playwright | je Frontend unter `<projekt>/e2e/*.spec.ts`, gestartet über `npm run e2e` |

Benennung durchgängig `Method_Scenario_ExpectedResult`.

CI (`.github/workflows/ci.yml`): Build, Unit- und Integrationstests, Coverage-Schwelle für
den Application-Layer, Lint und Build beider Frontends, Playwright-E2E.

## 12. Umsetzungsphasen

| Phase | Inhalt | Abnahmekriterium |
|---|---|---|
| 0 | Solution, `Directory.Packages.props`, ServiceDefaults, AppHost mit Postgres/Redis/Keycloak, CI-Workflow | `aspire run` startet alle Ressourcen, Health Checks grün |
| 1 | Walking Skeleton: Slice `Vehicles` (Create, List) durch alle Schichten; beide SPA-Shells mit OIDC-Login und Fahrzeugliste | Login → API → Postgres → Liste sichtbar in **beiden** Frontends |
| 2 | Drivers vollständig, Vehicles-Vollausbau (Update, Delete, Filter, Cache) | Stammdatenpflege in beiden UIs, Tests grün, Coverage-Ziel erreicht |
| 3 | TransportOrders inklusive Statusübergängen | Auftrag anlegen, suchen, stornieren in beiden UIs |
| 4 | Tours/Disposition inklusive Kapazitäts- und Verfügbarkeitsregeln | Tour planen, Aufträge zuordnen, Status melden |
| 5 | Dokumentation: CHANGELOG, Bedienhandbücher inkl. Screenshots, korrigierte AGENTS.md | Doku deckt den Stand vollständig ab |

Jede Phase endet mit lauffähigem Stand und grünen Tests.

## 13. Korrektur an AGENTS.md

AGENTS.md verweist an mehreren Stellen auf `FeWoBrain.Web`, `FeWoBrain.BlazorWeb`,
`FeWoBrain.Api` und `GuestInfo` sowie auf "alle drei Frontends" — Rückstände aus einem
Vorgängerprojekt. Korrekt für TransBrain sind zwei Frontends (`TransBrain.Web`,
`TransBrain.VueWeb`) gegen `TransBrain.Api`. AGENTS.md wird in Phase 5 entsprechend
berichtigt, ebenso die Namen der Bedienhandbücher
(`docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`).

## 14. Offene Punkte

Keine. Die Preview-Abhängigkeit auf die Keycloak-Aspire-Integration ist bekannt und
bewusst akzeptiert; die Version ist gepinnt.
