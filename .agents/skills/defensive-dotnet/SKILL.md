---
name: defensive-dotnet
description: Defensive programming rules for the project's C# backend.
---

# Defensive .NET development

## Input validation

Validate untrusted input at system boundaries.

Do not duplicate domain invariants in controllers.

## Exceptions

Do not use exceptions for normal control flow.

Do not catch Exception unless:
- adding meaningful context,
- translating at an application boundary,
- or performing required cleanup.

Unexpected exceptions should be handled by centralized API exception handling.

## Nullability

Respect nullable reference types.

Do not suppress nullable warnings with ! unless the invariant is proven.

## Configuration

Use strongly typed options.

Validate required configuration during application startup.

## External systems

All remote calls must define an explicit timeout.

Retry only transient failures.

Retry only operations that are safe to retry.

Use circuit breakers where repeated downstream failures could cascade.

## Persistence

Explicitly consider transaction boundaries.

Explicitly consider concurrency when multiple requests may update the same resource.

Never rely solely on application-side checks for database uniqueness.