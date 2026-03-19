using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CleanScan.Models;

namespace CleanScan.Services;

public sealed partial class ScriptService(SourceService source) : IScriptService
{
    // ── Public constants ────────────────────────────────────────────────────

    public const string ScriptUserFileName  = "ScriptUser.avs";
    public const string ScriptMasterFileName = "ScriptMaster.en.avs";

    public static readonly string[] TextFieldNames =
    [
        "source", "film", "img", "img_start", "img_end", "play_speed", "threads", "force_source",
        "Crop_L", "Crop_T", "Crop_R", "Crop_B",
        "degrain_mode", "degrain_thSAD", "degrain_thSADC", "degrain_blksize", "degrain_overlap", "degrain_pel", "degrain_search", "degrain_prefilter",
        "denoise_mode", "denoise_strength", "denoise_dist",
        "Lum_Bright", "Lum_Contrast", "Lum_Sat", "Lum_Hue", "Lum_GammaY",
        "LockChan", "LockVal", "Scale", "Th", "HiTh", "X", "Y", "W", "H",
        "Omin", "Omax", "Verbosity",
        "Sharp_Mode", "Sharp_Strength", "Sharp_Radius", "Sharp_Threshold",
    ];

    public static readonly string[] BoolFieldNames =
    [
        "enable_degrain", "enable_denoise", "denoise_grey",
        "enable_luma_levels", "enable_gammac", "enable_sharp",
        "preview", "preview_half", "Show"
    ];

    public const string UseImageConfigName    = "use_img";
    private const string ForceSourceConfigName = "force_source";
    private const string ForceSourceValue      = "FFMS2";

    // ── Compiled regexes ────────────────────────────────────────────────────

    [GeneratedRegex(@"^.*START CONFIGURATION.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex StartConfigRegex();

    [GeneratedRegex(@"^.*END CONFIGURATION.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex EndConfigRegex();

    [GeneratedRegex(@"^(?<prefix>\s*src\s*=\s*NOP\(\)\s*)(?<suffix>#.*)?$", RegexOptions.Multiline)]
    private static partial Regex SrcNopLineRegex();

    [GeneratedRegex(@"(?<prefix>\b(?:LoadPlugin|Import)\s*\(\s*"")(?<path>Plugins?/[^""\r\n]+)(?<suffix>""\s*\))", RegexOptions.IgnoreCase)]
    private static partial Regex PluginImportRegex();

    [GeneratedRegex(@"^\s+#")]
    private static partial Regex InlineCommentRegex();

    [GeneratedRegex(@"^#\s*__CUSTOM_INJECT_(\w+)__\s*$", RegexOptions.Multiline)]
    private static partial Regex CustomInjectMarkerRegex();

    [GeneratedRegex(@"^#\s*__CUSTOM_PLUGINS__\s*$", RegexOptions.Multiline)]
    private static partial Regex CustomPluginsMarkerRegex();

    [GeneratedRegex(@"^# __MODULE_CONFIG__[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex ModuleConfigMarkerRegex();

    [GeneratedRegex(@"^# __MODULE_FUNCTIONS__[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex ModuleFunctionsMarkerRegex();

    [GeneratedRegex(@"^# __MODULE_PIPELINE__[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex ModulePipelineMarkerRegex();

    [GeneratedRegex(@"^(?<prefix>\s*(?:global\s+)?(?<name>\w+)\s*=\s*)(?<value>[^#\r\n]*)(?<suffix>.*)$", RegexOptions.Multiline)]
    private static partial Regex ConfigLineRegex();

    // ── IScriptService ──────────────────────────────────────────────────────

    public void Generate(Dictionary<string, string> configValues, string lang = "en") =>
        Generate(configValues, customFilters: null, lang);

    public void Generate(Dictionary<string, string> configValues, IReadOnlyList<CustomFilter>? customFilters, string lang = "en")
    {
        var templatePath = GetMasterScriptPath(lang);
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            foreach (var pair in configValues)
                UpdateScriptUserFile(pair.Key, FormatValueForScript(pair.Key, pair.Value));
            return;
        }

        var template = File.ReadAllText(templatePath);

        // Convert ALL filters (built-in + user-created) to modules — no distinction
        var allModules = customFilters is not null
            ? ConvertCustomFiltersToModules(customFilters, configValues)
            : new List<ScriptModule>();

        // Assemble active modules only (replaces __MODULE_PIPELINE__, __CUSTOM_PLUGINS__)
        template = AssembleModules(template, allModules, configValues);

        // Expose flip states as AviSynth variables so src_flipped can mirror them
        configValues["flip_h"] = configValues.TryGetValue("cf_flip_h_enabled", out var fh) && fh == "true" ? "true" : "false";
        configValues["flip_v"] = configValues.TryGetValue("cf_flip_v_enabled", out var fv) && fv == "true" ? "true" : "false";

        var contents = BuildContents(template, configValues);

        // Normalize line endings to \r\n (Windows) for AviSynth compatibility
        contents = contents.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        foreach (var scriptPath in GetScriptPaths())
        {
            if (string.IsNullOrWhiteSpace(scriptPath)) continue;
            EnsureDirectory(scriptPath);
            File.WriteAllText(scriptPath, contents);
        }
    }

    public string? GetPrimaryScriptPath() => GetScriptPaths().FirstOrDefault();

    public IEnumerable<string> GetScriptPaths()
    {
        var userPath = EnsureUserScriptAvailableInternal();
        if (!string.IsNullOrWhiteSpace(userPath)) yield return userPath;

        var cwdPath = Path.Combine(Environment.CurrentDirectory, ScriptUserFileName);
        if (File.Exists(cwdPath) && !PathEquals(cwdPath, userPath)) yield return cwdPath;

        var basePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, ScriptUserFileName);
        if (File.Exists(basePath) && !PathEquals(basePath, cwdPath) && !PathEquals(basePath, userPath))
            yield return basePath;
    }

    public string? GetMasterScriptPath() => GetMasterScriptPath("en");

    private string? GetMasterScriptPath(string lang)
    {
        var normalizedLang = string.IsNullOrWhiteSpace(lang) ? "en" : lang.ToLowerInvariant();
        var langFileName = $"ScriptMaster.{normalizedLang}.avs";

        var cwdLang = Path.Combine(Environment.CurrentDirectory, langFileName);
        if (File.Exists(cwdLang)) return cwdLang;

        var baseLang = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, langFileName);
        if (File.Exists(baseLang)) return baseLang;

        // Fallback : ScriptMaster.en.avs
        var cwd = Path.Combine(Environment.CurrentDirectory, ScriptMasterFileName);
        if (File.Exists(cwd)) return cwd;
        var base_ = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, ScriptMasterFileName);
        return File.Exists(base_) ? base_ : null;
    }

    public void EnsureUserScriptAvailable() => EnsureUserScriptAvailableInternal();

    public void EnsureScriptCopiesInOutputDir()
    {
        var outputDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir)) return;

        // Master
        var outputMaster = Path.Combine(outputDir, ScriptMasterFileName);
        var sourceMaster = GetMasterScriptPath();
        if (!string.IsNullOrWhiteSpace(sourceMaster) && !PathEquals(sourceMaster, outputMaster))
        {
            try { File.Copy(sourceMaster, outputMaster, overwrite: true); } catch { }
        }

        // User
        var outputUser = Path.Combine(outputDir, ScriptUserFileName);
        if (File.Exists(outputUser))
        {
            EnsureScriptUsesAppBaseDir(outputUser);
            return;
        }

        var masterForCopy = File.Exists(outputMaster) ? outputMaster : sourceMaster;
        if (string.IsNullOrWhiteSpace(masterForCopy) || !File.Exists(masterForCopy)) return;

        try
        {
            File.Copy(masterForCopy, outputUser, overwrite: true);
            EnsureScriptUsesAppBaseDir(outputUser);
        }
        catch { }
    }

    public void EnsureScriptUsesAppBaseDir(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath)) return;
        try
        {
            var contents = File.ReadAllText(scriptPath);
            var updated = RewritePluginPathsToAbsolute(contents);
            if (!string.Equals(contents, updated, StringComparison.Ordinal))
                File.WriteAllText(scriptPath, updated);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public Dictionary<string, string> LoadScriptValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scriptPath = GetPrimaryScriptPath();
        if (scriptPath is null) return values;

        var knownNames = new HashSet<string>(
            TextFieldNames.Concat(BoolFieldNames).Append(UseImageConfigName),
            StringComparer.OrdinalIgnoreCase);

        var inConfigSection = false;
        foreach (var rawLine in File.ReadLines(scriptPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            var line = rawLine.Trim();

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                if (line.Contains("START CONFIGURATION", StringComparison.OrdinalIgnoreCase)) inConfigSection = true;
                else if (line.Contains("END CONFIGURATION", StringComparison.OrdinalIgnoreCase)) break;
                continue;
            }

            if (!inConfigSection) continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0) continue;

            var namePart = line[..equalsIndex].Trim();
            if (namePart.StartsWith("global ", StringComparison.OrdinalIgnoreCase))
                namePart = namePart["global ".Length..].Trim();

            if (string.Equals(namePart, "src", StringComparison.OrdinalIgnoreCase))
                namePart = "source";

            if (!knownNames.Contains(namePart)) continue;

            var valuePart = line[(equalsIndex + 1)..];
            var commentIndex = valuePart.IndexOf('#');
            if (commentIndex >= 0) valuePart = valuePart[..commentIndex];

            var value = valuePart.Trim();
            if (!string.IsNullOrEmpty(value))
                values[namePart] = value;
        }

        return values;
    }

    // ── Internal generation helpers ─────────────────────────────────────────

    internal string BuildContents(string scriptContents, Dictionary<string, string> configValues)
    {
        // Build a lookup of script config names → formatted values (skip cf_* keys)
        var scriptValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configValues)
        {
            if (pair.Key.StartsWith(CustomFilterConfigPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            scriptValues[GetScriptConfigName(pair.Key)] = FormatValueForScript(pair.Key, pair.Value);
        }

        // Replace all config values in one pass over the config section
        var updated = ReplaceConfigSection(scriptContents, scriptValues);

        updated = UpdateSourceClipLine(updated, configValues);
        return RewritePluginPathsToAbsolute(updated);
    }

    /// <summary>
    /// Replaces all config values within the START/END CONFIGURATION section in a single pass.
    /// </summary>
    private static string ReplaceConfigSection(string scriptContents, Dictionary<string, string> scriptValues)
    {
        if (scriptValues.Count == 0) return scriptContents;

        var startMatch = StartConfigRegex().Match(scriptContents);
        var endMatch = EndConfigRegex().Match(scriptContents);

        if (!startMatch.Success || !endMatch.Success || endMatch.Index <= startMatch.Index)
            return scriptContents;

        var startLineEnd = scriptContents.IndexOf('\n', startMatch.Index);
        if (startLineEnd < 0) return scriptContents;

        var sectionStart = startLineEnd + 1;
        var sectionEnd = endMatch.Index;
        var section = scriptContents[sectionStart..sectionEnd];

        // Single regex pass: match any "name = value" line in the config section
        var updated = ConfigLineRegex().Replace(section, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            if (name.StartsWith("global ", StringComparison.OrdinalIgnoreCase))
                name = name["global ".Length..].Trim();

            if (!scriptValues.TryGetValue(name, out var newValue))
                return m.Value; // not a known config key — leave unchanged

            return FormatReplacedLine(m, newValue);
        });

        return string.Equals(section, updated, StringComparison.Ordinal)
            ? scriptContents
            : $"{scriptContents[..sectionStart]}{updated}{scriptContents[sectionEnd..]}";
    }

    private string UpdateSourceClipLine(string scriptContents, Dictionary<string, string> configValues)
    {
        if (string.IsNullOrWhiteSpace(scriptContents)) return scriptContents;
        var sourceLabel = GetSelectedSourceLabel(configValues);
        if (string.IsNullOrWhiteSpace(sourceLabel)) return scriptContents;

        configValues.TryGetValue("img_start", out var imgStart);
        configValues.TryGetValue("img_end", out var imgEnd);

        return SrcNopLineRegex().Replace(
            scriptContents,
            $"${{prefix}}# full_path: {sourceLabel} | images: {imgStart ?? string.Empty} -> {imgEnd ?? string.Empty}");
    }

    private string GetSelectedSourceLabel(Dictionary<string, string> configValues)
    {
        if (configValues.TryGetValue("source", out var src) && !string.IsNullOrWhiteSpace(src))
            return source.NormalizePathForAvisynth(src);

        var key = IsImageSourceEnabled(configValues) ? "img" : "film";
        return configValues.TryGetValue(key, out var sel) && !string.IsNullOrWhiteSpace(sel)
            ? source.NormalizePathForAvisynth(sel)
            : string.Empty;
    }

    private static bool IsImageSourceEnabled(Dictionary<string, string> configValues) =>
        configValues.TryGetValue(UseImageConfigName, out var v) && bool.TryParse(v, out var b) && b;

    private string FormatValueForScript(string name, string value)
    {
        if (IsPathField(name)) return $"\"{source.NormalizePathForAvisynth(value)}\"";

        if (string.Equals(name, "Sharp_Mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "degrain_mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "degrain_prefilter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "denoise_mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "force_source", StringComparison.OrdinalIgnoreCase))
            return $"\"{NormalizeChoiceValue(value)}\"";

        return SanitizeScriptValue(name, value);
    }

    private static string SanitizeScriptValue(string name, string value)
    {
        value ??= string.Empty;

        if (string.Equals(name, "denoise_strength", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 1, max: 24, fallback: 10);

        if (string.Equals(name, "denoise_dist", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 1, max: 10, fallback: 3);

        if (string.Equals(name, "LockChan", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: -3, max: 2, fallback: 1);

        if (string.Equals(name, "LockVal", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 1, max: 255, fallback: 250);

        if (string.Equals(name, "Scale", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 0, max: 2, fallback: 1);

        if (string.Equals(name, "Th", StringComparison.OrdinalIgnoreCase))
            return ClampDoubleString(value, min: 0.0, max: 1.0, fallback: 0.12, decimals: 3);

        if (string.Equals(name, "HiTh", StringComparison.OrdinalIgnoreCase))
            return ClampDoubleString(value, min: 0.0, max: 1.0, fallback: 0.25, decimals: 3);

        if (string.Equals(name, "X", StringComparison.OrdinalIgnoreCase)
         || string.Equals(name, "Y", StringComparison.OrdinalIgnoreCase)
         || string.Equals(name, "W", StringComparison.OrdinalIgnoreCase)
         || string.Equals(name, "H", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 0, max: 10000, fallback: 0);

        if (string.Equals(name, "Omin", StringComparison.OrdinalIgnoreCase)
         || string.Equals(name, "Omax", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 0, max: 255, fallback: string.Equals(name, "Omax", StringComparison.OrdinalIgnoreCase) ? 255 : 0);

        if (string.Equals(name, "Verbosity", StringComparison.OrdinalIgnoreCase))
            return ClampIntString(value, min: 0, max: 6, fallback: 4);

        return value;
    }

    private static string ClampIntString(string raw, int min, int max, int fallback)
    {
        if (!int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            parsed = fallback;
        return Math.Clamp(parsed, min, max).ToString(CultureInfo.InvariantCulture);
    }

    private static string ClampDoubleString(string raw, double min, double max, double fallback, int decimals)
    {
        var normalized = (raw ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            parsed = fallback;

        var clamped = Math.Clamp(parsed, min, max);
        return clamped.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    private static bool IsPathField(string name) =>
        string.Equals(name, "source", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "film", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "img", StringComparison.OrdinalIgnoreCase);

    private static string ReplaceConfigValue(string scriptContents, string name, string value)
    {
        var pattern = $@"^(?<prefix>\s*(?:global\s+)?{Regex.Escape(name)}\s*=\s*)(?<value>[^#\r\n]*)(?<suffix>.*)$";
        var startMatch = StartConfigRegex().Match(scriptContents);
        var endMatch = EndConfigRegex().Match(scriptContents);

        if (startMatch.Success && endMatch.Success && endMatch.Index > startMatch.Index)
        {
            var startLineEnd = scriptContents.IndexOf('\n', startMatch.Index);
            if (startLineEnd >= 0)
            {
                var sectionStart = startLineEnd + 1;
                var sectionEnd = endMatch.Index;
                var section = scriptContents[sectionStart..sectionEnd];
                var updated = Regex.Replace(section, pattern, m => FormatReplacedLine(m, value), RegexOptions.Multiline);

                return string.Equals(section, updated, StringComparison.Ordinal)
                    ? scriptContents
                    : $"{scriptContents[..sectionStart]}{updated}{scriptContents[sectionEnd..]}";
            }
        }

        return Regex.Replace(scriptContents, pattern, m => FormatReplacedLine(m, value), RegexOptions.Multiline);
    }

    private static string FormatReplacedLine(System.Text.RegularExpressions.Match m, string value)
    {
        var suffix = m.Groups["suffix"].Value;
        if (suffix.Contains('#') && !InlineCommentRegex().IsMatch(suffix))
            suffix = $" {suffix}";
        return $"{m.Groups["prefix"].Value}{value}{suffix}";
    }

    private string RewritePluginPathsToAbsolute(string scriptContents)
    {
        var baseDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(scriptContents))
            return scriptContents;

        var updated = PluginImportRegex().Replace(
            scriptContents,
            m =>
            {
                var abs = Path.GetFullPath(Path.Combine(baseDir, m.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar)));
                return $"{m.Groups["prefix"].Value}{abs}{m.Groups["suffix"].Value}";
            });

        return updated;
    }

    // ── Script file path helpers ────────────────────────────────────────────

    private static string? EnsureUserScriptAvailableInternal()
    {
        var userPath = GetAppDataPath(ScriptUserFileName);
        if (File.Exists(userPath))
        {
            EnsureScriptUsesAppBaseDirStatic(userPath);
            return userPath;
        }

        var templatePath = GetMasterScriptPathStatic();
        if (string.IsNullOrWhiteSpace(templatePath)) return null;

        try
        {
            EnsureDirectory(userPath);
            File.Copy(templatePath, userPath, overwrite: true);
            EnsureScriptUsesAppBaseDirStatic(userPath);
        }
        catch { return null; }

        return File.Exists(userPath) ? userPath : null;
    }

    private static string? GetMasterScriptPathStatic()
    {
        var cwd = Path.Combine(Environment.CurrentDirectory, ScriptMasterFileName);
        if (File.Exists(cwd)) return cwd;
        var base_ = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, ScriptMasterFileName);
        return File.Exists(base_) ? base_ : null;
    }

    private static void EnsureScriptUsesAppBaseDirStatic(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath)) return;
        try
        {
            var baseDir = Path.GetDirectoryName(Environment.ProcessPath!)!;
            if (string.IsNullOrWhiteSpace(baseDir)) return;

            var contents = File.ReadAllText(scriptPath);

            var updated = PluginImportRegex().Replace(
                contents,
                m =>
                {
                    var abs = Path.GetFullPath(Path.Combine(baseDir, m.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar)));
                    return $"{m.Groups["prefix"].Value}{abs}{m.Groups["suffix"].Value}";
                });

            if (!string.Equals(contents, updated, StringComparison.Ordinal))
                File.WriteAllText(scriptPath, updated);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void UpdateScriptUserFile(string name, string value)
    {
        foreach (var scriptPath in GetScriptPaths())
        {
            if (!File.Exists(scriptPath)) continue;
            var contents = File.ReadAllText(scriptPath);
            var updated = ReplaceConfigValue(contents, GetScriptConfigName(name), value);
            if (!string.Equals(contents, updated, StringComparison.Ordinal))
                File.WriteAllText(scriptPath, updated);
        }
    }

    private static string GetScriptConfigName(string name) =>
        string.Equals(name, "source", StringComparison.OrdinalIgnoreCase) ? "src" : name;

    private static bool PathEquals(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Divides a numeric string value by 2 (rounded to nearest even int for crop compatibility).</summary>
    private static string HalveNumericValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var normalized = value.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            var halved = d / 2.0;
            // Round to nearest even integer for crop/dimension values
            var rounded = (int)(Math.Round(halved / 2.0) * 2);
            return Math.Max(0, rounded).ToString(CultureInfo.InvariantCulture);
        }
        return value;
    }

    private static string NormalizeChoiceValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Trim();
        var hash = cleaned.IndexOf('#');
        if (hash >= 0) cleaned = cleaned[..hash].TrimEnd();
        return cleaned.Trim().Trim('"');
    }

    private static string GetAppDataPath(string fileName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CleanScan", fileName);

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    // ── Custom filter injection ─────────────────────────────────────────────

    /// <summary>
    /// Valid injection positions matching __CUSTOM_INJECT_xxx__ markers in ScriptMaster.
    /// </summary>
    public static readonly string[] InjectionPositions =
        ["BeforePipeline", "AfterGamMac", "AfterDenoise", "AfterDegrain", "AfterLuma", "AfterSharpen"];

    /// <summary>Config key prefix for custom filter placeholder values.</summary>
    public const string CustomFilterConfigPrefix = "cf_";

    /// <summary>Returns the config key for a custom filter placeholder value.</summary>
    public static string GetCustomFilterConfigKey(string filterId, string placeholder) =>
        $"{CustomFilterConfigPrefix}{filterId}_{placeholder}";

    /// <summary>Config key suffix for the custom filter enabled toggle.</summary>
    public const string CustomFilterEnabledSuffix = "_enabled";

    /// <summary>Returns the config key for a custom filter's enabled state.</summary>
    public static string GetCustomFilterEnabledKey(string filterId) =>
        $"{CustomFilterConfigPrefix}{filterId}{CustomFilterEnabledSuffix}";

    /// <summary>
    /// Resolves a DLL name to a full path. If the path is already absolute and exists,
    /// returns it as-is. Otherwise searches the AviSynth+ plugins64+ folder.
    /// </summary>
    private static readonly string[] AviSynthPluginDirs =
    [
        @"C:\Program Files (x86)\AviSynth+\plugins64+",
        @"C:\Program Files\AviSynth+\plugins64+",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"AviSynth+\plugins64+"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"AviSynth+\plugins64+"),
    ];

    internal static string ResolveDllPath(string dll)
    {
        if (Path.IsPathRooted(dll))
            return dll.Replace('\\', '/');

        foreach (var dir in AviSynthPluginDirs)
        {
            if (!Directory.Exists(dir)) continue;
            // Direct match
            var candidate = Path.Combine(dir, dll);
            if (File.Exists(candidate))
                return candidate.Replace('\\', '/');
            // Search in subdirectories
            try
            {
                foreach (var found in Directory.GetFiles(dir, dll, SearchOption.AllDirectories))
                    return found.Replace('\\', '/');
            }
            catch { /* access denied */ }
        }

        return dll.Replace('\\', '/');
    }

    /// <summary>Resolve a .avsi script filename to its full path in Plugins/Scripts/ or AviSynth+ plugins.</summary>
    internal static string ResolveScriptPath(string script)
    {
        if (Path.IsPathRooted(script))
            return script.Replace('\\', '/');

        // App-local Plugins/Scripts/
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var candidate = Path.Combine(exeDir, "Plugins", "Scripts", script);
        if (File.Exists(candidate))
            return candidate.Replace('\\', '/');

        // AviSynth+ plugin directories (includes .avsi files)
        foreach (var dir in AviSynthPluginDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var c = Path.Combine(dir, script);
            if (File.Exists(c))
                return c.Replace('\\', '/');
            try
            {
                foreach (var found in Directory.GetFiles(dir, script, SearchOption.AllDirectories))
                    return found.Replace('\\', '/');
            }
            catch { /* access denied */ }
        }

        return script.Replace('\\', '/');
    }

    internal static string InjectCustomFilters(string scriptContents, IReadOnlyList<CustomFilter>? filters,
        Dictionary<string, string>? configValues = null)
    {
        if (filters is null || filters.Count == 0)
        {
            var result = CustomInjectMarkerRegex().Replace(scriptContents, "");
            return CustomPluginsMarkerRegex().Replace(result, "");
        }

        // Collect enabled filters
        var enabledFilters = filters.Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Code)).ToList();

        // ── 1. Inject TryLoadPlugin calls at __CUSTOM_PLUGINS__ marker (deduplicated) ──
        var allDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in enabledFilters)
            foreach (var dll in f.Dlls)
                if (!string.IsNullOrWhiteSpace(dll))
                    allDlls.Add(dll.Trim());

        // Collect scripts (Import .avsi)
        var allScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in enabledFilters)
            foreach (var script in f.Scripts)
                if (!string.IsNullOrWhiteSpace(script))
                    allScripts.Add(script.Trim());

        scriptContents = CustomPluginsMarkerRegex().Replace(scriptContents, _ =>
        {
            if (allDlls.Count == 0 && allScripts.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("# ── Custom filter plugins ──");
            foreach (var dll in allDlls)
                sb.AppendLine($"TryLoadPlugin(\"{ResolveDllPath(dll)}\")");
            foreach (var script in allScripts)
                sb.AppendLine($"Import(\"{ResolveScriptPath(script)}\")");
            return sb.ToString();
        });

        // ── 2. Group enabled filters by position ──
        var byPosition = new Dictionary<string, List<CustomFilter>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in enabledFilters)
        {
            if (!byPosition.TryGetValue(f.Position, out var list))
            {
                list = [];
                byPosition[f.Position] = list;
            }
            list.Add(f);
        }

        // ── 3. Inject code at __CUSTOM_INJECT_xxx__ markers (no LoadPlugin here) ──
        return CustomInjectMarkerRegex().Replace(scriptContents, m =>
        {
            var position = m.Groups[1].Value;
            if (!byPosition.TryGetValue(position, out var posFilters))
                return "";

            var sb = new StringBuilder();
            foreach (var f in posFilters)
            {
                sb.AppendLine($"# ── Custom filter: {f.Name} ──");

                // Resolve placeholders in code
                var code = f.Code.TrimEnd();
                foreach (var ctrl in f.Controls)
                {
                    var configKey = GetCustomFilterConfigKey(f.Id, ctrl.Placeholder);
                    var value = ctrl.Default; // fallback
                    if (configValues is not null &&
                        configValues.TryGetValue(configKey, out var cfgVal) &&
                        !string.IsNullOrEmpty(cfgVal))
                    {
                        value = cfgVal;
                    }
                    code = code.Replace($"{{{ctrl.Placeholder}}}", value, StringComparison.OrdinalIgnoreCase);
                }

                sb.AppendLine(code);
                sb.AppendLine();
            }

            return sb.ToString();
        });
    }

    // ── Module assembly ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps custom filter injection position strings to numeric pipeline order.
    /// Values sit between built-in module positions to preserve insertion order.
    /// </summary>
    private static readonly Dictionary<string, int> InjectionPositionOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FlipH"]          = 10,
        ["FlipV"]          = 15,
        ["Crop"]           = 20,
        ["BeforePipeline"] = 40,
        ["GamMac"]         = 100,
        ["AfterGamMac"]    = 150,
        ["Denoise"]        = 200,
        ["AfterDenoise"]   = 250,
        ["Degrain"]        = 300,
        ["AfterDegrain"]   = 350,
        ["Luma"]           = 400,
        ["AfterLuma"]      = 450,
        ["Sharpen"]        = 500,
        ["AfterSharpen"]   = 550,
    };

    /// <summary>Converts a position name to a numeric pipeline order. Accepts numeric strings directly.</summary>
    public static int InjectionPositionToOrder(string position)
    {
        if (int.TryParse(position, out var numeric)) return numeric;
        return InjectionPositionOrder.TryGetValue(position, out var order) ? order : 550;
    }

    /// <summary>
    /// Converts enabled custom filters to ScriptModules with resolved placeholders.
    /// </summary>
    public static List<ScriptModule> ConvertCustomFiltersToModules(
        IReadOnlyList<CustomFilter> filters, Dictionary<string, string>? configValues = null)
    {
        var isHalfPreview = configValues is not null
            && configValues.TryGetValue("preview_half", out var halfVal)
            && bool.TryParse(halfVal, out var halfBool) && halfBool;

        var modules = new List<ScriptModule>();
        foreach (var f in filters)
        {
            if (!f.Enabled || string.IsNullOrWhiteSpace(f.Code)) continue;

            // Resolve placeholders in code
            var code = f.Code.TrimEnd();
            foreach (var ctrl in f.Controls)
            {
                var configKey = GetCustomFilterConfigKey(f.Id, ctrl.Placeholder);
                var value = ctrl.Default;
                if (configValues is not null &&
                    configValues.TryGetValue(configKey, out var cfgVal) &&
                    !string.IsNullOrEmpty(cfgVal))
                {
                    value = cfgVal;
                }

                // Scale dimension/crop values by ½ when preview_half is active
                if (isHalfPreview && ctrl.ScaleWithPreview)
                    value = HalveNumericValue(value);

                code = code.Replace($"{{{ctrl.Placeholder}}}", value, StringComparison.OrdinalIgnoreCase);
            }

            modules.Add(new ScriptModule
            {
                Id = $"custom_{f.Id}",
                Name = f.Name,
                Position = InjectionPositionToOrder(f.Position),
                TemporalRadius = 0,
                Dlls = [.. f.Dlls.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim())],
                Scripts = [.. f.Scripts.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())],
                PipelineCode = $"# ── Custom filter: {f.Name} ──\n{code}",
            });
        }
        return modules;
    }

    /// <summary>
    /// Replaces __MODULE_FUNCTIONS__, __MODULE_PIPELINE__, and __CUSTOM_PLUGINS__ markers
    /// in the template with the assembled code from the provided script modules.
    /// Only active modules are included: modules with an EnableKey are skipped if
    /// the corresponding config value is not "true".
    /// </summary>
    public static string AssembleModules(string template, IReadOnlyList<ScriptModule> modules,
        Dictionary<string, string>? configValues = null)
    {
        var active = modules
            .Where(m => IsModuleActive(m, configValues))
            .OrderBy(m => m.Position)
            .ToList();

        // ── 1. Replace __CUSTOM_PLUGINS__ with TryLoadPlugin/Import calls from active modules ──
        template = CustomPluginsMarkerRegex().Replace(template, _ =>
        {
            var allDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in active)
            {
                foreach (var dll in m.Dlls)
                    if (!string.IsNullOrWhiteSpace(dll))
                        allDlls.Add(dll);
                foreach (var script in m.Scripts)
                    if (!string.IsNullOrWhiteSpace(script))
                        allScripts.Add(script);
            }

            if (allDlls.Count == 0 && allScripts.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("# ── Custom filter plugins ──");
            foreach (var dll in allDlls)
                sb.AppendLine($"TryLoadPlugin(\"{ResolveDllPath(dll)}\")");
            foreach (var script in allScripts)
                sb.AppendLine($"Import(\"{ResolveScriptPath(script)}\")");
            return sb.ToString().TrimEnd('\r', '\n');
        });

        // ── 2. Replace __MODULE_PIPELINE__ with all modules in position order ──
        template = ModulePipelineMarkerRegex().Replace(template, _ =>
            BuildPipelineBlock(active));

        return template;
    }

    private static string BuildPipelineBlock(IEnumerable<ScriptModule> modules)
    {
        var sb = new StringBuilder();
        foreach (var m in modules)
        {
            if (string.IsNullOrWhiteSpace(m.PipelineCode)) continue;
            sb.AppendLine($"# ── {m.Name} ──");
            sb.Append(m.PipelineCode);
            sb.AppendLine();
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Returns true if a module should be included in the assembled script.
    /// Modules without an EnableKey are always active.
    /// Modules with an EnableKey are active only if configValues contains "true" for that key.
    /// </summary>
    private static bool IsModuleActive(ScriptModule module, Dictionary<string, string>? configValues)
    {
        if (module.EnableKey is null) return true; // always active (custom filters, etc.)
        if (configValues is null) return true;     // no config = include all
        return configValues.TryGetValue(module.EnableKey, out var v)
            && bool.TryParse(v, out var enabled) && enabled;
    }

    /// <summary>
    /// Computes the maximum temporal radius required by the active modules.
    /// Used to determine border padding for ImageSource clips.
    /// </summary>
    public static int ComputeMaxTemporalRadius(IReadOnlyList<ScriptModule> modules,
        Dictionary<string, string>? configValues = null)
    {
        var max = 0;
        foreach (var m in modules)
        {
            if (m.TemporalRadius <= 0) continue;

            // If module has an enable key, check if it's enabled
            if (m.EnableKey is not null && configValues is not null)
            {
                if (!configValues.TryGetValue(m.EnableKey, out var v) ||
                    !bool.TryParse(v, out var enabled) || !enabled)
                    continue;
            }

            if (m.TemporalRadius > max)
                max = m.TemporalRadius;
        }
        return max;
    }
}
