using System.Diagnostics;
using System.Text.Json;
using TextToVideo.Api.Models;

namespace TextToVideo.Api.Services;

public class SpeechService : ISpeechService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SpeechService(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

   public async Task<SpeechResult> GenerateSpeechAsync(
    string text,
    string audioOutputPath,
    string subtitleOutputPath,
    string voiceName,
    CancellationToken cancellationToken = default)
    {
        var jobDirectory = Path.GetDirectoryName(audioOutputPath)
            ?? throw new Exception("Invalid audio output path.");

        Directory.CreateDirectory(jobDirectory);

        var textInputPath = Path.Combine(jobDirectory, "input.txt");
        var wordTimingJsonPath = Path.Combine(jobDirectory, "word_timings.json");

        await File.WriteAllTextAsync(textInputPath, text, cancellationToken);

        if (File.Exists(audioOutputPath))
            File.Delete(audioOutputPath);

        if (File.Exists(wordTimingJsonPath))
            File.Delete(wordTimingJsonPath);

        var pythonCommand = _configuration["EdgeTts:PythonCommand"] ?? "python3";
       

        var scriptPath = Path.Combine(
            _environment.ContentRootPath,
            "scripts",
            "edge_tts_generate.py");

        if (!File.Exists(scriptPath))
            throw new Exception($"Edge-TTS script not found: {scriptPath}");

        var arguments =
            $"\"{scriptPath}\" " +
            $"\"{textInputPath}\" " +
            $"\"{audioOutputPath}\" " +
            $"\"{wordTimingJsonPath}\" " +
            $"\"{voiceName}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonCommand,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new Exception($"Edge-TTS Python script failed: {error}");

        if (!File.Exists(audioOutputPath))
            throw new Exception("Audio file was not created.");

        if (!File.Exists(wordTimingJsonPath))
            throw new Exception("Word timing JSON file was not created.");

        var json = await File.ReadAllTextAsync(wordTimingJsonPath, cancellationToken);

        var wordTimings = JsonSerializer.Deserialize<List<WordTiming>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<WordTiming>();

        if (wordTimings.Count == 0)
            throw new Exception("No word timings were generated.");

        return new SpeechResult
        {
            AudioPath = audioOutputPath,
            WordTimings = wordTimings,
            DurationSeconds = wordTimings.Max(x => x.EndSeconds) + 1.0
        };
    }
}