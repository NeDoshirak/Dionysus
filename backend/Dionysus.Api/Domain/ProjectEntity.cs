public sealed class ProjectEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<VoiceRecording> Recordings { get; set; } = [];
}

public sealed class VoiceRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectEntityId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string SourceType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public byte[] AudioData { get; set; } = [];
    public string? Transcript { get; set; }
    public string? Language { get; set; }
    public string Status { get; set; } = "processing";
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TranscriptSegment> Segments { get; set; } = [];
}

public sealed class TranscriptSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoiceRecordingId { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public string Text { get; set; } = string.Empty;
}
