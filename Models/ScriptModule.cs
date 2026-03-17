using System.Collections.Generic;

namespace CleanScan.Models;

/// <summary>
/// Represents a modular AviSynth filter that can be assembled into the restoration pipeline.
/// Each module provides its own function definitions and pipeline code.
/// The assembler merges all active modules into a single script.
/// </summary>
public sealed class ScriptModule
{
    /// <summary>Unique identifier (e.g. "denoise", "degrain", "gammac").</summary>
    public required string Id { get; init; }

    /// <summary>Display name shown in UI.</summary>
    public required string Name { get; init; }

    /// <summary>Pipeline order (100=GamMac, 200=Denoise, 300=Degrain, 400=Luma, 500=Sharpen).</summary>
    public int Position { get; init; }

    /// <summary>ConfigStore key that enables/disables this module (null = always active).</summary>
    public string? EnableKey { get; init; }

    /// <summary>Max temporal radius in frames (0 = frame-local, 3 = MDegrain3).</summary>
    public int TemporalRadius { get; init; }

    /// <summary>AviSynth config variable declarations (enable toggle + parameters).</summary>
    public string ConfigSection { get; init; } = "";

    /// <summary>Plugin DLLs to load via TryLoadPlugin (deduplicated at merge time).</summary>
    public List<string> Dlls { get; init; } = [];

    /// <summary>AviSynth script imports (.avsi) to load via Import (deduplicated at merge time).</summary>
    public List<string> Scripts { get; init; } = [];

    /// <summary>AviSynth function definitions (e.g. DenoiseFilm, DegrainFilm).</summary>
    public string Functions { get; init; } = "";

    /// <summary>Pipeline step code inserted in order. Uses 'c' as the current clip variable.</summary>
    public string PipelineCode { get; init; } = "";

    /// <summary>Custom filter injection point name placed after this module's pipeline code.</summary>
    public string? InjectionPointAfter { get; init; }
}
