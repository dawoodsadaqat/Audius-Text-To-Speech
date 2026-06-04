using System.Diagnostics;
using Microsoft.Extensions.Options;
using TextToVideo.Api.Options;

namespace TextToVideo.Api.Services;

public interface IFfmpegService
{
    Task<string> CreateVideoAsync(
        string framesDirectory,
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
        string framesDirectory,
        string audioPath,
        string jobDirectory,
        CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(jobDirectory, "final.mp4");
        var framePattern = Path.Combine(framesDirectory, "frame_%06d.png");

        if (!Directory.Exists(framesDirectory))
            throw new DirectoryNotFoundException($"Frames directory not found: {framesDirectory}");

        if (!File.Exists(audioPath))
            throw new FileNotFoundException($"Audio file not found: {audioPath}");

        var arguments = string.Join(' ', new[]
        {
            "-y",

            // Input video frames
            $"-framerate {VideoRenderService.FramesPerSecond}",
            $"-i {Quote(framePattern)}",

            // Input audio
            $"-i {Quote(audioPath)}",

            // Video encoding
            "-c:v libx264",
            "-pix_fmt yuv420p",

            // Audio encoding
            "-c:a aac",
            "-b:a 192k",

            // IMPORTANT:
            // Do NOT use -shortest here.
            // -shortest can cut the last word/audio tail.
            // We allow FFmpeg to keep the full audio duration.
            "-af apad=pad_dur=0.8",

            Quote(outputPath)
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(_options.Path)
                ? "ffmpeg"
                : _options.Path,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Running FFmpeg: {Command} {Arguments}", startInfo.FileName, arguments);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg. Check Ffmpeg:Path in appsettings.json.");

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

            throw new InvalidOperationException($"FFmpeg failed to create the video. {standardError}");
        }

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("FFmpeg finished without creating final.mp4.");

        return outputPath;
    }

    private static string Quote(string value) => $"\"{value}\"";
}