# Rollenbasierte Startseite — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beide Frontends bekommen eine rollenbasierte Startseite, eine Navigations-Shell und eine Capability-Schicht, die in der gesamten Oberfläche nur noch die Aktionen anbietet, die die angemeldete Rolle serverseitig auch ausführen darf.

**Architecture:** Eine deklarative Tabelle `Rolle → Capabilities` je Frontend, gespeist aus dem `realm_access.roles`-Claim des ID-Tokens, gekapselt in einem Angular-`SessionService` bzw. dem vorhandenen Pinia-`auth`-Store. Die Startseite besteht aus Blöcken mit je eigener Sichtbarkeitsbedingung und eigener Datenbeschaffung über die vorhandenen List-Endpunkte. Keine neuen API-Endpunkte.

**Tech Stack:** Angular 22 + Angular Material + `angular-auth-oidc-client`; Vue 3 + Vuetify + Pinia + `oidc-client-ts`; Playwright für e2e; Keycloak-Realm-Import.

**Spec:** `docs/superpowers/specs/2026-09-01-role-based-home-design.md`

## Global Constraints

- Alle Beschriftungen der Oberfläche sind **englisch**. Code und Kommentare englisch, Dokumentation deutsch (AGENTS.md, `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md:8-11`).
- Einrückung 4 Leerzeichen, überall.
- Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`).
- Nach jeder Task committen. Vor jedem Commit die Tests der Task laufen lassen.
- Die Capability-Tabelle ist die Spiegelung von `src/TransBrain.Api/Program.cs:134-137` und wird im Code als solche kommentiert. Weicht sie ab, ist die Tabelle falsch, nicht der Server.
- e2e-Tests laufen **nicht** in CI (`.github/workflows/ci.yml`). Sie werden lokal gegen einen laufenden `dotnet run --project src/TransBrain.AppHost` ausgeführt.
- Playwright läuft in beiden Suites mit `workers: 1`; das bleibt so (siehe die Begründung in beiden `playwright.config.ts`).
- Vier Testbenutzer, Passwort gleich dem Präfix: `admin.user`/`admin`, `dispo.user`/`dispo`, `fahrer.user`/`fahrer`, `viewer.user`/`viewer` (`docs/KEYCLOAK.md`).
- Keine neuen Unit-Test-Frameworks. Vue bekommt kein Vitest-Setup.

## Test-IDs

Diese `data-testid`-Werte werden in mehreren Tasks verwendet und sind in beiden Frontends **identisch**:

| Bereich | IDs |
|---|---|
| Shell | `nav-home`, `nav-vehicles`, `nav-drivers`, `nav-orders`, `nav-tours`, `nav-user`, `logout` |
| Startseite Kopf | `home-greeting`, `home-role-chip` |
| Kennzahlen | `home-kpi-vehicles-available`, `home-kpi-vehicles-workshop`, `home-kpi-drivers-available`, `home-kpi-orders-draft`, `home-kpi-tours-today` |
| Kennzahl-Fehler | `home-kpi-vehicles-error`, `home-kpi-drivers-error`, `home-kpi-orders-error`, `home-kpi-tours-error` |
| Kacheln | `home-tile-vehicles`, `home-tile-drivers`, `home-tile-orders`, `home-tile-tours` |
| Kachel-Aktionen | `home-tile-vehicles-add`, `home-tile-drivers-add`, `home-tile-orders-add`, `home-tile-tours-add` |
| Draft-Orders-Block | `home-draft-orders`, `home-draft-order-row`, `home-draft-order-open`, `home-draft-orders-error`, `home-plan-tour` |
| Fahrer-Block | `home-my-tours`, `home-my-tour-row`, `home-my-tour-start`, `home-my-tour-complete`, `home-my-tours-error` |

## Abweichung von der Spec (bewusst, begründet)

Die Layout-Skizze in Spec §6.1 zeigt pro Draft-Auftrag einen `[Plan]`-Button. Ein solcher Button hätte kein Ziel: eine Zuordnung Auftrag → Tour passiert ausschließlich im Zuordnungs-Picker auf `/tours/:id`, und es gibt keinen Endpunkt, der aus einem Auftrag heraus eine Tour anlegt. Stattdessen:

- pro Zeile ein `home-draft-order-open`, das den Auftrag unter `/orders/:id` öffnet
- unter dem Block ein `home-plan-tour`, das nach `/tours/new` führt

Alles andere folgt der Spec unverändert.

## Dateiübersicht

**Angular (`src/TransBrain.Web/src/app/`)**

| Datei | Verantwortung | Task |
|---|---|---|
| `auth/capabilities.ts` | *neu* — Rollen, Capabilities, Bereiche; reine Tabellen und Funktionen, keine Angular-Abhängigkeit | 2 |
| `auth/session.service.ts` | *neu* — einmaliges `checkAuth()`, Signals für Rollen/Name/Capabilities, `login`/`logout` | 2 |
| `auth/capability.guard.ts` | *neu* — `requireAuthentication`, `requireCapability(c)` | 4 |
| `home/home.component.ts` | *neu* — Startseite, alle Blöcke | 2, 5 |
| `app.ts`, `app.html`, `app.scss` | Shell: Toolbar, Navigation, Benutzer, Abmelden | 2 |
| `app.routes.ts` | `''` → Home, Guards | 2, 4 |
| `vehicles/vehicle.service.ts`, `drivers/driver.service.ts` | `status`-Parameter für `list()` | 5 |
| `*/**-list.component.ts`, `*/**-form.component.ts`, `tours/tour-detail.component.ts` | Capability-Gating, Entfernen der Auth-Duplikation | 6 |
| `e2e/login.ts` | *neu* — gemeinsame Anmeldung für alle vier Rollen | 2 |
| `e2e/home.spec.ts` | *neu* — vier Rollen | 2, 4, 5 |

**Vue (`src/TransBrain.VueWeb/src/`)**

| Datei | Verantwortung | Task |
|---|---|---|
| `auth/capabilities.ts` | *neu* — identische Tabellen, framework-frei | 3 |
| `stores/auth.ts` | Rollen, Name, `can()`, `logout()` | 3 |
| `views/Home.vue` | *neu* — Startseite, alle Blöcke | 3, 9 |
| `App.vue` | Shell | 3 |
| `main.ts` | `/` → Home, `meta.capability`, `router.beforeEach` | 3, 8 |
| `api/vehicles.ts`, `api/drivers.ts` | `status`-Parameter | 9 |
| `views/*.vue` | Capability-Gating, Entfernen der Auth-Duplikation | 10 |
| `e2e/login.ts`, `e2e/home.spec.ts` | *neu* | 3, 8, 9 |

**Serverseitig / Doku**

| Datei | Task |
|---|---|
| `src/TransBrain.AppHost/realms/transbrain-realm.json` | 1 |
| `docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `_VUEWEB.md`, `docs/img/**`, `CHANGELOG.md` | 11 |

---

### Task 1: Rollen-Claim ins ID-Token bringen und verifizieren

Diese Task steht allein, weil von ihrem Ausgang abhängt, woher die anderen neun Tasks die Rollen lesen. Sie liefert kein UI, sondern eine **verifizierte Tatsache** plus die Realm-Änderung.

**Files:**
- Modify: `src/TransBrain.AppHost/realms/transbrain-realm.json:43-56`

**Interfaces:**
- Consumes: nichts
- Produces: der Claim `realm_access.roles` als String-Array in ID-Token, Access-Token und UserInfo-Antwort des Clients `transbrain-spa`. Tasks 2 und 3 lesen ihn.

- [x] **Step 1: Realm-Mapper ergänzen**

In `transbrain-realm.json` das `protocolMappers`-Array des Clients `transbrain-spa` um einen zweiten Eintrag erweitern. Das Array enthält bisher nur `transbrain-api-audience`; der neue Eintrag kommt dahinter.

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
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        }
```

`userinfo.token.claim` steht hier zusätzlich zu dem, was Spec §4 zeigt: `angular-auth-oidc-client` befüllt `userData` per Voreinstellung aus dem UserInfo-Endpunkt, nicht aus dem ID-Token. Ohne dieses Flag wäre `userData` in der Angular-App rollenlos, obwohl das ID-Token die Rollen trägt.

- [x] **Step 2: Stack starten**

```bash
dotnet run --project src/TransBrain.AppHost
```

Der AppHost startet Postgres, Redis, Keycloak, die API **und beide Dev-Server** (`AppHost.cs:51-61`) — ein separater `npm start` ist nicht nötig. Warten, bis Keycloak und der Dev-Server antworten:

```bash
until curl -sk -o /dev/null https://localhost:8080/realms/transbrain/.well-known/openid-configuration && curl -s -o /dev/null http://localhost:4200; do sleep 3; done
```

Der Realm wird bei jedem Start neu importiert, die Änderung ist also sofort wirksam.

- [x] **Step 3+4: Ein echtes Token holen, den Claim und die API prüfen**

Der Realm erlaubt für `transbrain-spa` keinen Direct Access Grant (`"directAccessGrantsEnabled": false`), ein `curl` mit Passwort funktioniert also nicht — die Anmeldung muss durch das Frontend laufen. Statt das von Hand in der DevTools-Konsole zu tun, erledigt es eine temporäre Playwright-Spec: reproduzierbar, und das Ergebnis steht im Testprotokoll.

`src/TransBrain.Web/e2e/_claim-check.spec.ts` anlegen: als `admin.user` anmelden, den kompletten `sessionStorage` rekursiv nach JWT-förmigen Strings durchlaufen und von jedem `realm_access` ausgeben, dann mit dem gefundenen Access-Token ein `POST /api/vehicles` absetzen und den Status ausgeben. Rekursiv durchlaufen ist wichtig: `angular-auth-oidc-client` legt die Rohtokens unter `0-transbrain-spa.authnResult.*` ab, ein Zugriff auf `parsed.accessToken` findet nichts und erzeugt ein irreführendes `401`.

```bash
cd src/TransBrain.Web && npx playwright test e2e/_claim-check.spec.ts
```

Zu prüfen ist viererlei:
1. `realm_access.roles` ist in ID- **und** Access-Token vorhanden.
2. `roles` enthält die Rolle **genau einmal** — kein `["admin","admin"]`.
3. Es gibt keinen zusätzlichen, anders verschachtelten Claim wie `realm_access.realm_access`.
4. `POST /api/vehicles` als `admin.user` liefert `201`. Der Default-Scope schrieb `realm_access` schon vorher ins Access-Token; ein `403` hier hieße, dass der neue Mapper die Autorisierung der API zerlegt hat — dann greift Step 6.

**Ergebnis der Ausführung am 2026-09-01:**

```
0-transbrain-spa.authnResult.access_token (jwt)  {"roles":["admin"]}
0-transbrain-spa.authnResult.id_token     (jwt)  {"roles":["admin"]}
0-transbrain-spa.authzData                (jwt)  {"roles":["admin"]}
userData.realm_access                            {"roles":["admin"]}
POST /api/vehicles -> 201
```

Alle vier Kriterien erfüllt. Die Spec danach wieder löschen.

- [x] **Step 5: Wenn Steps 3 und 4 wie erwartet ausgehen — committen und Task beenden**

```bash
git add src/TransBrain.AppHost/realms/transbrain-realm.json
git commit -m "feat(auth): expose realm roles in the SPA client's id token"
```

**Verifizierte Rollenquelle für Tasks 2 und 3: `userData.realm_access.roles` (Angular) bzw. `user.profile.realm_access.roles` (Vue).** Der Rückfallweg aus Step 6 wird nicht gebraucht.

- [ ] **Step 6: Nur falls Step 3 oder 4 fehlschlägt — Rückfallweg** *(nicht eingetreten, entfällt)*

Realm-Änderung zurücknehmen:

```bash
git checkout -- src/TransBrain.AppHost/realms/transbrain-realm.json
```

Stattdessen eine gemeinsame Hilfsfunktion anlegen, die den Access-Token-Payload dekodiert. Sie kommt in **beide** Frontends, jeweils als `src/app/auth/token-roles.ts` bzw. `src/auth/token-roles.ts`:

```ts
/**
 * Reads realm roles out of a raw JWT payload.
 *
 * Parsing an access token in the client is not what OIDC intends - the access token is the
 * resource server's - but Keycloak's built-in realm-roles mapper writes realm_access only
 * there, and adding a mapper that also writes it to the id token turned out to conflict with
 * that built-in one (see the task 1 report). Reading it here is the smaller evil: it is a
 * read, it never leaves the browser, and every authorization decision that matters is still
 * taken by the API against the same token.
 */
export function realmRolesFromJwt(token: string | null | undefined): string[] {
    if (!token) {
        return [];
    }

    const segments = token.split('.');
    if (segments.length < 2) {
        return [];
    }

    try {
        const json = atob(segments[1].replace(/-/g, '+').replace(/_/g, '/'));
        const payload = JSON.parse(json) as { realm_access?: { roles?: unknown } };
        const roles = payload.realm_access?.roles;
        return Array.isArray(roles) ? roles.filter((role): role is string => typeof role === 'string') : [];
    } catch {
        // A malformed token is an unauthenticated user's problem, not a crash: no roles, and the
        // API will refuse the request anyway.
        return [];
    }
}
```

Tasks 2 und 3 beziehen die Rollen dann daraus statt aus `userData` / `user.profile`; alles Weitere bleibt unverändert. Im Task-Bericht festhalten, welcher Weg gilt.

```bash
git add src/TransBrain.Web/src/app/auth/token-roles.ts src/TransBrain.VueWeb/src/auth/token-roles.ts
git commit -m "feat(auth): read realm roles from the access token payload"
```

---

### Task 2: Angular — Capability-Schicht, Shell und Startseiten-Gerüst

Deliverable: Die Angular-App hat eine Kopfleiste mit rollengefilterter Navigation und eine Startseite unter `/`, die die zur Rolle passenden Bereichskacheln zeigt. Noch ohne Kennzahlen und Arbeitslisten — die kommen in Task 5.

**Files:**
- Create: `src/TransBrain.Web/src/app/auth/capabilities.ts`
- Create: `src/TransBrain.Web/src/app/auth/session.service.ts`
- Create: `src/TransBrain.Web/src/app/home/home.component.ts`
- Create: `src/TransBrain.Web/e2e/login.ts`
- Create: `src/TransBrain.Web/e2e/home.spec.ts`
- Modify: `src/TransBrain.Web/src/app/app.ts`
- Modify: `src/TransBrain.Web/src/app/app.html`
- Modify: `src/TransBrain.Web/src/app/app.scss`
- Modify: `src/TransBrain.Web/src/app/app.routes.ts:13-23`
- Modify: `src/TransBrain.Web/src/app/app.spec.ts`

**Interfaces:**
- Consumes: aus Task 1 die Rollenquelle `userData.realm_access.roles` (oder `realmRolesFromJwt(accessToken)`, falls Task 1 den Rückfallweg genommen hat)
- Produces:
  - `type Capability = 'read' | 'masterData.write' | 'dispatch.write' | 'tourStatus.write'`
  - `type AppRole = 'admin' | 'disponent' | 'fahrer' | 'viewer'`
  - `type Area = 'vehicles' | 'drivers' | 'orders' | 'tours'`
  - `knownRoles(roles: readonly string[]): AppRole[]`
  - `capabilitiesFor(roles: readonly string[]): ReadonlySet<Capability>`
  - `areasFor(roles: readonly string[]): ReadonlySet<Area>`
  - `SessionService` mit `isAuthenticated: Signal<boolean>`, `error: Signal<string | null>`, `roles: Signal<AppRole[]>`, `displayName: Signal<string>`, `areas: Signal<ReadonlySet<Area>>`, `ready: Observable<boolean>`, `can(c: Capability): boolean`, `hasRole(r: AppRole): boolean`, `login(): void`, `logout(): void`, `initialize(): void`
  - `signIn(page, role)` und `type TestRole` aus `e2e/login.ts`

- [x] **Step 1: Den fehlschlagenden e2e-Test schreiben**

`src/TransBrain.Web/e2e/login.ts`:

```ts
import { expect, type Page } from '@playwright/test';

export type TestRole = 'admin' | 'dispo' | 'fahrer' | 'viewer';

/** Realm passwords are the username prefix, see docs/KEYCLOAK.md. */
const PASSWORDS: Record<TestRole, string> = {
    admin: 'admin',
    dispo: 'dispo',
    fahrer: 'fahrer',
    viewer: 'viewer',
};

/**
 * Signs in through the real Keycloak login form and waits for the home page.
 *
 * Every spec in this suite needs this, and each copy of it used to carry its own version of the
 * '#password' workaround below - one of them subtly different. Playwright gives each test a
 * fresh browser context, so there is no session to clear between roles: a test that wants a
 * different role simply calls this with a different one.
 */
export async function signIn(page: Page, role: TestRole): Promise<void> {
    await page.goto('/');
    await page.getByTestId('login').click();
    // Keycloak's default theme also renders a "Show password" toggle button whose aria-label
    // contains the substring "password", so `getByLabel('Password')` matches both it and the
    // real input under Playwright's default case-insensitive substring match and throws a
    // strict-mode violation. Target the two form fields by their stable Keycloak-theme ids.
    await page.locator('#username').fill(`${role}.user`);
    await page.locator('#password').fill(PASSWORDS[role]);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByTestId('home-greeting')).toBeVisible();
}
```

`src/TransBrain.Web/e2e/home.spec.ts`:

```ts
import { expect, test } from '@playwright/test';
import { signIn } from './login';

test('unauthenticatedVisitor_atRoot_seesSignInButtonAndNoNavigation', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
    await expect(page.getByTestId('nav-tours')).toBeHidden();
});

test('adminUser_onHome_seesEveryAreaAndEveryAddAction', async ({ page }) => {
    await signIn(page, 'admin');

    await expect(page.getByTestId('home-role-chip')).toHaveText('admin');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`nav-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}-add`)).toBeVisible();
    }
});

test('disponentUser_onHome_seesEveryAreaButNoMasterDataAddActions', async ({ page }) => {
    await signIn(page, 'dispo');

    await expect(page.getByTestId('home-role-chip')).toHaveText('disponent');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
    }
    // dispatch.write yes, masterData.write no - the distinction this whole layer exists for.
    await expect(page.getByTestId('home-tile-orders-add')).toBeVisible();
    await expect(page.getByTestId('home-tile-tours-add')).toBeVisible();
    await expect(page.getByTestId('home-tile-vehicles-add')).toBeHidden();
    await expect(page.getByTestId('home-tile-drivers-add')).toBeHidden();
});

test('fahrerUser_onHome_seesOnlyToursAndNoAddActions', async ({ page }) => {
    await signIn(page, 'fahrer');

    await expect(page.getByTestId('home-role-chip')).toHaveText('fahrer');
    await expect(page.getByTestId('home-tile-tours')).toBeVisible();
    await expect(page.getByTestId('nav-tours')).toBeVisible();
    for (const area of ['vehicles', 'drivers', 'orders']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeHidden();
        await expect(page.getByTestId(`nav-${area}`)).toBeHidden();
    }
    await expect(page.getByTestId('home-tile-tours-add')).toBeHidden();
});

test('viewerUser_onHome_seesEveryAreaAndNoAddActionAtAll', async ({ page }) => {
    await signIn(page, 'viewer');

    await expect(page.getByTestId('home-role-chip')).toHaveText('viewer');
    for (const area of ['vehicles', 'drivers', 'orders', 'tours']) {
        await expect(page.getByTestId(`home-tile-${area}`)).toBeVisible();
        await expect(page.getByTestId(`home-tile-${area}-add`)).toBeHidden();
    }
});

test('signedInUser_afterSigningOut_isBackAtTheSignInButton', async ({ page }) => {
    await signIn(page, 'admin');
    await page.getByTestId('logout').click();
    await expect(page.getByTestId('login')).toBeVisible();
});
```

- [x] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Voraussetzung: `dotnet run --project src/TransBrain.AppHost` läuft — er bringt den Dev-Server auf Port 4200 selbst mit.

```bash
cd src/TransBrain.Web && npx playwright test e2e/home.spec.ts
```

Erwartet: alle sechs Tests scheitern, die fünf angemeldeten mit einem Timeout auf `home-greeting` (die Startseite existiert noch nicht), der erste mit einem Timeout auf `nav-tours`, weil `toBeHidden()` zwar zutrifft, aber `login` auf `/` noch von der Fahrzeugliste kommt — je nach Reihenfolge kann dieser eine Test bereits durchlaufen. Das ist in Ordnung; entscheidend sind die fünf anderen.

- [x] **Step 3: Die Capability-Tabelle schreiben**

`src/TransBrain.Web/src/app/auth/capabilities.ts`:

```ts
/**
 * What a role is allowed to DO. Mirror of the server-side policies in
 * TransBrain.Api/Program.cs:134-137 - if the two ever disagree, this table is the wrong one:
 * the API decides, this only decides what is worth offering.
 */
export type Capability = 'read' | 'masterData.write' | 'dispatch.write' | 'tourStatus.write';

/** The realm roles defined in src/TransBrain.AppHost/realms/transbrain-realm.json. */
export type AppRole = 'admin' | 'disponent' | 'fahrer' | 'viewer';

/** The four functional areas of the application. */
export type Area = 'vehicles' | 'drivers' | 'orders' | 'tours';

const CAPABILITIES_BY_ROLE: Record<AppRole, readonly Capability[]> = {
    admin: ['read', 'masterData.write', 'dispatch.write', 'tourStatus.write'],
    disponent: ['read', 'dispatch.write', 'tourStatus.write'],
    fahrer: ['read', 'tourStatus.write'],
    viewer: ['read'],
};

/**
 * What a role NEEDS to see, which is a different question from what it is allowed to read.
 * Policies.Read covers all four roles, so a fahrer may read the vehicle master data - but they
 * do not work with it, so it is not on their home page or in their navigation. Typing the URL
 * still works; see the guards in auth/capability.guard.ts.
 */
const AREAS_BY_ROLE: Record<AppRole, readonly Area[]> = {
    admin: ['vehicles', 'drivers', 'orders', 'tours'],
    disponent: ['vehicles', 'drivers', 'orders', 'tours'],
    fahrer: ['tours'],
    viewer: ['vehicles', 'drivers', 'orders', 'tours'],
};

const ALL_ROLES = Object.keys(CAPABILITIES_BY_ROLE) as AppRole[];

/** Keeps the realm roles this app knows and drops the rest, rather than failing on a stranger. */
export function knownRoles(roles: readonly string[]): AppRole[] {
    return ALL_ROLES.filter((role) => roles.includes(role));
}

/**
 * Roles are UNIONED, not picked: a user holding two roles gets both sets. A user holding no
 * known role gets nothing, which matches the API's fail-closed SetFallbackPolicy.
 */
export function capabilitiesFor(roles: readonly string[]): ReadonlySet<Capability> {
    const granted = new Set<Capability>();
    for (const role of knownRoles(roles)) {
        for (const capability of CAPABILITIES_BY_ROLE[role]) {
            granted.add(capability);
        }
    }
    return granted;
}

export function areasFor(roles: readonly string[]): ReadonlySet<Area> {
    const relevant = new Set<Area>();
    for (const role of knownRoles(roles)) {
        for (const area of AREAS_BY_ROLE[role]) {
            relevant.add(area);
        }
    }
    return relevant;
}
```

- [x] **Step 4: Den SessionService schreiben**

`src/TransBrain.Web/src/app/auth/session.service.ts`:

```ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { Area, AppRole, Capability, areasFor, capabilitiesFor, knownRoles } from './capabilities';

interface RealmAccess {
    realm_access?: { roles?: unknown };
    name?: unknown;
    preferred_username?: unknown;
}

/**
 * The single place this SPA knows anything about who is signed in.
 *
 * checkAuth() runs exactly ONCE, driven by the App component, and every consumer shares that
 * one result through `ready`. Before this service existed, all nine screens called checkAuth()
 * themselves - four list components to decide whether to render at all, five forms purely to
 * hydrate the stored session before their first request went out.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
    private readonly oidc = inject(OidcSecurityService);

    private readonly authenticated = signal(false);
    private readonly claims = signal<RealmAccess | null>(null);
    private readonly checkError = signal<string | null>(null);

    readonly isAuthenticated = this.authenticated.asReadonly();
    readonly error = this.checkError.asReadonly();

    readonly roles = computed<AppRole[]>(() => {
        const raw = this.claims()?.realm_access?.roles;
        const values = Array.isArray(raw) ? raw.filter((r): r is string => typeof r === 'string') : [];
        return knownRoles(values);
    });

    readonly displayName = computed<string>(() => {
        const data = this.claims();
        if (typeof data?.name === 'string' && data.name.length > 0) {
            return data.name;
        }
        if (typeof data?.preferred_username === 'string') {
            return data.preferred_username;
        }
        return '';
    });

    readonly areas = computed<ReadonlySet<Area>>(() => areasFor(this.roles()));

    private readonly capabilities = computed<ReadonlySet<Capability>>(() => capabilitiesFor(this.roles()));

    /**
     * Emits once checkAuth() has settled. Guards and any request made before the first render
     * must wait on this, or angular-auth-oidc-client has not yet rehydrated the stored session
     * and the request goes out without a bearer token.
     *
     * shareReplay(1) is what makes "exactly once" true: later subscribers get the stored value
     * instead of triggering a second checkAuth().
     */
    readonly ready: Observable<boolean> = this.oidc.checkAuth().pipe(
        tap(({ isAuthenticated, userData }) => {
            this.authenticated.set(isAuthenticated);
            this.claims.set((userData as RealmAccess | null) ?? null);
        }),
        map(({ isAuthenticated }) => isAuthenticated),
        catchError(() => {
            // A checkAuth failure (Keycloak unreachable, a rejected code) must not leave callers
            // hanging on a never-emitting observable - guards would block the router forever.
            this.checkError.set('Could not verify your sign-in status. Please try signing in again.');
            this.authenticated.set(false);
            return of(false);
        }),
        shareReplay(1),
    );

    /** Called by the App component so the one checkAuth() runs as early as the app itself. */
    initialize(): void {
        this.ready.subscribe();
    }

    can(capability: Capability): boolean {
        return this.capabilities().has(capability);
    }

    hasRole(role: AppRole): boolean {
        return this.roles().includes(role);
    }

    login(): void {
        this.oidc.authorize();
    }

    logout(): void {
        this.oidc.logoff().subscribe();
    }
}
```

Falls Task 1 den Rückfallweg genommen hat: `claims` zusätzlich aus dem Access-Token speisen, indem im `tap` `this.claims.set({ ...userData, realm_access: { roles: realmRolesFromJwt(accessToken) } })` gesetzt wird — `accessToken` ist Teil der `LoginResponse`.

- [x] **Step 5: Die Startseite schreiben (Gerüst)**

`src/TransBrain.Web/src/app/home/home.component.ts`:

```ts
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { RouterLink } from '@angular/router';
import { Area } from '../auth/capabilities';
import { SessionService } from '../auth/session.service';

interface AreaTile {
    area: Area;
    title: string;
    description: string;
    route: string;
    addLabel: string;
    addRoute: string;
    addCapability: 'masterData.write' | 'dispatch.write';
}

const TILES: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'Vehicles',
        description: 'Fleet master data: plates, payload, inspections.',
        route: '/vehicles',
        addLabel: 'Add vehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'Drivers',
        description: 'Driver master data: licences, availability.',
        route: '/drivers',
        addLabel: 'Add driver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'Orders',
        description: 'Transport orders from pickup to delivery.',
        route: '/orders',
        addLabel: 'New order',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'Tours',
        description: 'Plan orders onto vehicles and follow execution.',
        route: '/tours',
        addLabel: 'Plan tour',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [MatButtonModule, MatCardModule, MatChipsModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1 data-testid="home-greeting">Welcome, {{ session.displayName() }}</h1>
            <mat-chip-set>
                @for (role of session.roles(); track role) {
                    <mat-chip data-testid="home-role-chip">{{ role }}</mat-chip>
                }
            </mat-chip-set>

            <section class="tiles">
                @for (tile of tiles; track tile.area) {
                    @if (session.areas().has(tile.area)) {
                        <mat-card [attr.data-testid]="'home-tile-' + tile.area">
                            <mat-card-title>{{ tile.title }}</mat-card-title>
                            <mat-card-content>{{ tile.description }}</mat-card-content>
                            <mat-card-actions>
                                <a mat-button [routerLink]="tile.route">Open</a>
                                @if (session.can(tile.addCapability)) {
                                    <a
                                        mat-raised-button
                                        [routerLink]="tile.addRoute"
                                        [attr.data-testid]="'home-tile-' + tile.area + '-add'"
                                        >{{ tile.addLabel }}</a
                                    >
                                }
                            </mat-card-actions>
                        </mat-card>
                    }
                }
            </section>
        } @else {
            @if (session.error(); as message) {
                <p data-testid="home-error">{{ message }}</p>
            }
            <button mat-raised-button data-testid="login" (click)="session.login()">Sign in</button>
        }
    `,
    styles: `
        .tiles {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
        }

        .tiles mat-card {
            flex: 1 1 16rem;
        }
    `,
})
export class HomeComponent {
    protected readonly session = inject(SessionService);
    protected readonly tiles = TILES;
}
```

- [x] **Step 6: Die Shell schreiben**

`src/TransBrain.Web/src/app/app.html`:

```html
<mat-toolbar>
    <span class="brand">TransBrain</span>
    @if (session.isAuthenticated()) {
        <nav>
            <a mat-button routerLink="/" data-testid="nav-home">Home</a>
            @if (session.areas().has('vehicles')) {
                <a mat-button routerLink="/vehicles" data-testid="nav-vehicles">Vehicles</a>
            }
            @if (session.areas().has('drivers')) {
                <a mat-button routerLink="/drivers" data-testid="nav-drivers">Drivers</a>
            }
            @if (session.areas().has('orders')) {
                <a mat-button routerLink="/orders" data-testid="nav-orders">Orders</a>
            }
            @if (session.areas().has('tours')) {
                <a mat-button routerLink="/tours" data-testid="nav-tours">Tours</a>
            }
        </nav>
        <span class="spacer"></span>
        <span data-testid="nav-user">{{ session.displayName() }}</span>
        <button mat-button data-testid="logout" (click)="session.logout()">Sign out</button>
    }
</mat-toolbar>

<main>
    <router-outlet />
</main>
```

`src/TransBrain.Web/src/app/app.ts`:

```ts
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SessionService } from './auth/session.service';

@Component({
    imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule],
    selector: 'app-root',
    styleUrl: './app.scss',
    templateUrl: './app.html',
})
export class App {
    protected readonly session = inject(SessionService);

    constructor() {
        // The one checkAuth() of the whole SPA. It must run from a component that is mounted at
        // the OIDC redirectUrl (the origin, i.e. path ''), which App always is - see the comment
        // in app.routes.ts about why moving it broke the callback once before.
        this.session.initialize();
    }
}
```

`src/TransBrain.Web/src/app/app.scss` (die Datei ist bisher leer):

```scss
.spacer {
    flex: 1 1 auto;
}

.brand {
    font-weight: 600;
    margin-right: 1.5rem;
}

main {
    padding: 1rem;
}
```

- [x] **Step 7: Routing umstellen**

In `src/TransBrain.Web/src/app/app.routes.ts` die Lade-Funktion ergänzen und den Kopf der Routenliste ersetzen. Die bisherigen Zeilen 13-23 (Kommentarblock, `{ path: '', ... }`, der „Two canonical URLs"-Kommentar und `{ path: 'vehicles', ... }`) werden zu:

```ts
const loadHome = () => import('./home/home.component').then((m) => m.HomeComponent);
```

```ts
export const routes: Routes = [
    // angular-auth-oidc-client's checkAuth() detects the OIDC callback by comparing the current
    // URL against the configured redirectUrl (the origin, i.e. path '/'). A `redirectTo` here
    // would move the browser off '/' before checkAuth() runs, and the library would then discard
    // a valid authorization code because the path no longer matches. Home is a real component
    // mounted at '', so the callback is processed where the library expects it.
    { path: '', loadComponent: loadHome },
    { path: 'vehicles', loadComponent: loadVehicleList },
```

Der Rest der Liste bleibt unverändert. Die frühere Dublette `''` → `VehicleListComponent` und der Kommentar, der sie als „stopgap, not a design choice" markierte, entfallen ersatzlos — die Konsolidierung, die er ankündigte, ist genau das hier.

- [x] **Step 8: Den bestehenden Unit-Test anpassen**

`app.spec.ts` rendert heute die `App`-Komponente. Die braucht jetzt `SessionService`, der wiederum `OidcSecurityService` braucht. Die Datei prüfen und, falls sie nur auf Erzeugung testet, den Auth-Provider ergänzen:

```ts
import { provideAuth } from 'angular-auth-oidc-client';
import { authConfig } from './auth/auth.config';
```

und in `TestBed.configureTestingModule({ providers: [...] })` `provideAuth(authConfig)` sowie `provideHttpClient()` aufnehmen.

- [x] **Step 9: Build prüfen**

```bash
cd src/TransBrain.Web && npm run build
```

Erwartet: erfolgreich, keine TypeScript-Fehler.

- [x] **Step 10: Die e2e-Tests laufen lassen**

```bash
cd src/TransBrain.Web && npx playwright test e2e/home.spec.ts
```

Erwartet: alle sechs Tests grün.

Die bestehenden Specs (`vehicles.spec.ts` und die anderen) scheitern ab jetzt, weil sie nach der Anmeldung auf `/` die Überschrift „Vehicles" erwarten. Das ist bekannt und wird in Task 6 behoben — hier **nicht** nebenbei reparieren.

- [x] **Step 11: Committen**

```bash
git add src/TransBrain.Web/src/app/auth/capabilities.ts \
        src/TransBrain.Web/src/app/auth/session.service.ts \
        src/TransBrain.Web/src/app/home/home.component.ts \
        src/TransBrain.Web/src/app/app.ts \
        src/TransBrain.Web/src/app/app.html \
        src/TransBrain.Web/src/app/app.scss \
        src/TransBrain.Web/src/app/app.routes.ts \
        src/TransBrain.Web/src/app/app.spec.ts \
        src/TransBrain.Web/e2e/login.ts \
        src/TransBrain.Web/e2e/home.spec.ts
git commit -m "feat(web): add a role-aware home page and navigation shell"
```

---

**Ausführung am 2026-09-01 — zwei Ergänzungen gegenüber dem Planwortlaut:**

- `src/TransBrain.Web/angular.json`: die Warnschwelle des Initial-Bundles von `500kB` auf `600kB` angehoben. Die Shell legt `MatToolbarModule` und `MatButtonModule` dauerhaft ins Root-Bundle und überschreitet die alte Schwelle um 3 kB. Eine Warnung, die ab jetzt bei jedem Build feuert, entwertet Warnungen.
- `app.spec.ts` bekommt einen `SessionServiceStub` statt echter Provider. Mit dem echten Dienst würde `App`s Konstruktor `checkAuth()` gegen ein laufendes Keycloak feuern — ein Unit-Test, der einen Realm braucht, ist keiner.

Ergebnis: `npm run build` warnungsfrei, `npm test` grün, `npx playwright test e2e/home.spec.ts` 6/6 grün. Die bestehenden Specs scheitern jetzt erwartungsgemäß — Task 6.

### Task 3: Vue — Capability-Schicht, Shell und Startseiten-Gerüst

Das Gegenstück zu Task 2. Gleiche Test-IDs, gleiches Verhalten, Vue-idiomatische Umsetzung.

**Files:**
- Create: `src/TransBrain.VueWeb/src/auth/capabilities.ts`
- Create: `src/TransBrain.VueWeb/src/views/Home.vue`
- Create: `src/TransBrain.VueWeb/e2e/login.ts`
- Create: `src/TransBrain.VueWeb/e2e/home.spec.ts`
- Modify: `src/TransBrain.VueWeb/src/stores/auth.ts`
- Modify: `src/TransBrain.VueWeb/src/App.vue`
- Modify: `src/TransBrain.VueWeb/src/main.ts:20-42`

**Interfaces:**
- Consumes: aus Task 1 die Rollenquelle `user.profile.realm_access.roles`
- Produces: dieselben Typen und Funktionen wie Task 2 (`Capability`, `AppRole`, `Area`, `knownRoles`, `capabilitiesFor`, `areasFor`); Store-Mitglieder `roles`, `displayName`, `can(c)`, `hasRole(r)`, `logout()`; `signIn(page, role)` aus `e2e/login.ts`

- [x] **Step 1: Den fehlschlagenden e2e-Test schreiben**

`src/TransBrain.VueWeb/e2e/login.ts` — **identisch** zur Angular-Fassung aus Task 2, Step 1. Die Datei wortgleich anlegen (die beiden Suites teilen keinen Code, wie schon bei den bestehenden Specs).

`src/TransBrain.VueWeb/e2e/home.spec.ts` — **identisch** zur Angular-Fassung aus Task 2, Step 1.

- [x] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Voraussetzung: `dotnet run --project src/TransBrain.AppHost` läuft — er bringt den Dev-Server auf Port 4300 selbst mit.

```bash
cd src/TransBrain.VueWeb && npx playwright test e2e/home.spec.ts
```

Erwartet: die fünf angemeldeten Tests scheitern mit Timeout auf `home-greeting`.

- [x] **Step 3: Die Capability-Tabelle schreiben**

`src/TransBrain.VueWeb/src/auth/capabilities.ts` — inhaltlich identisch zu `src/TransBrain.Web/src/app/auth/capabilities.ts` aus Task 2, Step 3. Die Datei enthält keine Angular-Abhängigkeit; sie kann eins zu eins übernommen werden, inklusive aller Kommentare. Nur der Verweis auf die Guards am Ende des `AREAS_BY_ROLE`-Kommentars lautet hier `see the router guard in main.ts`.

- [x] **Step 4: Den auth-Store erweitern**

`src/TransBrain.VueWeb/src/stores/auth.ts` vollständig ersetzen:

```ts
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import type { User } from 'oidc-client-ts';
import { userManager } from '../auth/userManager';
import { areasFor, capabilitiesFor, knownRoles, type AppRole, type Area, type Capability } from '../auth/capabilities';

export const useAuthStore = defineStore('auth', () => {
    const user = ref<User | null>(null);
    const isAuthenticated = ref(false);

    const roles = computed<AppRole[]>(() => {
        const raw = (user.value?.profile as { realm_access?: { roles?: unknown } } | undefined)?.realm_access?.roles;
        const values = Array.isArray(raw) ? raw.filter((r): r is string => typeof r === 'string') : [];
        return knownRoles(values);
    });

    const displayName = computed<string>(() => {
        const profile = user.value?.profile;
        if (typeof profile?.name === 'string' && profile.name.length > 0) {
            return profile.name;
        }
        return typeof profile?.preferred_username === 'string' ? profile.preferred_username : '';
    });

    const areas = computed<ReadonlySet<Area>>(() => areasFor(roles.value));
    const capabilities = computed<ReadonlySet<Capability>>(() => capabilitiesFor(roles.value));

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

    /**
     * RP-initiated logout. Without it there is no way to change role in a running browser other
     * than a private window, which makes a role-aware UI untestable by hand. The realm allows
     * the post-logout redirect for both frontend ports (transbrain-realm.json).
     */
    async function logout(): Promise<void> {
        await userManager.signoutRedirect();
    }

    function can(capability: Capability): boolean {
        return capabilities.value.has(capability);
    }

    function hasRole(role: AppRole): boolean {
        return roles.value.includes(role);
    }

    return {
        user,
        isAuthenticated,
        roles,
        displayName,
        areas,
        load,
        login,
        completeLogin,
        logout,
        can,
        hasRole,
    };
});
```

- [x] **Step 5: Die Startseite schreiben (Gerüst)**

`src/TransBrain.VueWeb/src/views/Home.vue`:

```vue
<script setup lang="ts">
import { useAuthStore } from '../stores/auth';
import type { Area, Capability } from '../auth/capabilities';

interface AreaTile {
    area: Area;
    title: string;
    description: string;
    route: string;
    addLabel: string;
    addRoute: string;
    addCapability: Capability;
}

const tiles: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'Vehicles',
        description: 'Fleet master data: plates, payload, inspections.',
        route: '/vehicles',
        addLabel: 'Add vehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'Drivers',
        description: 'Driver master data: licences, availability.',
        route: '/drivers',
        addLabel: 'Add driver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'Orders',
        description: 'Transport orders from pickup to delivery.',
        route: '/orders',
        addLabel: 'New order',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'Tours',
        description: 'Plan orders onto vehicles and follow execution.',
        route: '/tours',
        addLabel: 'Plan tour',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

const auth = useAuthStore();
</script>

<template>
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1 data-testid="home-greeting">Welcome, {{ auth.displayName }}</h1>
            <v-chip v-for="role in auth.roles" :key="role" data-testid="home-role-chip">{{ role }}</v-chip>

            <v-row class="mt-4">
                <template v-for="tile in tiles" :key="tile.area">
                    <v-col v-if="auth.areas.has(tile.area)" cols="12" md="3">
                        <v-card :data-testid="`home-tile-${tile.area}`">
                            <v-card-title>{{ tile.title }}</v-card-title>
                            <v-card-text>{{ tile.description }}</v-card-text>
                            <v-card-actions>
                                <v-btn :to="tile.route">Open</v-btn>
                                <v-btn
                                    v-if="auth.can(tile.addCapability)"
                                    :to="tile.addRoute"
                                    :data-testid="`home-tile-${tile.area}-add`"
                                    >{{ tile.addLabel }}</v-btn
                                >
                            </v-card-actions>
                        </v-card>
                    </v-col>
                </template>
            </v-row>
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
</template>
```

- [x] **Step 6: Die Shell schreiben**

`src/TransBrain.VueWeb/src/App.vue`:

```vue
<script setup lang="ts">
import { onMounted } from 'vue';
import { useAuthStore } from './stores/auth';

const auth = useAuthStore();

// The one place the stored session is hydrated. Every view used to call auth.load() itself;
// the router guard in main.ts awaits the same call, so a directly opened URL is covered too.
onMounted(async () => {
    await auth.load();
});
</script>

<template>
    <v-app>
        <v-app-bar>
            <v-app-bar-title>TransBrain</v-app-bar-title>
            <template v-if="auth.isAuthenticated">
                <v-btn to="/" data-testid="nav-home">Home</v-btn>
                <v-btn v-if="auth.areas.has('vehicles')" to="/vehicles" data-testid="nav-vehicles">Vehicles</v-btn>
                <v-btn v-if="auth.areas.has('drivers')" to="/drivers" data-testid="nav-drivers">Drivers</v-btn>
                <v-btn v-if="auth.areas.has('orders')" to="/orders" data-testid="nav-orders">Orders</v-btn>
                <v-btn v-if="auth.areas.has('tours')" to="/tours" data-testid="nav-tours">Tours</v-btn>
                <v-spacer />
                <span data-testid="nav-user">{{ auth.displayName }}</span>
                <v-btn data-testid="logout" @click="auth.logout()">Sign out</v-btn>
            </template>
        </v-app-bar>
        <v-main>
            <router-view />
        </v-main>
    </v-app>
</template>
```

- [x] **Step 7: Routing umstellen**

In `src/TransBrain.VueWeb/src/main.ts` den Import ergänzen:

```ts
import Home from './views/Home.vue';
```

und die ersten beiden Routen samt des „Two canonical URLs"-Kommentars ersetzen durch:

```ts
        { path: '/', component: Home },
        { path: '/vehicles', component: VehicleList },
```

Der Rest der Liste bleibt unverändert, inklusive `{ path: '/callback', component: AuthCallback }`.

- [x] **Step 8: Build prüfen**

```bash
cd src/TransBrain.VueWeb && npm run build
```

Erwartet: erfolgreich; `vue-tsc` meldet keine Typfehler.

- [x] **Step 9: Die e2e-Tests laufen lassen**

```bash
cd src/TransBrain.VueWeb && npx playwright test e2e/home.spec.ts
```

Erwartet: alle sechs Tests grün. Die bestehenden Specs scheitern jetzt ebenfalls; das behebt Task 10.

- [x] **Step 10: Committen**

```bash
git add src/TransBrain.VueWeb/src/auth/capabilities.ts \
        src/TransBrain.VueWeb/src/stores/auth.ts \
        src/TransBrain.VueWeb/src/views/Home.vue \
        src/TransBrain.VueWeb/src/App.vue \
        src/TransBrain.VueWeb/src/main.ts \
        src/TransBrain.VueWeb/e2e/login.ts \
        src/TransBrain.VueWeb/e2e/home.spec.ts
git commit -m "feat(vueweb): add a role-aware home page and navigation shell"
```

---

**Ausführung am 2026-09-01 — eine Ergänzung gegenüber dem Planwortlaut:**

`signIn()` wartet in **beiden** Suiten jetzt explizit mit 30 s auf `#username`. Beobachtet: beim allerersten Lauf gegen einen kalten Vite-Dev-Server scheiterte der erste authentifizierte Test am 5-s-Standard-Timeout (39,5 s Gesamtlaufzeit gegen 10,5 s bei warmem Server); die übrigen fünf liefen durch. Eine Umleitung zu einem externen IdP ist legitim langsamer als jede Zusicherung gegen die eigenen Seiten — die Frist gehört an diese eine Stelle, nicht als Retry über die ganze Suite.

Ergebnis: `npm run build` grün (`vue-tsc` sauber; die Chunk-Größen-Warnung ist vorbestehend, Vuetify wird komplett gebündelt), `npx playwright test e2e/home.spec.ts` 6/6 grün in beiden Frontends.


### Task 4: Angular — Route-Guards

**Files:**
- Create: `src/TransBrain.Web/src/app/auth/capability.guard.ts`
- Modify: `src/TransBrain.Web/src/app/app.routes.ts`
- Modify: `src/TransBrain.Web/e2e/home.spec.ts`

**Interfaces:**
- Consumes: `SessionService` (Task 2), `Capability` (Task 2)
- Produces: `requireAuthentication: CanActivateFn`, `requireCapability(capability: Capability): CanActivateFn`

- [x] **Step 1: Die fehlschlagenden Tests schreiben**

An `src/TransBrain.Web/e2e/home.spec.ts` anhängen:

```ts
test('viewerUser_openingTheVehicleForm_isSentBackToHome', async ({ page }) => {
    await signIn(page, 'viewer');

    await page.goto('/vehicles/new');

    // Redirected, not 403'd: a user who typed a URL they cannot use lands on their own home.
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page).toHaveURL(/\/$/);
});

test('fahrerUser_openingTheVehicleList_isLetThrough', async ({ page }) => {
    await signIn(page, 'fahrer');

    await page.goto('/vehicles');

    // Hidden from the navigation is not the same as forbidden: Policies.Read covers a fahrer,
    // so inventing a client-side block here would be a second, disagreeing truth.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
});

test('disponentUser_openingTheOrderForm_isLetThrough', async ({ page }) => {
    await signIn(page, 'dispo');

    await page.goto('/orders/new');

    await expect(page.getByTestId('order-save')).toBeVisible();
});

test('unauthenticatedVisitor_openingTheTourList_isSentBackToHome', async ({ page }) => {
    await page.goto('/tours');

    await expect(page.getByTestId('login')).toBeVisible();
});
```

- [x] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.Web && npx playwright test e2e/home.spec.ts
```

Erwartet: `viewerUser_openingTheVehicleForm_isSentBackToHome` und `unauthenticatedVisitor_openingTheTourList_isSentBackToHome` scheitern (ungeschützte Routen lassen beide durch). Die anderen beiden laufen bereits grün und müssen das nach Step 3 immer noch tun.

- [x] **Step 3: Die Guards schreiben**

`src/TransBrain.Web/src/app/auth/capability.guard.ts`:

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { Capability } from './capabilities';
import { SessionService } from './session.service';

/**
 * Both guards wait on SessionService.ready before deciding. Without that wait the router runs
 * before checkAuth() has rehydrated the stored session, and a directly opened URL - a reload, a
 * bookmark - would be judged as signed-out and bounced to '/'.
 */
export const requireAuthentication: CanActivateFn = () => {
    const session = inject(SessionService);
    const router = inject(Router);

    return session.ready.pipe(map(() => (session.isAuthenticated() ? true : router.createUrlTree(['/']))));
};

/**
 * Guards a route behind one capability. A user who fails goes to their own home page rather
 * than to a 403 screen: they are signed in and the app has somewhere useful to put them.
 *
 * Note what is NOT guarded this way - the four list routes and /tours/:id take
 * requireAuthentication only. The API lets every role read them (Policies.Read), so a client
 * side block would be stricter than the server for no reason. Hiding a tile means "you do not
 * need this", not "you may not have this".
 */
export function requireCapability(capability: Capability): CanActivateFn {
    return () => {
        const session = inject(SessionService);
        const router = inject(Router);

        return session.ready.pipe(
            map(() => {
                if (!session.isAuthenticated()) {
                    return router.createUrlTree(['/']);
                }
                return session.can(capability) ? true : router.createUrlTree(['/']);
            }),
        );
    };
}
```

- [x] **Step 4: Die Guards auf die Routen legen**

`src/TransBrain.Web/src/app/app.routes.ts` — Import ergänzen:

```ts
import { requireAuthentication, requireCapability } from './auth/capability.guard';
```

und die Routenliste ab `vehicles` so setzen:

```ts
    { path: 'vehicles', loadComponent: loadVehicleList, canActivate: [requireAuthentication] },
    // 'new' must be registered before ':id' - the router matches path segments in order, and a
    // ':id' route registered first would swallow '/vehicles/new' by treating "new" as an id.
    { path: 'vehicles/new', loadComponent: loadVehicleForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'vehicles/:id', loadComponent: loadVehicleForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'drivers', loadComponent: loadDriverList, canActivate: [requireAuthentication] },
    { path: 'drivers/new', loadComponent: loadDriverForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'drivers/:id', loadComponent: loadDriverForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'orders', loadComponent: loadOrderList, canActivate: [requireAuthentication] },
    { path: 'orders/new', loadComponent: loadOrderForm, canActivate: [requireCapability('dispatch.write')] },
    { path: 'orders/:id', loadComponent: loadOrderForm, canActivate: [requireCapability('dispatch.write')] },
    { path: 'tours', loadComponent: loadTourList, canActivate: [requireAuthentication] },
    { path: 'tours/new', loadComponent: loadTourForm, canActivate: [requireCapability('dispatch.write')] },
    // Not guarded by a capability: the fahrer must reach this to start their tour, and a viewer
    // may look. The start/complete/assign buttons inside are gated individually.
    { path: 'tours/:id', loadComponent: loadTourDetail, canActivate: [requireAuthentication] },
```

- [x] **Step 5: Tests laufen lassen**

```bash
cd src/TransBrain.Web && npm run build && npx playwright test e2e/home.spec.ts
```

Erwartet: Build erfolgreich, alle zehn Tests grün.

- [x] **Step 6: Committen**

```bash
git add src/TransBrain.Web/src/app/auth/capability.guard.ts \
        src/TransBrain.Web/src/app/app.routes.ts \
        src/TransBrain.Web/e2e/home.spec.ts
git commit -m "feat(web): guard the write routes behind capabilities"
```

---

**Ausführung am 2026-09-01 — eine Ergänzung gegenüber dem Planwortlaut:**

Beide Suiten bekommen ein `globalSetup` (`e2e/warm-up.ts`, in beiden Frontends gleich): es lädt einmal `/`, klickt „Sign in" und wartet auf Keycloaks Formular, bevor der erste Test läuft. Grund: der Dev-Server kompiliert auf Anfrage und Keycloak rendert sein Theme erstmalig — gemessen über 30 s direkt nach einer Quelltextänderung, gegen unter 2 s bei jeder späteren Anmeldung. Diese Kosten trafen bisher vollständig den ersten authentifizierten Test, der dadurch scheiterte und beim Wiederholungslauf grün war. Aufwärmen ist der Fristerhöhung vorzuziehen: ein echter Fehler scheitert weiterhin in Sekunden.

Verifiziert unter genau der Bedingung, die vorher rot war (`touch` auf eine Quelldatei, danach voller Lauf): beide Suiten 10/10.

### Task 5: Angular — Kennzahlen und Arbeitslisten auf der Startseite

**Files:**
- Modify: `src/TransBrain.Web/src/app/vehicles/vehicle.service.ts:41-44`
- Modify: `src/TransBrain.Web/src/app/drivers/driver.service.ts:41-44`
- Modify: `src/TransBrain.Web/src/app/home/home.component.ts`
- Modify: `src/TransBrain.Web/e2e/home.spec.ts`

**Interfaces:**
- Consumes: `SessionService` (Task 2); `VehicleService.list`, `DriverService.list`, `OrderService.list`, `TourService.list` (bestehend)
- Produces: `VehicleService.list(pageSize?: number, status?: string | null)`, `DriverService.list(pageSize?: number, status?: string | null)` — beide rückwärtskompatibel, der bestehende Aufruf `list(LIST_PAGE_SIZE)` bleibt gültig

- [x] **Step 1: Die fehlschlagenden Tests schreiben**

An `src/TransBrain.Web/e2e/home.spec.ts` anhängen:

```ts
test('adminUser_onHome_seesEveryKpiAndTheDispatchWorkList', async ({ page }) => {
    await signIn(page, 'admin');

    // The database starts empty on every AppHost run, so the counts are 0 - which is exactly
    // what proves the block rendered a number rather than an error or a spinner.
    await expect(page.getByTestId('home-kpi-vehicles-available')).toHaveText('0');
    await expect(page.getByTestId('home-kpi-vehicles-workshop')).toHaveText('0');
    await expect(page.getByTestId('home-kpi-drivers-available')).toHaveText('0');
    await expect(page.getByTestId('home-kpi-orders-draft')).toHaveText('0');
    await expect(page.getByTestId('home-kpi-tours-today')).toHaveText('0');
    await expect(page.getByTestId('home-draft-orders')).toBeVisible();
    await expect(page.getByTestId('home-plan-tour')).toBeVisible();
    await expect(page.getByTestId('home-my-tours')).toBeHidden();
});

test('viewerUser_onHome_seesKpisButNoWorkList', async ({ page }) => {
    await signIn(page, 'viewer');

    await expect(page.getByTestId('home-kpi-orders-draft')).toHaveText('0');
    // dispatch.write is what carries the work list, and a viewer does not have it.
    await expect(page.getByTestId('home-draft-orders')).toBeHidden();
    await expect(page.getByTestId('home-my-tours')).toBeHidden();
});

test('fahrerUser_onHome_seesOnlyTheOwnTourBlock', async ({ page }) => {
    await signIn(page, 'fahrer');

    await expect(page.getByTestId('home-my-tours')).toBeVisible();
    await expect(page.getByTestId('home-kpi-tours-today')).toHaveText('0');
    await expect(page.getByTestId('home-draft-orders')).toBeHidden();
    await expect(page.getByTestId('home-kpi-vehicles-available')).toBeHidden();
    await expect(page.getByTestId('home-kpi-drivers-available')).toBeHidden();
    await expect(page.getByTestId('home-kpi-orders-draft')).toBeHidden();
});
```

- [x] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.Web && npx playwright test e2e/home.spec.ts
```

Erwartet: die drei neuen Tests scheitern mit Timeout auf `home-kpi-*` bzw. `home-my-tours`.

- [x] **Step 3: Den `status`-Parameter in die beiden Services aufnehmen**

`src/TransBrain.Web/src/app/vehicles/vehicle.service.ts`, `list()` ersetzen:

```ts
    /**
     * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY vehicle to
     * choose from - the tour form - has to ask for more, or a recently added one simply is not
     * in the list and cannot be chosen. Capped at 100 by the API; a fleet larger than that
     * needs a searchable picker, not a bigger page.
     * @param status Filters server-side (VehicleEndpoints.cs). The home page asks with
     * pageSize 1 and reads only totalCount, so a fleet count costs one row over the wire.
     */
    list(pageSize?: number, status?: string | null): Observable<PagedResult<Vehicle>> {
        let params = new HttpParams();
        if (pageSize) {
            params = params.set('pageSize', String(pageSize));
        }
        // An omitted status must not become the string "null" in the query string - the API
        // rejects an unknown status with a 400 rather than ignoring it.
        if (status) {
            params = params.set('status', status);
        }
        return this.http.get<PagedResult<Vehicle>>('/api/vehicles', { params });
    }
```

`src/TransBrain.Web/src/app/drivers/driver.service.ts`, `list()` genauso ersetzen — gleicher Rumpf, `Driver` statt `Vehicle`, `/api/drivers` statt `/api/vehicles`, und im Kommentar „driver" statt „vehicle".

- [x] **Step 4: Die Blöcke in die Startseite einbauen**

`src/TransBrain.Web/src/app/home/home.component.ts` — Importe ergänzen:

```ts
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { Driver, DriverService } from '../drivers/driver.service';
import { Order, OrderService } from '../orders/order.service';
import { Tour, TourService } from '../tours/tour.service';
import { Vehicle, VehicleService } from '../vehicles/vehicle.service';
```

`MatTableModule` in `imports` aufnehmen. Oberhalb der Klasse ergänzen:

```ts
/** One row is enough when only totalCount is read. */
const COUNT_ONLY = 1;
/** Five rows fit the block; the rest is one click away on /orders. */
const DRAFT_PREVIEW_SIZE = 5;

function today(): string {
    // The API takes a DateOnly, so an ISO date without a time part. Local date, not UTC: a tour
    // planned for "today" is today in the depot's timezone, and toISOString() would roll over a
    // day early for anyone east of Greenwich after their evening.
    const now = new Date();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
}
```

Im Template, direkt nach dem `mat-chip-set` und vor `<section class="tiles">`:

```html
            <section class="kpis">
                @if (session.areas().has('vehicles')) {
                    <mat-card>
                        <mat-card-title>Vehicles</mat-card-title>
                        <mat-card-content>
                            @if (vehicleError(); as message) {
                                <p data-testid="home-kpi-vehicles-error">{{ message }}</p>
                            } @else {
                                <p>Available: <strong data-testid="home-kpi-vehicles-available">{{ vehiclesAvailable() }}</strong></p>
                                <p>In workshop: <strong data-testid="home-kpi-vehicles-workshop">{{ vehiclesInWorkshop() }}</strong></p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                @if (session.areas().has('drivers')) {
                    <mat-card>
                        <mat-card-title>Drivers</mat-card-title>
                        <mat-card-content>
                            @if (driverError(); as message) {
                                <p data-testid="home-kpi-drivers-error">{{ message }}</p>
                            } @else {
                                <p>Available: <strong data-testid="home-kpi-drivers-available">{{ driversAvailable() }}</strong></p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                @if (session.areas().has('orders')) {
                    <mat-card>
                        <mat-card-title>Orders</mat-card-title>
                        <mat-card-content>
                            @if (orderError(); as message) {
                                <p data-testid="home-kpi-orders-error">{{ message }}</p>
                            } @else {
                                <p>In draft: <strong data-testid="home-kpi-orders-draft">{{ ordersInDraft() }}</strong></p>
                            }
                        </mat-card-content>
                    </mat-card>
                }
                <mat-card>
                    <mat-card-title>Tours today</mat-card-title>
                    <mat-card-content>
                        @if (tourError(); as message) {
                            <p data-testid="home-kpi-tours-error">{{ message }}</p>
                        } @else {
                            <p><strong data-testid="home-kpi-tours-today">{{ toursToday() }}</strong></p>
                        }
                    </mat-card-content>
                </mat-card>
            </section>

            @if (session.can('dispatch.write')) {
                <section data-testid="home-draft-orders">
                    <h2>Orders awaiting a tour</h2>
                    @if (draftOrdersError(); as message) {
                        <p data-testid="home-draft-orders-error">{{ message }}</p>
                    } @else {
                        <table mat-table [dataSource]="draftOrders()">
                            <ng-container matColumnDef="orderNumber">
                                <th mat-header-cell *matHeaderCellDef>Order</th>
                                <td mat-cell *matCellDef="let o" data-testid="home-draft-order-row">{{ o.orderNumber }}</td>
                            </ng-container>
                            <ng-container matColumnDef="route">
                                <th mat-header-cell *matHeaderCellDef>Route</th>
                                <td mat-cell *matCellDef="let o">{{ o.consignor.city }} → {{ o.consignee.city }}</td>
                            </ng-container>
                            <ng-container matColumnDef="actions">
                                <th mat-header-cell *matHeaderCellDef>Actions</th>
                                <td mat-cell *matCellDef="let o">
                                    <a mat-button [routerLink]="['/orders', o.id]" data-testid="home-draft-order-open">Open</a>
                                </td>
                            </ng-container>
                            <tr mat-header-row *matHeaderRowDef="draftColumns"></tr>
                            <tr mat-row *matRowDef="let row; columns: draftColumns"></tr>
                        </table>
                        <!-- No per-row "Plan" button: an order is put onto a tour by the picker on
                             /tours/:id, and no endpoint creates a tour from an order. The honest
                             action here is to start a tour and assign from there. -->
                        <a mat-raised-button routerLink="/tours/new" data-testid="home-plan-tour">Plan a tour</a>
                    }
                </section>
            }

            @if (session.hasRole('fahrer')) {
                <section data-testid="home-my-tours">
                    <h2>My tours today</h2>
                    @if (myToursError(); as message) {
                        <p data-testid="home-my-tours-error">{{ message }}</p>
                    }
                    @for (tour of myTours(); track tour.id) {
                        <mat-card data-testid="home-my-tour-row">
                            <mat-card-title>{{ tour.vehicleLicensePlate }} — {{ tour.status }}</mat-card-title>
                            <mat-card-actions>
                                <a mat-button [routerLink]="['/tours', tour.id]">Open</a>
                                @if (tour.status === 'Planned') {
                                    <button mat-raised-button data-testid="home-my-tour-start" (click)="startTour(tour)">
                                        Start tour
                                    </button>
                                }
                                @if (tour.status === 'InProgress') {
                                    <button mat-raised-button data-testid="home-my-tour-complete" (click)="completeTour(tour)">
                                        Complete tour
                                    </button>
                                }
                            </mat-card-actions>
                        </mat-card>
                    }
                </section>
            }
```

In der Klasse ergänzen:

```ts
    private readonly vehicles = inject(VehicleService);
    private readonly drivers = inject(DriverService);
    private readonly orders = inject(OrderService);
    private readonly tours = inject(TourService);

    protected readonly draftColumns = ['orderNumber', 'route', 'actions'];

    protected readonly vehiclesAvailable = signal(0);
    protected readonly vehiclesInWorkshop = signal(0);
    protected readonly driversAvailable = signal(0);
    protected readonly ordersInDraft = signal(0);
    protected readonly toursToday = signal(0);
    protected readonly draftOrders = signal<Order[]>([]);
    protected readonly myTours = signal<Tour[]>([]);

    // One error signal per block, not one for the page: a failing vehicle count must not blank
    // the work list next to it. Same separation the lists already make between errorMessage and
    // actionError.
    protected readonly vehicleError = signal<string | null>(null);
    protected readonly driverError = signal<string | null>(null);
    protected readonly orderError = signal<string | null>(null);
    protected readonly tourError = signal<string | null>(null);
    protected readonly draftOrdersError = signal<string | null>(null);
    protected readonly myToursError = signal<string | null>(null);

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.loadBlocks();
            }
        });
    }

    protected startTour(tour: Tour): void {
        this.myToursError.set(null);
        this.tours.start(tour.id).subscribe({
            next: () => this.loadMyTours(),
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, 'The tour could not be started.')),
        });
    }

    protected completeTour(tour: Tour): void {
        this.myToursError.set(null);
        this.tours.complete(tour.id).subscribe({
            next: () => this.loadMyTours(),
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, 'The tour could not be completed.')),
        });
    }

    private loadBlocks(): void {
        const areas = this.session.areas();

        if (areas.has('vehicles')) {
            this.vehicles.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.vehiclesAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(this.describe(error, 'The vehicle counts could not be loaded.')),
            });
            this.vehicles.list(COUNT_ONLY, 'InWorkshop').subscribe({
                next: (page) => this.vehiclesInWorkshop.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.vehicleError.set(this.describe(error, 'The vehicle counts could not be loaded.')),
            });
        }

        if (areas.has('drivers')) {
            this.drivers.list(COUNT_ONLY, 'Available').subscribe({
                next: (page) => this.driversAvailable.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.driverError.set(this.describe(error, 'The driver counts could not be loaded.')),
            });
        }

        if (areas.has('orders')) {
            this.orders.list('Draft', COUNT_ONLY).subscribe({
                next: (page) => this.ordersInDraft.set(page.totalCount),
                error: (error: HttpErrorResponse) =>
                    this.orderError.set(this.describe(error, 'The order counts could not be loaded.')),
            });
        }

        // Every role sees this one - for a fahrer the API narrows it to their own tours, see
        // ListToursQueryHandler. That is also why the frontend never needs to know its own
        // driverId.
        this.tours.list({ tourDate: today() }).subscribe({
            next: (page) => {
                this.toursToday.set(page.totalCount);
                if (this.session.hasRole('fahrer')) {
                    this.myTours.set(page.items);
                }
            },
            error: (error: HttpErrorResponse) =>
                this.tourError.set(this.describe(error, "Today's tours could not be loaded.")),
        });

        if (this.session.can('dispatch.write')) {
            this.orders.list('Draft', DRAFT_PREVIEW_SIZE).subscribe({
                next: (page) => this.draftOrders.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.draftOrdersError.set(this.describe(error, 'The draft orders could not be loaded.')),
            });
        }
    }

    private loadMyTours(): void {
        this.tours.list({ tourDate: today() }).subscribe({
            next: (page) => {
                this.myTours.set(page.items);
                this.toursToday.set(page.totalCount);
            },
            error: (error: HttpErrorResponse) =>
                this.myToursError.set(this.describe(error, "Today's tours could not be reloaded.")),
        });
    }

    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
```

Hinweis zur Seitengröße der Touren: `TourService.list` nimmt keinen `pageSize` an — `TourFilters` kennt nur `tourDate`, `vehicleId` und `driverId`. Der API-Standard von 20 Zeilen bleibt also stehen. Für die Kennzahl ist das ohne Belang, weil `totalCount` unabhängig von der Seitengröße stimmt, und ein Fahrertag hat keine 20 Touren.

Die `.kpis`-Regel zu den `styles` hinzufügen:

```scss
        .kpis {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
            margin-bottom: 1.5rem;
        }
```

- [x] **Step 5: Tests laufen lassen**

```bash
cd src/TransBrain.Web && npm run build && npx playwright test e2e/home.spec.ts
```

Erwartet: Build erfolgreich, alle dreizehn Tests grün.

- [x] **Step 6: Committen**

```bash
git add src/TransBrain.Web/src/app/home/home.component.ts \
        src/TransBrain.Web/src/app/vehicles/vehicle.service.ts \
        src/TransBrain.Web/src/app/drivers/driver.service.ts \
        src/TransBrain.Web/e2e/home.spec.ts
git commit -m "feat(web): add role-specific counts and work lists to the home page"
```

---

**Ausführung am 2026-09-01 — zwei Abweichungen vom Planwortlaut:**

- Die Kennzahlen werden gegen `/^\d+$/` geprüft, nicht gegen `'0'`. Die Datenbank startet zwar leer, aber `vehicles-crud.spec.ts` legt Zeilen an, der Claim-Check aus Task 1 tat es auch, und wer die Anwendung vor dem Testlauf benutzt hat ebenfalls. Eine feste Zahl ließe den Test aus Gründen scheitern, die nichts mit seinem Gegenstand zu tun haben: welche Blöcke welche Rolle bekommt.
- `warm-up.ts` (Task 4) schluckt Fehler jetzt, statt den Lauf abzubrechen. Beobachtet: ein 120-s-Timeout bei nachweislich gesundem Stack (Dev-Server 200, Keycloak 200, derselbe Klick Sekunden später korrekt umleitend). Die Ursache blieb ungeklärt — deshalb darf eine Optimierung nicht den ganzen Lauf killen. Eigener Commit.

Ergebnis: `npm run build` grün, 13/13 e2e grün.

### Task 6: Angular — bestehende Screens rollenbewusst machen

Deliverable: In der gesamten Angular-App wird keine Aktion mehr angeboten, die die angemeldete Rolle nicht ausführen darf; die Auth-Duplikation aus neun Komponenten ist entfernt; die bestehenden e2e-Specs laufen wieder.

**Files:**
- Modify: `src/TransBrain.Web/src/app/vehicles/vehicle-list.component.ts`
- Modify: `src/TransBrain.Web/src/app/drivers/driver-list.component.ts`
- Modify: `src/TransBrain.Web/src/app/orders/order-list.component.ts`
- Modify: `src/TransBrain.Web/src/app/tours/tour-list.component.ts`
- Modify: `src/TransBrain.Web/src/app/tours/tour-detail.component.ts`
- Modify: `src/TransBrain.Web/src/app/vehicles/vehicle-form.component.ts:86-91`
- Modify: `src/TransBrain.Web/src/app/drivers/driver-form.component.ts:90-95`
- Modify: `src/TransBrain.Web/src/app/orders/order-form.component.ts:207-214`
- Modify: `src/TransBrain.Web/src/app/tours/tour-form.component.ts:77-82`
- Modify: `src/TransBrain.Web/e2e/vehicles.spec.ts`, `vehicles-crud.spec.ts`, `drivers.spec.ts`, `orders.spec.ts`, `tours.spec.ts`

**Interfaces:**
- Consumes: `SessionService` (Task 2)
- Produces: nichts Neues

- [ ] **Step 1: Die bestehenden Specs auf die neue Startseite umstellen**

Alle fünf bestehenden Specs melden sich heute selbst an und erwarten danach die Fahrzeugliste auf `/`. In jeder Datei:

1. Den eigenen Anmeldeblock durch `signIn(page, 'admin')` aus `./login` ersetzen (die Rolle ist überall `admin`, weil diese Specs schreiben).
2. Direkt danach zum jeweiligen Bereich navigieren, z. B. `await page.goto('/vehicles');`.
3. `unauthenticated_visitor_seesSignInButton` in `vehicles.spec.ts` bleibt unverändert — `data-testid="login"` liegt weiterhin auf `/`.

Beispiel für den Kopf von `vehicles.spec.ts`:

```ts
import { expect, test } from '@playwright/test';
import { signIn } from './login';

test('unauthenticated_visitor_seesSignInButton', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('login')).toBeVisible();
});

test('adminUser_afterKeycloakLogin_seesVehicleList', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
```

Der Rest des Tests (Token aus dem sessionStorage lesen, Fahrzeug per API anlegen, `page.reload()`) bleibt wortgleich, inklusive aller Kommentare. Nach `page.reload()` steht die Seite weiterhin auf `/vehicles`, die Liste wird also erneut geladen.

- [ ] **Step 2: Rollen-Assertions in die bestehenden Specs aufnehmen**

An `vehicles.spec.ts` anhängen:

```ts
test('viewerUser_onTheVehicleList_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');
    await page.goto('/vehicles');

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.getByTestId('vehicle-add')).toBeHidden();
    await expect(page.getByTestId('vehicle-edit')).toBeHidden();
    await expect(page.getByTestId('vehicle-delete')).toBeHidden();
});
```

An `orders.spec.ts` anhängen:

```ts
test('viewerUser_onTheOrderList_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');
    await page.goto('/orders');

    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    await expect(page.getByTestId('order-add')).toBeHidden();
});

test('disponentUser_onTheOrderList_seesTheWriteActions', async ({ page }) => {
    await signIn(page, 'dispo');
    await page.goto('/orders');

    await expect(page.getByTestId('order-add')).toBeVisible();
});
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.Web && npx playwright test
```

Erwartet: die drei neuen Rollen-Tests scheitern (die Buttons sind noch für jeden sichtbar). Die umgestellten Bestandstests laufen bereits grün, weil Step 1 nur die Navigation korrigiert hat.

- [ ] **Step 4: Die vier Listen umbauen**

In `vehicle-list.component.ts`:

- `OidcSecurityService`-Import und `-inject` entfernen, stattdessen `import { SessionService } from '../auth/session.service';` und `protected readonly session = inject(SessionService);`
- Die Signals `isAuthenticated` und die Methode `login()` entfernen.
- Den Konstruktor ersetzen:

```ts
    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }
```

- Im Template `@if (isAuthenticated())` durch `@if (session.isAuthenticated())` ersetzen und den `@else`-Zweig auf das Nötige eindampfen — der „Sign in"-Button lebt jetzt auf der Startseite:

```html
        } @else {
            <p>Please sign in to see the vehicles.</p>
        }
```

- Die drei Schreib-Bedienelemente gaten:

```html
            @if (session.can('masterData.write')) {
                <a mat-raised-button routerLink="/vehicles/new" data-testid="vehicle-add">Add vehicle</a>
            }
```

und in der `actions`-Spalte:

```html
                        <td mat-cell *matCellDef="let v">
                            @if (session.can('masterData.write')) {
                                <a mat-button [routerLink]="['/vehicles', v.id]" data-testid="vehicle-edit">Edit</a>
                                <button mat-button data-testid="vehicle-delete" (click)="delete(v)">Delete</button>
                            }
                        </td>
```

- Den überholten Kommentar über `delete()` (Zeilen 95-99, „Any authenticated user sees the Add/Edit/Delete controls above … out of scope for this task") ersatzlos löschen. Der Kommentar direkt über dem `error`-Handler in `delete()` („A policy failure … carries no ProblemDetails body at all") bleibt: ein 403 ist weiterhin möglich, wenn ein Token zwischen Laden und Klick seine Rolle verliert.

`driver-list.component.ts` genauso, mit `driver-add` / `driver-edit` / `driver-delete` und derselben Capability.

`order-list.component.ts` genauso, aber mit `dispatch.write` für `order-add`, `order-edit`, `order-cancel` (samt `order-cancel-confirm` / `order-cancel-abort`, die zum selben Ablauf gehören).

`tour-list.component.ts` genauso, mit `dispatch.write` für `tour-add`. `tour-open` bleibt ungegated — es ist ein Link auf eine Route, die jeder Angemeldete sehen darf.

- [ ] **Step 5: Die Tour-Detailseite umbauen**

In `tour-detail.component.ts`:

- `OidcSecurityService` durch `SessionService` ersetzen; `private readonly session = this.oidc.checkAuth().pipe(shareReplay(1));` entfällt, an seine Stelle tritt `protected readonly session = inject(SessionService);`
- In `run()` und `refresh()` `this.session.pipe(switchMap(...))` zu `this.session.ready.pipe(switchMap(...))` ändern. Der Import von `shareReplay` kann entfallen, `switchMap` und `Observable` bleiben.
- Den Zuordnungs-Abschnitt und die Statusbuttons gaten:

```html
            @if (session.can('dispatch.write')) {
                <section>
                    <!-- unveränderter Inhalt des bisherigen Zuordnungs-Abschnitts inkl. mat-select
                         und dem Assign-Button -->
                </section>
            }

            @if (session.can('tourStatus.write')) {
                <section>
                    @if (t.status === 'Planned') {
                        <button mat-raised-button data-testid="tour-start" (click)="start()">Start tour</button>
                    }
                    @if (t.status === 'InProgress') {
                        <button mat-raised-button data-testid="tour-complete" (click)="complete()">
                            Complete tour
                        </button>
                    }
                </section>
            }
```

- In der Stopp-Tabelle den `tour-remove`-Button in `@if (session.can('dispatch.write')) { … }` einschließen.

- [ ] **Step 6: Die vier Formulare umbauen**

In `vehicle-form.component.ts`, `driver-form.component.ts`, `order-form.component.ts` und `tour-form.component.ts` steht jeweils dieselbe Konstruktion:

```ts
    // angular-auth-oidc-client only hydrates its stored session when checkAuth() runs, and until
    // …
    private readonly session = this.oidc.checkAuth().pipe(shareReplay(1));
```

Diese durch den gemeinsamen Dienst ersetzen: `OidcSecurityService`-Import und `-inject` entfernen, `private readonly session = inject(SessionService);` setzen und jede Verwendung von `this.session.pipe(...)` zu `this.session.ready.pipe(...)` ändern. Den erklärenden Kommentar behalten, aber auf den neuen Ort umschreiben:

```ts
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its stored
    // session before the first request goes out; a directly opened form URL would otherwise send
    // it without a bearer token. The one checkAuth() behind it runs in the App component.
    private readonly session = inject(SessionService);
```

Der ungenutzte `shareReplay`-Import fliegt in allen vier Dateien raus.

- [ ] **Step 7: Build und die volle Suite laufen lassen**

```bash
cd src/TransBrain.Web && npm run build && npx playwright test
```

Erwartet: Build erfolgreich, alle Specs grün — die dreizehn aus `home.spec.ts`, die fünf bestehenden Dateien und die drei neuen Rollen-Tests.

- [ ] **Step 8: Committen**

```bash
git add src/TransBrain.Web/src/app src/TransBrain.Web/e2e
git commit -m "feat(web): hide actions the signed-in role may not perform"
```

---

### Task 7: Vue — Route-Guards

Das Gegenstück zu Task 4.

**Files:**
- Modify: `src/TransBrain.VueWeb/src/main.ts`
- Modify: `src/TransBrain.VueWeb/e2e/home.spec.ts`

**Interfaces:**
- Consumes: `useAuthStore` (Task 3), `Capability` (Task 3)
- Produces: `meta.capability` auf den Schreib-Routen; ein globaler `router.beforeEach`

- [x] **Step 1: Die fehlschlagenden Tests schreiben**

Die vier Tests aus Task 4, Step 1 wortgleich an `src/TransBrain.VueWeb/e2e/home.spec.ts` anhängen.

- [x] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.VueWeb && npx playwright test e2e/home.spec.ts
```

Erwartet: die beiden Umleitungs-Tests scheitern.

- [x] **Step 3: Guard und Route-Metadaten einbauen**

In `src/TransBrain.VueWeb/src/main.ts` den Import ergänzen:

```ts
import { useAuthStore } from './stores/auth';
import type { Capability } from './auth/capabilities';
```

Die Routenliste um `meta` erweitern:

```ts
        { path: '/', component: Home },
        { path: '/vehicles', component: VehicleList },
        // 'new' must be registered before ':id' - the router matches path segments in order, and
        // a ':id' route registered first would swallow '/vehicles/new' by treating "new" as an id.
        { path: '/vehicles/new', component: VehicleForm, meta: { capability: 'masterData.write' } },
        { path: '/vehicles/:id', component: VehicleForm, meta: { capability: 'masterData.write' } },
        { path: '/drivers', component: DriverList },
        { path: '/drivers/new', component: DriverForm, meta: { capability: 'masterData.write' } },
        { path: '/drivers/:id', component: DriverForm, meta: { capability: 'masterData.write' } },
        { path: '/orders', component: OrderList },
        { path: '/orders/new', component: OrderForm, meta: { capability: 'dispatch.write' } },
        { path: '/orders/:id', component: OrderForm, meta: { capability: 'dispatch.write' } },
        { path: '/tours', component: TourList },
        { path: '/tours/new', component: TourForm, meta: { capability: 'dispatch.write' } },
        // No capability: the fahrer must reach this to start their tour, and a viewer may look.
        // The start/complete/assign buttons inside are gated individually.
        { path: '/tours/:id', component: TourDetail },
        { path: '/callback', component: AuthCallback },
```

Direkt nach `const router = createRouter({...});` den Guard ergänzen:

```ts
/**
 * Runs before every navigation, including the very first. It awaits auth.load() rather than
 * reading the store as it happens to stand: on a directly opened URL - a reload, a bookmark -
 * nothing has hydrated the stored session yet, and the guard would judge a signed-in user as
 * signed out.
 *
 * '/callback' is skipped deliberately: the OIDC code is only exchanged inside AuthCallback.vue,
 * so at that moment there is legitimately no session yet, and bouncing it to '/' would break
 * every sign-in.
 *
 * The list routes carry no capability. The API lets every role read them (Policies.Read), so a
 * client-side block would be stricter than the server for no reason - hiding a tile means "you
 * do not need this", not "you may not have this".
 */
router.beforeEach(async (to) => {
    if (to.path === '/callback') {
        return true;
    }

    const auth = useAuthStore();
    await auth.load();

    if (!auth.isAuthenticated) {
        return to.path === '/' ? true : '/';
    }

    const required = to.meta.capability as Capability | undefined;
    return required && !auth.can(required) ? '/' : true;
});
```

- [x] **Step 4: Build und Tests laufen lassen**

```bash
cd src/TransBrain.VueWeb && npm run build && npx playwright test e2e/home.spec.ts
```

Erwartet: Build erfolgreich, alle zehn Tests grün.

- [x] **Step 5: Committen**

```bash
git add src/TransBrain.VueWeb/src/main.ts src/TransBrain.VueWeb/e2e/home.spec.ts
git commit -m "feat(vueweb): guard the write routes behind capabilities"
```

---

**Ausführung am 2026-09-01:** zusammen mit Task 4 umgesetzt, damit beide Frontends dieselbe Guard-Semantik zum selben Zeitpunkt tragen. `e2e/home.spec.ts` und `e2e/warm-up.ts` sind in beiden Suiten identisch. Ergebnis: `npm run build` grün, `npx playwright test e2e/home.spec.ts` 10/10 grün.

### Task 8: Vue — Kennzahlen und Arbeitslisten auf der Startseite

Das Gegenstück zu Task 5.

**Files:**
- Modify: `src/TransBrain.VueWeb/src/api/vehicles.ts:45-50`
- Modify: `src/TransBrain.VueWeb/src/api/drivers.ts:45-50`
- Modify: `src/TransBrain.VueWeb/src/views/Home.vue`
- Modify: `src/TransBrain.VueWeb/e2e/home.spec.ts`

**Interfaces:**
- Consumes: `useAuthStore` (Task 3); `listVehicles`, `listDrivers`, `listOrders`, `listTours`, `startTour`, `completeTour` (bestehend)
- Produces: `listVehicles(pageSize?: number, status?: string | null)`, `listDrivers(pageSize?: number, status?: string | null)`

- [x] **Step 1: Die fehlschlagenden Tests schreiben**

Die drei Tests aus Task 5, Step 1 wortgleich an `src/TransBrain.VueWeb/e2e/home.spec.ts` anhängen.

- [x] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.VueWeb && npx playwright test e2e/home.spec.ts
```

Erwartet: die drei neuen Tests scheitern mit Timeout.

- [x] **Step 3: Den `status`-Parameter in die beiden API-Clients aufnehmen**

`src/TransBrain.VueWeb/src/api/vehicles.ts`, `listVehicles` ersetzen:

```ts
/**
 * @param pageSize The API defaults to 20 rows. A picker that must offer EVERY vehicle to choose
 * from - the tour form - has to ask for more, or a recently added one simply is not in the list
 * and cannot be chosen. Capped at 100 by the API.
 * @param status Filters server-side (VehicleEndpoints.cs). The home page asks with pageSize 1
 * and reads only totalCount, so a fleet count costs one row over the wire.
 */
export async function listVehicles(pageSize?: number, status?: string | null): Promise<PagedResult<Vehicle>> {
    // Only filters that are actually set are sent. An omitted one must not become the string
    // "null" in the query string - the API would reject that with a 400.
    const params: Record<string, string> = {};
    if (pageSize) {
        params.pageSize = String(pageSize);
    }
    if (status) {
        params.status = status;
    }

    const response = await client.get<PagedResult<Vehicle>>('/vehicles', { params });
    return response.data;
}
```

`src/TransBrain.VueWeb/src/api/drivers.ts`, `listDrivers` genauso — `Driver` statt `Vehicle`, `/drivers` statt `/vehicles`, „driver" statt „vehicle" im Kommentar.

- [x] **Step 4: Die Blöcke in `Home.vue` einbauen**

Im `<script setup>` ergänzen, **unterhalb** des vorhandenen `const auth = useAuthStore();` — der neue Code verwendet `auth`:

```ts
import axios from 'axios';
import { onMounted, ref } from 'vue';
import { listVehicles } from '../api/vehicles';
import { listDrivers } from '../api/drivers';
import { listOrders, type Order } from '../api/orders';
import { completeTour, listTours, startTour, type Tour } from '../api/tours';

/** One row is enough when only totalCount is read. */
const COUNT_ONLY = 1;
/** Five rows fit the block; the rest is one click away on /orders. */
const DRAFT_PREVIEW_SIZE = 5;

function today(): string {
    // The API takes a DateOnly, so an ISO date without a time part. Local date, not UTC:
    // toISOString() would roll over a day early for anyone east of Greenwich after their evening.
    const now = new Date();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
}

const vehiclesAvailable = ref(0);
const vehiclesInWorkshop = ref(0);
const driversAvailable = ref(0);
const ordersInDraft = ref(0);
const toursToday = ref(0);
const draftOrders = ref<Order[]>([]);
const myTours = ref<Tour[]>([]);

// One error ref per block, not one for the page: a failing vehicle count must not blank the
// work list next to it.
const vehicleError = ref<string | null>(null);
const driverError = ref<string | null>(null);
const orderError = ref<string | null>(null);
const tourError = ref<string | null>(null);
const draftOrdersError = ref<string | null>(null);
const myToursError = ref<string | null>(null);

onMounted(async () => {
    await auth.load();
    if (auth.isAuthenticated) {
        await loadBlocks();
    }
});

async function loadBlocks(): Promise<void> {
    const jobs: Promise<void>[] = [];

    if (auth.areas.has('vehicles')) {
        jobs.push(
            (async () => {
                try {
                    vehiclesAvailable.value = (await listVehicles(COUNT_ONLY, 'Available')).totalCount;
                    vehiclesInWorkshop.value = (await listVehicles(COUNT_ONLY, 'InWorkshop')).totalCount;
                } catch (error) {
                    vehicleError.value = describe(error, 'The vehicle counts could not be loaded.');
                }
            })(),
        );
    }

    if (auth.areas.has('drivers')) {
        jobs.push(
            (async () => {
                try {
                    driversAvailable.value = (await listDrivers(COUNT_ONLY, 'Available')).totalCount;
                } catch (error) {
                    driverError.value = describe(error, 'The driver counts could not be loaded.');
                }
            })(),
        );
    }

    if (auth.areas.has('orders')) {
        jobs.push(
            (async () => {
                try {
                    ordersInDraft.value = (await listOrders('Draft', COUNT_ONLY)).totalCount;
                } catch (error) {
                    orderError.value = describe(error, 'The order counts could not be loaded.');
                }
            })(),
        );
    }

    // Every role gets this one - for a fahrer the API narrows it to their own tours, see
    // ListToursQueryHandler. That is why the frontend never needs to know its own driverId.
    jobs.push(
        (async () => {
            try {
                const page = await listTours({ tourDate: today() });
                toursToday.value = page.totalCount;
                if (auth.hasRole('fahrer')) {
                    myTours.value = page.items;
                }
            } catch (error) {
                tourError.value = describe(error, "Today's tours could not be loaded.");
            }
        })(),
    );

    if (auth.can('dispatch.write')) {
        jobs.push(
            (async () => {
                try {
                    draftOrders.value = (await listOrders('Draft', DRAFT_PREVIEW_SIZE)).items;
                } catch (error) {
                    draftOrdersError.value = describe(error, 'The draft orders could not be loaded.');
                }
            })(),
        );
    }

    await Promise.all(jobs);
}

async function reloadMyTours(): Promise<void> {
    try {
        const page = await listTours({ tourDate: today() });
        myTours.value = page.items;
        toursToday.value = page.totalCount;
    } catch (error) {
        myToursError.value = describe(error, "Today's tours could not be reloaded.");
    }
}

async function start(tour: Tour): Promise<void> {
    myToursError.value = null;
    try {
        await startTour(tour.id);
        await reloadMyTours();
    } catch (error) {
        myToursError.value = describe(error, 'The tour could not be started.');
    }
}

async function complete(tour: Tour): Promise<void> {
    myToursError.value = null;
    try {
        await completeTour(tour.id);
        await reloadMyTours();
    } catch (error) {
        myToursError.value = describe(error, 'The tour could not be completed.');
    }
}

function describe(error: unknown, fallback: string): string {
    if (axios.isAxiosError(error)) {
        const problem = error.response?.data as { title?: string; detail?: string } | undefined;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.response?.status ?? 'unknown'})`;
    }
    return fallback;
}
```

Im Template, zwischen dem Rollen-Chip und der Kachel-Reihe:

```html
            <v-row class="mt-4">
                <v-col v-if="auth.areas.has('vehicles')" cols="12" md="3">
                    <v-card>
                        <v-card-title>Vehicles</v-card-title>
                        <v-card-text>
                            <p v-if="vehicleError" data-testid="home-kpi-vehicles-error">{{ vehicleError }}</p>
                            <template v-else>
                                <p>Available: <strong data-testid="home-kpi-vehicles-available">{{ vehiclesAvailable }}</strong></p>
                                <p>In workshop: <strong data-testid="home-kpi-vehicles-workshop">{{ vehiclesInWorkshop }}</strong></p>
                            </template>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('drivers')" cols="12" md="3">
                    <v-card>
                        <v-card-title>Drivers</v-card-title>
                        <v-card-text>
                            <p v-if="driverError" data-testid="home-kpi-drivers-error">{{ driverError }}</p>
                            <p v-else>Available: <strong data-testid="home-kpi-drivers-available">{{ driversAvailable }}</strong></p>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col v-if="auth.areas.has('orders')" cols="12" md="3">
                    <v-card>
                        <v-card-title>Orders</v-card-title>
                        <v-card-text>
                            <p v-if="orderError" data-testid="home-kpi-orders-error">{{ orderError }}</p>
                            <p v-else>In draft: <strong data-testid="home-kpi-orders-draft">{{ ordersInDraft }}</strong></p>
                        </v-card-text>
                    </v-card>
                </v-col>
                <v-col cols="12" md="3">
                    <v-card>
                        <v-card-title>Tours today</v-card-title>
                        <v-card-text>
                            <p v-if="tourError" data-testid="home-kpi-tours-error">{{ tourError }}</p>
                            <p v-else><strong data-testid="home-kpi-tours-today">{{ toursToday }}</strong></p>
                        </v-card-text>
                    </v-card>
                </v-col>
            </v-row>

            <section v-if="auth.can('dispatch.write')" data-testid="home-draft-orders" class="mt-4">
                <h2>Orders awaiting a tour</h2>
                <p v-if="draftOrdersError" data-testid="home-draft-orders-error">{{ draftOrdersError }}</p>
                <template v-else>
                    <v-list>
                        <v-list-item v-for="order in draftOrders" :key="order.id" data-testid="home-draft-order-row">
                            <v-list-item-title>
                                {{ order.orderNumber }} — {{ order.consignor.city }} → {{ order.consignee.city }}
                            </v-list-item-title>
                            <template #append>
                                <v-btn :to="`/orders/${order.id}`" data-testid="home-draft-order-open">Open</v-btn>
                            </template>
                        </v-list-item>
                    </v-list>
                    <!-- No per-row "Plan" button: an order is put onto a tour by the picker on
                         /tours/:id, and no endpoint creates a tour from an order. -->
                    <v-btn to="/tours/new" data-testid="home-plan-tour">Plan a tour</v-btn>
                </template>
            </section>

            <section v-if="auth.hasRole('fahrer')" data-testid="home-my-tours" class="mt-4">
                <h2>My tours today</h2>
                <p v-if="myToursError" data-testid="home-my-tours-error">{{ myToursError }}</p>
                <v-card v-for="tour in myTours" :key="tour.id" class="mb-2" data-testid="home-my-tour-row">
                    <v-card-title>{{ tour.vehicleLicensePlate }} — {{ tour.status }}</v-card-title>
                    <v-card-actions>
                        <v-btn :to="`/tours/${tour.id}`">Open</v-btn>
                        <v-btn
                            v-if="tour.status === 'Planned'"
                            data-testid="home-my-tour-start"
                            @click="start(tour)"
                            >Start tour</v-btn
                        >
                        <v-btn
                            v-if="tour.status === 'InProgress'"
                            data-testid="home-my-tour-complete"
                            @click="complete(tour)"
                            >Complete tour</v-btn
                        >
                    </v-card-actions>
                </v-card>
            </section>
```

- [x] **Step 5: Build und Tests laufen lassen**

```bash
cd src/TransBrain.VueWeb && npm run build && npx playwright test e2e/home.spec.ts
```

Erwartet: Build erfolgreich, alle dreizehn Tests grün.

- [x] **Step 6: Committen**

```bash
git add src/TransBrain.VueWeb/src/views/Home.vue \
        src/TransBrain.VueWeb/src/api/vehicles.ts \
        src/TransBrain.VueWeb/src/api/drivers.ts \
        src/TransBrain.VueWeb/e2e/home.spec.ts
git commit -m "feat(vueweb): add role-specific counts and work lists to the home page"
```

---

**Ausführung am 2026-09-01:** zusammen mit Task 5 umgesetzt. `npm run build` grün (`vue-tsc` sauber), 13/13 e2e grün. `e2e/home.spec.ts` ist weiterhin in beiden Suiten identisch.

### Task 9: Vue — bestehende Views rollenbewusst machen

Das Gegenstück zu Task 6.

**Files:**
- Modify: `src/TransBrain.VueWeb/src/views/VehicleList.vue`, `DriverList.vue`, `OrderList.vue`, `TourList.vue`, `TourDetail.vue`
- Modify: `src/TransBrain.VueWeb/src/views/VehicleForm.vue`, `DriverForm.vue`, `OrderForm.vue`, `TourForm.vue`
- Modify: `src/TransBrain.VueWeb/e2e/vehicles.spec.ts`, `vehicles-crud.spec.ts`, `drivers.spec.ts`, `orders.spec.ts`, `tours.spec.ts`

**Interfaces:**
- Consumes: `useAuthStore` (Task 3)
- Produces: nichts Neues

- [ ] **Step 1: Die bestehenden Specs umstellen**

Wie Task 6, Step 1: in allen fünf Specs den eigenen Anmeldeblock durch `signIn(page, 'admin')` aus `./login` ersetzen und danach zum jeweiligen Bereich navigieren. Der Rest bleibt wortgleich, inklusive des Kommentars zum `oidc.user:`-Schlüssel im `sessionStorage`.

- [ ] **Step 2: Rollen-Assertions ergänzen**

Die beiden Testblöcke aus Task 6, Step 2 wortgleich an `vehicles.spec.ts` bzw. `orders.spec.ts` anhängen.

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

```bash
cd src/TransBrain.VueWeb && npx playwright test
```

Erwartet: die drei neuen Rollen-Tests scheitern.

- [ ] **Step 4: Die vier Listen umbauen**

In `VehicleList.vue`:

- `onMounted` vereinfachen — `auth.load()` läuft jetzt im Router-Guard und in `App.vue`:

```ts
onMounted(async () => {
    if (auth.isAuthenticated) {
        await refresh();
    }
});
```

- Den überholten Kommentar über `remove()` (Zeilen 46-50) ersatzlos löschen; der Kommentar im `catch` bleibt.
- Im Template den `v-else`-Zweig auf einen Hinweis eindampfen und die Schreib-Bedienelemente gaten:

```html
        <template v-if="auth.isAuthenticated">
            <h1>Vehicles</h1>
            <v-btn
                v-if="auth.can('masterData.write')"
                data-testid="vehicle-add"
                @click="router.push('/vehicles/new')"
                >Add vehicle</v-btn
            >
```

und in der Aktionsspalte:

```html
                <template #item.actions="{ item }">
                    <template v-if="auth.can('masterData.write')">
                        <v-btn data-testid="vehicle-edit" @click="router.push(`/vehicles/${item.id}`)">Edit</v-btn>
                        <v-btn data-testid="vehicle-delete" @click="remove(item)">Delete</v-btn>
                    </template>
                </template>
```

sowie

```html
        <p v-else>Please sign in to see the vehicles.</p>
```

`DriverList.vue` genauso mit `masterData.write`. `OrderList.vue` und `TourList.vue` genauso mit `dispatch.write` für `order-add` / `order-edit` / `order-cancel` bzw. `tour-add`.

- [ ] **Step 5: `TourDetail.vue` umbauen**

Den Zuordnungs-Abschnitt und den `tour-remove`-Button in `v-if="auth.can('dispatch.write')"` einschließen, die Start-/Complete-Buttons in `v-if="auth.can('tourStatus.write')"`. Die Statuslogik (`v-if="tour.status === 'Planned'"` bzw. `'InProgress'`) bleibt zusätzlich bestehen.

- [ ] **Step 6: Die vier Formulare umbauen**

In `VehicleForm.vue`, `DriverForm.vue`, `OrderForm.vue` und `TourForm.vue` den `await auth.load()`-Aufruf aus `onMounted` entfernen — der Router-Guard hat ihn zu diesem Zeitpunkt bereits ausgeführt. Alles Übrige bleibt.

- [ ] **Step 7: Build und die volle Suite laufen lassen**

```bash
cd src/TransBrain.VueWeb && npm run build && npx playwright test
```

Erwartet: Build erfolgreich, alle Specs grün.

- [ ] **Step 8: Committen**

```bash
git add src/TransBrain.VueWeb/src src/TransBrain.VueWeb/e2e
git commit -m "feat(vueweb): hide actions the signed-in role may not perform"
```

---

### Task 10: Gesamtverifikation

Deliverable: der Nachweis, dass beide Frontends und die API zusammen funktionieren — nicht nur die zuletzt geänderte Datei.

**Files:** keine

- [ ] **Step 1: Die .NET-Suite laufen lassen**

```bash
dotnet build
dotnet test
```

Erwartet: beides erfolgreich. Serverseitig wurde nur `transbrain-realm.json` geändert; die Suite läuft zur Absicherung mit, weil ein kaputter Realm-Import die Integrationstests kippen würde.

- [ ] **Step 2: Beide Frontends bauen**

```bash
cd src/TransBrain.Web && npm run build
cd ../TransBrain.VueWeb && npm run build
```

Erwartet: beide erfolgreich, keine TypeScript-Fehler.

- [ ] **Step 3: Beide e2e-Suiten vollständig laufen lassen**

Mit laufendem AppHost:

```bash
cd src/TransBrain.Web && npx playwright test
cd ../TransBrain.VueWeb && npx playwright test
```

Erwartet: beide Suiten vollständig grün. Fehlschläge hier sind echte Regressionen und werden behoben, nicht durch einen Retry überspielt.

- [ ] **Step 4: Alle vier Rollen einmal von Hand durchklicken**

Für jede der vier Rollen, in beiden Frontends: anmelden, Startseite ansehen, abmelden, nächste Rolle. Geprüft wird, dass die Seite dem entspricht, was Spec §6 als Blocktabelle festlegt — insbesondere, dass ein `fahrer` weder Fahrzeuge noch Fahrer noch Aufträge in der Navigation hat und ein `viewer` nirgends eine Anlegen-Schaltfläche sieht.

Auffälligkeiten notieren, aber hier nicht beheben: eine echte Abweichung von §6 ist ein Fehler in Task 5 bzw. 8 und wird dort korrigiert.

---

### Task 11: Dokumentation und Screenshots

**Files:**
- Modify: `docs/BEDIENUNG_TRANSBRAIN_WEB.md`
- Modify: `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`
- Create: `docs/img/web/startseite-admin.png`, `docs/img/web/startseite-fahrer.png`
- Create: `docs/img/vueweb/startseite-admin.png`, `docs/img/vueweb/startseite-fahrer.png`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Die Screenshots aufnehmen**

Mit laufendem AppHost. Damit die Startseite nicht leer aussieht, zuerst über die Oberfläche als `admin.user` ein Fahrzeug, einen Fahrer und einen Auftrag anlegen.

Eine temporäre Spec `src/TransBrain.Web/e2e/screenshots.spec.ts` anlegen:

```ts
import { test } from '@playwright/test';
import { signIn } from './login';

test('capture home admin', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await signIn(page, 'admin');
    await page.screenshot({ path: '../../docs/img/web/startseite-admin.png', fullPage: true });
});

test('capture home fahrer', async ({ page }) => {
    await page.setViewportSize({ width: 480, height: 900 });
    await signIn(page, 'fahrer');
    await page.screenshot({ path: '../../docs/img/web/startseite-fahrer.png', fullPage: true });
});
```

```bash
cd src/TransBrain.Web && npx playwright test e2e/screenshots.spec.ts
```

Dasselbe für Vue mit `../../docs/img/vueweb/…` als Zielpfad. Danach **beide temporären Specs wieder löschen** — sie gehören nicht in die Suite.

- [ ] **Step 2: Das Kapitel „Startseite" in beide Anleitungen schreiben**

In `docs/BEDIENUNG_TRANSBRAIN_WEB.md` und `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md` jeweils **vor** dem Kapitel zu den Fahrzeugen ein neues Kapitel einfügen. Die beiden Fassungen sind bewusst wortgleich, wie es die Vue-Anleitung in ihrem Vorspann festhält; nur der Screenshot-Pfad unterscheidet sich.

```markdown
## Startseite

Nach der Anmeldung landen Sie auf der Startseite. Sie zeigt genau die Bereiche und
Schaltflächen, die Ihre Rolle benötigt — eine Fahrerin sieht dort etwas anderes als ein
Administrator. Über die Kopfleiste erreichen Sie dieselben Bereiche jederzeit wieder, ganz
rechts stehen Ihr Name und die Schaltfläche **Sign out** zum Abmelden.

![Startseite eines Administrators](img/web/startseite-admin.png)

Welche Rolle was sieht:

| Bestandteil | admin | disponent | fahrer | viewer |
|---|:-:|:-:|:-:|:-:|
| Kennzahlen zu Fahrzeugen und Fahrern | ✓ | ✓ | | ✓ |
| Kennzahl „Orders in draft" | ✓ | ✓ | | ✓ |
| Kennzahl „Tours today" | ✓ | ✓ | ✓ (eigene) | ✓ |
| Liste „Orders awaiting a tour" | ✓ | ✓ | | |
| Liste „My tours today" mit **Start tour** / **Complete tour** | | | ✓ | |
| Kacheln Vehicles, Drivers, Orders | ✓ | ✓ | | ✓ |
| Kachel Tours | ✓ | ✓ | ✓ | ✓ |
| Schaltflächen **Add vehicle** / **Add driver** | ✓ | | | |
| Schaltflächen **New order** / **Plan tour** | ✓ | ✓ | | |

Für eine Fahrerin ist die Seite bewusst schmal gehalten, damit sie auf einem Mobiltelefon
im Fahrerhaus bedienbar bleibt. Sie zeigt nur die eigenen Touren des Tages und erlaubt es,
sie direkt zu starten und abzuschließen, ohne den Umweg über die Tourenliste.

![Startseite einer Fahrerin](img/web/startseite-fahrer.png)

Dass eine Kachel fehlt, heißt nicht, dass Ihnen der Bereich verwehrt wäre: alle vier Rollen
dürfen alle Listen lesen. Ausgeblendet wird, was Ihre Rolle für ihre Arbeit nicht braucht.
Anders bei den Schaltflächen zum Anlegen und Ändern — die erscheinen nur, wenn Sie die
Änderung auch tatsächlich vornehmen dürfen. Rufen Sie ein Formular dennoch über die Adresszeile
auf, für das Ihnen die Berechtigung fehlt, bringt die Anwendung Sie zur Startseite zurück.
```

Für die Vue-Anleitung `img/vueweb/…` als Pfad einsetzen.

- [ ] **Step 3: Die Anmelde-Abschnitte anpassen**

In beiden Anleitungen jede Stelle suchen, die beschreibt, dass man nach dem Anmelden die
Fahrzeugliste sieht, und auf die Startseite umschreiben. Ebenso den Weg zu den Bereichen: statt
„öffnen Sie `http://localhost:4200/drivers`" nun „wählen Sie in der Kopfleiste **Drivers**".

```bash
grep -n "Fahrzeugliste\|4200/\|4300/" docs/BEDIENUNG_TRANSBRAIN_WEB.md docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md
```

- [ ] **Step 4: CHANGELOG-Eintrag**

Unter `[Unreleased]` in `CHANGELOG.md`, unter `### Added`:

```markdown
- Rollenbasierte Startseite in beiden Frontends: Kennzahlen, Arbeitslisten und Bereichskacheln
  richten sich nach der angemeldeten Keycloak-Rolle. Dazu eine Navigations-Shell mit Abmeldung,
  Route-Guards für die schreibenden Routen und rollenabhängige Schaltflächen in allen Screens.
```

- [ ] **Step 5: Committen**

```bash
git add docs CHANGELOG.md
git commit -m "docs: document the role-based home page in both operator guides"
```

---

## Selbstprüfung des Plans

**Spec-Abdeckung**

| Spec-Abschnitt | Task |
|---|---|
| §3 Capability-Tabelle | 2 (Angular), 3 (Vue) |
| §4 Rollenherkunft, Risiko, Rückfallweg | 1 |
| §5 Session-Schicht, `logout()`, Entfernen der Duplikation | 2, 3 (Schicht), 6, 9 (Duplikation) |
| §6 Startseite: Blöcke, Layout, Daten, Fehler | 2, 5 (Angular), 3, 8 (Vue) |
| §7 App-Shell | 2, 3 |
| §8 Routing und Guards | 2, 4 (Angular), 3, 7 (Vue) |
| §9 Bedienelemente in bestehenden Screens | 6, 9 |
| §10 API-Client-Erweiterungen | 5, 8 |
| §11 Tests | 2, 4, 5, 6 (Angular), 3, 7, 8, 9 (Vue) |
| §12 Verifikation | 10 |
| §13 Umsetzungsreihenfolge | Task-Reihenfolge 1 → 11 |
| §14 Dokumentation | 11 |
| §15 Weggelassenes | nirgends implementiert — korrekt |

**Bekannte Abweichung:** der `[Plan]`-Button pro Draft-Zeile aus §6.1 ist durch `home-draft-order-open` plus einen Block-Button `home-plan-tour` ersetzt, begründet oben unter „Abweichung von der Spec".

**Typkonsistenz:** `Capability`, `AppRole`, `Area`, `knownRoles`, `capabilitiesFor`, `areasFor` sind in Task 2 definiert und in Task 3 wortgleich gespiegelt; `SessionService.ready`, `.can()`, `.hasRole()`, `.areas()` werden in den Tasks 4, 5, 6 unverändert so benutzt; `auth.can()`, `auth.hasRole()`, `auth.areas` entsprechend in 7, 8, 9. `listVehicles`/`listDrivers` und `VehicleService.list`/`DriverService.list` bekommen in 5 bzw. 8 dieselbe Parameterreihenfolge `(pageSize, status)`.
