# TransBrain — Rollenbasierte Startseite: Design

**Datum:** 2026-09-01
**Status:** Genehmigt
**Umfang:** Rollen-Schicht, Startseite, App-Shell und Guards in beiden Frontends
(`TransBrain.Web`, `TransBrain.VueWeb`); serverseitig nur eine Ergänzung am Keycloak-Realm

## 1. Zweck

Beide Frontends öffnen heute ohne Einstiegspunkt: Route `''` zeigt die Fahrzeugliste, es
gibt keine Navigation und keine Auswertung der Rollen aus dem Token. Der einzige
Auth-Zustand ist `isAuthenticated`, jeweils in jeder Liste einzeln ermittelt.

Diese Spec beschreibt eine Startseite, die jeder Rolle genau die Softwareteile zeigt, die
sie für ihre Arbeit braucht, und die dafür nötige Rollen-Infrastruktur. Die Rollen-Schicht
wird zugleich in der gesamten übrigen Oberfläche wirksam: Aktionen, die eine Rolle
serverseitig nicht ausführen darf, werden nicht mehr angeboten.

Nicht Teil dieser Spec: Rollenwechsel zur Laufzeit, eine 403-Seite, Lokalisierung,
Pagination auf den Arbeitslisten der Startseite, persistente Dashboard-Einstellungen,
neue fachliche API-Endpunkte.

## 2. Ausgangslage

| Befund | Fundstelle |
|---|---|
| Keine Navigations-Shell | `app.html` (nur `<router-outlet />`), `App.vue` (nur `<router-view />`) |
| Keine Rollen-Auswertung im Client | `vehicle-list.component.ts:95-99`, `VehicleList.vue:46-50` |
| `checkAuth()` / `auth.load()` in jeder Liste und jedem Formular einzeln | neun Angular-Komponenten, vier Vue-Views |
| Doppelte Route für die Fahrzeugliste, als Notlösung markiert | `app.routes.ts:20-23`, `main.ts:24-28` |
| Keine Abmeldung in beiden Apps | — |
| Realm deklariert keinen Rollen-Mapper | `transbrain-realm.json:43-55` (nur Audience-Mapper) |

Serverseitig ist das Rollenmodell dagegen vollständig und bleibt die maßgebliche Instanz
(`Program.cs:130-137`).

## 3. Rollen und Capabilities

Die Client-Tabelle spiegelt `Program.cs:134-137` und wird im Code als solche kommentiert.

| Capability | admin | disponent | fahrer | viewer | Server-Policy |
|---|:-:|:-:|:-:|:-:|---|
| `read` | ✓ | ✓ | ✓ | ✓ | `Policies.Read` |
| `masterData.write` | ✓ | | | | `Policies.MasterDataWrite` |
| `dispatch.write` | ✓ | ✓ | | | `Policies.DispatchWrite` |
| `tourStatus.write` | ✓ | ✓ | ✓ | | `Policies.TourStatusWrite` |

Regeln:

- Mehrere Rollen werden **vereinigt**, nicht ausgewählt.
- Eine unbekannte Rolle im Token wird ignoriert, nicht als Fehler behandelt.
- Ohne bekannte Rolle gilt keine Capability — fail closed, analog zur `SetFallbackPolicy`
  der API.
- Die Tabelle steuert ausschließlich **Aktionen**. Sichtbarkeit von Bereichen ist eine
  eigene Achse, siehe §6.

## 4. Herkunft der Rollen im Client

Der Realm deklariert keinen Rollen-Mapper, es greift also Keycloaks eingebauter
„realm roles"-Mapper aus dem Default-Client-Scope. Dieser schreibt `realm_access.roles`
per Voreinstellung nur ins **Access-Token**, nicht ins ID-Token. Die API liest die Rollen
genau dort (`Program.cs:100-115`); ein Client erreicht sie über `getUserData()` bzw.
`user.profile` deshalb nicht.

**Lösung:** In `src/TransBrain.AppHost/realms/transbrain-realm.json` erhält der Client
`transbrain-spa` einen zusätzlichen Protocol-Mapper:

```json
{
  "name": "realm-roles-in-id-token",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-usermodel-realm-role-mapper",
  "consentRequired": false,
  "config": {
    "multivalued": "true",
    "claim.name": "realm_access.roles",
    "jsonType.label": "String",
    "id.token.claim": "true",
    "access.token.claim": "true"
  }
}
```

Damit tragen ID- und Access-Token dasselbe Claim-Format, und kein Frontend muss ein
Access-Token selbst parsen — das ist per OIDC-Vertrag nicht für den Client bestimmt, und
`angular-auth-oidc-client` gibt es ohnehin nur als rohen String heraus. Der Realm wird bei
jedem Start neu importiert, die Änderung wirkt also ohne weiteres Zutun.

**Risiko, vor allem anderen zu verifizieren:** der Default-Scope bringt bereits einen
Mapper mit demselben Claim-Namen mit. An einem echten Token ist zu prüfen, dass
`realm_access.roles` weder doppelt noch zusätzlich verschachtelt ankommt — sowohl im
ID- als auch im Access-Token, denn Letzteres wertet die API aus und darf nicht brechen.

**Rückfallweg, falls die Prüfung scheitert:** clientseitiges Dekodieren des
Access-Token-Payloads (Base64url-Split des mittleren Segments, rund acht Zeilen, ohne
zusätzliche Bibliothek), gekapselt hinter derselben Schnittstelle aus §5. Die
Realm-Änderung wird dann zurückgenommen. Die Oberfläche ist von dieser Entscheidung nicht
betroffen.

## 5. Session-Schicht

Beide Frontends erhalten eine gleich geformte Schnittstelle, jeweils framework-idiomatisch
umgesetzt:

| Mitglied | Bedeutung |
|---|---|
| `isAuthenticated` | angemeldet und Token gültig |
| `displayName` | `name` bzw. `preferred_username` aus dem ID-Token |
| `roles` | die erkannten Realm-Rollen |
| `can(capability)` | Capability-Prüfung nach der Tabelle aus §3 |
| `login()` | Redirect zu Keycloak |
| `logout()` | RP-initiated Logout, neu in beiden Apps |

**Angular** — neuer `SessionService` in `src/app/auth/session.service.ts`, Zustand als
Signals. Er ruft `checkAuth()` **genau einmal** auf, aus der `App`-Komponente heraus, und
teilt das Ergebnis. `App` ist auch bei Route `''` gemountet, `checkAuth()` läuft also
weiterhin auf der konfigurierten `redirectUrl` — der in `app.routes.ts:14-19` beschriebene
Fallstrick bleibt vermieden.

**Vue** — Erweiterung von `src/stores/auth.ts` um `roles`, `displayName`, `can()` und
`logout()`. `load()` bleibt der Einstiegspunkt und wird künftig einmalig aus `App.vue`
aufgerufen statt in jeder View.

`logout()` ist funktional notwendig, nicht kosmetisch: ohne Abmeldung lässt sich eine
rollenbewusste Oberfläche im Alltag nicht prüfen, weil ein Wechsel von `admin.user` zu
`fahrer.user` sonst ein privates Browserfenster erfordert. Beide OIDC-Konfigurationen
setzen die `post_logout_redirect_uri` bereits, der Realm erlaubt sie
(`transbrain-realm.json:41`).

Als Folge entfallen `isAuthenticated`, `login()` und der eigene `checkAuth()`-Aufruf aus
allen Listen und Formularen. Das ist kein Nebenprojekt: für die Button-Sichtbarkeit wird
ohnehin jede dieser Dateien angefasst, und die vorhandene Duplikation würde sonst wachsen.

## 6. Startseite

Die Seite besteht aus **Blöcken** mit je eigener Sichtbarkeitsbedingung und eigener
Datenbeschaffung — nicht aus vier handgeschriebenen Rollen-Seiten.

Zwei Arten von Bedingungen, bewusst getrennt:

- **Aktionen** hängen an Capabilities (§3) und spiegeln damit die Server-Policy.
- **Relevanz** hängt an der Rolle. Ein `fahrer` darf die Fahrzeugstammdaten lesen
  (`Policies.Read` schließt ihn ein), braucht sie aber nicht auf seiner Startseite.

| Block | admin | disponent | fahrer | viewer |
|---|:-:|:-:|:-:|:-:|
| Kennzahl Vehicles: available / in workshop | ✓ | ✓ | | ✓ |
| Kennzahl Drivers: available | ✓ | ✓ | | ✓ |
| Kennzahl Orders: in Draft | ✓ | ✓ | | ✓ |
| Kennzahl Tours today | ✓ | ✓ | ✓ (eigene) | ✓ |
| Arbeitsliste „Orders awaiting a tour" | ✓ | ✓ | | |
| Arbeitsliste „My tours today" mit Start / Complete | | | ✓ | |
| Bereichskachel Vehicles | ✓ | ✓ | | ✓ |
| Bereichskachel Drivers | ✓ | ✓ | | ✓ |
| Bereichskachel Orders | ✓ | ✓ | | ✓ |
| Bereichskachel Tours | ✓ | ✓ | ✓ | ✓ |

Die Arbeitsliste „Orders awaiting a tour" hängt an `can('dispatch.write')`; der Admin
bekommt sie folglich mit. „My tours today" hängt dagegen an der Rolle `fahrer`, weil sie
eine Verknüpfung zu einem `Driver`-Datensatz über `externalUserId` voraussetzt, die ein
Admin nicht hat.

Jede Bereichskachel zeigt ihre Anlegen-Aktion („Add vehicle", „Add driver", „New order",
„Plan tour") nur bei passender Capability. Für `viewer` bleiben alle vier Kacheln damit
reine Links.

### 6.1 Layout

```
admin / disponent / viewer                    fahrer
┌──────────────────────────────────────┐      ┌──────────────────────┐
│ Welcome, Anna Admin        [admin]   │      │ Welcome, Frank F.    │
├──────────────────────────────────────┤      │              [fahrer]│
│ ┌────┐ ┌────┐ ┌────┐ ┌────┐          │      ├──────────────────────┤
│ │ 12 │ │  2 │ │  7 │ │  3 │  KPIs    │      │ ┌──────────────────┐ │
│ │Veh.│ │Shop│ │Draft││Tours│         │      │ │ Tours today: 2   │ │
│ └────┘ └────┘ └────┘ └────┘          │      │ └──────────────────┘ │
├──────────────────────────────────────┤      ├──────────────────────┤
│ Orders awaiting a tour               │      │ My tours today       │
│  ORD-0007  Hamburg→Bremen  [Plan]    │      │  T-14  Planned       │
│  ORD-0011  Köln→Essen      [Plan]    │      │        [Start tour]  │
├──────────────────────────────────────┤      │  T-15  InProgress    │
│ ┌────────┐┌────────┐┌────────┐┌─────┐│      │        [Complete]    │
│ │Vehicles││Drivers ││Orders  ││Tours││      ├──────────────────────┤
│ │[+ Add] ││[+ Add] ││[+ New] ││[+…] ││      │ ┌──────────────────┐ │
│ └────────┘└────────┘└────────┘└─────┘│      │ │ Tours            │ │
└──────────────────────────────────────┘      │ └──────────────────┘ │
```

Die Fahrer-Variante ist absichtlich einspaltig und für ein Fahrerhandy brauchbar.

### 6.2 Datenbeschaffung

Ohne eine Zeile neue API. Jede Kennzahl ist ein Request mit `pageSize=1`, gelesen wird nur
`PagedResult.TotalCount`. Die Arbeitslisten holen Zeilen und Zähler in einem Zug.

| Block | Request |
|---|---|
| Vehicles available / in workshop | `GET /api/vehicles?status=Available&pageSize=1`, dito `InWorkshop` |
| Drivers available | `GET /api/drivers?status=Available&pageSize=1` |
| Orders in Draft + Arbeitsliste | `GET /api/orders?status=Draft&pageSize=5` |
| Tours today + „My tours today" | `GET /api/tours?tourDate=<heute>&pageSize=100` |

Für einen Admin sind das fünf parallele Requests, für einen Fahrer genau einer. Der
`ListToursQueryHandler` schränkt für einen `fahrer` bereits serverseitig auf dessen eigene
Touren ein (`ListToursQueryHandler.cs:34-46`); das Frontend muss seine eigene `driverId`
also gar nicht kennen.

Start/Complete auf der Fahrer-Karte rufen die vorhandenen `TourService.start()` /
`.complete()` bzw. die Funktionen aus `api/tours.ts` auf. Neu sind nur der Button und eine
Fehlerzeile in der Karte; die Statuslogik bleibt an einer Stelle.

### 6.3 Fehlerverhalten

Jeder Block lädt und scheitert für sich. Ein Fehler bei den Fahrzeugzahlen zeigt eine
Meldung in dieser einen Kachel; die Arbeitsliste daneben lädt trotzdem. Das folgt der
Trennung, die die Listen heute zwischen `errorMessage` und `actionError` machen.

## 7. App-Shell

Kopfleiste (`mat-toolbar` / `v-app-bar`): Marke „TransBrain", Navigationslinks, rechts
Anzeigename mit Rollen-Chip und „Sign out".

Die Links folgen exakt der Relevanz-Spalte aus §6: ein `fahrer` sieht `Home | Tours`, ein
Admin `Home | Vehicles | Drivers | Orders | Tours`. Fünf Links passen in jede Breite,
deshalb kein Burger-Menü.

Ist niemand angemeldet, zeigt die Leiste nur die Marke. Der „Sign in"-Button
(`data-testid="login"`) lebt dann allein auf der Startseite statt wie heute in fünf Listen
parallel.

## 8. Routing und Guards

| Route | heute | künftig |
|---|---|---|
| `''` / `/` | `VehicleList` | `Home` |
| `/vehicles` | `VehicleList` (Dublette) | `VehicleList` (einzige) |
| `/callback` (nur Vue) | `AuthCallback` | unverändert, guard-frei |

Damit löst sich der als Notlösung markierte Doppelpfad auf. Der eigentliche Grund für den
Kommentar in `app.routes.ts:14-19` bleibt und wird umformuliert übernommen: `''` muss
weiterhin eine echte Komponente tragen, sonst verwirft `angular-auth-oidc-client` einen
gültigen Authorization Code. Die Komponente ist jetzt `Home`.

| Route | Bedingung |
|---|---|
| `/vehicles`, `/drivers`, `/orders`, `/tours`, `/tours/:id` | nur angemeldet |
| `/vehicles/new`, `/vehicles/:id`, `/drivers/new`, `/drivers/:id` | `masterData.write` |
| `/orders/new`, `/orders/:id`, `/tours/new` | `dispatch.write` |

Die Listen sind bewusst **nur authentifizierungspflichtig, nicht rollengeschützt.** Ein
`fahrer`, der `/vehicles` eintippt, kommt hin — die API erlaubt ihm das lesend. Eine
Kachel auszublenden heißt „brauchst du nicht", nicht „darfst du nicht"; eine Sperre zu
erfinden, die der Server nicht kennt, wäre eine zweite, abweichende Wahrheit.
`/tours/:id` ist aus demselben Grund offen: der Fahrer muss dorthin, um zu starten, der
Viewer darf zuschauen — die Buttons darin sind einzeln gated.

Wer an einem Capability-Guard scheitert, wird auf `/` umgeleitet.

Angular: `authGuard` plus eine `capabilityGuard(capability)`-Factory als `CanActivateFn`.
Vue: ein `router.beforeEach`, das `to.meta.capability` auswertet, `auth.load()` abwartet
und `/callback` überspringt.

## 9. Rollenabhängige Bedienelemente in bestehenden Screens

Je Frontend dieselben, hier mit den vorhandenen `data-testid`s:

| Capability | Bedienelemente |
|---|---|
| `masterData.write` | `vehicle-add`, `vehicle-edit`, `vehicle-delete`, `driver-add`, `driver-edit`, `driver-delete` |
| `dispatch.write` | `order-add`, `order-edit`, `order-cancel`, `tour-add`, `tour-assign`, `tour-remove` |
| `tourStatus.write` | `tour-start`, `tour-complete` |

Die überholten Kommentare zur fehlenden Rollen-Infrastruktur
(`vehicle-list.component.ts:95-99`, `VehicleList.vue:46-50`) werden gelöscht, nicht
umformuliert.

## 10. Erweiterungen der API-Clients

`VehicleService.list()` / `listVehicles()` und `DriverService.list()` / `listDrivers()`
nehmen heute nur `pageSize`. Für die Kennzahlen aus §6.2 brauchen sie den
`status`-Parameter, den die Endpunkte bereits anbieten (`VehicleEndpoints.cs:40`,
`DriverEndpoints.cs:32`). Order- und Tour-Clients haben ihre Filter schon.

## 11. Tests

Je Frontend ein neues `e2e/home.spec.ts` mit vier Tests — `admin.user`, `dispo.user`,
`fahrer.user`, `viewer.user`. Jeder prüft, welche Kacheln, Navigationslinks und
Aktionsbuttons sichtbar **und** welche abwesend sind. Die Abwesenheitsprüfung ist der
eigentliche Gegenstand: sie ist das Einzige, was eine falsche Capability-Zuordnung
auffliegen lässt.

Die vier Anmeldungen wandern in ein gemeinsames `e2e/login.ts`, samt der `#password`-
Eigenheit von Keycloaks Theme, die in `vehicles.spec.ts:11-17` dokumentiert ist.

Die bestehenden Specs brechen und werden mitgezogen: sie melden sich auf `/` an und
erwarten dort die Überschrift „Vehicles"; künftig landen sie auf der Startseite und müssen
einen Schritt weiter nach `/vehicles` gehen. `unauthenticated_visitor_seesSignInButton`
bleibt gültig, weil `data-testid="login"` auf `/` erhalten bleibt.

Keine neuen Unit-Tests: die Capability-Ableitung ist eine Tabellenabfrage, und Vue hätte
dafür noch kein Test-Setup. Die vier e2e-Tests decken dieselbe Tabelle end-to-end ab.

## 12. Verifikation vor Abschluss

- `dotnet build` und `dotnet test` — nur die Realm-Datei ist serverseitig berührt, die
  Suite läuft zur Absicherung dennoch mit
- `npm run build` in beiden Frontends (bei Vue schließt das `vue-tsc` ein)
- `npm run e2e` in beiden Frontends gegen einen laufenden
  `dotnet run --project src/TransBrain.AppHost`
- Sichtprüfung des Rollen-Claims aus §4 an einem echten Token, vor allem anderen

## 13. Umsetzungsreihenfolge

1. Realm-Mapper ergänzen und den Claim in ID- **und** Access-Token verifizieren (§4);
   bei Fehlschlag auf den Rückfallweg wechseln, bevor irgendetwas anderes gebaut wird
2. Session-Schicht und Capability-Tabelle in beiden Frontends (§3, §5)
3. App-Shell mit Navigation und Logout (§7)
4. Routing umstellen und Guards ergänzen (§8)
5. Startseite mit ihren Blöcken, dazu die API-Client-Erweiterungen (§6, §10)
6. Bedienelemente in den bestehenden Screens gaten und die Auth-Duplikation entfernen (§9)
7. e2e-Tests: `login.ts`, `home.spec.ts`, Anpassung der bestehenden Specs (§11)
8. Dokumentation und Screenshots (§14)

Schritt 1 steht bewusst allein: von seinem Ausgang hängt ab, woher die Rollen kommen, und
das ist die einzige offene technische Frage dieser Spec.

## 14. Dokumentation

Durch AGENTS.md verpflichtend, da die Änderung in beiden Oberflächen sichtbar ist:

- neues Kapitel „Startseite" in `docs/BEDIENUNG_TRANSBRAIN_WEB.md` und
  `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`, mit der Rollentabelle aus §6
- angepasste Anmelde-Schritte in den bestehenden Kapiteln, da die Anmeldung nun auf der
  Startseite endet
- neue Screenshots `docs/img/web/startseite-admin.png`,
  `docs/img/web/startseite-fahrer.png` und die beiden Entsprechungen unter
  `docs/img/vueweb/` — die zwei Layouts aus §6.1
- Eintrag unter `[Unreleased]` in `CHANGELOG.md`

## 15. Bewusst nicht enthalten

Rollenwechsel zur Laufzeit, 403-Seite, Lokalisierung, Pagination auf den Arbeitslisten
(die Draft-Liste zeigt fünf Zeilen und verlinkt für den Rest auf `/orders`), Persistenz
von Dashboard-Einstellungen, serverseitige Aggregat-Endpunkte für die Kennzahlen.
