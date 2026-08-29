# Bedienung TransBrain.Web (Angular)

Diese Anleitung beschreibt die Angular-Oberfläche von TransBrain (`src/TransBrain.Web`).
Die zweite Oberfläche, TransBrain.VueWeb, bietet denselben Funktionsumfang gegen dieselbe
API — siehe `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`.

Die Beschriftungen der Oberfläche selbst (Schaltflächen, Feldnamen, Fehlermeldungen) sind
auf Englisch, da die Anwendung noch nicht lokalisiert ist. Diese Anleitung ist auf Deutsch
verfasst, nennt aber die tatsächlichen englischen Beschriftungen, damit sie zu dem passt,
was auf dem Bildschirm zu sehen ist.

> **Achtung — Entwicklungsumgebung, keine Daten bleiben erhalten:** Bei jedem Neustart
> von `dotnet run --project src/TransBrain.AppHost` beginnt die Anwendung mit einer
> **leeren Datenbank**. Postgres und Keycloak haben in dieser Umgebung absichtlich kein
> dauerhaftes Datenlaufwerk — Postgres führt bei jedem Start alle Migrationen neu von
> Grund auf aus, und Keycloak importiert die Realm-Konfiguration erneut aus der Datei.
> Das macht jeden Start reproduzierbar, bedeutet aber auch: **Jedes Fahrzeug, jeder
> Fahrer und jede Änderung, die Sie über die Keycloak-Verwaltungskonsole vornehmen, geht
> beim nächsten Neustart verloren.** Tragen Sie hier keine Daten ein, auf deren
> Fortbestand Sie sich verlassen — das gilt auch für eine ganze Vormittagsarbeit an
> Stammdaten.

## Voraussetzungen

- Der gesamte Stack läuft (`dotnet run --project src/TransBrain.AppHost`), siehe
  README.md.
- Ein gültiges Benutzerkonto in Keycloak. Für die lokale Entwicklung siehe die
  Testbenutzer in README.md, Abschnitt „Test users“ — je einer pro Rolle
  (`admin`, `disponent`, `fahrer`, `viewer`).
- Die Anwendung ist unter `http://localhost:4200` erreichbar (Port siehe Aspire-Dashboard,
  falls abweichend).

## Anmeldung

1. `http://localhost:4200` im Browser öffnen.
2. Ohne aktive Sitzung zeigt die Seite nur eine Schaltfläche **„Sign in“**. Es gibt keine
   Fahrzeug- oder Fahrerliste zu sehen, bevor eine Anmeldung stattgefunden hat.
3. Klick auf „Sign in“ leitet zur Keycloak-Anmeldeseite weiter (Benutzername/Passwort).
4. Nach erfolgreicher Anmeldung erfolgt die Rückleitung zu TransBrain.Web, und die
   Fahrzeugliste wird angezeigt (die Startseite `/` zeigt dieselbe Liste wie `/vehicles`).
5. Schlägt die Anmeldung fehl oder ist Keycloak nicht erreichbar, erscheint die Meldung
   „Could not verify your sign-in status. Please try signing in again.“

**Hinweis für die Fehlersuche:** Wenn die Anmeldung überhaupt nicht bis zur
Keycloak-Login-Seite kommt, ist meist das lokale HTTPS-Entwicklungszertifikat nicht als
vertrauenswürdig hinterlegt — siehe README.md, Abschnitt „Trust the development HTTPS
certificate“.

## Fahrzeugliste

Erreichbar über `/` oder `/vehicles`.

- Angezeigte Spalten: License plate (Kennzeichen), Type (Fahrzeugtyp), Payload (kg)
  (Zuladung).
- Schaltfläche **„Add vehicle“** oberhalb der Tabelle legt ein neues Fahrzeug an.
- Pro Zeile: **„Edit“** öffnet das Fahrzeug zum Bearbeiten, **„Delete“** löscht es sofort
  (ohne Rückfrage-Dialog).

**Bekannte Einschränkungen der Liste (Stand dieser Phase):**

- Es gibt weder eine Filter- noch eine Sortiermöglichkeit in der Oberfläche. Die API
  unterstützt zwar Filter nach Status und Typ sowie Seitenweise-Abruf, aber die Oberfläche
  ruft immer nur die erste Seite mit der Standardgröße ab. Bei mehr als 20 Fahrzeugen sind
  ältere Einträge in dieser Ansicht **nicht sichtbar**.
- Es gibt noch keine Navigation (kein Menü) zwischen der Fahrzeug- und der Fahrerliste.
  Um die Fahrerliste zu öffnen, muss die Adresse `/drivers` direkt in der Adressleiste des
  Browsers eingegeben werden.

## Fahrzeugformular (Anlegen / Bearbeiten)

Erreichbar über „Add vehicle“ (neu, `/vehicles/new`) oder „Edit“ in der Liste
(`/vehicles/{id}`).

| Feld (Beschriftung)  | Bedeutung                       | Pflichtfeld | Regeln                                  |
|----------------------|----------------------------------|:-----------:|------------------------------------------|
| License plate        | Kennzeichen                      | ja          | muss eindeutig sein (siehe unten)         |
| Type                 | Fahrzeugtyp                      | ja          | Auswahl: Tractor, RigidTruck, Van         |
| Payload (kg)         | Zuladung in Kilogramm            | ja          | muss größer als 0 sein                    |
| Load meters          | Lademeter                        | ja          | muss größer als 0 sein                    |
| Next inspection due  | Datum der nächsten Untersuchung  | ja          | Datum                                     |

- **„Save“** speichert; bei Erfolg erfolgt die Rückkehr zur Liste.
- **„Cancel“** verwirft die Eingabe und kehrt ohne Speichern zur Liste zurück.
- Ein leer gelassenes Pflichtfeld zeigt nach einem Speicherversuch direkt unter dem Feld
  „This field is required.“
- Lehnt die API die Eingabe ab (z. B. ein bereits vergebenes Kennzeichen, oder Zuladung
  bzw. Lademeter mit 0 oder weniger), erscheint die Fehlermeldung der API direkt unter dem
  betroffenen Feld — bei einem doppelten Kennzeichen z. B. eine Konfliktmeldung
  (HTTP 409).
- Ein Ladefehler beim Öffnen zum Bearbeiten (z. B. wenn das Fahrzeug zwischenzeitlich
  gelöscht wurde) erscheint als Meldung oberhalb des Formulars.

## Fahrerliste

Erreichbar nur über die direkte Eingabe von `/drivers` in der Adressleiste (siehe
„Bekannte Einschränkungen“ oben — es gibt noch keinen Menüpunkt dafür).

- Angezeigte Spalten: Last name (Nachname), First name (Vorname), License classes
  (Führerscheinklassen), Status.
- Schaltfläche **„Add driver“** legt einen neuen Fahrer an.
- Pro Zeile: **„Edit“** und **„Delete“**, wie bei Fahrzeugen.
- Dieselben Einschränkungen wie bei der Fahrzeugliste gelten auch hier (keine Filter,
  keine Sortierung, nur die erste Seite von bis zu 20 Einträgen).

## Fahrerformular (Anlegen / Bearbeiten)

Erreichbar über „Add driver“ (neu, `/drivers/new`) oder „Edit“ in der Liste
(`/drivers/{id}`).

| Feld (Beschriftung)  | Bedeutung                          | Pflichtfeld | Regeln                                   |
|----------------------|--------------------------------------|:-----------:|--------------------------------------------|
| First name           | Vorname                              | ja          | darf nicht leer sein                       |
| Last name            | Nachname                             | ja          | darf nicht leer sein                       |
| License classes      | Führerscheinklassen (Kontrollkästchen: B, C1, C, CE) | ja | mindestens eine Klasse muss ausgewählt sein |
| License valid until  | Gültig bis (Datum)                   | ja          | Datum                                      |

- Führerscheinklassen werden über Kontrollkästchen aus- bzw. abgewählt, nicht über ein
  Dropdown.
- **„Save“** und **„Cancel“** verhalten sich wie im Fahrzeugformular.
- Validierungsfehler (fehlende Pflichtfelder, keine Führerscheinklasse ausgewählt) und
  serverseitige Fehler werden auf dieselbe Weise wie im Fahrzeugformular direkt am
  betroffenen Feld angezeigt.

## Rollen und Rechte

Die Anmeldung erfolgt über Keycloak mit genau einer der vier Rollen `admin`,
`disponent`, `fahrer` oder `viewer`.

| Rolle        | Fahrzeuge/Fahrer ansehen | Fahrzeuge/Fahrer anlegen, bearbeiten, löschen |
|--------------|:------------------------:|:-----------------------------------------------:|
| `admin`      | ja                        | ja                                                |
| `disponent`  | ja                        | **nein**                                          |
| `fahrer`     | ja                        | **nein**                                          |
| `viewer`     | ja                        | **nein**                                          |

**Wichtig — dies ist keine offensichtliche Einschränkung der Oberfläche:** Die
Schaltflächen „Add vehicle“/„Add driver“, „Edit“ und „Delete“ werden **jedem angemeldeten
Benutzer angezeigt**, unabhängig von seiner Rolle — die Oberfläche blendet sie für
Disponenten, Fahrer oder Betrachter nicht aus. Klickt ein Benutzer ohne die Rolle `admin`
trotzdem auf eine dieser Schaltflächen, lehnt die API den Schreibversuch ab, und die
Oberfläche zeigt eine Fehlermeldung wie zum Beispiel „The vehicle could not be deleted.
(HTTP 403)“. Das ist kein Fehler in der Anwendung, sondern der aktuelle Stand: Nur ein
Administrator-Konto kann tatsächlich schreiben, auch wenn die Schaltflächen für alle
sichtbar sind.

## Bekannte Einschränkungen (Zusammenfassung)

- Keine Navigation zwischen den Listen — Adressen müssen bei Bedarf manuell eingegeben
  werden (`/vehicles`, `/drivers`).
- Keine Filter- oder Sortierfunktion in der Oberfläche; nur die erste Seite (bis zu 20
  Einträge) wird angezeigt.
- Schreibschaltflächen sind für alle Rollen sichtbar, auch wenn nur `admin` sie tatsächlich
  nutzen kann.
- Löschen erfolgt sofort, ohne Sicherheitsabfrage.
