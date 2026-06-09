using Audius.LicenseServer.Models;
using Audius.LicenseServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Audius.LicenseServer.Controllers;

[ApiController]
[Route("api/license")]
public sealed class LicenseController : ControllerBase
{
    private readonly IFileLicenseService _licenseService;

    public LicenseController(IFileLicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<LicenseValidationResponse>> Validate(
        LicenseValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _licenseService.ValidateAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}