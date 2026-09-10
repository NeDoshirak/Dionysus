using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(AppDbContext db, IMediaConverter media, ITranscriptionService transcription) : ControllerBase
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
        byte[] audio;
        try { audio = sourceType == "video" ? await media.ExtractAudioAsync(stream.ToArray(), Path.GetExtension(mediaFile.FileName), ct) : stream.ToArray(); }
        catch (Exception ex) { return Problem(ex.Message, statusCode: 422); }
        var project = new ProjectEntity { OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, Name = name.Trim() };
        var recording = new VoiceRecording { ProjectEntityId = project.Id, FileName = sourceType == "video" ? Path.ChangeExtension(mediaFile.FileName, ".wav") : mediaFile.FileName, ContentType = sourceType == "video" ? "audio/wav" : type, SourceType = sourceType, SizeBytes = audio.LongLength, AudioData = audio };
        project.Recordings.Add(recording); db.Projects.Add(project); await db.SaveChangesAsync(ct);
        try
        {
            var result = await transcription.TranscribeAsync(audio, recording.FileName, ct);
            recording.Transcript = result.Text;
            recording.Language = result.Language;
            recording.Segments = result.Segments.Select(segment => new TranscriptSegment
            {
                VoiceRecordingId = recording.Id,
                StartSeconds = segment.StartSeconds,
                EndSeconds = segment.EndSeconds,
                Text = segment.Text
            }).ToList();
            db.TranscriptSegments.AddRange(recording.Segments);
            recording.Status = "completed";
            await db.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Get), new { id = project.Id }, ToDetails(project));
        }
        catch (Exception ex) { recording.Status = "failed"; recording.Error = ex.Message; await db.SaveChangesAsync(ct); return Problem("Transcription failed", statusCode: 502); }
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
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var project = await db.Projects
            .Where(x => x.Id == id && x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            .Include(x => x.Recordings)
            .ThenInclude(x => x.Segments)
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
                .ToList())).ToList());
}

public sealed class CreateProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public IFormFile Media { get; init; } = null!;
}
