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

        _logger.LogInformation("Creating fixed-position paged ASS subtitle file.");

        var ass = BuildAssSubtitle(wordTimings, durationSeconds);

        await File.WriteAllTextAsync(assPath, ass, Encoding.UTF8, cancellationToken);

        return assPath;
    }

    private static string BuildAssSubtitle(
        IReadOnlyList<WordTiming> wordTimings,
        double durationSeconds)
    {
        var cleanTimings = wordTimings
            .Where(x => !string.IsNullOrWhiteSpace(x.Word))
            .OrderBy(x => x.StartSeconds)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("PlayResX: 720");
        sb.AppendLine("PlayResY: 1280");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        sb.AppendLine("Style: Default,Arial,56,&H0000A5FF,&H00000000,&H00FFFFFF,&H00000000,-1,0,0,0,100,100,0,0,1,2,0,5,70,70,80,1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        var pages = BuildTimedPages(cleanTimings);

        for (int i = 0; i < pages.Count; i++)
{
    var page = pages[i];

    var startSeconds = page.StartSeconds;

    double endSeconds;

    if (i + 1 < pages.Count)
    {
        // Keep current page visible until next page starts
        endSeconds = pages[i + 1].StartSeconds;
    }
    else
    {
        // Keep final page visible until audio ends
        endSeconds = durationSeconds + 1.0;
    }

    if (endSeconds <= startSeconds)
    {
        endSeconds = startSeconds + 0.30;
    }

    var start = ToAssTime(startSeconds);
    var end = ToAssTime(endSeconds);
    var dialogueText = string.Join(" ", page.Words);

    sb.AppendLine(
        $"Dialogue: 0,{start},{end},Default,,0,0,0,,{{\\an5\\pos(360,720)}}{dialogueText}");
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
        IReadOnlyList<WordTiming> wordTimings)
    {
        const int maxWordsPerPage = 8;
        const double maxSecondsPerPage = 3.0;

        var pages = new List<SubtitlePage>();

        var currentWords = new List<WordTiming>();
        double pageStart = 0;

        foreach (var timing in wordTimings)
        {
            if (currentWords.Count == 0)
            {
                pageStart = timing.StartSeconds;
            }

            var wouldExceedWords = currentWords.Count >= maxWordsPerPage;
            var wouldExceedTime = timing.EndSeconds - pageStart > maxSecondsPerPage;

            if ((wouldExceedWords || wouldExceedTime) && currentWords.Count > 0)
            {
                pages.Add(BuildPage(currentWords));
                currentWords.Clear();
                pageStart = timing.StartSeconds;
            }

            currentWords.Add(timing);
        }

        if (currentWords.Count > 0)
        {
            pages.Add(BuildPage(currentWords));
        }

        return pages;
    }

    private static SubtitlePage BuildPage(List<WordTiming> timings)
    {
        var words = new List<string>();

        foreach (var timing in timings)
        {
            var word = EscapeAssText(timing.Word);

            if (string.IsNullOrWhiteSpace(word))
                continue;

            var durationCentiseconds = Math.Max(
                5,
                (int)Math.Round((timing.EndSeconds - timing.StartSeconds) * 100));

            words.Add($@"{{\k{durationCentiseconds}}}{word}");
        }

        return new SubtitlePage
        {
            StartSeconds = timings.First().StartSeconds,
            EndSeconds = timings.Last().EndSeconds,
            Words = words
        };
    }

    private static string ToAssTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
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