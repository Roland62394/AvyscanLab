using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CleanScan.Services;

public sealed partial class SourceService
{
    [GeneratedRegex(@"%0?\d*d", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSequencePatternRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericFileNameRegex();
    private static readonly string[] VideoExtensions = [".avi", ".mp4", ".mov", ".mkv", ".wmv", ".m4v", ".mpeg", ".mpg", ".webm"];
    private static readonly string[] ImageExtensions = [".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp"];

    public bool IsVideoSource(string path)
    {
        var ext = Path.GetExtension(NormalizeConfiguredPath(path));
        return !string.IsNullOrWhiteSpace(ext) && VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsImageSource(string path)
    {
        var ext = Path.GetExtension(NormalizeConfiguredPath(path));
        return !string.IsNullOrWhiteSpace(ext) && ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public string NormalizeConfiguredPath(string path)
    {
        path ??= string.Empty;
        var trimmed = path.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    public string BuildImageSequenceSourcePath(string filePath)
    {
        var normalized = NormalizeConfiguredPath(filePath);
        var directory = Path.GetDirectoryName(normalized);
        var extension = Path.GetExtension(normalized);
        var name = Path.GetFileNameWithoutExtension(normalized);

        if (!string.IsNullOrWhiteSpace(name) && ImageSequencePatternRegex().IsMatch(name))
        {
            return normalized;
        }

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(name) || !NumericFileNameRegex().IsMatch(name))
        {
            return normalized;
        }

        var pattern = $"%0{name.Length}d{extension}";
        return Path.Combine(directory, pattern);
    }

    public string NormalizePathForAvisynth(string path) =>
        NormalizeConfiguredPath(path).Replace('\\', '/');

    public bool ImageSequenceExists(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        configuredPath = NormalizeConfiguredPath(configuredPath);
        if (File.Exists(configuredPath))
        {
            return true;
        }

        var directory = Path.GetDirectoryName(configuredPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Any(file =>
            {
                var ext = Path.GetExtension(file);
                if (!ext.Equals(".tif", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var n = Path.GetFileNameWithoutExtension(file);
                return !string.IsNullOrWhiteSpace(n) && NumericFileNameRegex().IsMatch(n);
            });
    }
}
