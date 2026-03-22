using System;
using System.Collections.Generic;

namespace AvyscanLab.Models;

public sealed class CustomFilter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Custom";
    public bool Enabled { get; set; }
    public List<string> Dlls { get; set; } = [];
    public List<string> Scripts { get; set; } = [];
    public string Code { get; set; } = "";
    public string Position { get; set; } = "AfterSharpen";

    /// <summary>When true, the filter supports region drawing via mouse on video.</summary>
    public bool RegionDraw { get; set; }

    /// <summary>
    /// Region draw mode: "xywh" or "crop".
    /// </summary>
    public string RegionDrawMode { get; set; } = "xywh";

    /// <summary>
    /// Placeholder names for region draw.
    /// XYWH mode: [X, Y, W, H]. Crop mode: [left, top, right, bottom].
    /// When empty, defaults are used: X/Y/W/H or crop_left/crop_top/crop_right/crop_bottom.
    /// </summary>
    public List<string> RegionDrawPlaceholders { get; set; } = [];

    /// <summary>Per-placeholder control definitions (Phase 3).</summary>
    public List<CustomFilterControl> Controls { get; set; } = [];

    /// <summary>Returns the effective placeholder names for region draw (4 elements).</summary>
    public (string P0, string P1, string P2, string P3) GetRegionPlaceholders()
    {
        if (RegionDrawPlaceholders is { Count: >= 4 })
            return (RegionDrawPlaceholders[0], RegionDrawPlaceholders[1],
                    RegionDrawPlaceholders[2], RegionDrawPlaceholders[3]);

        return RegionDrawMode == "crop"
            ? ("crop_left", "crop_top", "crop_right", "crop_bottom")
            : ("X", "Y", "W", "H");
    }
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
