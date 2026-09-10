# Final fix report

Implemented the final specification-pipeline review findings on the current branch.

## Fixes

- Stage 2 business-context comparison now compares IDs, text, and source-segment values rather than list-reference equality.
- Publishing now creates the required standalone `BusinessContext` item first, with provenance links to its context statements.
- Retry cleanup explicitly removes all statement, topic, relation, function, item, and link rows before re-queueing, and clears stale raw stage responses.
- Stage 2 accepts renamed topics and reconciles merged topics by statement membership while preserving omitted topic rows safely.
- Stage 2 relation persistence replaces links safely and removes relations omitted by the review response.
- Workers now atomically claim queued analyses using the run-id concurrency token; only the winning worker receives the rotated claimed run ID.
- Transcription failures persist and expose only a generic client-safe error; provider exception text is not returned.
- Manual function/item create and update endpoints reload statement navigations before mapping API responses.

## Regression coverage

Added focused tests for each fix in:

- `SpecificationContractValidatorTests`
- `SpecificationOrchestratorTests`
- `SpecificationAnalysisQueueTests`
- `SpecificationApiTests`
- `ProjectApiTests`

## Verification

- `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --no-restore --filter "FullyQualifiedName!~SpecificationPersistenceTests"`: 85 passed.
- Full test command executed: 85 passed; 8 PostgreSQL integration tests could not connect because the local PostgreSQL role `dionysus` does not exist.
