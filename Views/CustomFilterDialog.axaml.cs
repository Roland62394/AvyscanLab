using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CleanScan.Models;
using CleanScan.Services;
using CleanScan.ViewModels;

namespace CleanScan.Views;

public partial class CustomFilterDialog : Window
{
    private readonly CustomFilter _filter;
    private readonly List<string> _dlls;
    private readonly List<string> _scripts;
    private readonly List<CustomFilterControl> _controls;
    private readonly MainWindowViewModel? _vm;
    private readonly ThemeService? _themeService;

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex AssignmentRegex = new(@"^\s*(\w+)\s*=\s*(.+?)\s*$", RegexOptions.Compiled);

    /// <summary>Session-level flag: suppress the "convert to control" prompt.</summary>
    private static bool _suppressConvertPrompt;

    // Persisted layout for the custom filter dialog
    private sealed record CfDlgLayout(double? Width, double? Height, int? X, int? Y,
        double? CodeRowHeight, double? ParamsRowHeight);

    private static CfDlgLayout? _cachedLayout;

    private static string LayoutFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanScan", "cfdlg-layout.json");

    private static CfDlgLayout? LoadLayout()
    {
        if (_cachedLayout is not null) return _cachedLayout;
        try
        {
            if (!File.Exists(LayoutFilePath)) return null;
            _cachedLayout = System.Text.Json.JsonSerializer.Deserialize<CfDlgLayout>(
                File.ReadAllText(LayoutFilePath));
            return _cachedLayout;
        }
        catch { return null; }
    }

    private static void SaveLayout(CfDlgLayout layout)
    {
        _cachedLayout = layout;
        try
        {
            var dir = Path.GetDirectoryName(LayoutFilePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(LayoutFilePath,
                System.Text.Json.JsonSerializer.Serialize(layout,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }

    /// <summary>True if user clicked Save.</summary>
    public bool Saved { get; private set; }

    /// <summary>True if user clicked Delete.</summary>
    public bool Deleted { get; private set; }

    /// <summary>Callback to preview the current code live (regenerate script + reload mpv).</summary>
    public Action<CustomFilter>? OnPreview { get; set; }

    public CustomFilterDialog() : this(new CustomFilter()) { }

    public CustomFilterDialog(CustomFilter filter, bool isNew = false, MainWindowViewModel? vm = null,
        double ownerHeight = 0, ThemeService? themeService = null)
    {
        _filter = filter;
        _dlls = [..filter.Dlls];
        _scripts = [..filter.Scripts];
        _controls = filter.Controls.Select(CloneControl).ToList();
        _vm = vm;
        _themeService = themeService;

        InitializeComponent();

        // Apply theme palette to this dialog's resources
        ApplyThemePalette();

        // Restore persisted size/position/grid
        var layout = LoadLayout();
        if (layout is { Width: > 0, Height: > 0 })
        {
            Width = layout.Width.Value;
            Height = layout.Height.Value;
        }
        else if (ownerHeight > 0)
        {
            Height = Math.Max(500, ownerHeight * 0.8);
        }

        if (layout is { X: not null, Y: not null })
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(layout.X.Value, layout.Y.Value);
        }

        // Restore grid splitter row heights (code / params ratio)
        if (layout is { CodeRowHeight: > 0, ParamsRowHeight: > 0 })
        {
            Opened += (_, _) =>
            {
                var mainGrid = this.FindControl<Grid>("MainContentGrid");
                if (mainGrid is null) return;
                mainGrid.RowDefinitions[1].Height = new GridLength(layout.CodeRowHeight!.Value, GridUnitType.Star);
                mainGrid.RowDefinitions[3].Height = new GridLength(layout.ParamsRowHeight!.Value, GridUnitType.Star);
            };
        }

        // Save everything on close
        Closing += (_, _) =>
        {
            double? codeH = null, paramsH = null;
            var mainGrid = this.FindControl<Grid>("MainContentGrid");
            if (mainGrid is not null)
            {
                codeH = mainGrid.RowDefinitions[1].ActualHeight;
                paramsH = mainGrid.RowDefinitions[3].ActualHeight;
            }
            SaveLayout(new CfDlgLayout(Bounds.Width, Bounds.Height,
                Position.X, Position.Y, codeH, paramsH));
        };

        // Wire events
        TitleBar.PointerPressed += (_, e) => BeginMoveDrag(e);
        CloseXButton.Click += (_, _) => Close();
        CancelBtn.Click += (_, _) => Close();
        SaveBtn.Click += (_, _) => OnSave();
        DeleteBtn.Click += (_, _) => OnDelete();
        AddDllBtn.Click += async (_, _) => await OnAddDll();
        AddScriptBtn.Click += async (_, _) => await OnAddScript();

        PreviewBtn.Click += (_, _) => OnPreviewClick();
        ToolTip.SetTip(PreviewBtn, L("CfDlgPreviewTip"));
        ExportBtn.Click += async (_, _) => await OnExport();
        ToolTip.SetTip(ExportBtn, L("CfDlgExportTip"));
        HelpBtn.Click += async (_, _) => await ShowHelp();

        // Hide delete button for new filters (not yet saved)
        DeleteBtn.IsVisible = !isNew;

        // Populate fields
        FilterNameBox.Text = filter.Name;

        foreach (var pos in ScriptService.InjectionPositions)
            PositionCombo.Items.Add(pos);
        PositionCombo.SelectedItem = filter.Position;

        RebuildDllList();
        RebuildScriptList();

        // Set code BEFORE wiring TextChanged to avoid wiping _controls
        CodeBox.Text = filter.Code;

        // Auto-detect placeholders when code changes
        CodeBox.TextChanged += (_, _) => SyncPlaceholders();
        SyncPlaceholders();

        // Offer to convert "var = value" selections into {placeholder} controls
        CodeBox.PointerReleased += OnCodeBoxPointerReleased;

        // Apply translations
        ApplyLocalization();
    }

    private string L(string key) => _vm?.GetUiText(key) ?? key;

    private void ApplyLocalization()
    {
        TitleText.Text = L("CfDlgTitle").ToUpperInvariant();
        FilterNameLabel.Text = L("CfDlgFilterName");
        FilterNameBox.Watermark = L("CfDlgFilterNameWatermark");
        PositionLabel.Text = L("CfDlgPosition");
        PluginsLabel.Text = L("CfDlgPlugins");
        AddDllBtn.Content = L("CfDlgAddDll");
        ScriptsLabel.Text = L("CfDlgScripts");
        AddScriptBtn.Content = L("CfDlgAddScript");
        CodeLabel.Text = L("CfDlgCode");
        CodeHint.Text = L("CfDlgCodeHint");
        PreviewBtn.Content = "\u25B6 " + L("CfDlgPreview");
        ParamsLabel.Text = L("CfDlgParams");
        ParamsHint.Text = L("CfDlgParamsHint");
        DeleteBtn.Content = L("CfDlgDelete");
        ExportBtn.Content = "\u2191 " + L("CfDlgExport");
        HelpBtn.Content = "? " + L("CfDlgHelp");
        CancelBtn.Content = L("CfDlgCancel");
        SaveBtn.Content = L("CfDlgSave");
    }

    private void ApplyThemePalette()
    {
        if (_themeService is null) return;
        var palette = ThemeService.GetPalette(_themeService.Theme, _themeService.Accent);
        foreach (var (key, hex) in palette)
            Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private SolidColorBrush ThemeBrush(string key) =>
        Resources.TryGetValue(key, out var val) && val is SolidColorBrush b
            ? b
            : new SolidColorBrush(Colors.Magenta);

    private async System.Threading.Tasks.Task ShowHelp()
    {
        var okBtn = new Button { Content = "OK", MinWidth = 80, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0) };
        var scrollContent = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = L("CfDlgHelpBody"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 20
            }
        };
        Grid.SetRow(scrollContent, 0);
        Grid.SetRow(okBtn, 1);
        var helpGrid = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("*,Auto"),
            Margin = new Thickness(16)
        };
        helpGrid.Children.Add(scrollContent);
        helpGrid.Children.Add(okBtn);
        var dialog = new Window
        {
            Title = L("CfDlgHelpTitle"),
            Width = 520,
            Height = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = helpGrid
        };
        okBtn.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private void OnSave()
    {
        _filter.Name = string.IsNullOrWhiteSpace(FilterNameBox.Text)
            ? "Custom"
            : FilterNameBox.Text.Trim();
        _filter.Position = PositionCombo.SelectedItem as string ?? "AfterSharpen";
        _filter.Dlls = [.._dlls];
        _filter.Scripts = [.._scripts];
        _filter.Code = CodeBox.Text ?? "";
        _filter.Controls = _controls.Select(CloneControl).ToList();

        Saved = true;
        Close();
    }

    private async void OnDelete()
    {
        var filterName = string.IsNullOrWhiteSpace(FilterNameBox.Text) ? "Custom" : FilterNameBox.Text.Trim();
        var msg = L("CfDlgDeleteConfirm").Replace("$name$", filterName);

        var confirmed = false;
        var yesBtn = new Button { Content = L("CfDlgConvertYes"), MinWidth = 80 };
        var noBtn = new Button { Content = L("CfDlgConvertNo"), MinWidth = 80 };

        var dialog = new Window
        {
            Title = L("CfDlgDeleteConfirmTitle"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = 460,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = msg, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 400, FontSize = 13 },
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { yesBtn, noBtn } }
                }
            }
        };

        yesBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };
        noBtn.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);

        if (!confirmed) return;

        Deleted = true;
        Close();
    }

    private void OnPreviewClick()
    {
        // Apply current form state to filter temporarily for preview
        _filter.Name = string.IsNullOrWhiteSpace(FilterNameBox.Text)
            ? "Custom" : FilterNameBox.Text.Trim();
        _filter.Code = CodeBox.Text ?? "";
        _filter.Dlls = [.._dlls];
        _filter.Scripts = [.._scripts];
        _filter.Position = PositionCombo.SelectedItem as string ?? "AfterSharpen";
        _filter.Controls = _controls.Select(CloneControl).ToList();

        // Ensure filter is enabled for preview
        var wasEnabled = _filter.Enabled;
        _filter.Enabled = true;

        OnPreview?.Invoke(_filter);

        _filter.Enabled = wasEnabled;
    }

    private static readonly JsonSerializerOptions ExportJsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private async System.Threading.Tasks.Task OnExport()
    {
        // Build a snapshot of current form state
        var snapshot = new CustomFilter
        {
            Id = _filter.Id,
            Name = string.IsNullOrWhiteSpace(FilterNameBox.Text) ? "Custom" : FilterNameBox.Text.Trim(),
            Enabled = _filter.Enabled,
            Position = PositionCombo.SelectedItem as string ?? "AfterSharpen",
            Dlls = [.._dlls],
            Code = CodeBox.Text ?? "",
            Controls = _controls.Select(CloneControl).ToList()
        };

        var safeName = string.Join("_", snapshot.Name.Split(Path.GetInvalidFileNameChars()));
        var picker = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L("CfDlgExportTitle"),
            SuggestedFileName = $"{safeName}.json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });

        if (picker is null) return;

        var json = JsonSerializer.Serialize(new[] { snapshot }, ExportJsonOpts);
        await File.WriteAllTextAsync(picker.Path.LocalPath, json);
    }

    /// <summary>Deserialize filters from a JSON file. Returns empty list on error.</summary>
    public static List<CustomFilter> ImportFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var filters = JsonSerializer.Deserialize<List<CustomFilter>>(json, ExportJsonOpts);
            if (filters is null) return [];

            // Assign fresh IDs to avoid collisions
            foreach (var f in filters)
                f.Id = Guid.NewGuid().ToString("N")[..8];

            return filters;
        }
        catch
        {
            return [];
        }
    }

    // ── Selection → placeholder conversion ─────────────────────────────

    private async void OnCodeBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_suppressConvertPrompt) return;

        var selected = CodeBox.SelectedText;
        if (string.IsNullOrWhiteSpace(selected)) return;

        var m = AssignmentRegex.Match(selected);
        if (!m.Success) return;

        var varName = m.Groups[1].Value;
        var rawValue = m.Groups[2].Value.Trim();

        // Don't offer if this variable is already a placeholder
        if (PlaceholderRegex.IsMatch(rawValue)) return;

        // Build confirmation dialog — normalize selection direction
        var selStart = Math.Min(CodeBox.SelectionStart, CodeBox.SelectionEnd);
        var selEnd = Math.Max(CodeBox.SelectionStart, CodeBox.SelectionEnd);
        var displaySel = selected.Trim();

        var result = await ShowConvertPrompt(displaySel, varName);

        if (result == ConvertPromptResult.DontShowAgain)
        {
            _suppressConvertPrompt = true;
            return;
        }

        if (result != ConvertPromptResult.Yes) return;

        // Replace the value part with {varName} in the code
        var code = CodeBox.Text ?? "";
        var selectedText = code.Substring(selStart, selEnd - selStart);
        var assignMatch = AssignmentRegex.Match(selectedText);
        if (!assignMatch.Success) return;

        // Build the replacement: keep var and = , replace value with {varName}
        var valueGroup = assignMatch.Groups[2];
        var valueStartInSelection = valueGroup.Index;
        var newSelected = selectedText[..valueStartInSelection] + $"{{{varName}}}" + selectedText[(valueStartInSelection + valueGroup.Length)..];

        CodeBox.Text = code[..selStart] + newSelected + code[selEnd..];

        // Set default value on the newly created control (SyncPlaceholders will fire via TextChanged)
        // Find or wait for the control
        var ctrl = _controls.FirstOrDefault(c => c.Placeholder.Equals(varName, StringComparison.OrdinalIgnoreCase));
        if (ctrl is not null)
        {
            ctrl.Default = rawValue;
        }
        else
        {
            // SyncPlaceholders will create it; set after a short dispatch
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var c = _controls.FirstOrDefault(c2 => c2.Placeholder.Equals(varName, StringComparison.OrdinalIgnoreCase));
                if (c is not null)
                {
                    c.Default = rawValue;
                    RebuildControlsPanel();
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private enum ConvertPromptResult { Yes, No, DontShowAgain }

    private async System.Threading.Tasks.Task<ConvertPromptResult> ShowConvertPrompt(string selection, string varName)
    {
        var result = ConvertPromptResult.No;

        var msg = L("CfDlgConvertPrompt")
            .Replace("$selection$", selection)
            .Replace("$control$", $"{{{varName}}}");

        var yesBtn = new Button
        {
            Content = L("CfDlgConvertYes"),
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var noBtn = new Button
        {
            Content = L("CfDlgConvertNo"),
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var dontShowBtn = new Button
        {
            Content = L("CfDlgConvertDontShow"),
            MinWidth = 80,
            Foreground = ThemeBrush("TextPrimary"),
            FontSize = 11
        };

        var dialog = new Window
        {
            Title = L("CfDlgConvertTitle"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = 500,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = msg,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MaxWidth = 440,
                        FontSize = 13
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { yesBtn, noBtn, dontShowBtn }
                    }
                }
            }
        };

        yesBtn.Click += (_, _) => { result = ConvertPromptResult.Yes; dialog.Close(); };
        noBtn.Click += (_, _) => { result = ConvertPromptResult.No; dialog.Close(); };
        dontShowBtn.Click += (_, _) => { result = ConvertPromptResult.DontShowAgain; dialog.Close(); };

        await dialog.ShowDialog(this);
        return result;
    }

    // ── Placeholder actions ────────────────────────────────────────────

    private void HighlightPlaceholderInCode(string placeholder)
    {
        var code = CodeBox.Text ?? "";
        var token = $"{{{placeholder}}}";
        var idx = code.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return;

        // Set caret first to scroll the TextBox, then apply selection
        CodeBox.Focus();
        CodeBox.CaretIndex = idx + token.Length;
        CodeBox.SelectionStart = idx;
        CodeBox.SelectionEnd = idx + token.Length;
    }

    private async System.Threading.Tasks.Task<bool> ConfirmRemovePlaceholder(string placeholder)
    {
        var confirmed = false;
        var msg = L("CfDlgRemoveConfirm").Replace("$name$", $"{{{placeholder}}}");

        var yesBtn = new Button { Content = L("CfDlgConvertYes"), MinWidth = 80 };
        var noBtn = new Button { Content = L("CfDlgConvertNo"), MinWidth = 80 };

        var dialog = new Window
        {
            Title = L("CfDlgRemoveConfirmTitle"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = 460,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = msg, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 400, FontSize = 13 },
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { yesBtn, noBtn } }
                }
            }
        };

        yesBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };
        noBtn.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
        return confirmed;
    }

    private void RemovePlaceholderFromCode(CustomFilterControl ctrl)
    {
        var code = CodeBox.Text ?? "";
        var token = $"{{{ctrl.Placeholder}}}";

        // Replace all occurrences of {placeholder} with its default value
        var replacement = string.IsNullOrEmpty(ctrl.Default) ? ctrl.Placeholder : ctrl.Default;
        var updated = code.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(code, updated, StringComparison.Ordinal))
            CodeBox.Text = updated;  // triggers SyncPlaceholders via TextChanged
    }

    // ── Placeholder detection & control config ──────────────────────────

    private void SyncPlaceholders()
    {
        var code = CodeBox.Text ?? "";
        var detected = PlaceholderRegex.Matches(code)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Remove controls for placeholders no longer in code
        _controls.RemoveAll(c => !detected.Contains(c.Placeholder, StringComparer.OrdinalIgnoreCase));

        // Add new controls for newly detected placeholders
        foreach (var ph in detected)
        {
            if (!_controls.Any(c => c.Placeholder.Equals(ph, StringComparison.OrdinalIgnoreCase)))
            {
                _controls.Add(new CustomFilterControl { Placeholder = ph, Default = "0" });
            }
        }

        // Reorder to match detection order
        var ordered = new List<CustomFilterControl>();
        foreach (var ph in detected)
        {
            var ctrl = _controls.First(c => c.Placeholder.Equals(ph, StringComparison.OrdinalIgnoreCase));
            ordered.Add(ctrl);
        }
        _controls.Clear();
        _controls.AddRange(ordered);

        RebuildControlsPanel();
    }

    private void RebuildControlsPanel()
    {
        ControlsPanel.Children.Clear();

        if (_controls.Count == 0)
        {
            ControlsPanel.Children.Add(new TextBlock
            {
                Text = L("CfDlgNoPlaceholder"),
                FontSize = 10,
                Foreground = ThemeBrush("TextPrimary"),
                FontStyle = FontStyle.Italic
            });
            return;
        }

        foreach (var ctrl in _controls)
            ControlsPanel.Children.Add(BuildControlRow(ctrl));
    }

    private Border BuildControlRow(CustomFilterControl ctrl)
    {
        var border = new Border
        {
            Background = ThemeBrush("BgInput"),
            BorderBrush = ThemeBrush("BorderSubtle"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(3)
        };

        // Single-line layout: eye | X | {name} | type | type-specific fields
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        // Eye button — highlight placeholder in code
        var eyeBtn = new Button
        {
            Content = "\uD83D\uDC41",   // 👁
            FontSize = 16,
            Width = 30, Height = 30,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = ThemeBrush("TextPrimary"),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(eyeBtn, L("CfDlgShowInCode"));
        eyeBtn.Click += (_, _) =>
        {
            // Check if this eye is already active (green) — toggle off
            var isActive = eyeBtn.Background is SolidColorBrush sb
                           && sb.Color == Color.Parse("#2E8B3D");

            // Reset all eye buttons to default style
            foreach (var child in ControlsPanel.Children)
            {
                if (child is Border b && b.Child is StackPanel sp)
                    foreach (var c in sp.Children)
                        if (c is Button btn && btn.Content is string s && s == "\uD83D\uDC41")
                        {
                            btn.Background = Brushes.Transparent;
                            btn.Foreground = ThemeBrush("TextPrimary");
                        }
            }

            if (isActive)
            {
                // Deactivate: clear selection in code
                CodeBox.SelectionStart = CodeBox.SelectionEnd = CodeBox.CaretIndex;
            }
            else
            {
                // Activate: highlight in green and select in code
                eyeBtn.Background = new SolidColorBrush(Color.Parse("#2E8B3D"));
                eyeBtn.Foreground = Brushes.White;
                HighlightPlaceholderInCode(ctrl.Placeholder);
            }
        };
        row.Children.Add(eyeBtn);

        // Red X button — remove placeholder, restore default value in code
        var removeBtn = new Button
        {
            Content = "\u2715",
            FontSize = 15,
            Width = 30, Height = 30,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#C05050")),  // red always
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(removeBtn, L("CfDlgRemoveControl"));
        removeBtn.Click += async (_, _) =>
        {
            if (await ConfirmRemovePlaceholder(ctrl.Placeholder))
                RemovePlaceholderFromCode(ctrl);
        };
        row.Children.Add(removeBtn);

        // {placeholder} label
        row.Children.Add(new TextBlock
        {
            Text = $"{{{ctrl.Placeholder}}}",
            FontFamily = UiConstants.CodeFont,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrush("AccentBlue"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 120
        });

        // Type selector
        var typeCombo = new ComboBox
        {
            Width = 95,
            FontSize = 10,
            Background = ThemeBrush("BgPanel"),
            Foreground = ThemeBrush("TextSecondary"),
            BorderBrush = ThemeBrush("BorderSubtle"),
            Items = { "slider", "combo", "checkbox", "text" },
            VerticalAlignment = VerticalAlignment.Center
        };
        typeCombo.SelectedItem = ctrl.Type;
        typeCombo.SelectionChanged += (_, _) =>
        {
            ctrl.Type = typeCombo.SelectedItem as string ?? "text";
            RebuildControlsPanel();
        };
        row.Children.Add(typeCombo);

        // Type-specific inline fields
        switch (ctrl.Type)
        {
            case "slider":
                AddInlineSliderFields(row, ctrl);
                break;
            case "combo":
                AddInlineComboFields(row, ctrl);
                break;
            case "checkbox":
                AddInlineCheckboxFields(row, ctrl);
                break;
            default:
                AddInlineTextField(row, ctrl);
                break;
        }

        border.Child = row;
        return border;
    }

    private TextBlock MakeFieldLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = ThemeBrush("TextSecondary"),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 2, 0)
    };

    private TextBox MakeFieldBox(string text, double width) => new()
    {
        Text = text,
        FontSize = 11,
        Width = width,
        Padding = new Thickness(4, 2),
        Background = ThemeBrush("BgPanel"),
        Foreground = ThemeBrush("TextSecondary"),
        BorderBrush = ThemeBrush("BorderSubtle"),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private void AddInlineSliderFields(StackPanel row, CustomFilterControl ctrl)
    {
        row.Children.Add(MakeFieldLabel("Min"));
        var minBox = MakeFieldBox(ctrl.Min.ToString(CultureInfo.InvariantCulture), 48);
        minBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(minBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) ctrl.Min = d;
        };
        row.Children.Add(minBox);

        row.Children.Add(MakeFieldLabel("Max"));
        var maxBox = MakeFieldBox(ctrl.Max.ToString(CultureInfo.InvariantCulture), 48);
        maxBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(maxBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) ctrl.Max = d;
        };
        row.Children.Add(maxBox);

        row.Children.Add(MakeFieldLabel("Step"));
        var stepBox = MakeFieldBox(ctrl.Step.ToString(CultureInfo.InvariantCulture), 42);
        stepBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(stepBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) ctrl.Step = d;
        };
        row.Children.Add(stepBox);

        row.Children.Add(MakeFieldLabel(L("CfDlgDefault")));
        var defBox = MakeFieldBox(ctrl.Default, 48);
        defBox.LostFocus += (_, _) => ctrl.Default = defBox.Text ?? "";
        row.Children.Add(defBox);

        // "½" toggle — when active, value is halved in preview_half mode
        var halfBtn = new Button
        {
            Content = "\u00BD",
            FontSize = 11,
            Width = 26, Height = 22,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Background = ctrl.ScaleWithPreview ? ThemeBrush("AccentGreen") : ThemeBrush("BgPanel"),
            Foreground = ctrl.ScaleWithPreview ? Brushes.White : ThemeBrush("TextSecondary"),
            BorderBrush = ThemeBrush("BorderSubtle"),
            BorderThickness = new Thickness(1),
            Tag = ctrl.ScaleWithPreview
        };
        ToolTip.SetTip(halfBtn, L("CfDlgScaleHalf"));
        halfBtn.Click += (_, _) =>
        {
            ctrl.ScaleWithPreview = !ctrl.ScaleWithPreview;
            halfBtn.Tag = ctrl.ScaleWithPreview;
            halfBtn.Background = ctrl.ScaleWithPreview ? ThemeBrush("AccentGreen") : ThemeBrush("BgPanel");
            halfBtn.Foreground = ctrl.ScaleWithPreview ? Brushes.White : ThemeBrush("TextSecondary");
        };
        row.Children.Add(halfBtn);
    }

    private void AddInlineComboFields(StackPanel row, CustomFilterControl ctrl)
    {
        row.Children.Add(MakeFieldLabel(L("CfDlgValues")));
        var optBox = MakeFieldBox(string.Join(", ", ctrl.Options), 200);
        optBox.Watermark = "val1, val2, val3";
        optBox.LostFocus += (_, _) =>
        {
            ctrl.Options = (optBox.Text ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (ctrl.Options.Count > 0 && string.IsNullOrEmpty(ctrl.Default))
                ctrl.Default = ctrl.Options[0];
        };
        row.Children.Add(optBox);

        row.Children.Add(MakeFieldLabel(L("CfDlgDefault")));
        var defBox = MakeFieldBox(ctrl.Default, 80);
        defBox.LostFocus += (_, _) => ctrl.Default = defBox.Text ?? "";
        row.Children.Add(defBox);
    }

    private void AddInlineCheckboxFields(StackPanel row, CustomFilterControl ctrl)
    {
        row.Children.Add(MakeFieldLabel(L("CfDlgOn")));
        var onBox = MakeFieldBox(ctrl.OnValue, 60);
        onBox.LostFocus += (_, _) => ctrl.OnValue = onBox.Text ?? "";
        row.Children.Add(onBox);

        row.Children.Add(MakeFieldLabel(L("CfDlgOff")));
        var offBox = MakeFieldBox(ctrl.OffValue, 60);
        offBox.LostFocus += (_, _) => ctrl.OffValue = offBox.Text ?? "";
        row.Children.Add(offBox);

        row.Children.Add(MakeFieldLabel(L("CfDlgDefault")));
        var defBox = MakeFieldBox(ctrl.Default, 60);
        defBox.LostFocus += (_, _) => ctrl.Default = defBox.Text ?? "";
        row.Children.Add(defBox);
    }

    private void AddInlineTextField(StackPanel row, CustomFilterControl ctrl)
    {
        row.Children.Add(MakeFieldLabel(L("CfDlgDefault")));
        var defBox = MakeFieldBox(ctrl.Default, 150);
        defBox.LostFocus += (_, _) => ctrl.Default = defBox.Text ?? "";
        row.Children.Add(defBox);
    }

    // ── DLL list ────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task OnAddDll()
    {
        var picker = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L("CfDlgPickDll"),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("DLL") { Patterns = ["*.dll"] }]
        });

        foreach (var file in picker)
            _dlls.Add(file.Path.LocalPath);

        if (picker.Count > 0)
            RebuildDllList();
    }

    private void RebuildDllList() => RebuildFileList(_dlls, DllListPanel, idx =>
    {
        _dlls.RemoveAt(idx);
        RebuildDllList();
    });

    private async System.Threading.Tasks.Task OnAddScript()
    {
        var picker = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L("CfDlgPickScript"),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("AviSynth Script") { Patterns = ["*.avsi", "*.avs"] }]
        });

        if (picker.Count == 0) return;

        // Copy selected scripts to Plugins/Scripts/ and store the filename only
        var scriptsDir = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? ".",
            "Plugins", "Scripts");
        Directory.CreateDirectory(scriptsDir);

        foreach (var file in picker)
        {
            var srcPath = file.Path.LocalPath;
            var fileName = Path.GetFileName(srcPath);
            var destPath = Path.Combine(scriptsDir, fileName);

            // Copy if not already there or if source is newer
            if (!File.Exists(destPath)
             || File.GetLastWriteTimeUtc(srcPath) > File.GetLastWriteTimeUtc(destPath))
                File.Copy(srcPath, destPath, overwrite: true);

            // Store just the filename (resolved at script generation time)
            if (!_scripts.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                _scripts.Add(fileName);
        }
        RebuildScriptList();
    }

    private void RebuildScriptList() => RebuildFileList(_scripts, ScriptListPanel, idx =>
    {
        _scripts.RemoveAt(idx);
        RebuildScriptList();
    });

    /// <summary>Shared implementation for RebuildDllList / RebuildScriptList.</summary>
    private void RebuildFileList(List<string> items, StackPanel panel, Action<int> onRemove)
    {
        panel.Children.Clear();

        for (var i = 0; i < items.Count; i++)
        {
            var idx = i;
            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,6,28"),
            };

            var pathBox = new TextBox
            {
                Text = items[idx],
                FontSize = 11,
                IsReadOnly = true,
                Background = ThemeBrush("BgInput"),
                Foreground = ThemeBrush("TextSecondary"),
                BorderBrush = ThemeBrush("BorderSubtle")
            };
            Grid.SetColumn(pathBox, 0);
            row.Children.Add(pathBox);

            var delBtn = new Button
            {
                Content = "\u2715",
                FontSize = 10,
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Foreground = ThemeBrush("TextPrimary"),
                BorderThickness = new Thickness(0)
            };
            delBtn.Click += (_, _) => onRemove(idx);
            Grid.SetColumn(delBtn, 2);
            row.Children.Add(delBtn);

            panel.Children.Add(row);
        }
    }

    /// <summary>Pre-fill the code box. Call after construction, before ShowDialog.</summary>
    public void SetCode(string code) => CodeBox.Text = code;

    private static CustomFilterControl CloneControl(CustomFilterControl src) => new()
    {
        Placeholder = src.Placeholder,
        Type = src.Type,
        Default = src.Default,
        Min = src.Min,
        Max = src.Max,
        Step = src.Step,
        Options = [..src.Options],
        OnValue = src.OnValue,
        OffValue = src.OffValue,
        ScaleWithPreview = src.ScaleWithPreview
    };
}
