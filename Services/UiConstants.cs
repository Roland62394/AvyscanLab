using Avalonia.Media;

namespace AvyScanLab.Services;

/// <summary>
/// Shared UI constants (fonts, etc.) to avoid duplicating magic strings across files.
/// </summary>
public static class UiConstants
{
    /// <summary>Monospace font for logs, encoding output, diagnostics.</summary>
    public static readonly FontFamily MonoFont = new("Consolas,Cascadia Code,monospace");

    /// <summary>Monospace font for code editors and script display.</summary>
    public static readonly FontFamily CodeFont = new("Consolas,Courier New,monospace");
}
