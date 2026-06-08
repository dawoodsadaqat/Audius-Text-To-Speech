using System.Diagnostics;
using System.Text.Json;
using TextToVideo.Api.Models;

namespace TextToVideo.Api.Services;

public class SpeechService : ISpeechService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeechService> _logger;

    public SpeechService(
        IConfiguration configuration,
        ILogger<SpeechService> logger)
    {
        _configuration = configuration;
        _logger = logger;
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

        var pythonCommand =
            _configuration["EdgeTts:PythonCommand"];

        if (string.IsNullOrWhiteSpace(pythonCommand))
        {
            pythonCommand = OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".audius",
                    "runtime",
                    "venv",
                    "Scripts",
                    "python.exe")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".audius",
                    "runtime",
                    "venv",
                    "bin",
                    "python3");
        }

        var scriptPath = ResolveEdgeTtsScriptPath();

        _logger.LogInformation("Python path: {PythonPath}", pythonCommand);
        _logger.LogInformation("Edge-TTS script path: {ScriptPath}", scriptPath);

        if (!File.Exists(pythonCommand))
            throw new Exception($"Python runtime not found: {pythonCommand}");

        if (!File.Exists(scriptPath))
            throw new Exception($"Edge-TTS script not found: {scriptPath}");

        var arguments =
            $"\"{scriptPath}\" " +
            $"\"{textInputPath}\" " +
            $"\"{audioOutputPath}\" " +
            $"\"{wordTimingJsonPath}\" " +
            $"\"{voiceName}\"";

        using var process = new Process
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
        {
            throw new Exception(
                $"Edge-TTS Python script failed. Output: {output}. Error: {error}");
        }

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

    private static string ResolveEdgeTtsScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "edge_tts_generate.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "scripts", "edge_tts_generate.py"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "edge_tts_generate.py")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return candidates[0];
    }
}