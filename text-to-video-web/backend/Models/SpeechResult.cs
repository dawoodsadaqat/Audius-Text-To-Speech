namespace TextToVideo.Api.Models;

public class SpeechResult
{
    public string AudioPath { get; set; } = "";
    public List<WordTiming> WordTimings { get; set; } = new();
    public double DurationSeconds { get; set; }
}