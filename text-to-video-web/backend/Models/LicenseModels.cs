namespace TextToVideo.Api.Models;

public sealed class LicenseValidationResult
{
    public bool Active { get; set; }
    public string Message { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public string Plan { get; set; } = "";
}