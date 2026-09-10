# Task 5 Report

## Implemented

- Added `SpecificationOrchestrator` implementing `ISpecificationAnalysisOrchestrator.RunAsync`.
- Loads the analysis aggregate with its project, current recording, and transcript segments ordered by start time and ID.
- Executes stages 0, 1, and 2 through `IStructuredSpecificationAiService`.
- Persists only stage-0 `CleanedText`, preserving original transcript and segment text.
- Persists canonical JSON for successful stage responses in the stage raw-response fields.
- Persists stage-1 business-context statements, topics, statements, and transcript source links.
- Persists stage-2 topic membership, statement statuses, relations, and relation source/target links.
- Advances the analysis to `RunningStage3` without invoking or publishing stage 3 functions/items.
- Checks the job RunId and completed status before every mutation; stale jobs return without mutation.
- Propagates cancellation without marking the analysis failed.
- Marks contract/provider/persistence failures with only `stageN failed: ExceptionType`, retains earlier successful responses, and does not enqueue retries.
- Registered the orchestrator as scoped in `Program.cs` for worker resolution.

## Tests added

`SpecificationOrchestratorTests.cs` covers:

1. Cleaned segment persistence, cleaned-text stage-1 input, business context, topic membership, statement status/source links, relation links, raw responses, and `RunningStage3`.
2. Invalid stage 2 failure handling, safe diagnostic, prior raw-response retention, and absence of final functions/items.
3. Stale RunId no-op behavior.
4. Cancellation propagation without `Failed` status.

## Verification

- Focused suite: `dotnet test backend\\Dionysus.Api.Tests\\Dionysus.Api.Tests.csproj --no-restore --filter FullyQualifiedName~SpecificationOrchestratorTests` — 4 passed.
- Full non-PostgreSQL suite: `dotnet test backend\\Dionysus.Api.Tests\\Dionysus.Api.Tests.csproj --no-restore --filter FullyQualifiedName!~SpecificationPersistenceTests` — 59 passed.
- Full suite attempted: 59 passed, 8 failed during existing PostgreSQL persistence-test setup because the configured PostgreSQL role `dionysus` does not exist (`28000`). No Task 5 test failed.
- `git diff --check` passed.

## Commit

Committed directly on `main` as `Implement Task 5 specification orchestrator`.

## Fix round 1

Review findings addressed:

1. `SpecificationAnalysis.RunId` is now configured as an EF optimistic concurrency token. Every orchestrator save includes the job RunId in its concurrency predicate, so a RunId change after the guard check causes the stale save to fail atomically. The existing failure path then rechecks RunId and leaves the newer run untouched.
2. Stage 2 now clears `AnalysisTopicId` for every statement in the analysis before applying memberships from the response. Topics omitted from the response therefore no longer retain stale memberships.

Regression coverage added:

- `Run_id_change_during_save_rejects_the_stale_mutation` uses a save interceptor to change RunId between the orchestrator guard query and persistence, and verifies status/raw/cleaned data are unchanged.
- `Stage2_clears_memberships_for_topics_omitted_from_response` verifies an omitted topic has no statements after normalization.

Fix-round verification:

- Focused suite: `dotnet test backend\\Dionysus.Api.Tests\\Dionysus.Api.Tests.csproj --no-restore --filter FullyQualifiedName~SpecificationOrchestratorTests` — 6 passed.

Full-suite verification for fix round 1:

- Full suite: `dotnet test backend\\Dionysus.Api.Tests\\Dionysus.Api.Tests.csproj --no-restore` — 61 passed, 8 failed during existing PostgreSQL persistence-test setup because the configured PostgreSQL role `dionysus` does not exist (`28000`).
- The 8 failures are unchanged environment/setup failures; all Task 5 focused tests passed.
- API build after the fixes: passed with 0 warnings and 0 errors.
- A later fresh test-project build is blocked by unrelated uncommitted Task 4 edits in `ProjectApiTests.cs` (four-argument `ProjectsController`, `SpecificationStatus`, and missing `MemoryStream` imports). That file was left untouched and excluded from this commit.
- Fix round 1 was committed directly on `main` after verification.
