# AGENTS.md

This file provides guidance to KI Agent (claude.ai/code, Github Copilot, Jetbrains AI) when working with code in this repository.

## Project

**TransBrain** is a .NET/C# application is a software for a trucking company

## Stack
- .NET 10 LTS with C# 14
- ASP.NET Minimal APIs (no controllers)
- EF Core 10 with Postgresql
- Custom, hand-rolled CQRS mediator (`TransBrain.Application/Common/Messaging/`) — not MediatR, which became commercially licensed at v13
- FluentValidation for validation
- xUnit + FluentAssertions for tests
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
- Minimum 80% coverage in the Application layer
- Playwright e2e tests live in `<project>/e2e/*.spec.ts` for all frontends, run via `npm run e2e`; they run in CI (`.github/workflows/ci.yml`)
- K6 load tests (`k6/`) run manually against a running environment (not in CI); see `k6/README.md`

## Git
- Conventional Commits (feat:, fix:, refactor:, etc.)
- Branch: feature/feature-name
- Always run tests before committing

## Documentation
- Use Markdown for documentation
- Document all public APIs
- Use diagrams and examples where appropriate
- Maintain a CHANGELOG.md (Keep a Changelog format) — add an entry under `[Unreleased]` for each notable change
- User-facing UI changes (new field, new workflow, changed dialog, removed feature) in any of the three frontends must update the matching operator guide (`docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `_VUEWEB.md`) and, if the change is visible, its screenshot under `docs/img/<web|vueweb>/` in the same change

## General
- Use consistent indentation (4 spaces)
- Avoid unnecessary comments
- Keep code clean and readable
- Document your steps