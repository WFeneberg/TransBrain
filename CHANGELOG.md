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
- Integration tests using Testcontainers and a test authentication scheme, using AwesomeAssertions rather than FluentAssertions (FluentAssertions became proprietary from version 8)
- CI workflow building the solution, running .NET tests and building both frontends
