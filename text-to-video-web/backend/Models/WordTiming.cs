namespace TextToVideo.Api.Models;

public class WordTiming
{
    public string Word { get; set; } = "";
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
}