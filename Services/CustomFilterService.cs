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

        var existingIds = new HashSet<string>(_filters.ConvertAll(f => f.Id), StringComparer.OrdinalIgnoreCase);
        var imported = false;

        foreach (var jsonFile in Directory.GetFiles(filtersDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var filter = JsonSerializer.Deserialize<CustomFilter>(json, JsonOpts);
                if (filter is null || string.IsNullOrWhiteSpace(filter.Id)) continue;
                if (existingIds.Contains(filter.Id)) continue;

                _filters.Add(filter);
                existingIds.Add(filter.Id);
                imported = true;
            }
            catch { /* skip malformed files */ }
        }

        if (imported) Save();
    }
}
