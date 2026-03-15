using System.Collections.Generic;

namespace CleanScan.Models;

/// <summary>Per-clip state (replaces 6 parallel lists in MainWindow).</summary>
public sealed class ClipState
{
    public string Path { get; set; } = "";
    public Dictionary<string, string> Config { get; set; } = new();
    public string? PresetName { get; set; }
    public string? OutputName { get; set; }
    public string? GammacPresetName { get; set; }
    public bool BatchSelected { get; set; } = true;
    public string? BatchEncodingPreset { get; set; }
}
