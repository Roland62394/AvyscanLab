using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanScan.Services;

public sealed class ConfigStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, string>? Changed;

    public IReadOnlyDictionary<string, string> Values => _values;

    public string Get(string key, string fallback = "")
        => _values.TryGetValue(key, out var value) ? value : fallback;

    public bool Set(string key, string value, Func<string, string>? normalize = null)
    {
        value ??= string.Empty;
        if (normalize is not null)
        {
            value = normalize(value);
        }

        if (_values.TryGetValue(key, out var oldValue) && string.Equals(oldValue, value, StringComparison.Ordinal))
        {
            return false;
        }

        _values[key] = value;
        Changed?.Invoke(key, value);
        return true;
    }

    public Dictionary<string, string> Snapshot()
        => _values.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    public void ReplaceAll(Dictionary<string, string> values)
    {
        _values.Clear();
        foreach (var kv in values)
        {
            _values[kv.Key] = kv.Value ?? string.Empty;
        }
    }
}
