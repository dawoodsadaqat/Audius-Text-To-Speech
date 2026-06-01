namespace TextToVideo.Api.Models;

public sealed class VideoGenerationResponse
{
    public bool Success { get; init; }
    public string? VideoUrl { get; init; }
    public string? Error { get; init; }
}
