namespace TextToVideo.Api.Services;

public interface IVideoGenerationService
{
    Task<string> GenerateAsync(IFormFile file, string? voiceName, CancellationToken cancellationToken);
}

public sealed class VideoGenerationService : IVideoGenerationService
{
    private readonly ITextFileService _textFileService;
    private readonly ISpeechService _speechService;
    private readonly IVideoRenderService _videoRenderService;
    private readonly IFfmpegService _ffmpegService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VideoGenerationService> _logger;

    public VideoGenerationService(
        ITextFileService textFileService,
        ISpeechService speechService,
        IVideoRenderService videoRenderService,
        IFfmpegService ffmpegService,
        IWebHostEnvironment environment,
        ILogger<VideoGenerationService> logger)
    {
        _textFileService = textFileService;
        _speechService = speechService;
        _videoRenderService = videoRenderService;
        _ffmpegService = ffmpegService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
    IFormFile file,
    string? voiceName,
    CancellationToken cancellationToken)
{
    var jobId = Guid.NewGuid().ToString("N");

    var outputsRoot = Path.Combine(_environment.ContentRootPath, "outputs");
    var jobDirectory = Path.Combine(outputsRoot, jobId);

    Directory.CreateDirectory(jobDirectory);

    try
    {
        var text = await _textFileService.ReadTextAsync(file, cancellationToken);

        var selectedVoice = string.IsNullOrWhiteSpace(voiceName)
            ? "en-US-JennyNeural"
            : voiceName.Trim();

        var audioPath = Path.Combine(jobDirectory, "audio.mp3");
        var subtitlePath = Path.Combine(jobDirectory, "subtitles.vtt");

        var speechResult = await _speechService.GenerateSpeechAsync(
            text,
            audioPath,
            subtitlePath,
            cancellationToken);

        var framesDirectory = await _videoRenderService.RenderFramesAsync(
            text,
            speechResult.WordTimings,
            speechResult.DurationSeconds,
            jobDirectory,
            cancellationToken);

        await _ffmpegService.CreateVideoAsync(
            framesDirectory,
            speechResult.AudioPath,
            jobDirectory,
            cancellationToken);

        return $"/outputs/{jobId}/final.mp4";
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Video job {JobId} failed. Keeping output folder for troubleshooting: {JobDirectory}",
            jobId,
            jobDirectory);

        throw;
    }
}
}
