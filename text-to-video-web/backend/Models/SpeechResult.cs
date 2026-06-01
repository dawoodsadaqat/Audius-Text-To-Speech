namespace TextToVideo.Api.Models;

public sealed class SpeechResult
{
    public required string AudioPath { get; init; }
    public required IReadOnlyList<WordTiming> WordTimings { get; init; }
    public double DurationSeconds { get; init; }
}
