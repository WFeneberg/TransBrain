# Online-Kartendienste einrichten (Routing & Geocoding)

TransBrain nutzt zwei Kartendienste: **OpenRouteService** plant LKW-Routen für Touren
(Profil `driving-hgv`), **Nominatim** macht aus Adressen Koordinaten (Geocoding) für die
Karten in Aufträgen, Anlieferorten und Touren.

Standardmäßig laufen beide als **selbst gehostete Container** auf der eigenen Maschine und
rufen keinen externen Dienst auf — dafür brauchen sie einen heruntergeladenen
OpenStreetMap-Auszug und einen einmaligen Import (Minuten bis Stunden, siehe
[DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md), Abschnitt 4.1). Alternativ kann jeder der beiden
Dienste **online** betrieben werden, also gegen den öffentlichen Dienst im Internet. Diese
Anleitung beschreibt, was dafür nötig ist — inklusive der Registrierung.

## Wann lohnt sich der Online-Modus?

| | Selbst gehostet (Standard) | Online |
|---|---|---|
| Abdeckung | nur der importierte Kartenauszug (z. B. Oberbayern) | weltweit |
| Vorbereitung | Extract herunterladen, Graph-Build/Import abwarten, RAM/Platte vorhalten | Konto anlegen (nur Routing), Schalter umlegen |
| Externe Aufrufe | keine — Adressen bleiben im Haus | **Adressen und Koordinaten gehen an externe Server** |
| Limits | keine | Routing: Tageskontingent des Gratis-Plans; Geocoding: Fair-Use-Regeln |
| Kosten | eigene Hardware | kostenlos in den Gratis-Stufen |

> **Datenschutz:** Im Online-Modus verlassen Kunden- und Auftragsadressen das eigene System
> (Routing: HeiGIT gGmbH, Heidelberg; Geocoding: OpenStreetMap Foundation). Vor dem Einsatz
> mit echten Kundendaten prüfen, ob das zur eigenen Datenschutzlage passt.

Beides ist unabhängig wählbar — z. B. Routing online, Geocoding weiterhin lokal.

## Was wird benötigt?

| Dienst | Registrierung | Zugangsdaten | Kosten |
|---|---|---|---|
| Routing — `api.openrouteservice.org` | ja, kostenloses HeiGIT-Konto | API-Key | kostenlos (Gratis-Plan) |
| Geocoding — `nominatim.openstreetmap.org` | **nein** | keine, nur identifizierender User-Agent (setzt TransBrain automatisch) | kostenlos (Fair-Use) |

## 1. Registrierung bei openrouteservice (Routing)

Der öffentliche OpenRouteService wird vom HeiGIT (Heidelberg Institute for Geoinformation
Technology) betrieben und verlangt einen persönlichen API-Key:

1. <https://account.heigit.org> öffnen und ein Konto anlegen („Sign up“: E-Mail-Adresse und
   Passwort; alternativ geht der Weg über „Sign up“ auf <https://openrouteservice.org>).
2. Die Bestätigungs-E-Mail öffnen und das Konto aktivieren.
3. Anmelden; im Konto-Dashboard liegt der **API-Key** des kostenlosen Standard-Plans
   (ggf. über „Create token/key“ anlegen). Den Key kopieren.
4. Der Gratis-Plan umfasst derzeit rund **2.000 Routing-Anfragen pro Tag** (plus
   Minuten-Limits und max. 50 Wegpunkte pro Route; verbindlich sind die im Dashboard
   angezeigten Werte). Für eine Disposition mit gecachten Routen reicht das in der Regel;
   mehr gibt es über kostenpflichtige Pläne.

> Der API-Key ist ein Geheimnis: nicht ins Repository einchecken, nicht in
> `appsettings.json` eintragen — nur in User-Secrets oder Umgebungsvariablen (unten).
> Routenergebnisse des öffentlichen Dienstes stehen unter CC-BY 4.0 (Namensnennung
> „openrouteservice“), siehe <https://openrouteservice.org/terms-of-service/>.

## 2. Nominatim (Geocoding) — keine Registrierung

Die öffentliche Instanz <https://nominatim.openstreetmap.org> braucht **kein Konto und
keinen Key**, hat aber eine verbindliche
[Nutzungsrichtlinie](https://operations.osmfoundation.org/policies/nominatim/):

- höchstens **eine Anfrage pro Sekunde**, keine Massen-Geokodierung;
- jede Anfrage muss die Anwendung **identifizieren** (User-Agent). TransBrain sendet im
  Online-Modus automatisch `TransBrain (+https://github.com/WFeneberg/TransBrain)`; für den
  ernsthaften Betrieb den eigenen Kontakt eintragen (`Nominatim__UserAgent`, z. B.
  `MeineSpedition/1.0 (it@meine-spedition.example)`);
- Ergebnisse cachen ist erlaubt und erwünscht — TransBrain tut das ohnehin: jede Adresse
  wird nur einmal aufgelöst (Redis-Cache), manuelle Überschreibungen auf der
  Geodaten-Seite vermeiden Anfragen ganz.

## 3. TransBrain umschalten

### Variante A: Entwicklung mit Aspire (AppHost)

Ein Schalter genügt — **Geocoding folgt automatisch der Routing-Wahl**, und die Container
der Online-Dienste werden gar nicht erst gestartet (beide online = kein OSM-Extract nötig):

```bash
dotnet user-secrets set OpenRouteService:Mode Online --project src/TransBrain.AppHost
dotnet user-secrets set Parameters:ors-api-key <API-Key aus Schritt 1> --project src/TransBrain.AppHost
```

Danach den AppHost normal starten (`dotnet run --project src/TransBrain.AppHost`). Fehlt der
Key, fragt das Aspire-Dashboard beim Start danach.

Soll Geocoding **nicht** mitwechseln (oder nur Geocoding online gehen), den Modus getrennt
festlegen:

```bash
dotnet user-secrets set Nominatim:Mode SelfHosted --project src/TransBrain.AppHost   # oder Online
```

Zurück zu selbst gehostet:

```bash
dotnet user-secrets remove OpenRouteService:Mode --project src/TransBrain.AppHost
dotnet user-secrets remove Nominatim:Mode --project src/TransBrain.AppHost
```

### Variante B: Ohne AppHost (Docker Compose, Azure, eigener Host)

Die Api direkt per Umgebungsvariablen konfigurieren — so bekommen auch die Compose- und
Azure-Setups, die keine Karten-Container mitbringen, erstmals Karten:

| Variable | Wert |
|---|---|
| `OpenRouteService__Mode` | `Online` |
| `OpenRouteService__ApiKey` | der API-Key aus Schritt 1 (als Secret hinterlegen) |
| `Nominatim__Mode` | `Online` |
| `Nominatim__UserAgent` | eigene Kennung mit Kontakt, z. B. `MeineSpedition/1.0 (it@meine-spedition.example)` |

Ohne diese Variablen gilt `SelfHosted`, und die Api erwartet eigene Container unter
`OpenRouteService__BaseUrl` bzw. `Nominatim__BaseUrl`.

## 4. Prüfen, ob alles läuft

1. In einem der Frontends als `admin` die Seite **Geodaten** (`/geodata`) öffnen: Die
   Statuskarte muss Geocoder und Routing als **bereit** zeigen. Online-Routing zeigt
   dabei bewusst **kein OSM-Datenstand-Datum** — der öffentliche Dienst hält seine Daten
   selbst aktuell; der öffentliche Nominatim meldet sein Datum wie gewohnt.
2. Eine Tour mit Stopps öffnen: Die Route samt Fahrzeiten muss erscheinen — im
   Online-Modus auch für Ziele außerhalb des bisherigen Kartenauszugs.

Wenn nicht:

| Symptom | Ursache |
|---|---|
| Routing „nicht verfügbar“, Route antwortet `503 Routing.ServiceError … HTTP 401/403` | API-Key fehlt oder ist falsch (`OpenRouteService__ApiKey` bzw. `Parameters:ors-api-key` prüfen) |
| Route antwortet `503 Routing.ServiceError … HTTP 429` | Tages- oder Minutenkontingent des Gratis-Plans erschöpft — später erneut versuchen oder Plan erhöhen |
| `503 Routing.Unreachable` / Geocoder „nicht verfügbar“ | keine Internetverbindung vom Api-Host oder Tippfehler in `…__Mode`/`…BaseUrl` |
| Adresse wird nicht gefunden (`404 Geocoding.NoMatch`) | Adresse prüfen oder auf der Geodaten-Seite manuell Koordinaten setzen |

## 5. Grenzen und Hinweise

- **Routing-Kontingent:** Das Tageslimit zählt pro Routenabruf; TransBrain fragt Routen je
  Touranzeige ab. Bei intensiver Nutzung das Dashboard im HeiGIT-Konto im Blick behalten.
- **Max. 50 Wegpunkte** pro Route im Gratis-Plan — Touren mit sehr vielen Stopps können
  daran stoßen (selbst gehostet gilt das Limit nicht).
- **Nominatim-Fair-Use:** TransBrains Cache und die manuellen Überschreibungen halten die
  Last klein; trotzdem keine Massenimporte mit automatischem Geocoding über die
  öffentliche Instanz fahren.
- **Mischbetrieb** ist ausdrücklich vorgesehen — z. B. Geocoding lokal lassen (einmal
  importiert, keine Limits) und nur Routing online beziehen, oder umgekehrt.
- Der Wechsel ist jederzeit und ohne Datenverlust umkehrbar; die benannten Docker-Volumes
  der Container bleiben erhalten, solange sie nicht gelöscht werden.
