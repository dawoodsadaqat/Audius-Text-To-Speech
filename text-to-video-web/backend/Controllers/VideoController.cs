using Microsoft.AspNetCore.Mvc;
using TextToVideo.Api.Models;
using TextToVideo.Api.Services;

namespace TextToVideo.Api.Controllers;

[ApiController]
[Route("api/video")]
public sealed class VideoController : ControllerBase
{
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly ILicenseService _licenseService;
    private readonly ILogger<VideoController> _logger;

    public VideoController(
        IVideoGenerationService videoGenerationService,
        ILicenseService licenseService,
        ILogger<VideoController> logger)
    {
        _videoGenerationService = videoGenerationService;
        _licenseService = licenseService;
        _logger = logger;
    }

    [HttpPost("generate")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<VideoGenerationResponse>> Generate(
        [FromForm] IFormFile? file,
        [FromForm] string? voiceName,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new VideoGenerationResponse
            {
                Success = false,
                Error = "File is required."
            });
        }

        var licenseKey = Request.Headers["X-Audius-License"].FirstOrDefault();

        var license = await _licenseService.ValidateAsync(
            licenseKey ?? "",
            cancellationToken);

        if (!license.Active)
        {
            return Unauthorized(new VideoGenerationResponse
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(license.Message)
                    ? "License inactive. Please contact Audius support."
                    : license.Message
            });
        }

        try
        {
            var videoUrl = await _videoGenerationService.GenerateAsync(
                file,
                voiceName,
                cancellationToken);

            return Ok(new VideoGenerationResponse
            {
                Success = true,
                VideoUrl = videoUrl
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new VideoGenerationResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Video generation request was canceled by the client.");

            return StatusCode(
                StatusCodes.Status499ClientClosedRequest,
                new VideoGenerationResponse
                {
                    Success = false,
                    Error = "The request was canceled."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video generation failed.");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new VideoGenerationResponse
                {
                    Success = false,
                    Error = $"Video generation failed : {ex.Message}"
                });
        }
    }
}