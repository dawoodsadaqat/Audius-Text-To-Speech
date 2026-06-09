using System.Text.Json;
using Audius.LicenseServer.Models;

namespace Audius.LicenseServer.Services;

public interface IFileLicenseService
{
    Task<LicenseValidationResponse> ValidateAsync(
        LicenseValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed class FileLicenseService : IFileLicenseService
{
    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileLicenseService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<LicenseValidationResponse> ValidateAsync(
        LicenseValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            return Inactive("License key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MachineId))
        {
            return Inactive("Machine ID is required.");
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "licenses.json");

            if (!File.Exists(filePath))
            {
                return Inactive("License file not found.");
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            var licenseFile = JsonSerializer.Deserialize<LicenseFile>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new LicenseFile();

            var license = licenseFile.Licenses.FirstOrDefault(x =>
                x.LicenseKey.Equals(
                    request.LicenseKey.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (license is null)
            {
                return Inactive("Invalid license key.");
            }

            if (!license.Active)
            {
                return Inactive("License is inactive.", license);
            }

            if (license.ExpiresAt.HasValue &&
                license.ExpiresAt.Value.ToUniversalTime() < DateTime.UtcNow)
            {
                return Inactive("License has expired.", license);
            }

            var machineId = request.MachineId.Trim();

            if (license.MachineIds.Any(x =>
                    x.Equals(machineId, StringComparison.OrdinalIgnoreCase)))
            {
                return Active("License active.", license);
            }

            if (license.MachineIds.Count >= license.MaxActivations)
            {
                return Inactive("Activation limit reached.", license);
            }

            license.MachineIds.Add(machineId);

            var updatedJson = JsonSerializer.Serialize(
                licenseFile,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(filePath, updatedJson, cancellationToken);

            return Active("License activated successfully.", license);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static LicenseValidationResponse Active(
        string message,
        LicenseRecord license)
    {
        return new LicenseValidationResponse
        {
            Active = true,
            Message = message,
            Plan = license.Plan,
            ExpiresAt = license.ExpiresAt
        };
    }

    private static LicenseValidationResponse Inactive(
        string message,
        LicenseRecord? license = null)
    {
        return new LicenseValidationResponse
        {
            Active = false,
            Message = message,
            Plan = license?.Plan ?? "",
            ExpiresAt = license?.ExpiresAt
        };
    }
}