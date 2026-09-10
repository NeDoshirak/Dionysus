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
        try { recording.Transcript = await transcription.TranscribeAsync(audio, recording.FileName, ct); recording.Status = "completed"; await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Get), new { id = project.Id }, new { ProjectId = project.Id, project.Name, RecordingId = recording.Id, recording.Status, recording.Transcript }); }
        catch (Exception ex) { recording.Status = "failed"; recording.Error = ex.Message; await db.SaveChangesAsync(ct); return Problem("Transcription failed", statusCode: 502); }
    }
    [HttpGet]
    public Task<List<ProjectEntity>> List() => db.Projects.Where(x => x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)).Include(x => x.Recordings).ToListAsync();
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) { var project = await db.Projects.Where(x => x.Id == id && x.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)).Include(x => x.Recordings).FirstOrDefaultAsync(); return project is null ? NotFound() : Ok(project); }
}

public sealed class CreateProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public IFormFile Media { get; init; } = null!;
}
