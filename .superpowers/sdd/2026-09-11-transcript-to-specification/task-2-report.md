# Task 2 Report: Strict Stage and HTTP Contracts

## Scope delivered

Implemented only the Task 2 contract surface:

- immutable JSON records for stages 0–3, including every approved lower-camel field;
- strict `SpecificationJson` options and deserialization;
- public read/provenance/timestamp DTOs plus the exact manual editing request contracts;
- focused serialization coverage.

No controller, persistence, AI provider, queue, migration, or domain-enum changes were made.

## Changed files

- `backend/Dionysus.Api/Application/SpecificationContracts.cs`
  - Adds the stage 0–3 requests/responses and their nested records.
  - Uses `JsonRequired` for all stage JSON properties, including every mandatory stage-3 collection.
  - Adds strict web JSON options: camel-case names, case-sensitive binding, unmapped-member rejection, and camel-case string enum conversion.
  - Rejects empty, root-null, and nested-null AI JSON values before binding.
  - Adds `SpecificationDetailsDto`, function/item DTOs, source statement/segment timestamp DTOs, and the four manual-edit request DTOs.
- `backend/Dionysus.Api.Tests/SpecificationContractSerializationTests.cs`
  - Covers unknown and case-mismatched JSON fields, invalid stage-2 enum text, valid stage-3 empty arrays, an absent required stage-3 collection, and empty/null payloads.

## TDD evidence

### RED

Added `SpecificationContractSerializationTests.cs` before `SpecificationContracts.cs` existed, then ran:

```powershell
dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests
```

Result: compilation failed as intended with `CS0103`/`CS0246` because `SpecificationJson`, `Stage0CleanupResponse`, `Stage2ReviewResponse`, and `Stage3FunctionResponse` did not exist.

The tests name the behavioral breaks they prevent: accepting extra/case-mismatched JSON, accepting an invalid status enum, treating a required stage-3 array as optional, or accepting empty/null AI output.

### GREEN

Implemented the minimal records and serializer configuration, then reran the focused suite:

```powershell
dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests
```

Result: passed 7/7.

## Verification

`git diff --check` completed without whitespace errors.

The initial host full-suite invocation compiled successfully but could not authenticate the pre-existing local PostgreSQL integration database: 28 passed and 8 `SpecificationPersistenceTests` failed before test execution with missing role `dionysus`. The existing Compose database also rejected its default password, confirming environment configuration rather than a code regression.

Reran the full backend suite in the repository Docker network against a fresh, temporary PostgreSQL 16 container with the test suite's expected credentials. The container was stopped and removed as part of the command:

```powershell
docker run --rm --network dionysus_default -e TEST_DATABASE_URL='<temporary PostgreSQL connection>' -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj
```

Result: passed 36/36, failed 0, skipped 0.

## Self-review

- Stage 0 includes immutable transcript and cleaned-segment contracts.
- Stage 1 represents business context, topics, and atomic statements.
- Stage 2 represents normalized topics, statements with the Task 1 `AnalysisStatementStatus`, and relations with the Task 1 `AnalysisRelationType`.
- Stage 3 represents the function, roles, functional requirements, scenarios, constraints, conditions, agreements, and key questions; all eight stage-3 collections are `IReadOnlyList<T>` and required by JSON binding.
- `SpecificationJson` rejects unknown members, case mismatches, invalid camel-case enum strings, absent required members, null values, and empty payloads.
- No enum declarations were duplicated; the stage status/relation contracts reuse Task 1 domain enums.
- Request constructors match the Task 2 brief exactly.
- The only concern is local persistent Compose PostgreSQL credentials: they differ from the repository defaults, so local host integration runs require the appropriate `TEST_DATABASE_URL`. Isolated Docker verification is green.

## Commit

Implementation commit: `14a4679` (`feat: define specification pipeline contracts`).

---

# Review Fix Report, Round 1

## Findings addressed

1. `JsonStringEnumConverter` was configured with its default `allowIntegerValues: true`, which accepted numeric values for Task 1 statement-status and relation-type enums. The converter now uses `allowIntegerValues: false` while retaining camel-case string naming.
2. `SpecificationJson.Deserialize<T>` only checked whether binding produced a non-null value; it did not validate the root stage contract's version. All root stage contract records now implement `ISpecificationStageContract`, and the generic deserialize boundary rejects every version except exactly `"1.0"` after strict JSON binding.
3. Functional requirement priority is now an explicit `SpecificationPriority` enum so the same string-only policy applies to `required`, `desirable`, `future`, and `unknown`. Key-question reason is likewise represented by its finite `KeyQuestionReason` contract enum.

## TDD evidence

### RED

Added focused tests before changing production code, then ran:

```powershell
dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests
```

Result: 3 failed and 8 passed.

- A numeric stage-2 statement status was accepted.
- A numeric stage-2 relation type was accepted.
- An otherwise valid stage-3 response with `schemaVersion: "2.0"` was accepted.

The numeric requirement-priority test already threw because the old contract field was a string. It is retained as a regression test after changing priority to an explicit enum, which proves numeric enum tokens remain rejected by the shared converter policy.

### GREEN

Set `allowIntegerValues: false`, added the stage-contract marker boundary and exact-version check, and converted stage-3 priority/reason to their finite contract enums. Reran the focused suite:

```powershell
dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests
```

Result: passed 11/11.

## Verification

Ran the full backend suite with a fresh temporary PostgreSQL 16 container in the repository Docker network. The container was stopped and removed after the run:

```powershell
docker run --rm --network dionysus_default -e TEST_DATABASE_URL='<temporary PostgreSQL connection>' -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj
```

Result: passed 40/40, failed 0, skipped 0.

## Self-review

- Unknown-member rejection and case-sensitive binding remain configured before contract-version validation; no permissive fallback was added.
- Numeric enum values fail before a contract object can be accepted, while camel-case enum strings remain the supported wire format.
- The schema-version check applies uniformly to every root stage request and response through `ISpecificationStageContract`.
- The scope remains limited to Task 2 contracts, serialization tests, and this report.

## Commit

Review-fix implementation: `a862418` (`fix: enforce strict specification contract values`).
