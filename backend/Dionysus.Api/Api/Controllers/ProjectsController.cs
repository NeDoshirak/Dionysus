using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(AppDbContext db, ITranscriptionQueue transcriptionQueue) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Create([FromForm] CreateProjectRequest request, CancellationToken ct)
    {
        var name = request.Name;
        var mediaFile = request.Media;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || mediaFile.Length is 0 or > 104857600) return BadRequest();
        var type = mediaFile.ContentType.ToLowerInvariant();
        if (!type.StartsWith("audio/") && !type.StartsWith("video/")) return BadRequest(new { detail = "Only audio or video is supported" });
        using var stream = new MemoryStream(); await mediaFile.CopyToAsync(stream, ct);
        var sourceType = type.StartsWith("video/") ? "video" : "audio";
        var project = new ProjectEntity { OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, Name = name.Trim() };
        var recording = new VoiceRecording { ProjectEntityId = project.Id, FileName = mediaFile.FileName, ContentType = type, SourceType = sourceType, SizeBytes = stream.Length, AudioData = stream.ToArray(), IsCurrent = true };
        project.Recordings.Add(recording); db.Projects.Add(project); await db.SaveChangesAsync(ct);
        await transcriptionQueue.EnqueueAsync(new TranscriptionJob(recording.Id), ct);
        return AcceptedAtAction(nameof(Get), new { id = project.Id }, ToDetails(project));
    }
    [HttpGet]
    public async Task<List<ProjectSummaryDto>> List()
    {
        var projects = await db.Projects
            .Where(x => x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .Include(x => x.Recordings)
            .ToListAsync();
        return projects.Select(project => new ProjectSummaryDto(
            project.Id,
            project.Name,
            project.CreatedAt,
            project.Recordings.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.Status ?? "empty")).ToList();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return BadRequest(new { detail = "Query must contain at least 2 characters" });

        var projects = await db.Projects
            .AsNoTracking()
            .Where(x => x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .Include(x => x.Recordings)
            .ToListAsync();

        var results = FuzzySearch.Rank(projects, query, project => project.Name)
            .Select(match => new ProjectSearchResultDto(
                match.Value.Id,
                match.Value.Name,
                match.Value.CreatedAt,
                match.Value.Recordings.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.Status ?? "empty",
                match.Score))
            .ToList();

        return Ok(results);
    }

    [HttpGet("{id:guid}/transcription-search")]
    public async Task<IActionResult> SearchTranscription(Guid id, [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return BadRequest(new { detail = "Query must contain at least 2 characters" });

        var project = await db.Projects
            .AsNoTracking()
            .Where(x => x.Id == id && x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .Include(x => x.Recordings)
            .ThenInclude(x => x.Segments)
            .Include(x => x.SpecificationAnalysis)
            .FirstOrDefaultAsync();
        if (project is null) return NotFound();

        var candidates = project.Recordings
            .SelectMany(recording => recording.Segments.Select(segment => new TranscriptionSearchCandidate(recording, segment)));
        var results = FuzzySearch.Rank(candidates, query, candidate => candidate.Segment.Text)
            .Select(match => new TranscriptionSearchResultDto(
                project.Id,
                project.Name,
                match.Value.Recording.Id,
                match.Value.Recording.FileName,
                match.Value.Segment.StartSeconds,
                match.Value.Segment.EndSeconds,
                match.Value.Segment.Text,
                match.Score))
            .ToList();

        return Ok(results);
    }

    private sealed record TranscriptionSearchCandidate(VoiceRecording Recording, TranscriptSegment Segment);

    [HttpGet("{projectId:guid}/recordings/{recordingId:guid}/stream")]
    public async Task<IActionResult> Stream(Guid projectId, Guid recordingId)
    {
        var recording = await db.VoiceRecordings
            .AsNoTracking()
            .Where(x => x.Id == recordingId && x.ProjectEntityId == projectId)
            .Join(db.Projects.Where(x => x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)),
                recording => recording.ProjectEntityId,
                project => project.Id,
                (recording, _) => recording)
            .FirstOrDefaultAsync();

        return recording is null
            ? NotFound()
            : File(recording.AudioData, recording.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var project = await db.Projects
            .Where(x => x.Id == id && x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .Include(x => x.Recordings)
            .ThenInclude(x => x.Segments)
            .Include(x => x.SpecificationAnalysis)
            .FirstOrDefaultAsync();
        return project is null ? NotFound() : Ok(ToDetails(project));
    }

    private static ProjectDetailsDto ToDetails(ProjectEntity project) => new(
        project.Id,
        project.Name,
        project.CreatedAt,
        project.Recordings.Select(recording => new RecordingDetailsDto(
            recording.Id,
            recording.FileName,
            recording.ContentType,
            recording.SourceType,
            recording.SizeBytes,
            recording.Status,
            recording.Error,
            recording.Transcript,
            recording.Language,
            recording.Segments.OrderBy(x => x.StartSeconds)
                .Select(segment => new TranscriptionSegmentDto(segment.StartSeconds, segment.EndSeconds, segment.Text))
                .ToList())).ToList(),
        project.SpecificationAnalysis?.Status.ToString());
}

public sealed class CreateProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public IFormFile Media { get; init; } = null!;
}
