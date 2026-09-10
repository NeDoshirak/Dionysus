using Xunit;

public sealed class TranscriptionTests
{
    [Fact]
    public void Parses_whisper_segments_and_detected_language()
    {
        const string json = """
        {
          "language": "ru",
          "text": "Привет мир",
          "segments": [
            { "timestamps": [0.12, 1.84], "transcript": "Привет" },
            { "start": 1.84, "end": 2.70, "text": "мир" }
          ]
        }
        """;

        var result = WhisperTranscriptionParser.Parse(json);

        Assert.Equal("ru", result.Language);
        Assert.Equal("Привет мир", result.Text);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(0.12, result.Segments[0].StartSeconds, 2);
        Assert.Equal(1.84, result.Segments[0].EndSeconds, 2);
        Assert.Equal("мир", result.Segments[1].Text);
    }
}
