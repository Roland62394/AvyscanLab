using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CleanScan.Services;

public sealed class UpdateService
{
    /// <summary>Current application version. Must match the version shown in the splash/about screens.</summary>
    public const string CurrentVersion = "1.0.0";

    private const string LatestUrl = "https://www.scanfilm.ch/cleanscan/latest.txt";
    private const string DownloadBaseUrl = "https://www.scanfilm.ch/cleanscan/";

    /// <summary>
    /// Checks for a newer version by reading latest.txt from the server.
    /// Line 1: version number (e.g. "1.0.1"). Line 2 (optional): filename (e.g. "CleanScan RC 1.0.1 x64.exe").
    /// Returns (latestVersion, downloadUrl) if an update is available, or null if up-to-date or on error.
    /// </summary>
    public static async Task<(string Version, string DownloadUrl)?> CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var content = await http.GetStringAsync(LatestUrl);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (lines.Length == 0)
                return null;

            var latestVersion = lines[0];
            if (string.IsNullOrEmpty(latestVersion))
                return null;

            if (CompareVersions(latestVersion, CurrentVersion) > 0)
            {
                var fileName = lines.Length >= 2 ? lines[1] : $"CleanScan v{latestVersion} x64.exe";
                var downloadUrl = $"{DownloadBaseUrl}{Uri.EscapeDataString(fileName)}";
                return (latestVersion, downloadUrl);
            }

            return null;
        }
        catch
        {
            // Network errors, DNS failures, etc. — silently ignore
            return null;
        }
    }

    /// <summary>
    /// Compares two version strings. Returns &gt;0 if a is newer, &lt;0 if b is newer, 0 if equal.
    /// Supports formats like "1.0.0", "1.2", "2.0.1.3".
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.');
        var partsB = b.Split('.');
        var len = Math.Max(partsA.Length, partsB.Length);
        for (int i = 0; i < len; i++)
        {
            var numA = i < partsA.Length && int.TryParse(partsA[i], out var va) ? va : 0;
            var numB = i < partsB.Length && int.TryParse(partsB[i], out var vb) ? vb : 0;
            if (numA != numB) return numA.CompareTo(numB);
        }
        return 0;
    }
}
