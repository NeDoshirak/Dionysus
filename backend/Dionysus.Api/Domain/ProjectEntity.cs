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
    public string Status { get; set; } = "processing";
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
