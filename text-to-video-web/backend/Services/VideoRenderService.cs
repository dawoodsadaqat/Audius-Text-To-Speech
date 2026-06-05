using System.Text;
using TextToVideo.Api.Models;

namespace TextToVideo.Api.Services;

public interface IVideoRenderService
{
    Task<string> RenderFramesAsync(
        string text,
        IReadOnlyList<WordTiming> wordTimings,
        double durationSeconds,
        string jobDirectory,
        CancellationToken cancellationToken);
}

public sealed class VideoRenderService : IVideoRenderService
{
    public const int Width = 720;
    public const int Height = 1280;
    public const int FramesPerSecond = 15;

    private readonly ILogger<VideoRenderService> _logger;

    public VideoRenderService(ILogger<VideoRenderService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RenderFramesAsync(
        string text,
        IReadOnlyList<WordTiming> wordTimings,
        double durationSeconds,
        string jobDirectory,
        CancellationToken cancellationToken)
    {
        var assPath = Path.Combine(jobDirectory, "highlight.ass");

        _logger.LogInformation("Creating optimized ASS subtitle file.");

        var ass = BuildAssSubtitle(wordTimings, durationSeconds);

        await File.WriteAllTextAsync(assPath, ass, Encoding.UTF8, cancellationToken);

        return assPath;
    }

    private static string BuildAssSubtitle(
        IReadOnlyList<WordTiming> wordTimings,
        double durationSeconds)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("PlayResX: 720");
        sb.AppendLine("PlayResY: 1280");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

        // ASS color format: &HAABBGGRR
        // PrimaryColour: highlighted orange
        // SecondaryColour: normal black
        sb.AppendLine("Style: Default,Arial,54,&H0000A5FF,&H00000000,&H00FFFFFF,&H00000000,-1,0,0,0,100,100,0,0,1,2,0,5,60,60,60,1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        var start = "0:00:00.00";
        var end = ToAssTime(durationSeconds + 1.0);

        var lines = BuildKaraokeLines(wordTimings, maxWordsPerLine: 7);
        var dialogueText = string.Join(@"\N", lines);

        sb.AppendLine($"Dialogue: 0,{start},{end},Default,,0,0,0,,{{\\an5}}{dialogueText}");

        return sb.ToString();
    }

    private static List<string> BuildKaraokeLines(
        IReadOnlyList<WordTiming> wordTimings,
        int maxWordsPerLine)
    {
        var result = new List<string>();
        var currentLine = new StringBuilder();
        var wordsInLine = 0;

        foreach (var timing in wordTimings)
        {
            var word = EscapeAssText(timing.Word);

            if (string.IsNullOrWhiteSpace(word))
                continue;

            var durationCentiseconds = Math.Max(
                1,
                (int)Math.Round((timing.EndSeconds - timing.StartSeconds) * 100));

            if (wordsInLine >= maxWordsPerLine)
            {
                result.Add(currentLine.ToString().Trim());
                currentLine.Clear();
                wordsInLine = 0;
            }

            currentLine.Append($@"{{\k{durationCentiseconds}}}{word} ");
            wordsInLine++;
        }

        if (currentLine.Length > 0)
            result.Add(currentLine.ToString().Trim());

        return result;
    }

    private static string ToAssTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
    }

    private static string EscapeAssText(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("{", "")
            .Replace("}", "")
            .Replace("\n", " ")
            .Trim();
    }
}