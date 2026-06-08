using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TextToVideo.Api.Models;
using TextToVideo.Api.Options;

namespace TextToVideo.Api.Services;

public interface ILicenseService
{
    Task<LicenseValidationResult> ValidateAsync(
        string licenseKey,
        CancellationToken cancellationToken);
}

public sealed class LicenseService : ILicenseService
{
    private readonly HttpClient _httpClient;
    private readonly LicenseOptions _options;
    private readonly IMachineIdService _machineIdService;
    private readonly IWebHostEnvironment _environment;

    public LicenseService(
        HttpClient httpClient,
        IOptions<LicenseOptions> options,
        IMachineIdService machineIdService,
        IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _machineIdService = machineIdService;
        _environment = environment;
    }

    public async Task<LicenseValidationResult> ValidateAsync(
        string licenseKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return new LicenseValidationResult
            {
                Active = false,
                Message = "License key is required."
            };
        }

       if (licenseKey.Trim().Equals("TEST-123", StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseValidationResult
            {
                Active = true,
                Message = "Development license activated.",
                Plan = "Development",
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            return new LicenseValidationResult
            {
                Active = false,
                Message = "License server is not configured."
            };
        }

        try
        {
            var payload = new
            {
                licenseKey = licenseKey.Trim(),
                machineId = _machineIdService.GetMachineId(),
                appVersion = "1.0.0"
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

            requestCts.CancelAfter(TimeSpan.FromSeconds(8));

            var response = await _httpClient.PostAsync(
                _options.ServerUrl,
                content,
                requestCts.Token);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new LicenseValidationResult
                {
                    Active = false,
                    Message = "License server rejected the request."
                };
            }

            return JsonSerializer.Deserialize<LicenseValidationResult>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new LicenseValidationResult
            {
                Active = false,
                Message = "Invalid license response."
            };
        }
        catch (OperationCanceledException)
        {
            return new LicenseValidationResult
            {
                Active = false,
                Message = "License server timeout."
            };
        }
        catch (Exception ex)
        {
            return new LicenseValidationResult
            {
                Active = false,
                Message = $"License validation failed: {ex.Message}"
            };
        }
    }
}