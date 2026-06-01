using System.Text;

namespace TextToVideo.Api.Services;

public interface ITextFileService
{
    Task<string> ReadTextAsync(IFormFile file, CancellationToken cancellationToken);
}

public sealed class TextFileService : ITextFileService
{
    private const long MaxFileBytes = 1024 * 1024; // 1 MB is enough for an MVP text script.

    public async Task<string> ReadTextAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("A non-empty .txt file is required.");
        }

        if (!Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .txt files are supported.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new ArgumentException("The uploaded text file is too large for the MVP limit of 1 MB.");
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("The uploaded text file does not contain any readable text.");
        }

        return text.Trim();
    }
}
