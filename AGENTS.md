# AGENTS.md

This file provides guidance to KI Agent (claude.ai/code, Github Copilot, Jetbrains AI) when working with code in this repository.

## Project

**TransBrain** is a .NET/C# application is a software for a trucking company

## Stack
- .NET 11 (STS) with C# 15
- ASP.NET Minimal APIs (no controllers)
- EF Core 10 with Postgresql
- Custom, hand-rolled CQRS mediator (`TransBrain.Application/Common/Messaging/`) — not MediatR, which became commercially licensed at v13
- FluentValidation for validation
- xUnit + AwesomeAssertions for tests (not FluentAssertions, which became proprietary from v8)
- OpenApi Support
- Aspire Support
- Redis cache

## Web Stack for Web Project
- Angular latest
- Angular Material (`@angular/material`) for components/styling
- Node.js >=26.4.0 required
- e2e tests with Playwright (you can use MCP)

## Web Stack Vue Web Project (TransBrain.VueWeb)
- Vue 3 (Composition API, `<script setup lang="ts">`), Vite, TypeScript
- Vuetify (`@vuetify`) for components/styling, Pinia for state, Vue Router for routing, axios for HTTP
- Dev server on port 4300 (already allowed by the Api's CORS config)
- Same functionality as TransBrain.Web, against the same TransBrain.Api
- Node.js >=26.4.0 required

## Mobile Stack Driver App (TransBrain.DriverApp)
- Flutter (stable, >=3.50 / Dart 3.14) for Android, iOS and web (the web build is the test demo, dev port 4400 — allowed by the Api's CORS config and the `transbrain-mobile` Keycloak client)
- German-only UI aimed at drivers: own tours, start/complete, route map (flutter_map/OSM), profile
- Pure-Dart OIDC PKCE sign-in (no platform auth plugin): system browser + `de.transbrain.driver://callback` deep link (app_links) on mobile, same-tab redirect on web; plus a backend-free demo mode with bundled sample data
- Note: Flutter >=3.50 deprecates `package:flutter/material.dart` — import `package:material_ui/material_ui.dart` instead
- Tests: `flutter analyze` + `flutter test` (unit + widget tests, run against the demo data — no backend needed); not part of the .NET CI workflow
- UI changes here must update `docs/BEDIENUNG_TRANSBRAIN_DRIVERAPP.md` and, if visible, its screenshot under `docs/img/driverapp/`

## Code Conventions
- Use records for DTOs and Value Objects
- English naming for code, English for docs
- Nullable reference types always enabled
- File-scoped namespaces
- Primary constructors where appropriate

## Architecture
- Clean Architecture with 4 layers
- Vertical slices inside Application
- Result Pattern (never throw exceptions for control flow)
- Each feature in its own folder with Command/Query/Handler

## Tests
- Naming: Method_Scenario_ExpectedResult
- Use Testcontainers for integration tests
- WebApplicationFactory for API tests
- Minimum 80% coverage in the Application layer, enforced by a CI gate (`.github/workflows/ci.yml`)
- Playwright e2e tests live in `<project>/e2e/*.spec.ts` for both frontends (vehicles, drivers, orders, tours), run via `npm run e2e`; they do **not** run in CI — `.github/workflows/ci.yml` says so explicitly, because there is no headless Aspire startup path yet (no Postgres/Redis/Keycloak available to a CI job). Run them locally against `dotnet run --project src/TransBrain.AppHost`

## Git
- Conventional Commits (feat:, fix:, refactor:, etc.)
- Branch: feature/feature-name
- Always run tests before committing

## Documentation
- Use Markdown for documentation
- Document all public APIs
- Use diagrams and examples where appropriate
- Maintain a CHANGELOG.md (Keep a Changelog format) — add an entry under `[Unreleased]` for each notable change
- User-facing UI changes (new field, new workflow, changed dialog, removed feature) in either of the two frontends must update the matching operator guide (`docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `_VUEWEB.md`) and, if the change is visible, its screenshot under `docs/img/<web|vueweb>/` in the same change

## General
- Use consistent indentation (4 spaces)
- Avoid unnecessary comments
- Keep code clean and readable
- Document your steps