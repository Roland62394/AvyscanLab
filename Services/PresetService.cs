using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanScan.Services;

public sealed class PresetService(string filePath) : IPresetService
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    public List<Preset> LoadPresets()
    {
        if (!File.Exists(filePath)) return [];
        try
        {
            var store = JsonSerializer.Deserialize<PresetStore>(File.ReadAllText(filePath));
            return store?.Presets ?? [];
        }
        catch (JsonException) { return []; }
    }

    public void SavePresets(List<Preset> presets)
    {
        EnsureDirectory(filePath);
        File.WriteAllText(filePath, JsonSerializer.Serialize(new PresetStore(presets), JsonIndented));
    }

    public static readonly HashSet<string> ExcludedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "source", "film", "img", "img_start", "img_end", "use_img",
        "enable_crop", "Crop_L", "Crop_T", "Crop_R", "Crop_B"
    };

    public Dictionary<string, string> CaptureCurrentValues(ConfigStore config)
    {
        var snapshot = config.Snapshot();
        foreach (var key in ExcludedKeys)
            snapshot.Remove(key);
        return snapshot;
    }

    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    private sealed record PresetStore(List<Preset> Presets);
}
