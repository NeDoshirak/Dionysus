# Task 7 report

Implemented queue analysis after successful project transcription.

## Changes

- Injected `ISpecificationAnalysisQueue` into `ProjectsController`.
- After transcription segments are persisted, creates and saves one `SpecificationAnalysis` in `Queued` state and enqueues `SpecificationAnalysisJob(analysis.Id, analysis.RunId)`.
- Enqueue failures leave the saved analysis queued for recovery.
- Failed transcription creates no analysis.
- New recordings explicitly set `IsCurrent = true`.
- Added nullable `SpecificationStatus` to `ProjectDetailsDto`; raw AI response fields are not exposed.
- Added focused controller tests for successful queueing and failed transcription.

## Verification

- Focused: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~ProjectApiTests` — 6 passed.
- Full: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj` — 63 passed, 8 failed.
- The 8 full-suite failures are PostgreSQL integration tests blocked by the local environment: `Npgsql.PostgresException: 28000: role "dionysus" does not exist`.

## Scope

Changed only `Api/Controllers/ProjectsController.cs`, `Application/Contracts.cs`, `ProjectApiTests.cs`, plus this report. `Program`, migrations, queue implementation, and `SpecificationOrchestrator` were not changed.
