using SkiaSharp;
using TextToVideo.Api.Models;

namespace TextToVideo.Api.Services;

public interface IVideoRenderService
{
    Task<string> RenderFramesAsync(string text, IReadOnlyList<WordTiming> wordTimings, double durationSeconds, string jobDirectory, CancellationToken cancellationToken);
}

public sealed class VideoRenderService : IVideoRenderService
{
    public const int Width = 1080;
    public const int Height = 1920;
    public const int FramesPerSecond = 30;

    private readonly ILogger<VideoRenderService> _logger;

    public VideoRenderService(ILogger<VideoRenderService> logger)
    {
        _logger = logger;
    }

    public Task<string> RenderFramesAsync(string text, IReadOnlyList<WordTiming> wordTimings, double durationSeconds, string jobDirectory, CancellationToken cancellationToken)
    {
        var framesDirectory = Path.Combine(jobDirectory, "frames");
        Directory.CreateDirectory(framesDirectory);

        var layoutWords = BuildLayoutWords(wordTimings);
        var totalFrames = Math.Max(1, (int)Math.Ceiling(durationSeconds * FramesPerSecond));
        _logger.LogInformation("Rendering {FrameCount} PNG frames at {Fps} FPS.", totalFrames, FramesPerSecond);

        for (var frame = 0; frame < totalFrames; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seconds = frame / (double)FramesPerSecond;
            var activeIndex = FindActiveWordIndex(wordTimings, seconds);
            RenderSingleFrame(layoutWords, activeIndex, Path.Combine(framesDirectory, $"frame_{frame:D6}.png"));
        }

        return Task.FromResult(framesDirectory);
    }

    private static IReadOnlyList<LayoutWord> BuildLayoutWords(IReadOnlyList<WordTiming> wordTimings)
    {
        // The rendered script is based on Azure words so the highlight can sync exactly to timestamps.
        return wordTimings.Select((timing, index) => new LayoutWord(timing.Word, index)).ToList();
    }

    private static int FindActiveWordIndex(IReadOnlyList<WordTiming> wordTimings, double seconds)
    {
        for (var i = 0; i < wordTimings.Count; i++)
        {
            if (seconds >= wordTimings[i].StartSeconds && seconds <= wordTimings[i].EndSeconds)
            {
                return i;
            }
        }

        return -1;
    }

    private static void RenderSingleFrame(IReadOnlyList<LayoutWord> words, int activeIndex, string outputPath)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextSize = 72
        };

        using var highlightPaint = new SKPaint
        {
            Color = new SKColor(255, 166, 0),
            IsAntialias = true
        };

        var lines = WrapWords(words, textPaint, Width - 160);
        var lineHeight = 98f;
        var startY = (Height - lines.Count * lineHeight) / 2f + 80f;

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineWidth = MeasureLine(line, textPaint);
            var x = (Width - lineWidth) / 2f;
            var baseline = startY + lineIndex * lineHeight;

            foreach (var word in line)
            {
                var wordWidth = textPaint.MeasureText(word.Text);
                if (word.Index == activeIndex)
                {
                    var rect = SKRect.Create(x - 14, baseline - 74, wordWidth + 28, 88);
                    canvas.DrawRoundRect(rect, 22, 22, highlightPaint);
                }

                canvas.DrawText(word.Text, x, baseline, textPaint);
                x += wordWidth + textPaint.MeasureText(" ");
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        using var fileStream = File.OpenWrite(outputPath);
        data.SaveTo(fileStream);
    }

    private static List<List<LayoutWord>> WrapWords(IReadOnlyList<LayoutWord> words, SKPaint paint, float maxWidth)
    {
        var lines = new List<List<LayoutWord>>();
        var currentLine = new List<LayoutWord>();
        var currentWidth = 0f;
        var spaceWidth = paint.MeasureText(" ");

        foreach (var word in words)
        {
            var wordWidth = paint.MeasureText(word.Text);
            var proposedWidth = currentLine.Count == 0 ? wordWidth : currentWidth + spaceWidth + wordWidth;

            if (currentLine.Count > 0 && proposedWidth > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = new List<LayoutWord>();
                currentWidth = 0;
            }

            currentLine.Add(word);
            currentWidth = currentLine.Count == 1 ? wordWidth : currentWidth + spaceWidth + wordWidth;
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private static float MeasureLine(IReadOnlyList<LayoutWord> line, SKPaint paint)
    {
        var width = 0f;
        var spaceWidth = paint.MeasureText(" ");
        for (var i = 0; i < line.Count; i++)
        {
            width += paint.MeasureText(line[i].Text);
            if (i + 1 < line.Count)
            {
                width += spaceWidth;
            }
        }

        return width;
    }

    private sealed record LayoutWord(string Text, int Index);
}
