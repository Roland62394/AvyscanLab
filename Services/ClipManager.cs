using System;
using System.Collections.Generic;
using System.Linq;
using AvyScanLab.Models;

namespace AvyScanLab.Services;

/// <summary>Manages the collection of clips and their per-clip state.</summary>
public sealed class ClipManager
{
    private readonly ConfigStore _config;

    /// <summary>Trial: maximum number of clips allowed. 0 = unlimited (full version).</summary>
    public const int TrialMaxClips = 3;

    public ClipManager(ConfigStore config)
    {
        _config = config;
    }

    public List<ClipState> Clips { get; } = [];
    public int ActiveIndex { get; set; } = -1;

    /// <summary>Returns true if the trial clip limit has been reached.</summary>
    public bool IsClipLimitReached => TrialMaxClips > 0 && Clips.Count >= TrialMaxClips;

    public ClipState? ActiveClip =>
        ActiveIndex >= 0 && ActiveIndex < Clips.Count ? Clips[ActiveIndex] : null;

    /// <summary>Add a new clip or activate an existing one by path.</summary>
    /// <returns>True if a new clip was added, false if an existing one was activated.</returns>
    public bool AddOrActivate(string path, Dictionary<string, string> currentConfig)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var idx = Clips.FindIndex(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            ActiveIndex = idx;
            return false;
        }

        // Trial clip limit
        if (IsClipLimitReached) return false;

        Clips.Add(new ClipState
        {
            Path = path,
            Config = new Dictionary<string, string>(currentConfig, StringComparer.OrdinalIgnoreCase),
        });
        ActiveIndex = Clips.Count - 1;
        return true;
    }

    /// <summary>Captures the current config snapshot (filter-only, excludes source/crop).</summary>
    public Dictionary<string, string> CaptureConfig()
    {
        var snap = _config.Snapshot();
        foreach (var key in PresetService.ExcludedKeys)
            snap.Remove(key);
        return snap;
    }

    /// <summary>Saves the current config into the active clip's state.</summary>
    public void SaveActiveConfig()
    {
        if (ActiveClip is { } clip)
            clip.Config = CaptureConfig();
    }

    /// <summary>Removes a clip by index and adjusts ActiveIndex.</summary>
    /// <returns>The removal result indicating what action the caller should take.</returns>
    public ClipRemoveResult Remove(int index)
    {
        if (index < 0 || index >= Clips.Count)
            return new ClipRemoveResult(false, false, -1);

        bool wasActive = index == ActiveIndex;
        Clips.RemoveAt(index);

        if (Clips.Count == 0)
        {
            ActiveIndex = -1;
            return new ClipRemoveResult(true, wasActive, -1);
        }

        if (wasActive)
        {
            ActiveIndex = Math.Min(index, Clips.Count - 1);
            return new ClipRemoveResult(true, true, ActiveIndex);
        }

        if (index < ActiveIndex)
            ActiveIndex--;

        return new ClipRemoveResult(true, false, ActiveIndex);
    }

    /// <summary>Returns a unique "persoN" name not already used by another clip.</summary>
    public string GetNextPersoName()
    {
        int n = 1;
        while (Clips.Any(c => string.Equals(c.PresetName, $"perso{n}", StringComparison.OrdinalIgnoreCase)))
            n++;
        return $"perso{n}";
    }

    /// <summary>Applies a preset name and config snapshot to all clips.</summary>
    public void PropagatePresetToAll(string presetName, Dictionary<string, string> filterConfig)
    {
        foreach (var clip in Clips)
        {
            clip.Config = new Dictionary<string, string>(filterConfig, StringComparer.OrdinalIgnoreCase);
            clip.PresetName = presetName;
        }
    }

    /// <summary>Clears all clips and resets state.</summary>
    public void Clear()
    {
        Clips.Clear();
        ActiveIndex = -1;
    }
}

public readonly record struct ClipRemoveResult(bool Removed, bool WasActive, int NewActiveIndex);

/// <summary>Provides indexed read/write access to ClipState.Config dictionaries.</summary>
public sealed class ClipConfigView(List<ClipState> clips)
{
    public int Count => clips.Count;
    public Dictionary<string, string> this[int i]
    {
        get => clips[i].Config;
        set => clips[i].Config = value;
    }
}

/// <summary>Provides indexed read/write access to a projected property of ClipState,
/// allowing legacy code that used parallel lists to keep working unchanged.</summary>
public sealed class ClipListView<T>(List<ClipState> clips, Func<ClipState, T> getter, Action<ClipState, T> setter)
{
    public int Count => clips.Count;
    public T this[int i]
    {
        get => getter(clips[i]);
        set => setter(clips[i], value);
    }

    public bool Contains(T value, StringComparer? comparer = null)
    {
        if (comparer is not null && value is string sv)
            return clips.Any(c => comparer.Equals(getter(c) as string, sv));
        return clips.Any(c => EqualityComparer<T>.Default.Equals(getter(c), value));
    }
}
