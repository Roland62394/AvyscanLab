using System;
using System.IO;
using System.Text.Json;

namespace CleanScan.Services;

public sealed class SessionService(string filePath)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SessionState? Load()
    {
        if (!File.Exists(filePath)) return null;
        try { return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(filePath), JsonOpts); }
        catch { return null; }
    }

    public void Save(SessionState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, JsonSerializer.Serialize(state, JsonOpts));
        }
        catch { /* Session save must never crash the app */ }
    }

    public void Delete()
    {
        try { if (File.Exists(filePath)) File.Delete(filePath); }
        catch { }
    }
}
