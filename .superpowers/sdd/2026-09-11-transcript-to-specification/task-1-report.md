# Task 1 Report: Persistent Specification Model and Migration

## Scope delivered

Implemented the persistent transcript-to-specification analysis aggregate for the .NET 8 / EF Core backend. The schema preserves provenance through relational source-link tables, rather than serialized identifier arrays, and enforces one recording and one analysis per project.

## Changed files

- `backend/Dionysus.Api/Domain/SpecificationEntities.cs` — analysis, topic, statement, relation, function, item, and source-link entities; required enums.
- `backend/Dionysus.Api/Domain/ProjectEntity.cs` — added `ProjectEntity.SpecificationAnalysis` and nullable `TranscriptSegment.CleanedText`; retained `Recordings`.
- `backend/Dionysus.Api/Infrastructure/Persistence/AppDbContext.cs` — DbSets, relationship mappings, composite keys, source-link mappings, and unique indexes.
- `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/20260910175804_AddSpecificationAnalysis.cs` — generated PostgreSQL migration.
- `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/20260910175804_AddSpecificationAnalysis.Designer.cs` — generated migration metadata.
- `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — generated current EF model snapshot.
- `backend/Dionysus.Api.Tests/SpecificationPersistenceTests.cs` — PostgreSQL-backed persistence coverage.

## TDD evidence

### RED

1. Added `SpecificationPersistenceTests` before any production implementation.
2. Ran:

   ```powershell
   dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPersistenceTests
   ```

3. The initial run identified missing explicit `System` / `System.Threading.Tasks` imports in the new test file because this test project does not enable implicit usings. Corrected those test imports only.
4. Reran the same command. It failed as intended because `SpecificationAnalysis`, `AnalysisTopic`, `AnalysisStatement`, `SpecificationFunction`, `SpecificationItem`, the source-link types, and their `AppDbContext` DbSets did not exist. Representative errors were `CS0246` and `CS1061` for missing specification types and `SpecificationAnalyses`.

### GREEN

1. Added the smallest persistence model and fluent mappings satisfying the tests and task requirements.
2. Generated `AddSpecificationAnalysis` with EF Core after selecting the Npgsql provider through a design-time `DATABASE_URL`.
3. Ran the focused tests in an isolated disposable PostgreSQL database on the repository Docker network:

   ```powershell
   docker run --rm --network <project-network> -e TEST_DATABASE_URL=<isolated-connection> -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPersistenceTests
   ```

   Result: passed 2/2.

4. The tests prove:
   - a second `VoiceRecording` for the same project throws `DbUpdateException` under PostgreSQL;
   - statement-to-segment, relation source/target-to-statement, function-to-statement, and item-to-statement links persist and reload correctly.

## Migration verification

Ran a disposable PostgreSQL upgrade fixture from the pre-transcript schema:

```powershell
dotnet ef database update 20260910133541_AddTranscriptSegments --project backend/Dionysus.Api/Dionysus.Api.csproj --startup-project backend/Dionysus.Api/Dionysus.Api.csproj
dotnet ef database update --project backend/Dionysus.Api/Dionysus.Api.csproj --startup-project backend/Dionysus.Api/Dionysus.Api.csproj
```

Both migrations applied successfully. A schema check confirmed all six principal specification tables exist: `SpecificationAnalyses`, `AnalysisTopics`, `AnalysisStatements`, `AnalysisRelations`, `SpecificationFunctions`, and `SpecificationItems` (count: 6). The disposable database was dropped afterward.

Verified the snapshot and model are synchronized:

```powershell
dotnet ef migrations has-pending-model-changes --project backend\Dionysus.Api\Dionysus.Api.csproj --startup-project backend\Dionysus.Api\Dionysus.Api.csproj
```

Result: `No changes have been made to the model since the last migration.`

## Full test result

Ran the complete backend test project in the repository Docker network with a disposable PostgreSQL test connection:

```powershell
docker run --rm --network <project-network> -e TEST_DATABASE_URL=<isolated-connection> -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj
```

Result: passed 23/23, failed 0.

## Self-review

- Confirmed every required enum value is present.
- Confirmed `SpecificationAnalysis` includes lifecycle state, run/retry/error metadata, stage 0–2 raw responses, and timestamps.
- Confirmed `CleanedText` is nullable and does not modify the original transcript text.
- Confirmed `Recordings` remains present while the database enforces the one-recording invariant through a unique index.
- Confirmed the one-analysis invariant through a second unique index.
- Confirmed required composite keys for `AnalysisStatementSegment` and `SpecificationItemStatement`; function and relation source-link tables also use composite keys.
- Confirmed all provenance paths are foreign-key relationships to statements or transcript segments; no JSON ID arrays were introduced.
- Confirmed generated migration adds `CleanedText`, all required tables, source foreign keys, and the unique indexes without data rewrite or deletion.
- Ran `git diff --check`; no whitespace errors.

## Commit

Implementation commit SHA: `08b21ac` (`feat: add persistent specification analysis model`).
