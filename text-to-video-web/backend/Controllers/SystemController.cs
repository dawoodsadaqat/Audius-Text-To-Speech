using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TextToVideo.Api.Options;

namespace TextToVideo.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly RuntimeOptions _runtimeOptions;

    public SystemController(
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _runtimeOptions = runtimeOptions.Value;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check()
    {
        var pythonPath = GetPythonPath();

        var ffmpegExists =
            CommandExists("ffmpeg");

        var pythonExists =
            System.IO.File.Exists(pythonPath);

        var edgeTtsInstalled =
            pythonExists &&
            await CheckPythonPackage(
                pythonPath,
                "edge_tts");

        var stableWhisperInstalled =
            pythonExists &&
            await CheckPythonPackage(
                pythonPath,
                "stable_whisper");

        var runtimeReady =
            ffmpegExists &&
            pythonExists &&
            edgeTtsInstalled &&
            stableWhisperInstalled;

        return Ok(new
        {
            runtimeReady,

            ffmpeg = ffmpegExists,

            python = pythonExists,

            edgeTts = edgeTtsInstalled,

            stableWhisper = stableWhisperInstalled,

            pythonPath,

            userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),

            home = Environment.GetEnvironmentVariable("HOME"),

            baseDirectory = AppContext.BaseDirectory,

            currentDirectory = Directory.GetCurrentDirectory(),

            message = runtimeReady
                ? "Audius Runtime Ready"
                : "Audius Runtime Incomplete"
        });
    }

    private string GetPythonPath()
{
    if (OperatingSystem.IsWindows())
    {
        var configured = Environment.ExpandEnvironmentVariables(
            _runtimeOptions.WindowsPythonPath);

        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".audius",
            "runtime",
            "venv",
            "Scripts",
            "python.exe");
    }

    var macConfigured = Environment.ExpandEnvironmentVariables(
        _runtimeOptions.MacPythonPath);

    if (!string.IsNullOrWhiteSpace(macConfigured))
        return macConfigured;

    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".audius",
        "runtime",
        "venv",
        "bin",
        "python3");
}

    private static bool CommandExists(
        string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            process.WaitForExit(5000);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckPythonPackage(
        string pythonPath,
        string packageName)
    {
        try
        {
            using var process =
                new Process();

            process.StartInfo =
                new ProcessStartInfo
                {
                    FileName = pythonPath,

                    Arguments =
                        $"-c \"import {packageName}\"",

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    UseShellExecute = false,

                    CreateNoWindow = true
                };

            process.Start();

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}