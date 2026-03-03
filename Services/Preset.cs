using System.Collections.Generic;

namespace CleanScan.Services;

public sealed class Preset(string name, Dictionary<string, string> values)
{
    public string Name { get; set; } = name;
    public Dictionary<string, string> Values { get; set; } = values;
}
