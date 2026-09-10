# Task 9 report

Implemented Task 9 on `main`.

## Changes

- Added `SpecificationApiFactory` with PostgreSQL and Valkey Testcontainers.
- Replaced SMTP, media conversion, transcription, text generation, and specification queue services with deterministic test doubles.
- Added explicit JWT test clients, PostgreSQL schema setup, per-test truncation, and deterministic queued-job handling.
- Added `SpecificationPostgresTests` covering queued analysis creation, completed provenance and timestamp reconstruction, completed retry rejection, failed-only retry requeue, and cross-owner `404` behavior.
- Added `Testcontainers.PostgreSql` and `Testcontainers.Redis` package references. The Redis container runs the Valkey image because no `Testcontainers.Valkey` package exists on NuGet.

No Task 8 route was missing, so no planned test was omitted for that reason.

## Verification

- Focused command: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPostgresTests --no-restore`
- Result: 4 passed, 0 failed, 0 skipped.
- Full command: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --no-restore`
- Result: 75 passed, 8 failed. The 8 failures are existing `SpecificationPersistenceTests` that default to `Host=localhost;Port=5432;Database=dionysus;Username=dionysus;Password=dionysus` and fail with `role "dionysus" does not exist`; they are outside Task 9 files.

## Known repository limitation

The current migration chain assumes a pre-existing legacy schema: applying `Database.Migrate()` to a clean PostgreSQL container fails because `AddTranscriptSegments` references `VoiceRecordings` before a baseline migration exists. The Task 9 fixture therefore uses `EnsureCreated` for a clean container schema and reports this limitation without modifying production or migration files.
