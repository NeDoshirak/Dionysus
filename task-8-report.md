# Task 8 report

Implemented the protected specification API in the shared checkout.

## Changes

- Added JWT-protected specification GET and function/item CRUD routes.
- Added owner-scoped project queries so missing and cross-owner resources return `404`.
- Added failed-only retry with a new run ID, retry count, cleanup of prior analysis output, and one queue enqueue.
- Added mapper logic for ordered functions/items, source statements, and transcript segment timestamps reconstructed from persisted links.
- Added completed-analysis checks, manual item semantics, and validation that source statement IDs belong to the project analysis.
- Added focused API controller tests for authorization scope, retry, manual creation, source validation, and timestamp reconstruction.

## Verification

- `dotnet build backend/Dionysus.Api/Dionysus.Api.csproj --no-restore`
  - Passed.
- `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationApiTests --no-restore`
  - Passed: 4/4 before the pre-existing Task 9 Testcontainers files were included in the test project build.
- `git diff --check`
  - Passed.
- The complete test project is currently blocked by pre-existing untracked Task 9 infrastructure: `Testcontainers.Valkey` cannot be restored from configured NuGet sources; existing PostgreSQL tests also require a local `dionysus` PostgreSQL role.

No `ProjectsController` or orchestrator files were changed.
