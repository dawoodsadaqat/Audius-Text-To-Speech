using Microsoft.AspNetCore.Mvc;
using TextToVideo.Api.Services;

namespace TextToVideo.Api.Controllers;

[ApiController]
[Route("api/license")]
public sealed class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;

     public LicenseController(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    [HttpGet("validate")]
    public async Task<IActionResult> Validate(CancellationToken cancellationToken)
    {
        var result = await _licenseService.ValidateAsync(cancellationToken);

        return Ok(result);
    }
}

public sealed class LicenseRequest
{
    public string LicenseKey { get; set; } = "";
}