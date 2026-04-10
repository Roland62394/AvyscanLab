using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AvyScanLab.Models;

namespace AvyScanLab.Services;

public sealed class CustomFilterService
{
    private readonly string _filePath;
    private List<CustomFilter> _filters = [];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Authoritative DLL/.avsi metadata for built-in filters.
    /// This dictionary is the **single source of truth** that shields the app
    /// from the recurring "empty Dlls/Scripts arrays" regression in
    /// <c>Filters/*.json</c>: every time those JSON files were edited for an
    /// unrelated reason (controls, descriptions, code), the Dlls/Scripts arrays
    /// were silently reset to <c>[]</c> and the Custom Filter dialog stopped
    /// showing the absolute paths the user expects.
    ///
    /// Lookup is by filter <c>Id</c>. Applied as a fallback in
    /// <see cref="ImportBuiltInFilters"/> whenever the JSON-loaded list is empty,
    /// so future edits of <c>Filters/*.json</c> can never lose this metadata
    /// again. The DLL filenames must match what the NSIS installer copies into
    /// the AviSynth+ <c>plugins64+</c> folder (see <c>Installer/AvyScanLab.NSI</c>).
    /// </summary>
    private static readonly Dictionary<string, (string[] Dlls, string[] Scripts)> BuiltInPlugins =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["degrain"] = (["mvtools2.dll", "RgTools.dll"],                  []),
            ["denoise"] = (["RemoveDirt.dll", "RgTools.dll", "mvtools2.dll"], ["RemoveDirt.avsi"]),
            ["gammac"]  = (["GamMac_x64.dll"],                                []),
            ["sharpen"] = (["masktools2.dll"],                                []),
            ["stab8mm"] = (["DePanEstimate.dll", "DePan.dll", "mvtools2.dll"], []),
        };

    /// <summary>Apply the built-in DLL/script defaults to a filter when its
    /// JSON-loaded lists are empty. Returns true if anything was filled in.</summary>
    private static bool ApplyBuiltInPluginDefaults(CustomFilter filter)
    {
        if (!BuiltInPlugins.TryGetValue(filter.Id, out var defaults)) return false;
        var changed = false;
        if (filter.Dlls.Count == 0 && defaults.Dlls.Length > 0)
        {
            filter.Dlls = [..defaults.Dlls];
            changed = true;
        }
        if (filter.Scripts.Count == 0 && defaults.Scripts.Length > 0)
        {
            filter.Scripts = [..defaults.Scripts];
            changed = true;
        }
        return changed;
    }

    private static bool ListsEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool SyncDescriptions(List<CustomFilterControl> existing, List<CustomFilterControl> shipped)
    {
        var changed = false;
        foreach (var sc in shipped)
        {
            var ec = existing.Find(c => string.Equals(c.Placeholder, sc.Placeholder, StringComparison.OrdinalIgnoreCase));
            if (ec is null) continue;

            if (!string.IsNullOrWhiteSpace(sc.Description)
                && !string.Equals(ec.Description, sc.Description, StringComparison.Ordinal))
            {
                ec.Description = sc.Description;
                changed = true;
            }

            if (ec.ScaleWithPreview != sc.ScaleWithPreview)
            {
                ec.ScaleWithPreview = sc.ScaleWithPreview;
                changed = true;
            }
        }
        return changed;
    }

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

    public void InsertAt(int index, CustomFilter filter)
    {
        _filters.Insert(index, filter);
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

                // Heal the user's persisted state in case a previous run wrote
                // empty Dlls/Scripts for a built-in filter (regression defense).
                foreach (var f in _filters)
                    ApplyBuiltInPluginDefaults(f);
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

                // Defense against the recurring "empty Dlls/Scripts" regression
                // in shipped Filters/*.json: backfill from BuiltInPlugins so the
                // sync below propagates correct paths into the user's AppData.
                ApplyBuiltInPluginDefaults(filter);

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
                    // Always sync Dlls/Scripts/Descriptions even if Code unchanged
                    else
                    {
                        if (!ListsEqual(existing.Dlls, filter.Dlls)
                            || !ListsEqual(existing.Scripts, filter.Scripts))
                        {
                            existing.Dlls = filter.Dlls;
                            existing.Scripts = filter.Scripts;
                            changed = true;
                        }
                        // Sync descriptions from shipped controls
                        if (SyncDescriptions(existing.Controls, filter.Controls))
                            changed = true;
                    }
                    // Sync RegionDraw flag and mode from shipped version
                    if (existing.RegionDraw != filter.RegionDraw)
                    {
                        existing.RegionDraw = filter.RegionDraw;
                        changed = true;
                    }
                    if (!string.Equals(existing.RegionDrawMode, filter.RegionDrawMode, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.RegionDrawMode = filter.RegionDrawMode;
                        changed = true;
                    }
                    if (!ListsEqual(existing.RegionDrawPlaceholders, filter.RegionDrawPlaceholders))
                    {
                        existing.RegionDrawPlaceholders = filter.RegionDrawPlaceholders;
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
