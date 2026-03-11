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
}
