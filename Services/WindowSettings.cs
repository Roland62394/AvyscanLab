namespace CleanScan.Services;

public sealed record WindowSettings(double Width, double Height, int X, int Y, string? Language = null, double? BottomPanelHeight = null, string[]? OpenPanels = null, string? LastOutputDir = null, bool? AutoSaveEncodingPreset = null, bool? RecordPanelOpen = null, bool? TourCompleted = null, double? FilterColumnWidth = null, bool? SuppressExitFeedback = null);
