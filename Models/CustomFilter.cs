using System;
using System.Collections.Generic;

namespace CleanScan.Models;

public sealed class CustomFilter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Custom";
    public bool Enabled { get; set; }
    public List<string> Dlls { get; set; } = [];
    public List<string> Scripts { get; set; } = [];
    public string Code { get; set; } = "";
    public string Position { get; set; } = "AfterSharpen";

    /// <summary>Per-placeholder control definitions (Phase 3).</summary>
    public List<CustomFilterControl> Controls { get; set; } = [];
}

public sealed class CustomFilterControl
{
    public string Placeholder { get; set; } = "";
    public string Type { get; set; } = "text"; // slider, combo, checkbox, text
    public string Default { get; set; } = "";

    /// <summary>Optional tooltip description shown on the parameter label.</summary>
    public string? Description { get; set; }

    // Slider
    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;

    // ComboBox
    public List<string> Options { get; set; } = [];

    // CheckBox
    public string OnValue { get; set; } = "true";
    public string OffValue { get; set; } = "false";

    /// <summary>When true, the value is divided by 2 in preview_half mode (for crop/dimension parameters).</summary>
    public bool ScaleWithPreview { get; set; }
}
