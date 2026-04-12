using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AvyScanLab.Models;

namespace AvyScanLab.Services;

public sealed class CustomFilterService
{
    private readonly string _filtersDir;
    private readonly string _backupDir;
    private List<CustomFilter> _filters = [];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CustomFilterService(string filtersDir)
    {
        _filtersDir = filtersDir;
        _backupDir = AppConstants.GetFiltersBackupDir();
        Load();
    }

    public IReadOnlyList<CustomFilter> Filters => _filters;

    // ── CRUD ────────────────────────────────────────────────────────────

    public CustomFilter Add(string name = "Custom")
    {
        var filter = new CustomFilter { Name = name };
        _filters.Add(filter);
        SaveFilter(filter);
        return filter;
    }

    public void Add(CustomFilter filter)
    {
        _filters.Add(filter);
        SaveFilter(filter);
    }

    public void InsertAt(int index, CustomFilter filter)
    {
        _filters.Insert(index, filter);
        SaveFilter(filter);
    }

    public void Remove(string id)
    {
        _filters.RemoveAll(f => f.Id == id);
        var path = Path.Combine(_filtersDir, $"{id}.json");
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    public CustomFilter? GetById(string id) =>
        _filters.Find(f => f.Id == id);

    // ── Backup / Restore ────────────────────────────────────────────────

    /// <summary>Moves a filter to the backup directory instead of deleting it.</summary>
    public void BackupAndRemove(string id)
    {
        var srcPath = Path.Combine(_filtersDir, $"{id}.json");
        if (File.Exists(srcPath))
        {
            try
            {
                Directory.CreateDirectory(_backupDir);
                var destPath = Path.Combine(_backupDir, $"{id}.json");
                File.Move(srcPath, destPath, overwrite: true);
            }
            catch { /* best effort */ }
        }
        _filters.RemoveAll(f => f.Id == id);
    }

    /// <summary>Returns the list of filters currently in the backup directory.</summary>
    public List<CustomFilter> GetBackedUpFilters()
    {
        var result = new List<CustomFilter>();
        if (!Directory.Exists(_backupDir)) return result;
        foreach (var jsonFile in Directory.GetFiles(_backupDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var f = JsonSerializer.Deserialize<CustomFilter>(json, JsonOpts);
                if (f is not null && !string.IsNullOrWhiteSpace(f.Id))
                    result.Add(f);
            }
            catch { /* skip malformed */ }
        }
        return result;
    }

    /// <summary>Moves a filter from the backup directory back to the active directory.</summary>
    public bool RestoreFilter(string id)
    {
        var srcPath = Path.Combine(_backupDir, $"{id}.json");
        if (!File.Exists(srcPath)) return false;
        try
        {
            Directory.CreateDirectory(_filtersDir);
            var destPath = Path.Combine(_filtersDir, $"{id}.json");
            File.Move(srcPath, destPath, overwrite: true);
            Load();
            return true;
        }
        catch { return false; }
    }

    // ── Persistence ─────────────────────────────────────────────────────

    /// <summary>Writes a single filter to its own JSON file.
    /// Also removes any stale file whose name no longer matches the filter Id
    /// (e.g. a shipped file named differently from the Id it contains).</summary>
    public void SaveFilter(CustomFilter filter)
    {
        try
        {
            Directory.CreateDirectory(_filtersDir);
            var path = Path.Combine(_filtersDir, $"{filter.Id}.json");
            var json = JsonSerializer.Serialize(filter, JsonOpts);
            File.WriteAllText(path, json);
            CleanStaleFile(filter.Id);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// If a JSON file in the directory contains this Id but has a different
    /// file name, delete it to avoid duplicates after save.
    /// </summary>
    private void CleanStaleFile(string id)
    {
        var expectedName = $"{id}.json";
        if (!Directory.Exists(_filtersDir)) return;
        foreach (var file in Directory.GetFiles(_filtersDir, "*.json"))
        {
            if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var json = File.ReadAllText(file);
                var f = JsonSerializer.Deserialize<CustomFilter>(json, JsonOpts);
                if (f is not null && string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase))
                    File.Delete(file);
            }
            catch { /* skip */ }
        }
    }

    /// <summary>Writes all in-memory filters to disk (batch operation, e.g. reorder).</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_filtersDir);
            foreach (var f in _filters)
                SaveFilter(f);
        }
        catch { /* best effort */ }
    }

    /// <summary>Reads all *.json files from the filters directory.</summary>
    public void Load()
    {
        _filters = [];
        if (!Directory.Exists(_filtersDir)) return;
        foreach (var jsonFile in Directory.GetFiles(_filtersDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var f = JsonSerializer.Deserialize<CustomFilter>(json, JsonOpts);
                if (f is not null && !string.IsNullOrWhiteSpace(f.Id))
                    _filters.Add(f);
            }
            catch { /* skip malformed */ }
        }
    }

    // ── First-run seeding & migration ───────────────────────────────────

    /// <summary>
    /// Copies shipped filter files from {exe}/Filters/ into the AppData Filters
    /// directory. Only runs when the AppData directory does not yet exist (fresh install).
    /// </summary>
    public void SeedFromShipped()
    {
        if (Directory.Exists(_filtersDir)) return;
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeDir)) return;
        var shippedDir = Path.Combine(exeDir, "Filters");
        if (!Directory.Exists(shippedDir)) return;

        Directory.CreateDirectory(_filtersDir);
        foreach (var src in Directory.GetFiles(shippedDir, "*.json"))
        {
            try
            {
                var dest = Path.Combine(_filtersDir, Path.GetFileName(src));
                File.Copy(src, dest, overwrite: false);
            }
            catch { /* skip on error */ }
        }
        Load();
    }

    /// <summary>
    /// Migrates the legacy single-file format (custom_filters.json array) to
    /// individual per-filter files. The legacy file is renamed to .migrated.
    /// </summary>
    public void MigrateFromLegacy(string legacyFilePath)
    {
        if (!File.Exists(legacyFilePath)) return;

        // If the Filters directory already has files, just archive the legacy file
        if (Directory.Exists(_filtersDir) && Directory.GetFiles(_filtersDir, "*.json").Length > 0)
        {
            try { File.Move(legacyFilePath, legacyFilePath + ".migrated", overwrite: true); } catch { }
            return;
        }

        try
        {
            var json = File.ReadAllText(legacyFilePath);
            var filters = JsonSerializer.Deserialize<List<CustomFilter>>(json, JsonOpts);
            if (filters is not null)
            {
                Directory.CreateDirectory(_filtersDir);
                foreach (var f in filters)
                {
                    if (!string.IsNullOrWhiteSpace(f.Id))
                        SaveFilter(f);
                }
            }
            File.Move(legacyFilePath, legacyFilePath + ".migrated", overwrite: true);
            Load();
        }
        catch { /* if migration fails, SeedFromShipped will handle it */ }
    }

    // ── Shipped filter detection ────────────────────────────────────────

    /// <summary>
    /// Returns true if a filter with this ID exists in the shipped {exe}/Filters/
    /// directory. Used to decide between backup (base filter) and hard delete
    /// (user-created filter) when removing.
    /// </summary>
    public static bool IsShippedFilter(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeDir)) return false;
        var shippedPath = Path.Combine(exeDir, "Filters", $"{id}.json");
        return File.Exists(shippedPath);
    }
}
