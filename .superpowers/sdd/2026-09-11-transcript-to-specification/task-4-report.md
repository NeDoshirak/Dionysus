# Task 4 Report: Structured Yandex AI Adapter and Durable Job Dispatch

Date: 2026-09-11
Status: Implemented on `main`.

## Implemented

- Added `StructuredYandexAiService`, which serializes stage requests with `SpecificationJson.Options`, calls the existing `ITextGenerationService` transport, strictly deserializes responses, and invokes the matching Task 3 validator.
- Malformed response JSON and contract violations are exposed as `SpecificationContractException`; provider failures are wrapped in `SpecificationAiInfrastructureException` with a safe public message. Cancellation is preserved.
- Added an unbounded single-reader `Channel<SpecificationAnalysisJob>` queue.
- Added a hosted worker that creates a DI scope for each job and invokes `ISpecificationAnalysisOrchestrator.RunAsync`.
- Added startup recovery for queued and interrupted stage-running analyses. Interrupted records are reset to `Queued`, receive a new `RunId`, are saved, and are enqueued.
- Registered the validator, prompt factory, structured adapter, singleton queue, and hosted worker in `Program.cs`.
- Added adapter tests for invalid JSON, provider-error sanitization, request serialization/validation, and queue tests for job identity plus in-memory restart recovery.

## Scope compliance

- No controller or database-schema changes.
- The existing `ITextGenerationService` remains the only Yandex transport.
- No real network calls are made by the tests.
- Work was performed directly on `main`; no subagents were dispatched.

## Verification

- Focused Task 4 suite: **5 passed, 0 failed**.
- Non-PostgreSQL suite: **55 passed, 0 failed**.
- Application build: **passed**, 0 warnings, 0 errors.
- Full test suite: **55 passed, 8 failed**. All 8 failures are pre-existing PostgreSQL integration tests that cannot connect in this environment because PostgreSQL rejects the configured local role: `role "dionysus" does not exist`. No Task 4 test failed.
- `git diff --check`: passed.

## Commit

The implementation and this report are committed together as:

`feat: add background specification analysis queue`

## Concerns

- The hosted worker resolves `ISpecificationAnalysisOrchestrator` when it processes a job; its concrete registration is intentionally left for Task 5, where the orchestrator is implemented.
- Full-suite completion remains blocked by the local PostgreSQL role/configuration issue noted above.
