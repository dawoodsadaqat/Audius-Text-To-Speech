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

        _logger.LogInformation("Creating paged ASS subtitle file.");

        var ass = BuildAssSubtitle(wordTimings, durationSeconds);

        await File.WriteAllTextAsync(
            assPath,
            ass,
            Encoding.UTF8,
            cancellationToken);

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
        // PrimaryColour = active/highlight color
        // SecondaryColour = normal text color
        sb.AppendLine("Style: Default,Arial,58,&H0000A5FF,&H00000000,&H00FFFFFF,&H00000000,-1,0,0,0,100,100,0,0,1,2,0,5,70,70,80,1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        var pages = BuildTimedPages(
            wordTimings,
            maxWordsPerPage: 10);

        foreach (var page in pages)
        {
            var start = ToAssTime(page.StartSeconds);
            var end = ToAssTime(page.EndSeconds + 0.25);
            var dialogueText = string.Join(" ", page.Words);

            sb.AppendLine(
                $"Dialogue: 0,{start},{end},Default,,0,0,0,,{{\\an5}}{dialogueText}");
        }

        return sb.ToString();
    }

    private sealed class SubtitlePage
    {
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public List<string> Words { get; set; } = new();
    }

    private static List<SubtitlePage> BuildTimedPages(
        IReadOnlyList<WordTiming> wordTimings,
        int maxWordsPerPage)
    {
        var pages = new List<SubtitlePage>();

        for (int i = 0; i < wordTimings.Count; i += maxWordsPerPage)
        {
            var chunk = wordTimings
                .Skip(i)
                .Take(maxWordsPerPage)
                .ToList();

            if (chunk.Count == 0)
                continue;

            var words = new List<string>();

            foreach (var timing in chunk)
            {
                var word = EscapeAssText(timing.Word);

                if (string.IsNullOrWhiteSpace(word))
                    continue;

                var durationCentiseconds = Math.Max(
                    1,
                    (int)Math.Round(
                        (timing.EndSeconds - timing.StartSeconds) * 100));

                words.Add($@"{{\k{durationCentiseconds}}}{word}");
            }

            if (words.Count == 0)
                continue;

            pages.Add(new SubtitlePage
            {
                StartSeconds = chunk.First().StartSeconds,
                EndSeconds = chunk.Last().EndSeconds,
                Words = words
            });
        }

        return pages;
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