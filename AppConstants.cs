using System;
using System.IO;

namespace AvyScanLab;

/// <summary>
/// Centralised application-wide constants and helpers.
/// Eliminates duplication of folder names, file names, and path-building logic.
/// </summary>
public static class AppConstants
{
    public const string AppDataFolder = "AvyScanLab";

    // ── Trial limits ──
    /// <summary>Max recording duration per clip in seconds. 0 = unlimited.
    /// Currently no time limit is enforced in either trial or licensed mode.</summary>
    public const int TrialMaxSeconds = 0;

    // ── Lemon Squeezy integration ──
    /// <summary>Lemon Squeezy store ID that sells AvyScan Lab. Fill in after
    /// creating the store in the LS dashboard.</summary>
    public const string LemonSqueezyStoreId = "STORE_ID_TODO";

    /// <summary>Lemon Squeezy product ID for AvyScan Lab. Fill in after
    /// creating the product with "License keys" enabled in the LS dashboard.</summary>
    public const string LemonSqueezyProductId = "PRODUCT_ID_TODO";

    /// <summary>Base URL for the Lemon Squeezy License API (public, unauthenticated).</summary>
    public const string LemonSqueezyApiBase = "https://api.lemonsqueezy.com";

    /// <summary>Number of days a licensed user is allowed to run offline before the
    /// app silently falls back to trial mode. Counted from the last successful
    /// <c>/v1/licenses/validate</c> response.</summary>
    public const int OfflineGraceDays = 14;

    // ── Config file names ──
    public const string WindowSettingsFileName  = "window-settings.json";
    public const string PresetsFileName         = "presets.json";
    public const string EncodingPresetsFileName = "encoding_presets.json";
    public const string GammacPresetsFileName   = "gammac_presets.json";
    public const string SessionFileName         = "session.json";
    public const string CustomFiltersFileName   = "custom_filters.json"; // legacy, kept for migration
    public const string FiltersDirName          = "Filters";
    public const string FiltersBackupDirName    = "Filters_backup";
    public const string ThemeSettingsFileName    = "theme-settings.json";
    public const string CfDlgLayoutFileName     = "cfdlg-layout.json";

    public const string DefaultEncodingPresetName = "Default";

    /// <summary>Builds a full path under the user's AppData/AvyScanLab folder.</summary>
    public static string GetAppDataPath(string fileName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolder,
            fileName);

    /// <summary>User-writable Filters directory in AppData.</summary>
    public static string GetFiltersDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolder,
            FiltersDirName);

    /// <summary>Backup directory for removed base filters.</summary>
    public static string GetFiltersBackupDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolder,
            FiltersBackupDirName);
}
