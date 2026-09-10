# Transcript-to-Specification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically turn the single timestamped recording of a project into one editable, source-linked technical specification through the four-stage Yandex AI pipeline.

**Architecture:** Keep the existing single ASP.NET Core project and create focused Domain, Application, Infrastructure, and API files. A durable PostgreSQL analysis aggregate records state and raw AI responses; an in-process `Channel` queue plus `BackgroundService` runs one analysis at a time per project and resumes queued work after restart. AI output is accepted only after strict deserialization and provenance validation, then persisted as normalized specification functions and items.

**Tech Stack:** .NET 8, ASP.NET Core controllers, EF Core 8/Npgsql/PostgreSQL, hosted services and `System.Threading.Channels`, Yandex AI Studio Responses API, xUnit, Testcontainers for PostgreSQL and Valkey, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-11-transcript-to-specification-design.md`

## Global Constraints

- A project has exactly one `VoiceRecording` and exactly one `SpecificationAnalysis`.
- The original Whisper transcript and audio remain unchanged; stage 0 writes only `TranscriptSegment.CleanedText`.
- AI responses use JSON contract version `1.0`, must contain no unknown fields, and must preserve permitted source IDs exactly.
- Only a failed analysis may be retried. A completed, queued, or running analysis must never be started again.
- Stage 3 sends at most four Yandex AI requests concurrently and publishes final specification content only after every function succeeds.
- All specification routes require JWT and return `404` for another user's project.
- AI-generated final items require source statements; manually created items are explicitly marked `IsManual=true` and may have no source links.
- Keep Yandex API credentials in environment variables only; do not log request bodies, response bodies, keys, or transcript content on failures.

---

## File Structure

| Path | Responsibility |
| --- | --- |
| `backend/Dionysus.Api/Domain/SpecificationEntities.cs` | Persistent analysis, extracted facts, relations, specification functions, items, source-link entities, and enums. |
| `backend/Dionysus.Api/Domain/ProjectEntity.cs` | Existing project/recording model, extended with one analysis navigation and `CleanedText`. |
| `backend/Dionysus.Api/Application/SpecificationContracts.cs` | JSON DTOs for stages 0–3 and public API request/response DTOs. |
| `backend/Dionysus.Api/Application/SpecificationInterfaces.cs` | Queue, orchestrator, structured AI, and contract-validation interfaces. |
| `backend/Dionysus.Api/Application/SpecificationContractValidator.cs` | Pure validation of JSON contracts, IDs, references, and provenance rules. |
| `backend/Dionysus.Api/Application/SpecificationPromptFactory.cs` | Deterministic stage instructions built from the fixed contracts. |
| `backend/Dionysus.Api/Application/SpecificationOrchestrator.cs` | Transactional stage execution and status transitions. |
| `backend/Dionysus.Api/Infrastructure/Persistence/AppDbContext.cs` | DbSets, relationships, uniqueness constraints, indexes, and concurrency configuration. |
| `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/*AddSpecificationAnalysis*` | PostgreSQL migration for the specification schema and one-recording invariant. |
| `backend/Dionysus.Api/Infrastructure/Services/StructuredYandexAiService.cs` | Calls existing Yandex client and strictly parses contract JSON. |
| `backend/Dionysus.Api/Infrastructure/Services/SpecificationAnalysisQueue.cs` | Channel-backed queue and hosted worker, including restart recovery. |
| `backend/Dionysus.Api/Api/Controllers/SpecificationsController.cs` | JWT-protected read, retry, and manual editing endpoints. |
| `backend/Dionysus.Api/Api/Controllers/ProjectsController.cs` | Creates the analysis and queues it only after successful transcription. |
| `backend/Dionysus.Api/Program.cs` | Registers analysis services and hosted worker. |
| `backend/Dionysus.Api.Tests/SpecificationContractValidatorTests.cs` | Unit tests for all contract invariants. |
| `backend/Dionysus.Api.Tests/SpecificationOrchestratorTests.cs` | Unit tests for stage ordering, failure, retry, and bounded stage-3 parallelism. |
| `backend/Dionysus.Api.Tests/SpecificationApiTests.cs` | API authorization, lifecycle, ownership, and editing tests. |
| `backend/Dionysus.Api.Tests/SpecificationPostgresTests.cs` | PostgreSQL/Valkey integration coverage using Testcontainers. |
| `backend/Dionysus.Api.Tests/Support/*` | Isolated test host, scripted AI service, and deterministic queue test doubles. |
| `README.md` | Runtime behavior, routes, analysis lifecycle, and retry instructions. |

### Task 1: Create the persistent specification model and migration

**Files:**
- Create: `backend/Dionysus.Api/Domain/SpecificationEntities.cs`
- Modify: `backend/Dionysus.Api/Domain/ProjectEntity.cs`
- Modify: `backend/Dionysus.Api/Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/<timestamp>_AddSpecificationAnalysis.cs`
- Create: `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/<timestamp>_AddSpecificationAnalysis.Designer.cs`
- Modify: `backend/Dionysus.Api/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationPersistenceTests.cs`

**Interfaces:**
- Produces: `SpecificationAnalysis`, `AnalysisTopic`, `AnalysisStatement`, `AnalysisRelation`, `SpecificationFunction`, `SpecificationItem`, their source-link entities, and enums used by every remaining task.
- Produces: `ProjectEntity.SpecificationAnalysis`, `TranscriptSegment.CleanedText`, and database uniqueness for one recording and one analysis per project.

- [ ] **Step 1: Write failing persistence tests for the cardinality and source links**

```csharp
[Fact]
public async Task Project_accepts_one_recording_and_one_analysis_only()
{
    await using var db = CreatePostgresContext();
    var project = new ProjectEntity { OwnerId = "user-1", Name = "Interview" };
    db.Projects.Add(project);
    db.VoiceRecordings.Add(new VoiceRecording { ProjectEntityId = project.Id, FileName = "one.wav", ContentType = "audio/wav", SourceType = "audio" });
    db.SpecificationAnalyses.Add(new SpecificationAnalysis { ProjectEntityId = project.Id, Status = SpecificationAnalysisStatus.Queued });
    await db.SaveChangesAsync();

    db.VoiceRecordings.Add(new VoiceRecording { ProjectEntityId = project.Id, FileName = "two.wav", ContentType = "audio/wav", SourceType = "audio" });
    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
}
```

- [ ] **Step 2: Run the persistence test to verify the missing schema fails**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPersistenceTests`

Expected: FAIL because `SpecificationAnalysis` and its DbSet do not exist.

- [ ] **Step 3: Add focused domain entities and enums**

```csharp
public enum SpecificationAnalysisStatus { Queued, RunningStage0, RunningStage1, RunningStage2, RunningStage3, Completed, Failed }
public enum AnalysisStatementStatus { Active, Superseded, Unresolved }
public enum AnalysisRelationType { Same, Clarifies, Supersedes, Contradicts, Unresolved, Related }
public enum SpecificationItemKind { BusinessContext, Role, FunctionalRequirement, UserScenario, Constraint, Condition, Agreement, KeyQuestion, ProjectContradiction }

public sealed class SpecificationAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectEntityId { get; set; }
    public SpecificationAnalysisStatus Status { get; set; } = SpecificationAnalysisStatus.Queued;
    public Guid RunId { get; set; } = Guid.NewGuid();
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public string? Stage0RawResponse { get; set; }
    public string? Stage1RawResponse { get; set; }
    public string? Stage2RawResponse { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
```

Add `CleanedText` to `TranscriptSegment`. Add `SpecificationAnalysis? SpecificationAnalysis` to `ProjectEntity`; retain `Recordings` for compatibility with current project DTOs, while making `VoiceRecording.ProjectEntityId` unique at the database level. Model relation/source collections explicitly: a statement has segment links; a relation has source and target statement links; a function and item have statement links.

- [ ] **Step 4: Configure EF Core relationships and create the migration**

```csharp
modelBuilder.Entity<VoiceRecording>().HasIndex(x => x.ProjectEntityId).IsUnique();
modelBuilder.Entity<SpecificationAnalysis>().HasIndex(x => x.ProjectEntityId).IsUnique();
modelBuilder.Entity<AnalysisStatementSegment>().HasKey(x => new { x.AnalysisStatementId, x.TranscriptSegmentId });
modelBuilder.Entity<SpecificationItemStatement>().HasKey(x => new { x.SpecificationItemId, x.AnalysisStatementId });
```

Run: `dotnet ef migrations add AddSpecificationAnalysis --project backend/Dionysus.Api/Dionysus.Api.csproj --startup-project backend/Dionysus.Api/Dionysus.Api.csproj`

Inspect the generated `Up` method: it must create all specification tables, add `CleanedText`, add the two unique indexes, and preserve foreign keys from source links to existing transcript segments.

- [ ] **Step 5: Run the persistence tests and migration smoke test**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPersistenceTests`

Expected: PASS. Then run `dotnet ef database update --project backend/Dionysus.Api/Dionysus.Api.csproj --startup-project backend/Dionysus.Api/Dionysus.Api.csproj` against a disposable local database and confirm the migration applies.

- [ ] **Step 6: Commit the schema change**

```bash
git add backend/Dionysus.Api/Domain backend/Dionysus.Api/Infrastructure/Persistence backend/Dionysus.Api.Tests/SpecificationPersistenceTests.cs
git commit -m "feat: add persistent specification analysis model"
```

### Task 2: Define strict stage and HTTP contracts

**Files:**
- Create: `backend/Dionysus.Api/Application/SpecificationContracts.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationContractSerializationTests.cs`

**Interfaces:**
- Consumes: domain enums from Task 1.
- Produces: `Stage0CleanupRequest`, `Stage0CleanupResponse`, `Stage1ExtractionResponse`, `Stage2ReviewResponse`, `Stage3FunctionResponse`, and API DTOs consumed by Tasks 3–8.

- [ ] **Step 1: Write failing JSON serialization tests**

```csharp
[Fact]
public void Stage0_response_rejects_unknown_json_fields()
{
    const string json = """{ "schemaVersion":"1.0", "segments":[], "extra":true }""";
    Assert.Throws<JsonException>(() => SpecificationJson.Deserialize<Stage0CleanupResponse>(json));
}

[Fact]
public void Stage3_response_uses_required_empty_arrays()
{
    var response = SpecificationJson.Deserialize<Stage3FunctionResponse>(ValidStage3Json);
    Assert.NotNull(response.KeyQuestions);
    Assert.Empty(response.KeyQuestions);
}
```

- [ ] **Step 2: Run contract serialization tests to verify they fail**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests`

Expected: FAIL because `SpecificationJson` and stage contract types do not exist.

- [ ] **Step 3: Implement immutable contract records and strict serializer options**

```csharp
public static class SpecificationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException("AI response is empty");
}

public sealed record Stage0CleanupRequest(string SchemaVersion, IReadOnlyList<StageSegmentDto> Segments);
public sealed record Stage0CleanupResponse(string SchemaVersion, IReadOnlyList<Stage0CleanedSegmentDto> Segments);
public sealed record StageSegmentDto(Guid Id, double StartSeconds, double EndSeconds, string Text);
public sealed record Stage0CleanedSegmentDto(Guid SegmentId, string CleanedText);
```

Define all field names from the approved JSON contract, including explicit `IReadOnlyList<T>` fields for every stage-3 collection. Add public DTOs such as `SpecificationDetailsDto`, `SpecificationFunctionDto`, `SpecificationItemDto`, `CreateSpecificationFunctionRequest`, `UpdateSpecificationFunctionRequest`, `CreateSpecificationItemRequest`, and `UpdateSpecificationItemRequest`.

Use these editing request signatures so controller and test code share one contract:

```csharp
public sealed record CreateSpecificationFunctionRequest(string Title, string Description, int SortOrder, IReadOnlyList<Guid> SourceStatementIds);
public sealed record UpdateSpecificationFunctionRequest(string Title, string Description, int SortOrder, IReadOnlyList<Guid>? SourceStatementIds);
public sealed record CreateSpecificationItemRequest(SpecificationItemKind Kind, string? Title, string Description, string? Priority, IReadOnlyList<Guid> SourceStatementIds);
public sealed record UpdateSpecificationItemRequest(string? Title, string Description, string? Priority, IReadOnlyList<Guid>? SourceStatementIds);
```

- [ ] **Step 4: Run serialization tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractSerializationTests`

Expected: PASS, including rejection of unknown properties and missing required collections.

- [ ] **Step 5: Commit the contracts**

```bash
git add backend/Dionysus.Api/Application/SpecificationContracts.cs backend/Dionysus.Api.Tests/SpecificationContractSerializationTests.cs
git commit -m "feat: define specification pipeline contracts"
```

### Task 3: Implement pure contract validation and deterministic prompts

**Files:**
- Create: `backend/Dionysus.Api/Application/SpecificationContractValidator.cs`
- Create: `backend/Dionysus.Api/Application/SpecificationPromptFactory.cs`
- Create: `backend/Dionysus.Api/Application/SpecificationInterfaces.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationContractValidatorTests.cs`

**Interfaces:**
- Consumes: stage contracts from Task 2 and transcript segments from Task 1.
- Produces: `ISpecificationContractValidator`, `ISpecificationPromptFactory`, `IStructuredSpecificationAiService`, and validation exceptions used by Tasks 4–6.

- [ ] **Step 1: Write failing invariant tests**

```csharp
[Fact]
public void Stage0_requires_exactly_the_input_segment_ids_once()
{
    var input = new Stage0CleanupRequest("1.0", [Segment("00000000-0000-0000-0000-000000000001")]);
    var output = new Stage0CleanupResponse("1.0", []);

    Assert.Throws<SpecificationContractException>(() => validator.ValidateStage0(input, output));
}

[Fact]
public void Stage2_rejects_new_statement_text_or_source_segments()
{
    var stage1 = ValidStage1();
    var stage2 = ValidStage2() with { Statements = [ValidStage2().Statements[0] with { Text = "invented" }] };

    Assert.Throws<SpecificationContractException>(() => validator.ValidateStage2(stage1, stage2));
}
```

- [ ] **Step 2: Run validator tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationContractValidatorTests`

Expected: FAIL because the validator and exception type do not exist.

- [ ] **Step 3: Implement validators for every contract boundary**

```csharp
public interface ISpecificationContractValidator
{
    void ValidateStage0(Stage0CleanupRequest input, Stage0CleanupResponse output);
    void ValidateStage1(IReadOnlyCollection<TranscriptSegment> segments, Stage1ExtractionResponse output);
    void ValidateStage2(Stage1ExtractionResponse input, Stage2ReviewResponse output);
    void ValidateStage3(Stage3FunctionRequest input, Stage3FunctionResponse output);
}
```

Implement exact schema-version checks, nonempty/unique ID checks, known source segment checks, stage-2 statement identity preservation, one-topic-per-statement checks, relation endpoint checks, status/relation compatibility, and stage-3 source statement checks. `SpecificationContractException` contains only a safe stage name and rule name, never transcript text.

- [ ] **Step 4: Implement prompt generation with the contract embedded verbatim**

```csharp
public interface ISpecificationPromptFactory
{
    string Stage0Instructions();
    string Stage1Instructions();
    string Stage2Instructions();
    string Stage3Instructions();
}
```

Each instruction states the role, the allowed transformation, `schemaVersion: "1.0"`, the exact JSON shape, and “return one JSON object only; no Markdown”. Stage 0 explicitly preserves input segment count/order/IDs; stage 2 forbids new facts; stage 3 permits synthesis only from source statements and requires `keyQuestions`.

- [ ] **Step 5: Run validator and prompt tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~SpecificationContractValidatorTests|FullyQualifiedName~SpecificationPromptFactoryTests"`

Expected: PASS, with tests covering unknown IDs, duplicates, contradicting relation rules, empty stage-3 arrays, and required prompt safety clauses.

- [ ] **Step 6: Commit validation and prompts**

```bash
git add backend/Dionysus.Api/Application/SpecificationContractValidator.cs backend/Dionysus.Api/Application/SpecificationPromptFactory.cs backend/Dionysus.Api/Application/SpecificationInterfaces.cs backend/Dionysus.Api.Tests/SpecificationContractValidatorTests.cs backend/Dionysus.Api.Tests/SpecificationPromptFactoryTests.cs
git commit -m "feat: validate AI specification contracts"
```

### Task 4: Add structured Yandex AI adapter and durable job dispatch

**Files:**
- Create: `backend/Dionysus.Api/Infrastructure/Services/StructuredYandexAiService.cs`
- Create: `backend/Dionysus.Api/Infrastructure/Services/SpecificationAnalysisQueue.cs`
- Modify: `backend/Dionysus.Api/Program.cs`
- Test: `backend/Dionysus.Api.Tests/StructuredYandexAiServiceTests.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationAnalysisQueueTests.cs`

**Interfaces:**
- Consumes: `ITextGenerationService`, contracts, validator, and prompt factory from Tasks 2–3.
- Produces: `IStructuredSpecificationAiService.RunStage0Async`, `RunStage1Async`, `RunStage2Async`, `RunStage3Async`; `ISpecificationAnalysisQueue.EnqueueAsync`; and a hosted worker consumed by Task 5.

- [ ] **Step 1: Write failing adapter and queue tests**

```csharp
[Fact]
public async Task Structured_service_converts_invalid_model_json_to_contract_exception()
{
    var service = new StructuredYandexAiService(new ScriptedTextGenerationService("not json"), prompts, validator);
    await Assert.ThrowsAsync<SpecificationContractException>(() => service.RunStage1Async(ValidStage1Request(), CancellationToken.None));
}

[Fact]
public async Task Queue_delivers_a_job_with_its_run_id()
{
    var queue = new SpecificationAnalysisQueue();
    var job = new SpecificationAnalysisJob(Guid.NewGuid(), Guid.NewGuid());
    await queue.EnqueueAsync(job, CancellationToken.None);
    Assert.Equal(job, await queue.DequeueAsync(CancellationToken.None));
}
```

- [ ] **Step 2: Run these tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~StructuredYandexAiServiceTests|FullyQualifiedName~SpecificationAnalysisQueueTests"`

Expected: FAIL because structured adapter and queue types do not exist.

- [ ] **Step 3: Implement structured parsing over the existing Yandex client**

```csharp
public interface IStructuredSpecificationAiService
{
    Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct);
    Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct);
    Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct);
    Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct);
}
```

For each method serialize only the stage request with `SpecificationJson.Options`, call `ITextGenerationService.RespondAsync`, deserialize the returned text with the same options, and invoke the matching validator. Map malformed JSON and validation failures to `SpecificationContractException`; map provider failures to a safe infrastructure exception.

- [ ] **Step 4: Implement channel queue, worker startup recovery, and DI**

```csharp
public sealed record SpecificationAnalysisJob(Guid AnalysisId, Guid RunId);
public interface ISpecificationAnalysisQueue
{
    ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct);
    ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct);
}
```

Use an unbounded `Channel<SpecificationAnalysisJob>` with a single reader. The hosted worker creates a DI scope per job and calls `ISpecificationAnalysisOrchestrator.RunAsync(job, stoppingToken)`. In `StartAsync`, query analyses in `Queued` or any `RunningStage*` state, set running records back to `Queued` with a new `RunId`, save, and enqueue each record. Register queue as singleton, worker as hosted service, and structured service/validator/prompts as scoped services.

- [ ] **Step 5: Run adapter and queue tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~StructuredYandexAiServiceTests|FullyQualifiedName~SpecificationAnalysisQueueTests"`

Expected: PASS; no network request is made because tests use `ScriptedTextGenerationService`.

- [ ] **Step 6: Commit queue and structured adapter**

```bash
git add backend/Dionysus.Api/Infrastructure/Services/StructuredYandexAiService.cs backend/Dionysus.Api/Infrastructure/Services/SpecificationAnalysisQueue.cs backend/Dionysus.Api/Program.cs backend/Dionysus.Api.Tests/StructuredYandexAiServiceTests.cs backend/Dionysus.Api.Tests/SpecificationAnalysisQueueTests.cs
git commit -m "feat: add background specification analysis queue"
```

### Task 5: Orchestrate stages 0–2 and persist normalized evidence

**Files:**
- Create: `backend/Dionysus.Api/Application/SpecificationOrchestrator.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationOrchestratorTests.cs`

**Interfaces:**
- Consumes: analysis entities from Task 1, structured AI and queue abstractions from Task 4.
- Produces: `ISpecificationAnalysisOrchestrator.RunAsync(SpecificationAnalysisJob, CancellationToken)` and persisted cleaned segments, business context, topics, statements, source links, and relations for Task 6.

- [ ] **Step 1: Write failing lifecycle tests for stages 0–2**

```csharp
[Fact]
public async Task Run_persists_cleaned_segments_and_reviewed_statements_before_stage3()
{
    var fixture = await SpecificationFixture.CreateAsync(scriptedAi: ValidStage0ThenStage1ThenStage2());
    await fixture.Orchestrator.RunAsync(fixture.Job, CancellationToken.None);

    var segment = await fixture.Db.TranscriptSegments.SingleAsync();
    Assert.Equal("Исправленный текст.", segment.CleanedText);
    Assert.Equal(2, await fixture.Db.AnalysisStatements.CountAsync());
    Assert.Equal(1, await fixture.Db.AnalysisRelations.CountAsync());
    Assert.Equal(SpecificationAnalysisStatus.RunningStage3, await fixture.AnalysisStatusAsync());
}

[Fact]
public async Task Invalid_stage2_marks_analysis_failed_and_leaves_final_specification_unpublished()
{
    var fixture = await SpecificationFixture.CreateAsync(scriptedAi: InvalidStage2());
    await fixture.Orchestrator.RunAsync(fixture.Job, CancellationToken.None);
    Assert.Equal(SpecificationAnalysisStatus.Failed, await fixture.AnalysisStatusAsync());
    Assert.Empty(await fixture.Db.SpecificationFunctions.ToListAsync());
}
```

- [ ] **Step 2: Run lifecycle tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationOrchestratorTests`

Expected: FAIL because the orchestrator does not exist.

- [ ] **Step 3: Implement guarded status transitions and stages 0–2**

```csharp
public interface ISpecificationAnalysisOrchestrator
{
    Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct);
}
```

Load the analysis with project, its single recording, and ordered transcript segments. Before every stage, confirm `analysis.RunId == job.RunId` and the analysis is not completed; otherwise return without mutation. Persist the raw response only after validation. Transition through `RunningStage0`, `RunningStage1`, `RunningStage2`, and then `RunningStage3`, saving after each successful stage. Persist stage-1 statements with external IDs and source segment links, then persist stage-2 normalized topics and relations. This task deliberately ends in `RunningStage3`; Task 6 supplies the final publication logic.

- [ ] **Step 4: Implement safe failure handling**

```csharp
private static void Fail(SpecificationAnalysis analysis, string stage, Exception exception)
{
    analysis.Status = SpecificationAnalysisStatus.Failed;
    analysis.Error = $"{stage} failed: {exception.GetType().Name}";
}
```

On contract, network, cancellation-not-requested, or persistence error, set `Failed`, retain raw responses from prior successful stages, save the safe diagnostic, and do not enqueue another run. Let application shutdown cancellation propagate without incorrectly marking the analysis failed.

- [ ] **Step 5: Run the stage 0–2 lifecycle tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationOrchestratorTests`

Expected: PASS for cleaned text, evidence persistence, stale job suppression, and safe failure.

- [ ] **Step 6: Commit stages 0–2 orchestration**

```bash
git add backend/Dionysus.Api/Application/SpecificationOrchestrator.cs backend/Dionysus.Api.Tests/SpecificationOrchestratorTests.cs
git commit -m "feat: persist specification analysis evidence"
```

### Task 6: Run stage 3 concurrently and publish the final specification atomically

**Files:**
- Modify: `backend/Dionysus.Api/Application/SpecificationOrchestrator.cs`
- Modify: `backend/Dionysus.Api/Application/SpecificationContracts.cs`
- Modify: `backend/Dionysus.Api.Tests/SpecificationOrchestratorTests.cs`

**Interfaces:**
- Consumes: persisted stage-2 topics/statements/relations from Task 5.
- Produces: completed `SpecificationFunction` and `SpecificationItem` rows, project-level contradiction items, and terminal `Completed` status used by Task 8.

- [ ] **Step 1: Add failing concurrency and publication tests**

```csharp
[Fact]
public async Task Stage3_runs_no_more_than_four_function_requests_at_once()
{
    var ai = new BlockingStage3AiService(functionCount: 7);
    var fixture = await SpecificationFixture.CreateAtStage3Async(ai);

    await fixture.Orchestrator.RunAsync(fixture.Job, CancellationToken.None);

    Assert.InRange(ai.MaximumConcurrentStage3Calls, 1, 4);
    Assert.Equal(7, await fixture.Db.SpecificationFunctions.CountAsync());
}

[Fact]
public async Task Failed_one_stage3_function_publishes_no_final_functions()
{
    var fixture = await SpecificationFixture.CreateAtStage3Async(new FailingSecondFunctionAiService());
    await fixture.Orchestrator.RunAsync(fixture.Job, CancellationToken.None);

    Assert.Equal(SpecificationAnalysisStatus.Failed, await fixture.AnalysisStatusAsync());
    Assert.Empty(await fixture.Db.SpecificationFunctions.ToListAsync());
}
```

- [ ] **Step 2: Run stage-3 tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationOrchestratorTests`

Expected: FAIL because stage 3 is not orchestrated.

- [ ] **Step 3: Build one independent request per normalized topic with a concurrency cap**

```csharp
using var gate = new SemaphoreSlim(4, 4);
var outputs = await Task.WhenAll(topics.Select(async topic =>
{
    await gate.WaitAsync(ct);
    try { return await structuredAi.RunStage3Async(BuildStage3Request(topic), ct); }
    finally { gate.Release(); }
}));
```

Include active and unresolved statements from the topic plus all relations whose endpoints are inside that topic. Use business-context records as common context. Validate every response before returning it from the task; do not create final database rows while requests are still running.

- [ ] **Step 4: Persist final content in one transaction**

Create `SpecificationFunction` rows in topic order. Map every stage-3 array to a `SpecificationItem` with its proper `SpecificationItemKind`, title/description, optional priority/question reason, stable sort order, `IsManual=false`, and source statement links. Then create `ProjectContradiction` items after all functions, one per stage-2 `Contradicts` relation, with both statement sources. Save all rows and set `Completed`/`CompletedAt` in a transaction; if mapping or saving fails, roll back final rows and mark the analysis failed separately.

- [ ] **Step 5: Run stage-3 lifecycle tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationOrchestratorTests`

Expected: PASS for cap of four, complete source provenance, contradiction placement, and no partial publication.

- [ ] **Step 6: Commit stage 3**

```bash
git add backend/Dionysus.Api/Application/SpecificationOrchestrator.cs backend/Dionysus.Api/Application/SpecificationContracts.cs backend/Dionysus.Api.Tests/SpecificationOrchestratorTests.cs
git commit -m "feat: publish AI generated specification functions"
```

### Task 7: Queue analysis after successful project transcription and enforce the single-recording workflow

**Files:**
- Modify: `backend/Dionysus.Api/Api/Controllers/ProjectsController.cs`
- Modify: `backend/Dionysus.Api/Application/Contracts.cs`
- Modify: `backend/Dionysus.Api.Tests/ProjectApiTests.cs`

**Interfaces:**
- Consumes: `ISpecificationAnalysisQueue` and `SpecificationAnalysis` from Tasks 1 and 4.
- Produces: projects that own a queued analysis after successful Whisper transcription; `ProjectDetailsDto` with a concise analysis status.

- [ ] **Step 1: Write failing upload lifecycle tests**

```csharp
[Fact]
public async Task Successful_transcription_creates_and_queues_one_analysis()
{
    var queue = new RecordingSpecificationQueue();
    var controller = CreateProjectsController(transcription: CompletedTranscription(), queue: queue);

    var result = await controller.Create(ValidProjectForm(), CancellationToken.None);

    Assert.IsType<CreatedAtActionResult>(result);
    Assert.Single(queue.Jobs);
    Assert.Equal(SpecificationAnalysisStatus.Queued, (await db.SpecificationAnalyses.SingleAsync()).Status);
}

[Fact]
public async Task Failed_transcription_does_not_create_analysis()
{
    var controller = CreateProjectsController(transcription: ThrowingTranscription(), queue: new RecordingSpecificationQueue());
    await controller.Create(ValidProjectForm(), CancellationToken.None);
    Assert.Empty(await db.SpecificationAnalyses.ToListAsync());
}
```

- [ ] **Step 2: Run upload lifecycle tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~ProjectApiTests"

Expected: FAIL because `ProjectsController` does not accept the queue and does not create analysis records.

- [ ] **Step 3: Create analysis only after the recording status is completed**

```csharp
var analysis = new SpecificationAnalysis
{
    ProjectEntityId = project.Id,
    Status = SpecificationAnalysisStatus.Queued
};
db.SpecificationAnalyses.Add(analysis);
await db.SaveChangesAsync(ct);
await specificationQueue.EnqueueAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), ct);
```

Add `SpecificationStatus` to project details without exposing raw AI responses. If enqueue throws after persistence, leave the record queued; startup recovery from Task 4 will dispatch it. Do not add a second upload endpoint: creating a project remains the only recording creation path.

- [ ] **Step 4: Run project API tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~ProjectApiTests`

Expected: PASS, including the existing stream endpoint tests.

- [ ] **Step 5: Commit project integration**

```bash
git add backend/Dionysus.Api/Api/Controllers/ProjectsController.cs backend/Dionysus.Api/Application/Contracts.cs backend/Dionysus.Api.Tests/ProjectApiTests.cs
git commit -m "feat: queue specification analysis after transcription"
```

### Task 8: Expose protected specification read, retry, and editing endpoints

**Files:**
- Create: `backend/Dionysus.Api/Api/Controllers/SpecificationsController.cs`
- Create: `backend/Dionysus.Api/Application/SpecificationMapper.cs`
- Modify: `backend/Dionysus.Api/Application/SpecificationInterfaces.cs`
- Test: `backend/Dionysus.Api.Tests/SpecificationApiTests.cs`

**Interfaces:**
- Consumes: persisted analysis aggregate and queue from Tasks 1, 4, and 6; HTTP contracts from Task 2.
- Produces: all routes defined in the approved spec and Swagger-visible contracts.

- [ ] **Step 1: Write failing API authorization, retry, and edit tests**

```csharp
[Fact]
public async Task Other_user_gets_not_found_for_specification()
{
    var response = await ClientFor("user-2").GetAsync($"/api/projects/{projectOwnedByUser1}/specification");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}

[Fact]
public async Task Retry_accepts_only_failed_analysis_and_enqueues_new_run()
{
    var response = await ClientFor("user-1").PostAsync($"/api/projects/{failedProjectId}/specification/retry", null);
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    Assert.Single(queue.Jobs);
}

[Fact]
public async Task Manual_item_is_marked_manual_and_can_be_updated_then_deleted()
{
    var created = await ClientFor("user-1").PostAsJsonAsync(ItemUrl(functionId), new CreateSpecificationItemRequest(SpecificationItemKind.Constraint, null, "Manual restriction", null, []));
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
}
```

- [ ] **Step 2: Run specification API tests to verify failure**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationApiTests`

Expected: FAIL because `SpecificationsController` does not exist.

- [ ] **Step 3: Implement the read and retry endpoints**

```csharp
[HttpGet("/api/projects/{projectId:guid}/specification")]
public Task<IActionResult> Get(Guid projectId, CancellationToken ct);

[HttpPost("/api/projects/{projectId:guid}/specification/retry")]
public Task<IActionResult> Retry(Guid projectId, CancellationToken ct);
```

Load through `Projects.Where(p => p.Id == projectId && p.OwnerId == userId)`. `Get` returns analysis status/error, business context, ordered functions/items, source statements, and source segment IDs with reconstructed start/end seconds. `Retry` returns `404` for another user or missing project, `409 Conflict` unless status is `Failed`, deletes only unfinished stage/final rows, creates a new `RunId`, increments `RetryCount`, changes status to `Queued`, saves, and enqueues one job.

- [ ] **Step 4: Implement function and item editing endpoints**

```csharp
[HttpPost("/api/projects/{projectId:guid}/specification/functions")]
public Task<IActionResult> CreateFunction(Guid projectId, CreateSpecificationFunctionRequest request, CancellationToken ct);

[HttpPatch("/api/projects/{projectId:guid}/specification/functions/{functionId:guid}")]
public Task<IActionResult> UpdateFunction(Guid projectId, Guid functionId, UpdateSpecificationFunctionRequest request, CancellationToken ct);

[HttpPost("/api/projects/{projectId:guid}/specification/functions/{functionId:guid}/items")]
public Task<IActionResult> CreateItem(Guid projectId, Guid functionId, CreateSpecificationItemRequest request, CancellationToken ct);
```

Implement the matching `GET` function route and `DELETE`/`PATCH` routes from the spec. Restrict manual edits to completed analyses. Validate all supplied source statement IDs belong to the project's analysis. Set `IsManual=true` on manual create; patch does not erase existing AI provenance unless the request explicitly replaces source IDs.

- [ ] **Step 5: Run API tests and inspect Swagger security metadata**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationApiTests`

Expected: PASS for unauthorized `401`, cross-owner `404`, failed-only retry, source-link validation, CRUD, and deletion cascade. Then run `dotnet run --project backend/Dionysus.Api/Dionysus.Api.csproj` and confirm Swagger shows locks on every specification route.

- [ ] **Step 6: Commit HTTP API**

```bash
git add backend/Dionysus.Api/Api/Controllers/SpecificationsController.cs backend/Dionysus.Api/Application/SpecificationMapper.cs backend/Dionysus.Api/Application/SpecificationInterfaces.cs backend/Dionysus.Api.Tests/SpecificationApiTests.cs
git commit -m "feat: expose editable project specifications"
```

### Task 9: Add PostgreSQL/Valkey integration coverage and test infrastructure

**Files:**
- Create: `backend/Dionysus.Api.Tests/Support/SpecificationApiFactory.cs`
- Create: `backend/Dionysus.Api.Tests/Support/ScriptedTextGenerationService.cs`
- Create: `backend/Dionysus.Api.Tests/Support/RecordingSpecificationQueue.cs`
- Create: `backend/Dionysus.Api.Tests/SpecificationPostgresTests.cs`
- Modify: `backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj`

**Interfaces:**
- Consumes: all production service interfaces and endpoints from Tasks 1–8.
- Produces: reproducible database/cache integration tests that never call real SMTP, Whisper, or Yandex AI.

- [ ] **Step 1: Write one failing Testcontainers end-to-end test**

```csharp
[Fact]
public async Task Completed_analysis_is_singleton_and_exposes_timestamped_provenance()
{
    await using var fixture = await SpecificationPostgresFixture.StartAsync();
    var project = await fixture.CreateCompletedProjectAsync();

    var first = await fixture.ClientForOwner().GetFromJsonAsync<SpecificationDetailsDto>($"/api/projects/{project.Id}/specification");
    var second = await fixture.ClientForOwner().PostAsync($"/api/projects/{project.Id}/specification/retry", null);

    Assert.NotEmpty(first!.Functions.Single().Items.Single().SourceSegments);
    Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
}
```

- [ ] **Step 2: Add Testcontainers packages and run the test to verify failure**

Add `Testcontainers.PostgreSql` and `Testcontainers.Valkey` to the test project. Run:

`dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~SpecificationPostgresTests`

Expected: FAIL until the fixture configures migration, JWT test authentication, scripted AI, and deterministic queue processing.

- [ ] **Step 3: Implement isolated integration fixtures**

Start PostgreSQL and Valkey once per collection. Set `DATABASE_URL`, `VALKEY_CONNECTION`, test JWT key, and Yandex configuration in the host. Replace `ITextGenerationService`, `ITranscriptionService`, `IEmailSender`, and `ISpecificationAnalysisQueue` with deterministic test implementations. Apply `Database.Migrate()` before each test collection and delete all tables between tests with `Respawn` or ordered `ExecuteDeleteAsync` calls.

- [ ] **Step 4: Add end-to-end cases from the specification**

Cover: automatic queued analysis after successful transcription; stage-0 ID mismatch; stage-1 unknown source; stage-2 contradiction persistence; seven functions with max concurrency four; failed stage-3 then successful retry; completed retry rejection; cross-user `404`; manual function/item CRUD; source time reconstruction; and database uniqueness for recording/analysis.

- [ ] **Step 5: Run the complete test suite**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj`

Expected: PASS, with the container-based tests skipped only when Docker is unavailable and clearly marked by an xUnit trait.

- [ ] **Step 6: Commit integration tests**

```bash
git add backend/Dionysus.Api.Tests
git commit -m "test: cover specification analysis lifecycle"
```

### Task 10: Document, build, and verify the delivery pipeline

**Files:**
- Modify: `README.md`
- Modify: `.github/workflows/ci.yml`
- Modify: `.env.example`
- Test: `backend/Dionysus.Api.Tests/HealthTests.cs`

**Interfaces:**
- Consumes: the shipped routes and environment configuration from Tasks 1–9.
- Produces: an operator-facing guide and CI evidence for the entire feature.

- [ ] **Step 1: Add failing documentation assertions or route smoke checks**

```csharp
[Fact]
public async Task Swagger_contains_specification_routes()
{
    var json = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
    Assert.Contains("/api/projects/{projectId}/specification", json);
}
```

- [ ] **Step 2: Run the smoke check to verify the route list is complete**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter FullyQualifiedName~HealthTests`

Expected: PASS only after Task 8 is complete.

- [ ] **Step 3: Update operator documentation and CI**

Document automatic post-transcription analysis, all statuses, failure/retry rule, four-request stage-3 cap, provenance/timestamp behavior, and the specification routes. Keep `.env.example` limited to existing Yandex variable names and safe placeholders. In CI run the full backend test project with Docker available for Testcontainers, backend build, frontend build, `docker build ./backend`, and `docker compose config`.

- [ ] **Step 4: Run the final local verification matrix**

Run:

```bash
dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj
dotnet build backend/Dionysus.Api/Dionysus.Api.csproj --no-restore
docker build -t dionysus-backend-spec ./backend
docker compose config
npm run build --prefix frontend
```

Expected: every command exits with code `0`. Then start Compose with non-secret local values, upload one recording, wait for a completed specification, call the specification GET route with a valid access token, and verify returned item source times match transcript segment times.

- [ ] **Step 5: Commit documentation and verification updates**

```bash
git add README.md .env.example .github/workflows/ci.yml backend/Dionysus.Api.Tests/HealthTests.cs
git commit -m "docs: document specification analysis workflow"
```

## Self-Review

Coverage check: Tasks 1 and 7 enforce the single-recording/single-analysis model; Tasks 2–3 implement every fixed JSON contract; Tasks 4–6 implement background processing, retry-safe state, four-stage AI calls, concurrency, provenance, and contradictions; Task 8 implements protected read/retry/edit APIs; Task 9 validates PostgreSQL/Valkey behavior; Task 10 verifies builds, Docker, CI, and documentation.

Type consistency check: `SpecificationAnalysisJob(AnalysisId, RunId)` is defined in Task 4 and used by Tasks 5, 7, and 8. `IStructuredSpecificationAiService` is defined in Task 4 and used in Tasks 5–6. Stage DTOs are defined in Task 2 and validated by Task 3 before persistence in Tasks 4–6. `SpecificationDetailsDto` is introduced in Task 2 and returned by Task 8.
