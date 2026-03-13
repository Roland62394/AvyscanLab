using System.Collections.Generic;

namespace CleanScan.Services;

public sealed record ClipSession(
    string Path,
    Dictionary<string, string> FilterConfig,
    string? PresetName,
    bool BatchSelected,
    string? BatchEncodingPreset,
    string? OutputName = null);

public sealed record SessionState(
    int ActiveClipIndex,
    List<ClipSession> Clips,
    Dictionary<string, string>? EncodingValues = null,
    string? EncodingPresetName = null);
