# Task 6 report

Implemented Stage3 orchestration in the shared checkout.

## Changes

- Runs one Stage3 request per normalized topic with a maximum of four concurrent AI calls.
- Builds requests from persisted active/unresolved statements, in-topic relations, and shared business context.
- Validates each Stage3 response before any final rows are created.
- Publishes functions, mapped specification items, source links, project contradiction items, and `Completed` status together.
- Uses a relational transaction for final publication and suppresses publication when a run becomes stale or any Stage3 call/mapping/save fails.
- Added focused tests for concurrency, mapping/provenance/contradictions, and no-final-rows failure behavior.

## Verification

- `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationOrchestratorTests --no-restore`
  - Passed: 9/9.
- `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --no-restore`
  - Passed: 67/75.
  - 8 existing PostgreSQL persistence tests failed before exercising the change because the configured local PostgreSQL role `dionysus` does not exist (`28P01: role "dionysus" does not exist`).
- `git diff --check`
  - Passed.

No `ProjectsController` or Task 7 files were changed.
