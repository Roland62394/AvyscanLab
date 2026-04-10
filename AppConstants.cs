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

    // ── Config file names ──
    public const string WindowSettingsFileName  = "window-settings.json";
    public const string PresetsFileName         = "presets.json";
    public const string EncodingPresetsFileName = "encoding_presets.json";
    public const string GammacPresetsFileName   = "gammac_presets.json";
    public const string SessionFileName         = "session.json";
    public const string CustomFiltersFileName   = "custom_filters.json";
    public const string ThemeSettingsFileName    = "theme-settings.json";
    public const string CfDlgLayoutFileName     = "cfdlg-layout.json";

    public const string DefaultEncodingPresetName = "Default";

    /// <summary>Builds a full path under the user's AppData/AvyScanLab folder.</summary>
    public static string GetAppDataPath(string fileName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolder,
            fileName);
}
