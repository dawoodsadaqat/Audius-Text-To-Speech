using TextToVideo.Api.Models;

namespace TextToVideo.Api.Services;

public interface ISpeechService
{
    Task<SpeechResult> GenerateSpeechAsync(
        string text,
        string audioOutputPath,
        string subtitleOutputPath,
        CancellationToken cancellationToken = default);
}