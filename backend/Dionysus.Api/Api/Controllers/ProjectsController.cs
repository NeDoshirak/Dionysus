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
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile mediaFile, CancellationToken ct)
    {
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
