namespace TextToVideo.Api.Models;

public sealed class WordTiming
{
    public required string Word { get; init; }
    public double StartSeconds { get; init; }
    public double EndSeconds { get; set; }
}
