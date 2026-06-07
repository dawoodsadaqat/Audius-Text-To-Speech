using System.Security.Cryptography;
using System.Text;

namespace TextToVideo.Api.Services;

public interface IMachineIdService
{
    string GetMachineId();
}

public sealed class MachineIdService : IMachineIdService
{
    public string GetMachineId()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}