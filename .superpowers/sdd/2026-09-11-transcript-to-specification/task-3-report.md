# Task 3 Report: Pure Contract Validation and Deterministic Prompts

## Scope delivered

Implemented only the Task 3 application boundary for the specification pipeline:

- pure, deterministic validators for stages 0 through 3;
- safe `SpecificationContractException` diagnostics containing only a stage and rule;
- deterministic instructions for all four AI stages;
- interfaces for validation, prompts, structured AI, the analysis queue, and orchestration;
- focused unit coverage for contract violations and prompt safety clauses.

No persistence, controller, queue worker, provider, DI registration, or migration code was changed.

## Implementation details

`SpecificationContractValidator` verifies exact schema version, nonempty/unique input and output identities, and source provenance without database or network access.

- Stage 0 requires the exact input segment sequence, including count, order, and IDs, and nonempty cleaned text.
- Stage 1 limits source segments to the supplied recording, validates required external ID formats, and rejects duplicate IDs.
- Stage 2 preserves business context and every statement's ID, text, and source IDs; assigns every statement to exactly one normalized topic; validates relation endpoints; and enforces the `supersedes` and `contradicts` status rules.
- Stage 3 limits every generated source link to statements in the supplied function and validates local item IDs, priorities, and question reasons.

The exception message is generated from fixed stage/rule identifiers only, so it cannot contain transcript text.

`SpecificationPromptFactory` supplies a fixed role, schema-version, strict-single-JSON/no-Markdown, and contract-shape instruction for each stage. It additionally requires stage-0 identity preservation, prevents stage-2 fact invention, and restricts stage-3 synthesis to supplied source statements while requiring `keyQuestions`.

`SpecificationInterfaces.cs` adds the contracts required by later tasks, including async `RunStage0Async` through `RunStage3Async` methods and queue/orchestrator signatures based on `SpecificationAnalysisJob(Guid AnalysisId, Guid RunId)`.

## TDD evidence

### RED

Created the validator and prompt tests before production types existed, then ran:

```powershell
dotnet test backend\Dionysus.Api.Tests\Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~SpecificationContractValidatorTests|FullyQualifiedName~SpecificationPromptFactoryTests"
```

The project failed to compile as expected because `SpecificationContractValidator` and `SpecificationPromptFactory` did not exist (`CS0246`).

### GREEN

Implemented the minimal pure validation, prompts, and interfaces, corrected the prompt's emitted JSON example to avoid escaped quote characters, and reran the same focused command.

Result: passed 13/13.

## Verification

`git diff --check` completed with no whitespace errors before the implementation commit.

The host full backend command compiled and ran 45 tests successfully, but its 8 existing PostgreSQL persistence tests could not initialize because the host database has no `dionysus` role (`PostgresException 28000`). This is the pre-existing local environment configuration noted in the Task 2 report, not an application test failure.

Reran the complete backend suite against a fresh disposable PostgreSQL 16 container on the repository Docker network with `TEST_DATABASE_URL` set to that container. The full command exited 0. The disposable database container was stopped and removed after verification.

## Commit

Implementation commit: `5ed07bc` (`feat: validate AI specification contracts`).
