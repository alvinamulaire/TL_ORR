using Microsoft.Extensions.Options;
using TL_ORR.Options;

namespace TL_ORR.Services;

public sealed class UncPathConverter : IUncPathConverter
{
    private readonly FileShareOptions _options;
    private readonly ILogger<UncPathConverter> _logger;

    public UncPathConverter(IOptions<FileShareOptions> options, ILogger<UncPathConverter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ConvertToUncPath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        if (imagePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return imagePath;
        }

        if (string.IsNullOrWhiteSpace(_options.ServerIP) || string.IsNullOrWhiteSpace(_options.ShareName))
        {
            _logger.LogWarning("FileShare options are incomplete. Using original image path: {ImagePath}", imagePath);
            return imagePath;
        }

        try
        {
            var pathRoot = Path.GetPathRoot(imagePath);
            if (string.IsNullOrWhiteSpace(pathRoot))
            {
                _logger.LogWarning("Image path has no local root. Using original image path: {ImagePath}", imagePath);
                return imagePath;
            }

            var relativePath = imagePath[pathRoot.Length..].TrimStart('\\', '/');
            var normalizedRelativePath = relativePath.Replace('/', '\\');

            return $@"\\{_options.ServerIP}\{_options.ShareName}\{normalizedRelativePath}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert image path to UNC. Using original image path: {ImagePath}", imagePath);
            return imagePath;
        }
    }
}
