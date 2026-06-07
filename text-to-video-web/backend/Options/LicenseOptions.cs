namespace TextToVideo.Api.Options;

public sealed class LicenseOptions
{
    public string ServerUrl { get; set; } = "";
    public int RecheckHours { get; set; } = 24;
}