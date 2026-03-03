using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CleanScan.Services;

public sealed partial class ScriptService(SourceService source, IAviService avi) : IScriptService
{
    // ── Public constants ────────────────────────────────────────────────────

    public const string ScriptUserFileName  = "ScriptUser.avs";
    public const string ScriptMasterFileName = "ScriptMaster.fr.avs";

    public static readonly string[] TextFieldNames =
    [
        "source", "film", "img", "img_start", "img_end", "play_speed", "threads",
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
        "enable_flip_h", "enable_flip_v", "enable_crop",
        "enable_degrain", "enable_denoise", "denoise_grey",
        "enable_luma_levels", "enable_gammac", "enable_sharp",
        "preview", "Show"
    ];

    public const string UseImageConfigName    = "use_img";
    public const string PreferAviVfwConfigName = "prefer_avi_vfw";

    // ── Compiled regexes ────────────────────────────────────────────────────

    [GeneratedRegex(@"^.*START CONFIGURATION.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex StartConfigRegex();

    [GeneratedRegex(@"^.*END CONFIGURATION.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex EndConfigRegex();

    [GeneratedRegex(@"^(?<prefix>\s*src\s*=\s*NOP\(\)\s*)(?<suffix>#.*)?$", RegexOptions.Multiline)]
    private static partial Regex SrcNopLineRegex();

    [GeneratedRegex(@"(?<prefix>\b(?:LoadPlugin|Import)\s*\(\s*"")(?<path>Plugins?/[^""\r\n]+)(?<suffix>""\s*\))", RegexOptions.IgnoreCase)]
    private static partial Regex PluginImportRegex();

    [GeneratedRegex(@"^(?<prefix>\s*(?:global\s+)?pluginRoot\s*=\s*)(?<value>[^#\r\n]*)(?<suffix>.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PluginRootRegex();

    [GeneratedRegex(@"^\s+#")]
    private static partial Regex InlineCommentRegex();

    // ── IScriptService ──────────────────────────────────────────────────────

    public void Generate(Dictionary<string, string> configValues, string lang = "fr")
    {
        var templatePath = GetMasterScriptPath(lang);
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            foreach (var pair in configValues)
                UpdateScriptUserFile(pair.Key, FormatValueForScript(pair.Key, pair.Value));
            return;
        }

        var contents = BuildContents(File.ReadAllText(templatePath), configValues);
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

    public string? GetMasterScriptPath() => GetMasterScriptPath("fr");

    private string? GetMasterScriptPath(string lang)
    {
        // Cherche ScriptMaster.{lang}.avs, sauf pour "fr" qui utilise le fichier de base
        if (!string.IsNullOrWhiteSpace(lang) && !lang.Equals("fr", StringComparison.OrdinalIgnoreCase))
        {
            var langFileName = $"ScriptMaster.{lang.ToLowerInvariant()}.avs";
            var cwdLang = Path.Combine(Environment.CurrentDirectory, langFileName);
            if (File.Exists(cwdLang)) return cwdLang;
            var baseLang = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, langFileName);
            if (File.Exists(baseLang)) return baseLang;
        }

        // Fallback : ScriptMaster.fr.avs (français par défaut)
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
        var updated = scriptContents;
        foreach (var pair in configValues)
            updated = ReplaceConfigValue(updated, GetScriptConfigName(pair.Key), FormatValueForScript(pair.Key, pair.Value));

        updated = ApplyAviSourceStrategy(updated, configValues);
        updated = UpdateSourceClipLine(updated, configValues);
        return RewritePluginPathsToAbsolute(updated);
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

    private string ApplyAviSourceStrategy(string scriptContents, Dictionary<string, string> configValues) =>
        ReplaceConfigValue(scriptContents, PreferAviVfwConfigName,
            ShouldPreferAviVfwForSelectedSource(configValues) ? "true" : "false");

    private bool ShouldPreferAviVfwForSelectedSource(Dictionary<string, string> configValues)
    {
        if (!TryGetSelectedVideoSourcePath(configValues, out var path)) return true;
        if (!string.Equals(Path.GetExtension(path), ".avi", StringComparison.OrdinalIgnoreCase)) return true;
        return !avi.IsAviFourCcKnownToFailWithAviSource(path);
    }

    private bool TryGetSelectedVideoSourcePath(Dictionary<string, string> configValues, out string selectedVideoPath)
    {
        selectedVideoPath = string.Empty;
        if (IsImageSourceEnabled(configValues)) return false;

        var raw = configValues.TryGetValue("source", out var s) ? s : string.Empty;
        if (string.IsNullOrWhiteSpace(raw) && configValues.TryGetValue("film", out var f)) raw = f;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        selectedVideoPath = source.NormalizeConfiguredPath(raw);
        return !string.IsNullOrWhiteSpace(selectedVideoPath);
    }

    private static bool IsImageSourceEnabled(Dictionary<string, string> configValues) =>
        configValues.TryGetValue(UseImageConfigName, out var v) && bool.TryParse(v, out var b) && b;

    private string FormatValueForScript(string name, string value)
    {
        if (IsPathField(name)) return $"\"{source.NormalizePathForAvisynth(value)}\"";

        if (string.Equals(name, "Sharp_Mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "degrain_mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "degrain_prefilter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "denoise_mode", StringComparison.OrdinalIgnoreCase))
            return $"\"{NormalizeChoiceValue(value)}\"";

        return value;
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

        var pluginRoot = source.NormalizePathForAvisynth(Path.GetFullPath(Path.Combine(baseDir, "Plugins")));
        return PluginRootRegex().Replace(
            updated,
            $"${{prefix}}\"{pluginRoot}/\"${{suffix}}");
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

            var pluginRoot = Path.GetFullPath(Path.Combine(baseDir, "Plugins")).Replace('\\', '/');
            updated = PluginRootRegex().Replace(
                updated,
                $"${{prefix}}\"{pluginRoot}/\"${{suffix}}");

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
}
