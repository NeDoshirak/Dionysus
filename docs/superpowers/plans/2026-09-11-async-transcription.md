# Asynchronous Transcription Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Return project creation promptly after upload and complete conversion, transcription, and specification dispatch in a recoverable background worker.

**Architecture:** `ProjectsController` persists a `processing` recording and publishes its ID to a new transcription queue, returning `202 Accepted`. A singleton queue and hosted worker recover persisted processing recordings after startup, convert/transcribe in independent DI scopes, save terminal state, and dispatch the existing specification-analysis job. The specification page polls its existing project-detail endpoint while transcription is processing.

**Tech Stack:** ASP.NET Core 8, EF Core, `System.Threading.Channels`, xUnit, Vue 3, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-11-async-transcription-design.md`

## Global Constraints

- Keep multipart fields `name` and `media`, accepted MIME families, and the 100 MB backend limit unchanged.
- `POST /api/projects` must return `202 Accepted`; it must never synchronously call FFmpeg or Whisper.
- Use only existing recording statuses: `processing`, `completed`, and `failed`.
- Persist only safe error text: `Conversion failed.` or `Transcription failed.`.
- Do not add a database migration or a new polling endpoint; poll `GET /api/projects/{id}` every 3 seconds.
- Stop polling after a terminal recording status, component unmount, or route change.

---

### Task 1: Persist and queue project uploads

**Files:**
- Modify: `backend/Dionysus.Api/Application/Interfaces.cs`
- Modify: `backend/Dionysus.Api/Api/Controllers/ProjectsController.cs`
- Modify: `backend/Dionysus.Api/Program.cs`
- Modify: `backend/Dionysus.Api.Tests/ProjectApiTests.cs`

**Interfaces:**
- Produces: `TranscriptionJob(Guid RecordingId)` and `ITranscriptionQueue.EnqueueAsync(TranscriptionJob, CancellationToken)` for the controller and worker.
- Consumes: `CreateProjectRequest`, `ProjectDetailsDto`, and the existing `ISpecificationAnalysisQueue` without changing their public shapes.

- [ ] **Step 1: Write the failing controller test**

```csharp
[Fact]
public async Task Create_project_returns_accepted_and_queues_processing_recording()
{
    var result = await controller.Create(CreateRequest(), CancellationToken.None);

    var accepted = Assert.IsType<AcceptedAtActionResult>(result);
    Assert.Equal("Get", accepted.ActionName);
    Assert.Equal("processing", Assert.IsType<ProjectDetailsDto>(accepted.Value).Recordings.Single().Status);
    Assert.Equal(new TranscriptionJob(recording.Id), Assert.Single(transcriptionQueue.Jobs));
    Assert.Empty(await db.SpecificationAnalyses.ToListAsync());
    Assert.Equal(0, transcription.Calls);
}
```

- [ ] **Step 2: Run the focused test and verify it fails because `ITranscriptionQueue` and `TranscriptionJob` do not exist**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter Create_project_returns_accepted`

Expected: compilation failure naming the missing queue types.

- [ ] **Step 3: Add the minimal queue contract and controller behavior**

```csharp
public sealed record TranscriptionJob(Guid RecordingId);

public interface ITranscriptionQueue
{
    ValueTask EnqueueAsync(TranscriptionJob job, CancellationToken ct);
    ValueTask<TranscriptionJob> DequeueAsync(CancellationToken ct);
}
```

Replace the synchronous conversion/transcription portion of `Create` with storing the original media in `VoiceRecording.AudioData`, calling `db.SaveChangesAsync(ct)`, enqueuing `new TranscriptionJob(recording.Id)`, and returning:

```csharp
return AcceptedAtAction(nameof(Get), new { id = project.Id }, ToDetails(project));
```

Inject `ITranscriptionQueue` into the controller. Register the implementation and hosted worker in `Program.cs`; do not register the worker’s dependencies as singletons.

- [ ] **Step 4: Run the focused test and verify it passes**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter Create_project_returns_accepted`

Expected: PASS; the accepted recording is persisted as `processing` and the transcription fake was not called.

- [ ] **Step 5: Commit**

```bash
git add backend/Dionysus.Api/Application/Interfaces.cs backend/Dionysus.Api/Api/Controllers/ProjectsController.cs backend/Dionysus.Api/Program.cs backend/Dionysus.Api.Tests/ProjectApiTests.cs
git commit -m "feat: queue project transcription"
```

### Task 2: Process and recover transcription jobs

**Files:**
- Create: `backend/Dionysus.Api/Infrastructure/Services/TranscriptionQueue.cs`
- Modify: `backend/Dionysus.Api.Tests/ProjectApiTests.cs`
- Create: `backend/Dionysus.Api.Tests/TranscriptionQueueTests.cs`

**Interfaces:**
- Consumes: `ITranscriptionQueue`, `IMediaConverter`, `ITranscriptionService`, `ISpecificationAnalysisQueue`, `AppDbContext`, and `TranscriptionJob`.
- Produces: `TranscriptionQueue` and `TranscriptionWorker`, registered by Task 1.

- [ ] **Step 1: Write failing worker tests**

```csharp
[Fact]
public async Task Worker_transcribes_processing_recording_and_queues_analysis()
{
    await worker.ProcessAsync(new TranscriptionJob(recording.Id), CancellationToken.None);

    Assert.Equal("completed", await db.VoiceRecordings.Select(x => x.Status).SingleAsync());
    Assert.Equal("transcript", await db.VoiceRecordings.Select(x => x.Transcript).SingleAsync());
    Assert.Single(await db.TranscriptSegments.ToListAsync());
    Assert.Single(specificationQueue.Jobs);
}

[Fact]
public async Task Worker_marks_failed_transcription_with_safe_error()
{
    await worker.ProcessAsync(new TranscriptionJob(recording.Id), CancellationToken.None);

    Assert.Equal("failed", await db.VoiceRecordings.Select(x => x.Status).SingleAsync());
    Assert.Equal("Transcription failed.", await db.VoiceRecordings.Select(x => x.Error).SingleAsync());
}
```

Add a startup-recovery test that stores a `processing` recording, invokes `RecoverAsync`, and asserts its ID was enqueued.

- [ ] **Step 2: Run focused worker tests and verify they fail because `TranscriptionWorker` is missing**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~TranscriptionQueueTests"`

Expected: compilation failure naming `TranscriptionWorker`.

- [ ] **Step 3: Implement a recoverable channel worker**

Use `Channel.CreateUnbounded<TranscriptionJob>` with a single reader. `StartAsync` calls `RecoverAsync`, which finds all `VoiceRecordings` with `Status == "processing"` and queues them.

`ProcessAsync` loads the recording in a new scope. For a video recording call `ExtractAudioAsync`, then replace `AudioData`, `FileName`, `ContentType`, and `SizeBytes` with WAV values before sending it to Whisper. On success add transcript segments, mark `completed`, create one queued `SpecificationAnalysis`, save, then enqueue `SpecificationAnalysisJob`. On converter and transcription exceptions, save only the respective safe error and `failed` status. Let cancellation stop the host; log other job-level exceptions and continue reading later jobs.

- [ ] **Step 4: Run focused worker tests and verify they pass**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj --filter "FullyQualifiedName~TranscriptionQueueTests|FullyQualifiedName~ProjectApiTests"`

Expected: PASS; completed recordings have segments and queued analysis, failures expose no provider detail, and restart recovery queues pending IDs.

- [ ] **Step 5: Commit**

```bash
git add backend/Dionysus.Api/Infrastructure/Services/TranscriptionQueue.cs backend/Dionysus.Api.Tests/ProjectApiTests.cs backend/Dionysus.Api.Tests/TranscriptionQueueTests.cs
git commit -m "feat: process transcription jobs in background"
```

### Task 3: Poll project transcription status in the specification route

**Files:**
- Modify: `frontend/src/pages/specification/SpecificationPage.vue`
- Modify: `frontend/src/pages/specification/SpecificationPage.test.js`

**Interfaces:**
- Consumes: existing `getProject(id)` and `getSpecification(id)` API functions; recording `status` and `error` in `ProjectDetailsDto`.
- Produces: automatic refresh of project and specification data every 3 seconds while the current recording is `processing`.

- [ ] **Step 1: Write failing page tests using fake timers**

```javascript
it('polls project details every three seconds while transcription is processing', async () => {
  vi.useFakeTimers()
  getProject.mockResolvedValue({ ...projectWithTranscript, recordings: [{ status: 'processing' }] })
  getSpecification.mockRejectedValue({ status: 404 })

  await mountPage('project-1')
  await vi.advanceTimersByTimeAsync(3000)

  expect(getProject).toHaveBeenCalledTimes(2)
  expect(getSpecification).toHaveBeenCalledTimes(2)
})

it('stops polling when transcription reaches a terminal status', async () => {
  vi.useFakeTimers()
  getProject.mockResolvedValueOnce({ ...projectWithTranscript, recordings: [{ status: 'processing' }] })
    .mockResolvedValueOnce({ ...projectWithTranscript, recordings: [{ status: 'completed' }] })

  const { wrapper } = await mountPage('project-1')
  await vi.advanceTimersByTimeAsync(6000)
  expect(getProject).toHaveBeenCalledTimes(2)
  wrapper.unmount()
})
```

Add a failure-state assertion that a failed recording displays its safe `error` instead of a generic analysis-loading state.

- [ ] **Step 2: Run the page tests and verify they fail because polling is absent**

Run: `npm run test:unit -- --run src/pages/specification/SpecificationPage.test.js`

Expected: FAIL because `getProject` is called only once.

- [ ] **Step 3: Implement lifecycle-safe polling**

Track one timeout ID rather than an interval. After each completed `loadWorkspace`, schedule a three-second reload only if the latest project’s current recording is `processing`; clear a previous timeout before replacing it. Clear it in `onBeforeUnmount` and before a route-triggered reload. Derive an explicit failed-transcription view state from the recording status and error. Do not poll for specification-analysis statuses: the current manual-refresh behavior remains unchanged.

- [ ] **Step 4: Run the page tests and verify they pass**

Run: `npm run test:unit -- --run src/pages/specification/SpecificationPage.test.js`

Expected: PASS; only processing recordings trigger repeated project and specification reads, terminal states stop scheduling work, and unmount cancels the timer.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/specification/SpecificationPage.vue frontend/src/pages/specification/SpecificationPage.test.js
git commit -m "feat: poll transcription status"
```

### Task 4: Verify the full change

**Files:**
- Verify only; no source changes expected.

- [ ] **Step 1: Run backend tests**

Run: `dotnet test backend/Dionysus.Api.Tests/Dionysus.Api.Tests.csproj`

Expected: PASS.

- [ ] **Step 2: Run frontend unit tests**

Run: `npm run test:unit`

Expected: PASS.

- [ ] **Step 3: Run frontend quality gates**

Run: `npm run lint && npm run build`

Expected: both commands exit 0.

- [ ] **Step 4: Inspect the resulting diff and commit verification-only corrections only if necessary**

Run: `git diff --check && git status --short`

Expected: no whitespace errors; only intentional tracked files changed.
