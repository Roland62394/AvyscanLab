using System.Collections.Generic;

namespace AvyscanLab.Services;

public sealed class Preset(string name, Dictionary<string, string> values)
{
    public string Name { get; set; } = name;
    public Dictionary<string, string> Values { get; set; } = values;
}

public sealed class PresetDialogResult
{
    public (string Name, Dictionary<string, string> Values, bool ApplyToAll)? Apply { get; set; }
    public Dictionary<string, Dictionary<string, string>> UpdatedPresets { get; } = new(System.StringComparer.OrdinalIgnoreCase);
}
