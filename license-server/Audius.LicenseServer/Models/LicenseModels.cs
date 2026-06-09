namespace Audius.LicenseServer.Models;

public sealed class LicenseValidationRequest
{
    public string LicenseKey { get; set; } = "";
    public string MachineId { get; set; } = "";
    public string AppVersion { get; set; } = "";
}

public sealed class LicenseValidationResponse
{
    public bool Active { get; set; }
    public string Message { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public string Plan { get; set; } = "";
}

public sealed class LicenseFile
{
    public List<LicenseRecord> Licenses { get; set; } = new();
}

public sealed class LicenseRecord
{
    public string LicenseKey { get; set; } = "";
    public string Plan { get; set; } = "";
    public bool Active { get; set; }
    public int MaxActivations { get; set; } = 1;
    public DateTime? ExpiresAt { get; set; }
    public List<string> MachineIds { get; set; } = new();
}