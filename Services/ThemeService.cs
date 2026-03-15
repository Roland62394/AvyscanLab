using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanScan.Services;

public sealed class ThemeService
{
    private const string FileName = "theme-settings.json";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _filePath;

    public string Theme { get; private set; } = "Dark";
    public string Accent { get; private set; } = "Blue";

    public ThemeService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "CleanScan", FileName);
        Load();
    }

    public void SetTheme(string theme)
    {
        Theme = theme;
        Save();
    }

    public void SetAccent(string accent)
    {
        Accent = accent;
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<ThemeData>(json);
            if (data is not null)
            {
                Theme = data.Theme ?? "Dark";
                Accent = data.Accent ?? "Blue";
            }
        }
        catch { /* keep defaults */ }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(new ThemeData { Theme = Theme, Accent = Accent }, JsonOpts));
        }
        catch { /* best effort */ }
    }

    // ── Palette definitions ──

    public static readonly string[] AvailableThemes = ["Dark", "Grey", "Light"];

    public static readonly string[] AvailableAccents = ["Blue", "Green", "Red", "Violet", "Orange"];

    /// <summary>Hex color used to render accent swatches in the UI.</summary>
    public static readonly Dictionary<string, string> AccentSwatchColors = new()
    {
        ["Blue"]   = "#3B82C4",
        ["Green"]  = "#35C156",
        ["Red"]    = "#DC3545",
        ["Violet"] = "#7C3AED",
        ["Orange"] = "#F59E0B",
    };

    /// <summary>Returns the full color palette for the given theme + accent combination.</summary>
    public static Dictionary<string, string> GetPalette(string theme, string accent)
    {
        var isLight = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        var isGrey = string.Equals(theme, "Grey", StringComparison.OrdinalIgnoreCase);

        var basePalette = isLight ? LightBase : isGrey ? GreyBase : DarkBase;
        var palette = new Dictionary<string, string>(basePalette);

        // Overlay accent-specific colors (Grey uses Dark accent variant)
        if (AccentPalettes.TryGetValue(accent, out var accentColors))
        {
            var variant = isLight ? accentColors.Light : accentColors.Dark;
            foreach (var (key, value) in variant)
                palette[key] = value;
        }

        return palette;
    }

    // ── Dark base palette ──
    private static readonly Dictionary<string, string> DarkBase = new()
    {
        ["BgDeep"]       = "#0F1319",
        ["BgPanel"]      = "#161B24",
        ["BgHeader"]     = "#1C2333",
        ["BgInput"]      = "#1A2030",
        ["BgHover"]      = "#243050",
        ["BgPressed"]    = "#1a2540",
        ["BorderSubtle"] = "#252E42",
        ["BorderAccent"] = "#2E3D5A",
        ["TextPrimary"]  = "#7984A5",
        ["TextSecondary"]= "#DBDBDB",
        ["TextLabel"]    = "#F6F6F6",
        ["AccentGreen"]  = "#35C156",
        ["AccentDimmed"] = "#1E3A2E",
    };

    // ── Grey base palette ──
    private static readonly Dictionary<string, string> GreyBase = new()
    {
        ["BgDeep"]       = "#2A2D32",
        ["BgPanel"]      = "#35393F",
        ["BgHeader"]     = "#3E4248",
        ["BgInput"]      = "#2F3338",
        ["BgHover"]      = "#484D54",
        ["BgPressed"]    = "#404448",
        ["BorderSubtle"] = "#50555C",
        ["BorderAccent"] = "#60666E",
        ["TextPrimary"]  = "#A0A5AD",
        ["TextSecondary"]= "#DBDBDB",
        ["TextLabel"]    = "#F0F0F0",
        ["AccentGreen"]  = "#35C156",
        ["AccentDimmed"] = "#2A3E30",
    };

    // ── Light base palette ──
    private static readonly Dictionary<string, string> LightBase = new()
    {
        ["BgDeep"]       = "#F0F2F5",
        ["BgPanel"]      = "#FFFFFF",
        ["BgHeader"]     = "#E4E8EE",
        ["BgInput"]      = "#FFFFFF",
        ["BgHover"]      = "#D4DAE4",
        ["BgPressed"]    = "#C8D0DC",
        ["BorderSubtle"] = "#CDD2DA",
        ["BorderAccent"] = "#B8C0D0",
        ["TextPrimary"]  = "#5A6070",
        ["TextSecondary"]= "#404550",
        ["TextLabel"]    = "#1A1E28",
        ["AccentGreen"]  = "#2DA84A",
        ["AccentDimmed"] = "#D0F0DA",
    };

    // ── Accent-specific overrides (dark + light variants) ──
    private static readonly Dictionary<string, AccentVariants> AccentPalettes = new()
    {
        ["Blue"] = new(
            Dark: new() { ["AccentBlue"] = "#3B82C4", ["AccentHover"] = "#5BA5E4", ["AccentPressed"] = "#2870B8" },
            Light: new() { ["AccentBlue"] = "#2563EB", ["AccentHover"] = "#1D4ED8", ["AccentPressed"] = "#3B82F6" }
        ),
        ["Green"] = new(
            Dark: new() { ["AccentBlue"] = "#22C55E", ["AccentHover"] = "#4ADE80", ["AccentPressed"] = "#16A34A" },
            Light: new() { ["AccentBlue"] = "#16A34A", ["AccentHover"] = "#15803D", ["AccentPressed"] = "#22C55E" }
        ),
        ["Red"] = new(
            Dark: new() { ["AccentBlue"] = "#DC3545", ["AccentHover"] = "#F06070", ["AccentPressed"] = "#B82030" },
            Light: new() { ["AccentBlue"] = "#DC2626", ["AccentHover"] = "#B91C1C", ["AccentPressed"] = "#EF4444" }
        ),
        ["Violet"] = new(
            Dark: new() { ["AccentBlue"] = "#7C3AED", ["AccentHover"] = "#A78BFA", ["AccentPressed"] = "#6D28D9" },
            Light: new() { ["AccentBlue"] = "#7C3AED", ["AccentHover"] = "#6D28D9", ["AccentPressed"] = "#A78BFA" }
        ),
        ["Orange"] = new(
            Dark: new() { ["AccentBlue"] = "#F59E0B", ["AccentHover"] = "#FBC34D", ["AccentPressed"] = "#D97706" },
            Light: new() { ["AccentBlue"] = "#D97706", ["AccentHover"] = "#B45309", ["AccentPressed"] = "#F59E0B" }
        ),
    };

    private sealed record AccentVariants(Dictionary<string, string> Dark, Dictionary<string, string> Light);

    /// <summary>Read a themed brush from Application.Current.Resources.</summary>
    public static Avalonia.Media.SolidColorBrush Brush(string key) =>
        Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var val) == true
        && val is Avalonia.Media.SolidColorBrush b
            ? b
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(
                DarkBase.TryGetValue(key, out var hex) ? hex : "#FF00FF"));

    private sealed class ThemeData
    {
        public string? Theme { get; set; }
        public string? Accent { get; set; }
    }
}
