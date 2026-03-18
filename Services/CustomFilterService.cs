using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CleanScan.Models;

namespace CleanScan.Services;

public sealed class CustomFilterService
{
    private readonly string _filePath;
    private List<CustomFilter> _filters = [];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CustomFilterService(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public IReadOnlyList<CustomFilter> Filters => _filters;

    public CustomFilter Add(string name = "Custom")
    {
        var filter = new CustomFilter { Name = name };
        _filters.Add(filter);
        Save();
        return filter;
    }

    public void Add(CustomFilter filter)
    {
        _filters.Add(filter);
        Save();
    }

    public void Remove(string id)
    {
        _filters.RemoveAll(f => f.Id == id);
        Save();
    }

    public CustomFilter? GetById(string id) =>
        _filters.Find(f => f.Id == id);

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_filters, JsonOpts);
            File.WriteAllText(_filePath, json);
        }
        catch { /* best effort */ }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _filters = JsonSerializer.Deserialize<List<CustomFilter>>(json, JsonOpts) ?? [];
            }
        }
        catch
        {
            _filters = [];
        }
    }

    /// <summary>
    /// Imports built-in filter JSON files from the Filters/ directory next to the executable.
    /// Only imports filters whose Id is not already present in the user's list.
    /// Called once at startup.
    /// </summary>
    public void ImportBuiltInFilters()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeDir)) return;

        var filtersDir = Path.Combine(exeDir, "Filters");
        if (!Directory.Exists(filtersDir)) return;

        var existingById = new Dictionary<string, CustomFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in _filters)
            existingById[f.Id] = f;
        var changed = false;

        foreach (var jsonFile in Directory.GetFiles(filtersDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var filter = JsonSerializer.Deserialize<CustomFilter>(json, JsonOpts);
                if (filter is null || string.IsNullOrWhiteSpace(filter.Id)) continue;

                if (existingById.TryGetValue(filter.Id, out var existing))
                {
                    // Update code/scripts/dlls from shipped version (user may have customized controls)
                    if (!string.Equals(existing.Code, filter.Code, StringComparison.Ordinal))
                    {
                        existing.Code = filter.Code;
                        existing.Dlls = filter.Dlls;
                        existing.Scripts = filter.Scripts;
                        existing.Controls = filter.Controls;
                        changed = true;
                    }
                }
                else
                {
                    _filters.Add(filter);
                    existingById[filter.Id] = filter;
                    changed = true;
                }
            }
            catch { /* skip malformed files */ }
        }

        if (changed) Save();
    }
}
