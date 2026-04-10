// CS0162: ForceLicensed is a compile-time toggle; some branches are intentionally
// unreachable depending on its value. Suppress for the whole file.
#pragma warning disable CS0162

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AvyScanLab.Services;

/// <summary>
/// Single source of truth for "trial vs licensed" state.
///
/// All trial limitations (single clip, no custom-filter editing, etc.) check
/// <see cref="IsLicensed"/> at runtime. The license key is persisted in
/// %AppData%\AvyScanLab\license.dat after a successful activation.
///
/// Cheat sheet:
///   • Set <see cref="ForceLicensed"/> to <c>true</c> below to unlock everything
///     at compile time (ignores any license file). Useful for dev/test builds.
///   • To restore trial behaviour, set it back to <c>false</c> AND delete the
///     license file via <see cref="Deactivate"/> or by removing license.dat.
/// </summary>
public static class LicenseService
{
    // ─────────────────────────────────────────────────────────────────
    //  DEV TOGGLE — flip to true to force-unlock for testing.
    //  ⚠️ Must be false for production / trial builds.
    // ─────────────────────────────────────────────────────────────────
    public const bool ForceLicensed = false;

    private const string LicenseFileName = "license.dat";

    /// <summary>Secret used to derive the license key checksum.
    /// Change this between releases to invalidate older keys.</summary>
    private const string LicenseSecret = "AvyScanLab-2026-ScanFilmSNC-K8x";

    private static string LicensePath => AppConstants.GetAppDataPath(LicenseFileName);

    /// <summary>True when the user has a valid license (or ForceLicensed is on).</summary>
    public static bool IsLicensed { get; private set; }

    /// <summary>The currently loaded license key, if any.</summary>
    public static string? LicenseKey { get; private set; }

    /// <summary>Fired whenever <see cref="IsLicensed"/> changes (after activate/deactivate).</summary>
    public static event Action? LicenseChanged;

    static LicenseService()
    {
        if (ForceLicensed)
        {
            IsLicensed = true;
            LicenseKey = "FORCE-LICENSED";
            return;
        }
        TryLoadFromDisk();
    }

    private static void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(LicensePath)) return;
            var key = File.ReadAllText(LicensePath).Trim();
            if (Validate(key))
            {
                LicenseKey = key;
                IsLicensed = true;
            }
        }
        catch
        {
            // Best-effort: a corrupt license file leaves the app in trial mode.
        }
    }

    /// <summary>
    /// Validates the supplied key and, on success, persists it and unlocks the app.
    /// </summary>
    /// <returns>True if the key is valid and was activated.</returns>
    public static bool TryActivate(string? key)
    {
        if (ForceLicensed) return true;

        var trimmed = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (!Validate(trimmed)) return false;

        try
        {
            var dir = Path.GetDirectoryName(LicensePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(LicensePath, trimmed);
        }
        catch
        {
            // Even if we can't persist, unlock the current session.
        }

        LicenseKey = trimmed;
        IsLicensed = true;
        LicenseChanged?.Invoke();
        return true;
    }

    /// <summary>Removes the persisted license file and locks the app back to trial mode.</summary>
    public static void Deactivate()
    {
        if (ForceLicensed) return;

        try
        {
            if (File.Exists(LicensePath)) File.Delete(LicensePath);
        }
        catch { /* best effort */ }

        LicenseKey = null;
        IsLicensed = false;
        LicenseChanged?.Invoke();
    }

    /// <summary>
    /// Checks whether <paramref name="key"/> is a structurally valid AvyScanLab key.
    /// Format: <c>AVSL-XXXX-XXXX-CCCC</c> where CCCC is a 4-char HMAC-SHA256 checksum
    /// of the two middle groups, keyed with <see cref="LicenseSecret"/>.
    /// </summary>
    public static bool Validate(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Trim().ToUpperInvariant().Split('-');
        if (parts.Length != 4) return false;
        if (parts[0] != "AVSL") return false;
        if (parts.Any(p => p.Length != 4)) return false;

        var expected = ChecksumOf($"{parts[1]}-{parts[2]}");
        return string.Equals(parts[3], expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Generates a license key for the supplied two-group middle (e.g. "ABCD-1234").
    /// Used by the in-house key generator — never call this from app UI.
    /// </summary>
    public static string GenerateKey(string middle)
    {
        if (string.IsNullOrWhiteSpace(middle)) throw new ArgumentException("middle is required");
        var parts = middle.Trim().ToUpperInvariant().Split('-');
        if (parts.Length != 2 || parts.Any(p => p.Length != 4))
            throw new ArgumentException("middle must be formatted as XXXX-XXXX");
        var checksum = ChecksumOf($"{parts[0]}-{parts[1]}");
        return $"AVSL-{parts[0]}-{parts[1]}-{checksum}";
    }

    private static string ChecksumOf(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(LicenseSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..4]; // first 4 hex chars (uppercase)
    }
}
