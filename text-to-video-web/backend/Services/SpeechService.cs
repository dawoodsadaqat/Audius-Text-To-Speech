using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using TextToVideo.Api.Models;
using TextToVideo.Api.Options;

namespace TextToVideo.Api.Services;

public interface ISpeechService
{
    Task<SpeechResult> GenerateSpeechAsync(string text, string voiceName, string jobDirectory, CancellationToken cancellationToken);
}

public sealed class SpeechService : ISpeechService
{
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<SpeechService> _logger;

    public SpeechService(IOptions<AzureSpeechOptions> options, ILogger<SpeechService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SpeechResult> GenerateSpeechAsync(string text, string voiceName, string jobDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Key) || string.IsNullOrWhiteSpace(_options.Region))
        {
            throw new InvalidOperationException("Azure Speech Key and Region must be configured in appsettings.json or environment variables.");
        }

        var audioPath = Path.Combine(jobDirectory, "audio.wav");
        var timings = new List<WordTiming>();

        var speechConfig = SpeechConfig.FromSubscription(_options.Key, _options.Region);
        speechConfig.SpeechSynthesisVoiceName = string.IsNullOrWhiteSpace(voiceName) ? "en-US-JennyNeural" : voiceName;
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm);

        using var audioConfig = AudioConfig.FromWavFileOutput(audioPath);
        using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);

        // Azure raises this event for each spoken word. AudioOffset and Duration are measured in 100-nanosecond ticks.
        synthesizer.SynthesisWordBoundary += (_, args) =>
        {
            if (args.BoundaryType != SpeechSynthesisBoundaryType.Word || string.IsNullOrWhiteSpace(args.Text))
            {
                return;
            }

            var startSeconds = args.AudioOffset / 10_000_000.0;
            var durationSeconds = args.Duration.TotalSeconds;

            timings.Add(new WordTiming
            {
                Word = args.Text,
                StartSeconds = startSeconds,
                EndSeconds = startSeconds + Math.Max(durationSeconds, 0.08)
            });
        };

        _logger.LogInformation("Generating Azure speech for {CharacterCount} characters using {VoiceName}.", text.Length, speechConfig.SpeechSynthesisVoiceName);
        var result = await synthesizer.SpeakTextAsync(text).WaitAsync(cancellationToken);

        if (result.Reason == ResultReason.Canceled)
        {
            var details = SpeechSynthesisCancellationDetails.FromResult(result);
            throw new InvalidOperationException($"Azure speech synthesis failed: {details.Reason}. {details.ErrorDetails}");
        }

        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            throw new InvalidOperationException($"Azure speech synthesis ended unexpectedly: {result.Reason}.");
        }

        if (timings.Count == 0)
        {
            throw new InvalidOperationException("Azure did not return word-boundary timestamps for this text.");
        }

        NormalizeTimingEnds(timings);
        var durationSecondsTotal = GetWavDurationSeconds(audioPath);
        return new SpeechResult
        {
            AudioPath = audioPath,
            WordTimings = timings,
            DurationSeconds = Math.Max(durationSecondsTotal, timings[^1].EndSeconds + 0.5)
        };
    }

    private static void NormalizeTimingEnds(List<WordTiming> timings)
    {
        for (var i = 0; i < timings.Count; i++)
        {
            var nextStart = i + 1 < timings.Count ? timings[i + 1].StartSeconds : timings[i].EndSeconds + 0.35;
            var safeEnd = Math.Min(timings[i].EndSeconds, nextStart - 0.01);
            timings[i].EndSeconds = Math.Max(safeEnd, timings[i].StartSeconds + 0.08);
        }
    }

    private static double GetWavDurationSeconds(string audioPath)
    {
        // A small WAV parser keeps the MVP independent from extra audio packages.
        using var stream = File.OpenRead(audioPath);
        using var reader = new BinaryReader(stream);

        reader.ReadBytes(22);
        var channels = reader.ReadInt16();
        var sampleRate = reader.ReadInt32();
        reader.ReadBytes(6);
        var bitsPerSample = reader.ReadInt16();

        while (stream.Position < stream.Length - 8)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();
            if (chunkId == "data")
            {
                var bytesPerSecond = sampleRate * channels * bitsPerSample / 8.0;
                return chunkSize / bytesPerSecond;
            }

            stream.Position += chunkSize;
        }

        return 0;
    }
}
