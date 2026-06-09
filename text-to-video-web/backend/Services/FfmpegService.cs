using System.Diagnostics;
using Microsoft.Extensions.Options;
using TextToVideo.Api.Options;

namespace TextToVideo.Api.Services;

public interface IFfmpegService
{
    Task<string> CreateVideoAsync(
        string subtitlePath,
        string audioPath,
        string jobDirectory,
        CancellationToken cancellationToken);
}

public sealed class FfmpegService : IFfmpegService
{
    private readonly FfmpegOptions _options;
    private readonly ILogger<FfmpegService> _logger;

    public FfmpegService(
        IOptions<FfmpegOptions> options,
        ILogger<FfmpegService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateVideoAsync(
        string subtitlePath,
        string audioPath,
        string jobDirectory,
        CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(jobDirectory, "final.mp4");

        if (!File.Exists(subtitlePath))
            throw new FileNotFoundException($"Subtitle file not found: {subtitlePath}");

        if (!File.Exists(audioPath))
            throw new FileNotFoundException($"Audio file not found: {audioPath}");

        var ffmpegPath = ResolveFfmpegPath(_options.Path);

        if (string.IsNullOrWhiteSpace(ffmpegPath))
            throw new FileNotFoundException(
                "FFmpeg was not found. Install FFmpeg or configure Ffmpeg:Path.");

        var safeSubtitlePath = subtitlePath
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");

        var subtitleFilter =
            $"subtitles=filename='{safeSubtitlePath}'";

        var blankVideoInput =
            $"color=c=white:s={VideoRenderService.Width}x{VideoRenderService.Height}:r={VideoRenderService.FramesPerSecond}:d=9999";

        var arguments = string.Join(' ', new[]
        {
            "-y",
            "-f lavfi",
            $"-i {Quote(blankVideoInput)}",
            $"-i {Quote(audioPath)}",
            $"-vf {Quote(subtitleFilter)}",
            "-c:v libx264",
            "-preset ultrafast",
            "-crf 30",
            "-pix_fmt yuv420p",
            "-c:a aac",
            "-b:a 128k",
            "-shortest",
            Quote(outputPath)
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Running optimized FFmpeg without PNG frame rendering.");
        _logger.LogInformation("FFmpeg path: {FfmpegPath}", ffmpegPath);
        _logger.LogInformation("FFmpeg arguments: {Arguments}", arguments);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg.");

        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardError = await standardErrorTask;
        var standardOutput = await standardOutputTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "FFmpeg failed with exit code {ExitCode}. Output: {Output} Error: {Error}",
                process.ExitCode,
                standardOutput,
                standardError);

            throw new InvalidOperationException($"FFmpeg failed to create video. {standardError}");
        }

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("FFmpeg finished without creating final.mp4.");

        return outputPath;
    }

    public static string ResolveFfmpegPath(string? configuredPath = null)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
            candidates.Add(Environment.ExpandEnvironmentVariables(configuredPath));

        candidates.AddRange(new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".audius",
                "runtime",
                "ffmpeg"),
            "/opt/homebrew/bin/ffmpeg",
            "/usr/local/bin/ffmpeg",
            "/usr/bin/ffmpeg",
            "ffmpeg"
        });

        foreach (var candidate in candidates.Distinct())
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (candidate.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
            {
                if (CanRunFfmpeg(candidate))
                    return candidate;

                continue;
            }

            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private static bool CanRunFfmpeg(string ffmpegPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return false;

            process.WaitForExit(3000);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Quote(string value) => $"\"{value}\"";
}