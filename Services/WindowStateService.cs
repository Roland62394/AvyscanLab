using System;
using System.IO;
using System.Text.Json;

namespace AvyScanLab.Services;

public sealed class WindowStateService(string filePath) : IWindowStateService
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    public WindowSettings? Load()
    {
        if (!File.Exists(filePath)) return null;
        try { return JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(filePath)); }
        catch (JsonException) { return null; }
    }

    public void Save(WindowSettings settings)
    {
        EnsureDirectory(filePath);
        File.WriteAllText(filePath, JsonSerializer.Serialize(settings, JsonIndented));
    }

    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }
}
