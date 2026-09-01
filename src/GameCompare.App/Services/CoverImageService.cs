using System.Collections.Concurrent;
using System.IO;

namespace GameCompare.App.Services;

public sealed class CoverImageService
{
    private const long MaximumImageBytes = 12 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, string?> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public string? GetSource(string? coverPath)
    {
        if (string.IsNullOrWhiteSpace(coverPath))
        {
            return null;
        }

        if (Uri.TryCreate(coverPath, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return coverPath;
        }

        return cache.GetOrAdd(coverPath, ReadAsDataUri);
    }

    public void Clear() => cache.Clear();

    private static string? ReadAsDataUri(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length is <= 0 or > MaximumImageBytes)
            {
                return null;
            }

            var mediaType = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
            return $"data:{mediaType};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }
        catch
        {
            return null;
        }
    }
}
