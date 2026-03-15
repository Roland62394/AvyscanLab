using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CleanScan.Models;
using CleanScan.Services;
using CleanScan.ViewModels;

namespace CleanScan.Views;

/// <summary>Contract the encode controller needs from the host window.</summary>
public interface IEncodeHost
{
    T? FindControl<T>(string name) where T : Control;
    Window Window { get; }
    SolidColorBrush ThemeBrush(string key);
    string GetUiText(string key);
    MainWindowViewModel ViewModel { get; }
    ConfigStore Config { get; }
    IScriptService ScriptService { get; }
    SourceService SourceService { get; }
    IDialogService DialogService { get; }
    PresetService EncodingPresetService { get; }
    PresetService GammacPresetService { get; }
    CustomFilterService CustomFilterService { get; }
    ClipManager ClipManager { get; }
    MpvService? MpvService { get; }
    ThemeService ThemeService { get; }
    IStorageProvider StorageProvider { get; }

    bool RecordOpen { get; set; }
    bool IsEncoding { get; set; }
    bool IsInitializing { get; }
    bool IsClosing { get; }

    void RegenerateScript(bool showValidationError = true);
    Task LoadScriptAsync(bool resetPosition = false);
    void RestoreClipConfig(int index);
    Regex PreviewTrueRegex();
    Regex PreviewHalfTrueRegex();
    void MoveSliderToPointer(Slider slider, PointerEventArgs e);
}

/// <summary>
/// Manages all encoding/recording-related logic extracted from MainWindow.
/// </summary>
public sealed class EncodeController
{
    private readonly IEncodeHost _host;

    private const string DefaultEncodingPresetName = "Default";
    private const int TrialMaxSeconds = 30;

    // Encoding preset auto-save state
    private bool _autoSaveEncodingPreset;
    private bool _isLoadingEncodingPreset;
    private bool _pendingEncodingPresetPrompt;

    // GamMac preset state
    private bool _isLoadingGammacPreset;

    // Encoding process state
    private Process? _encodingProcess;
    private CancellationTokenSource? _encodingCts;
    private string _lastStderrLine = string.Empty;
    private readonly List<string> _stderrLines = [];

    // Bitrate/chroma sync guard
    private bool _syncingBitrateChroma;

#pragma warning disable IDE0028
    private readonly HashSet<string> _usedOutputPaths = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028

    // Convenience accessors
    private ConfigStore Config => _host.Config;
    private List<ClipState> Clips => _host.ClipManager.Clips;
    private string GetUiText(string key) => _host.GetUiText(key);
    private SolidColorBrush ThemeBrush(string key) => _host.ThemeBrush(key);

    public bool IsLoadingEncodingPreset
    {
        get => _isLoadingEncodingPreset;
        set => _isLoadingEncodingPreset = value;
    }

    public bool AutoSaveEncodingPreset
    {
        get => _autoSaveEncodingPreset;
        set => _autoSaveEncodingPreset = value;
    }

    /// <summary>Captures current encoding UI values as a dictionary (used by session save).</summary>
    public Dictionary<string, string> CaptureCurrentEncodingValues() => CaptureEncodingValues();

    /// <summary>Applies encoding values to the UI (used by session restore).</summary>
    public void ApplyCurrentEncodingValues(Dictionary<string, string> vals) => ApplyEncodingValues(vals);

    public EncodeController(IEncodeHost host)
    {
        _host = host;
    }

    // ── Record panel initialization ──────────────────────────────────

    public void InitRecordPanel()
    {
        if (_host.FindControl<TextBox>("RecordDir") is { } tb)
        {
            tb.LostFocus += (_, _) => UpdateDiskSpaceLabel(tb.Text);
            tb.TextChanged += (_, _) => UpdateDiskSpaceLabel(tb.Text);
        }

        if (_host.FindControl<ComboBox>("RecordEncoder") is { } enc)
            enc.SelectionChanged += OnRecordEncoderChanged;

        if (_host.FindControl<ComboBox>("RecordQualityMode") is { } qm)
            qm.SelectionChanged += OnRecordQualityModeChanged;

        if (_host.FindControl<ComboBox>("RecordChroma") is { } ch)
            ch.SelectionChanged += OnRecordChromaChanged;

        if (_host.FindControl<ComboBox>("RecordContainer") is { } ct)
            ct.SelectionChanged += (_, _) => OnEncodingSettingChanged();

        if (_host.FindControl<ComboBox>("RecordResize") is { } rs)
            rs.SelectionChanged += (_, _) => { UpdateBitrateHint(); OnEncodingSettingChanged(); };

        if (_host.FindControl<TextBox>("RecordBitrate") is { } brTb)
        {
            brTb.LostFocus += (_, _) => OnBitrateValidated();
            brTb.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter) OnBitrateValidated();
            };
        }

        if (_host.FindControl<Slider>("RecordCrfSlider") is { } slider)
        {
            slider.PropertyChanged += (_, args) =>
            {
                if (args.Property == Slider.ValueProperty &&
                    _host.FindControl<TextBlock>("RecordCrfValue") is { } lbl)
                    lbl.Text = ((int)slider.Value).ToString();
            };

            var crfDragging = false;
            slider.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            {
                if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
                crfDragging = true;
                e.Pointer.Capture(slider);
                _host.MoveSliderToPointer(slider, e);
                e.Handled = true;
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            slider.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
            {
                if (!crfDragging) return;
                _host.MoveSliderToPointer(slider, e);
                e.Handled = true;
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            slider.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
            {
                if (!crfDragging) return;
                crfDragging = false;
                e.Pointer.Capture(null);
                e.Handled = true;
                OnEncodingSettingChanged();
            }, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        RefreshEncodingPresetCombo();
        RefreshGammacPresetCombo();

        if (_host.FindControl<ComboBox>("RecordPresetCombo") is { } presetCombo)
        {
            presetCombo.SelectionChanged += OnRecordPresetSelectionChanged;
            if (presetCombo.SelectedItem is null)
                presetCombo.SelectedItem = DefaultEncodingPresetName;
        }
    }

    // ── Record overlay toggle ────────────────────────────────────────

    public void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        _host.RecordOpen = !_host.RecordOpen;
        if (_host.FindControl<Button>("RecordBtn") is { } btn)
        {
            btn.Background = new SolidColorBrush(Color.Parse(_host.RecordOpen ? "#C62828" : "#3B4C64"));
            btn.Foreground = Brushes.White;
        }
        if (_host.FindControl<Border>("RecordOverlay") is { } overlay)
            overlay.IsVisible = _host.RecordOpen;
        if (_host.RecordOpen)
        {
            RebuildBatchClipList();
            UpdateDiskSpaceLabel(_host.FindControl<TextBox>("RecordDir")?.Text);
        }
    }

    // ── Encoding settings changed handlers ───────────────────────────

    private void OnRecordPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var name = combo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(name)) return;

        var preset = _host.EncodingPresetService.LoadPresets()
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset?.Values is null) return;

        _isLoadingEncodingPreset = true;
        try { ApplyEncodingValues(preset.Values); }
        finally { _isLoadingEncodingPreset = false; }
    }

    private void OnRecordEncoderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("RecordEncoder") is not { } enc) return;
        var tag = (enc.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var isImageSeq = tag is "tiff" or "png" or "jpg";
        var isLossless = tag is "ffv1" or "utvideo" or "tiff" or "png";

        if (_host.FindControl<ComboBox>("RecordContainer") is { } cnt)
            cnt.IsEnabled = !isImageSeq;

        if (_host.FindControl<Grid>("RecordQualityPanel") is { } qp)
            qp.IsVisible = !isLossless;
        if (_host.FindControl<StackPanel>("RecordChromaPanel") is { } cp)
            cp.IsVisible = !isLossless;

        OnEncodingSettingChanged();
    }

    private void OnRecordQualityModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("RecordQualityMode") is not { } qm) return;
        var tag = (qm.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var isCrf = tag == "crf";
        if (_host.FindControl<StackPanel>("RecordCrfPanel") is { } crfP)
            crfP.IsVisible = isCrf;
        if (_host.FindControl<StackPanel>("RecordBitratePanel") is { } brP)
            brP.IsVisible = !isCrf;

        OnEncodingSettingChanged();
    }

    // ── Chroma / bitrate coupling ────────────────────────────────────

    /// <summary>
    /// Chroma multiplier relative to 4:2:0.
    /// 4:2:0 = 12 bits/pixel, 4:2:2 = 16 bits/pixel (x1.33), 4:4:4 = 24 bits/pixel (x2.0).
    /// </summary>
    private static double ChromaBitrateMultiplier(string chroma) => chroma switch
    {
        "yuv422p" => 16.0 / 12.0,
        "yuv444p" => 24.0 / 12.0,
        _         => 1.0
    };

    /// <summary>
    /// Computes a recommended minimum bitrate (Mb/s) based on resolution and chroma.
    /// </summary>
    private int ComputeMinBitrate()
    {
        var chroma = (_host.FindControl<ComboBox>("RecordChroma")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "yuv420p";
        var resize = (_host.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original";

        double refPixels = 2_073_600;
        double refBitrate = 15.0;

        double pixels = resize switch
        {
            "1080" => 1920.0 * 1080,
            "720"  => 1280.0 * 720,
            "576"  => 1024.0 * 576,
            "480"  => 854.0 * 480,
            _      => 1920.0 * 1080
        };

        var bitrate = refBitrate * (pixels / refPixels) * ChromaBitrateMultiplier(chroma);
        return Math.Max(1, (int)Math.Ceiling(bitrate));
    }

    private void UpdateBitrateHint()
    {
        if (_syncingBitrateChroma) return;
        if (_host.FindControl<TextBox>("RecordBitrate") is not { } tb) return;
        var min = ComputeMinBitrate();
        tb.Watermark = $"{min}";
    }

    private void OnRecordChromaChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingBitrateChroma) return;
        _syncingBitrateChroma = true;
        try
        {
            UpdateBitrateHint();
            if (_host.FindControl<TextBox>("RecordBitrate") is { } tb
                && int.TryParse(tb.Text?.Trim(), out var current))
            {
                var min = ComputeMinBitrate();
                if (current < min) tb.Text = min.ToString();
            }
        }
        finally { _syncingBitrateChroma = false; }

        OnEncodingSettingChanged();
    }

    private void OnBitrateValidated()
    {
        if (_syncingBitrateChroma) return;
        _syncingBitrateChroma = true;
        try
        {
            if (_host.FindControl<TextBox>("RecordBitrate") is not { } tb) return;
            if (_host.FindControl<ComboBox>("RecordChroma") is not { } combo) return;
            if (!int.TryParse(tb.Text?.Trim(), out var bitrate) || bitrate <= 0) return;

            var resize = (_host.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original";
            double pixels = resize switch
            {
                "1080" => 1920.0 * 1080,
                "720"  => 1280.0 * 720,
                "576"  => 1024.0 * 576,
                "480"  => 854.0 * 480,
                _      => 1920.0 * 1080
            };
            double refPixels = 2_073_600;
            double refBitrate = 15.0;

            string[] chromaOptions = ["yuv444p", "yuv422p", "yuv420p"];
            string bestChroma = "yuv420p";
            foreach (var ch in chromaOptions)
            {
                var minBr = (int)Math.Ceiling(refBitrate * (pixels / refPixels) * ChromaBitrateMultiplier(ch));
                if (bitrate >= minBr) { bestChroma = ch; break; }
            }

            var currentChroma = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (currentChroma == bestChroma) return;

            foreach (var item in combo.Items)
                if (item is ComboBoxItem ci && ci.Tag?.ToString() == bestChroma)
                {
                    combo.SelectedItem = ci;
                    break;
                }
        }
        finally { _syncingBitrateChroma = false; }

        OnEncodingSettingChanged();
    }

    // ── Encoding presets ─────────────────────────────────────────────

    private static readonly string[] EncodingPresetKeys =
        ["encoder", "container", "quality_mode", "crf", "bitrate", "chroma", "resize", "output_dir"];

    private Dictionary<string, string> CaptureEncodingValues()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["encoder"]      = (_host.FindControl<ComboBox>("RecordEncoder")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "x264",
            ["container"]    = (_host.FindControl<ComboBox>("RecordContainer")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mkv",
            ["quality_mode"] = (_host.FindControl<ComboBox>("RecordQualityMode")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "crf",
            ["crf"]          = ((int)(_host.FindControl<Slider>("RecordCrfSlider")?.Value ?? 18)).ToString(),
            ["bitrate"]      = _host.FindControl<TextBox>("RecordBitrate")?.Text?.Trim() ?? "20",
            ["chroma"]       = (_host.FindControl<ComboBox>("RecordChroma")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "yuv420p",
            ["resize"]       = (_host.FindControl<ComboBox>("RecordResize")?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "original",
            ["output_dir"]   = _host.FindControl<TextBox>("RecordDir")?.Text?.Trim() ?? "",
        };
    }

    private void ApplyEncodingValues(Dictionary<string, string> vals)
    {
        void SelectByTag(string controlName, string? tag)
        {
            if (_host.FindControl<ComboBox>(controlName) is not { } combo || tag is null) return;
            foreach (var item in combo.Items)
                if (item is ComboBoxItem ci && ci.Tag?.ToString() == tag) { combo.SelectedItem = ci; break; }
        }

        if (vals.TryGetValue("encoder", out var enc))      SelectByTag("RecordEncoder", enc);
        if (vals.TryGetValue("container", out var cnt))    SelectByTag("RecordContainer", cnt);
        if (vals.TryGetValue("quality_mode", out var qm))  SelectByTag("RecordQualityMode", qm);
        if (vals.TryGetValue("crf", out var crf) && int.TryParse(crf, out var crfVal))
        {
            if (_host.FindControl<Slider>("RecordCrfSlider") is { } slider) slider.Value = crfVal;
        }
        if (vals.TryGetValue("bitrate", out var br))
        {
            if (_host.FindControl<TextBox>("RecordBitrate") is { } tb) tb.Text = br;
        }
        if (vals.TryGetValue("chroma", out var ch))        SelectByTag("RecordChroma", ch);
        if (vals.TryGetValue("resize", out var rs))        SelectByTag("RecordResize", rs);
        if (vals.TryGetValue("output_dir", out var dir) && !string.IsNullOrWhiteSpace(dir))
        {
            if (_host.FindControl<TextBox>("RecordDir") is { } dirTb) dirTb.Text = dir;
        }
    }

    public void RefreshEncodingPresetCombo()
    {
        if (_host.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
        EnsureDefaultEncodingPreset();
        var list = _host.EncodingPresetService.LoadPresets()
            .OrderBy(p => string.Equals(p.Name, DefaultEncodingPresetName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Name)
            .ToList();
        combo.ItemsSource = list;
    }

    private void EnsureDefaultEncodingPreset()
    {
        var presets = _host.EncodingPresetService.LoadPresets();
        if (!presets.Any(p => string.Equals(p.Name, DefaultEncodingPresetName, StringComparison.OrdinalIgnoreCase)))
        {
            presets.Insert(0, new Preset(DefaultEncodingPresetName, CaptureEncodingValues()));
            _host.EncodingPresetService.SavePresets(presets);
        }
    }

    public void OnRecordPresetSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
        var name = (combo.Text ?? combo.SelectedItem?.ToString())?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = _host.EncodingPresetService.LoadPresets();
        var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            existing.Values = CaptureEncodingValues();
        else
            presets.Add(new Preset(name, CaptureEncodingValues()));

        _host.EncodingPresetService.SavePresets(presets);

        _isLoadingEncodingPreset = true;
        try
        {
            RefreshEncodingPresetCombo();
            combo.SelectedItem = name;
        }
        finally { _isLoadingEncodingPreset = false; }

        if (_host.RecordOpen) RebuildBatchClipList();
    }

    public void OnRecordPresetLoadClick(object? sender, RoutedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
        var name = combo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return;

        var preset = _host.EncodingPresetService.LoadPresets()
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null) return;
        _isLoadingEncodingPreset = true;
        try { ApplyEncodingValues(preset.Values); }
        finally { _isLoadingEncodingPreset = false; }
    }

    public void OnRecordPresetDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
        var name = combo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (string.Equals(name, DefaultEncodingPresetName, StringComparison.OrdinalIgnoreCase)) return;

        var presets = _host.EncodingPresetService.LoadPresets();
        presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        _host.EncodingPresetService.SavePresets(presets);

        _isLoadingEncodingPreset = true;
        try
        {
            RefreshEncodingPresetCombo();
            combo.SelectedItem = null;
            combo.Text = string.Empty;
        }
        finally { _isLoadingEncodingPreset = false; }

        for (int i = 0; i < Clips.Count; i++)
        {
            if (string.Equals(Clips[i].BatchEncodingPreset, name, StringComparison.OrdinalIgnoreCase))
                Clips[i].BatchEncodingPreset = null;
        }
        if (_host.RecordOpen) RebuildBatchClipList();
    }

    private async void OnEncodingSettingChanged()
    {
        if (_isLoadingEncodingPreset || _host.IsInitializing || _host.IsClosing) return;
        if (_pendingEncodingPresetPrompt) return;
        if (_host.FindControl<ComboBox>("RecordPresetCombo") is not { } combo) return;
        var presetName = combo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(presetName)) return;

        if (_autoSaveEncodingPreset)
        {
            SaveEncodingPresetByName(presetName);
            return;
        }

        _pendingEncodingPresetPrompt = true;
        try
        {
            var result = await ShowEncodingPresetModifiedDialog(presetName);
            if (result == true)
            {
                SaveEncodingPresetByName(presetName);
            }
            else if (result == false)
            {
                combo.SelectedItem = null;
            }
        }
        finally { _pendingEncodingPresetPrompt = false; }
    }

    private void SaveEncodingPresetByName(string name)
    {
        var presets = _host.EncodingPresetService.LoadPresets();
        var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            existing.Values = CaptureEncodingValues();
        else
            presets.Add(new Preset(name, CaptureEncodingValues()));
        _host.EncodingPresetService.SavePresets(presets);
    }

    private async Task<bool?> ShowEncodingPresetModifiedDialog(string presetName)
    {
        bool? dialogResult = null;

        var checkBox = new CheckBox
        {
            Content = GetUiText("PresetModifiedDontAsk"),
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            FontSize = 11
        };

        var yesButton = new Button
        {
            Content = GetUiText("YesButton"),
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        var noButton = new Button
        {
            Content = GetUiText("NoButton"),
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = GetUiText("PresetModifiedTitle"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.Format(GetUiText("PresetModifiedMsg"), presetName),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400
                    },
                    checkBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { yesButton, noButton }
                    }
                }
            }
        };

        yesButton.Click += (_, _) =>
        {
            dialogResult = true;
            if (checkBox.IsChecked == true)
                _autoSaveEncodingPreset = true;
            dialog.Close();
        };
        noButton.Click += (_, _) =>
        {
            dialogResult = false;
            dialog.Close();
        };

        await dialog.ShowDialog(_host.Window);
        return dialogResult;
    }

    // ── GamMac presets ───────────────────────────────────────────────

    public static readonly string[] GammacKeys =
        ["LockChan", "LockVal", "Scale", "Th", "HiTh", "X", "Y", "W", "H", "Omin", "Omax", "Verbosity", "ShowPreview"];

    private Dictionary<string, string> CaptureGammacValues()
    {
        var vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in GammacKeys)
            vals[key] = Config.Get(key) ?? "";
        return vals;
    }

    private void ApplyGammacValues(Dictionary<string, string> vals)
    {
        _isLoadingGammacPreset = true;
        try
        {
            foreach (var (key, val) in vals)
                if (!string.IsNullOrEmpty(val))
                    Config.Set(key, val);
        }
        finally { _isLoadingGammacPreset = false; }
    }

    public void RefreshGammacPresetCombo()
    {
        if (_host.FindControl<ComboBox>("GammacPresetCombo") is not { } combo) return;
        var list = _host.GammacPresetService.LoadPresets()
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Name)
            .ToList();
        combo.ItemsSource = list;
    }

    public void OnGammacPresetSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("GammacPresetCombo") is not { } combo) return;
        var name = (combo.Text ?? combo.SelectedItem?.ToString())?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = _host.GammacPresetService.LoadPresets();
        var existing = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            existing.Values = CaptureGammacValues();
        else
            presets.Add(new Preset(name, CaptureGammacValues()));

        _host.GammacPresetService.SavePresets(presets);
        _isLoadingGammacPreset = true;
        try
        {
            RefreshGammacPresetCombo();
            combo.SelectedItem = name;
            Config.Set("gammac_preset", name);
        }
        finally { _isLoadingGammacPreset = false; }
    }

    /// <summary>Restores the GammacPresetCombo selection without triggering value re-application.</summary>
    public void RestoreGammacPresetSelection(string? name)
    {
        if (_host.FindControl<ComboBox>("GammacPresetCombo") is not { } combo) return;
        RefreshGammacPresetCombo();
        _isLoadingGammacPreset = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(name))
                combo.SelectedItem = name;
            else
            {
                combo.SelectedIndex = -1;
                combo.Text = null;
            }
        }
        finally { _isLoadingGammacPreset = false; }
    }

    public void OnGammacPresetDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_host.FindControl<ComboBox>("GammacPresetCombo") is not { } combo) return;
        var name = combo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(name)) return;

        var presets = _host.GammacPresetService.LoadPresets();
        presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        _host.GammacPresetService.SavePresets(presets);
        RefreshGammacPresetCombo();
        combo.SelectedItem = null;
        Config.Set("gammac_preset", string.Empty);
    }

    public void OnGammacPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingGammacPreset || _host.IsInitializing) return;
        if (sender is ComboBox { SelectedItem: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var presets = _host.GammacPresetService.LoadPresets();
            var preset = presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset?.Values is not null)
            {
                ApplyGammacValues(preset.Values);
                Config.Set("gammac_preset", name);
                _host.RegenerateScript(showValidationError: false);
                _ = _host.LoadScriptAsync();
            }
        }
    }

    // ── Batch clip list ──────────────────────────────────────────────

    public void RebuildBatchClipList()
    {
        if (_host.FindControl<StackPanel>("BatchClipList") is not { } panel) return;
        panel.Children.Clear();

        var monoFont = UiConstants.MonoFont;
        EnsureDefaultEncodingPreset();
        var presetNames = _host.EncodingPresetService.LoadPresets()
            .OrderBy(p => string.Equals(p.Name, DefaultEncodingPresetName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Name)
            .ToList();

        for (int i = 0; i < Clips.Count; i++)
        {
            var index = i;
            var filename = Path.GetFileName(Clips[i].Path);
            if (string.IsNullOrWhiteSpace(filename)) filename = Clips[i].Path;

            var cb = new CheckBox
            {
                IsChecked = Clips[i].BatchSelected,
                MinWidth = 0,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            cb.Click += (_, _) =>
            {
                if (index < Clips.Count)
                    Clips[index].BatchSelected = cb.IsChecked == true;
            };

            var nameLabel = new TextBlock
            {
                Text = filename,
                FontSize = 12,
                FontFamily = monoFont,
                Foreground = new SolidColorBrush(Color.Parse("#C8D0E0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTip.SetTip(nameLabel, Clips[i].Path);

            var renameBox = new TextBox
            {
                Text = Clips[i].OutputName ?? "",
                FontSize = 12,
                FontFamily = monoFont,
                Height = 28,
                Padding = new Thickness(4, 2),
                Foreground = new SolidColorBrush(Color.Parse("#C8D0E0")),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            renameBox.LostFocus += (_, _) =>
            {
                if (index < Clips.Count)
                {
                    var text = renameBox.Text?.Trim();
                    Clips[index].OutputName = string.IsNullOrEmpty(text) ? null : text;
                }
            };

            var clipPreset = Clips[i].BatchEncodingPreset;
            var effectivePreset = clipPreset ?? DefaultEncodingPresetName;
            var selectedPresetName = effectivePreset;

            void SyncRightPanel(string? name)
            {
                if (name is null) return;
                var preset = _host.EncodingPresetService.LoadPresets()
                    .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (preset is null) return;
                _isLoadingEncodingPreset = true;
                try
                {
                    ApplyEncodingValues(preset.Values);
                    if (_host.FindControl<ComboBox>("RecordPresetCombo") is { } rpc)
                        rpc.SelectedItem = name;
                }
                finally { _isLoadingEncodingPreset = false; }
            }

            var presetLabel = new TextBlock
            {
                Text = selectedPresetName,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#C8D0E0")),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var presetLabelZone = new Border
            {
                Child = presetLabel,
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Padding = new Thickness(4, 0, 4, 0),
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderBrush = new SolidColorBrush(Color.Parse("#3C4558")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3, 0, 0, 3),
            };
            presetLabelZone.PointerPressed += (_, _) => SyncRightPanel(presetLabel.Text);

            var dropBtn = new Button
            {
                Content = "\u25be",
                FontSize = 14,
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.Parse("#3C4558")),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(0, 3, 3, 0),
            };
            var flyoutPanel = new StackPanel { Spacing = 0 };
            var flyout = new Flyout
            {
                Content = flyoutPanel,
                Placement = PlacementMode.BottomEdgeAlignedRight,
            };
            foreach (var pName in presetNames)
            {
                var itemBtn = new Button
                {
                    Content = pName,
                    FontSize = 12,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 4),
                    Foreground = new SolidColorBrush(Color.Parse("#C8D0E0")),
                };
                var capturedName = pName;
                itemBtn.Click += (_, _) =>
                {
                    presetLabel.Text = capturedName;
                    if (index < Clips.Count)
                        Clips[index].BatchEncodingPreset = capturedName;
                    SyncRightPanel(capturedName);
                    flyout.Hide();
                };
                flyoutPanel.Children.Add(itemBtn);
            }
            dropBtn.Flyout = flyout;

            var presetCell = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
            };
            presetCell.Children.Add(presetLabelZone);
            Grid.SetColumn(presetLabelZone, 0);
            presetCell.Children.Add(dropBtn);
            Grid.SetColumn(dropBtn, 1);

            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("24,2*,8,2*,8,1.5*"),
            };
            row.Children.Add(cb);
            Grid.SetColumn(cb, 0);
            row.Children.Add(nameLabel);
            Grid.SetColumn(nameLabel, 1);
            row.Children.Add(renameBox);
            Grid.SetColumn(renameBox, 3);
            row.Children.Add(presetCell);
            Grid.SetColumn(presetCell, 5);

            panel.Children.Add(row);
        }

        // Update localized labels
        if (_host.FindControl<TextBlock>("BatchClipListLabel") is { } lbl)
            lbl.Text = GetUiText("BatchClipListLabel");
        if (_host.FindControl<CheckBox>("BatchSelectAllCheck") is { } allCb)
            allCb.Content = GetUiText("BatchSelectAll");
        if (_host.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
            shutCb.Content = GetUiText("ShutdownCheckBox");
        if (_host.FindControl<TextBlock>("BatchColOriginal") is { } colOrig)
            colOrig.Text = GetUiText("BatchColOriginal");
        if (_host.FindControl<TextBlock>("BatchColRenamed") is { } colRenamed)
            colRenamed.Text = GetUiText("BatchColRenamed");
        if (_host.FindControl<TextBlock>("BatchColPreset") is { } colPreset)
            colPreset.Text = GetUiText("BatchColPreset");
    }

    public void OnBatchSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox allCb) return;
        var selected = allCb.IsChecked == true;
        for (int i = 0; i < Clips.Count; i++)
            Clips[i].BatchSelected = selected;
        RebuildBatchClipList();
        if (_host.FindControl<CheckBox>("BatchSelectAllCheck") is { } cb)
            cb.IsChecked = selected;
    }

    // ── Output directory ─────────────────────────────────────────────

    public async void OnRecordDirPickClick(object? sender, RoutedEventArgs e)
    {
        var folder = await _host.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = GetUiText("RecordDirPickTitle"),
                AllowMultiple = false
            });
        if (folder.Count > 0 && _host.FindControl<TextBox>("RecordDir") is { } tb)
        {
            try
            {
                var picked = folder[0].Path.LocalPath;
                if (picked.Length == 2 && picked[1] == ':')
                    picked += "\\";
                tb.Text = picked;
                UpdateDiskSpaceLabel(tb.Text);
            }
            catch
            {
                if (folder[0].TryGetLocalPath() is { } fallback)
                {
                    if (fallback.Length == 2 && fallback[1] == ':')
                        fallback += "\\";
                    tb.Text = fallback;
                    UpdateDiskSpaceLabel(tb.Text);
                }
            }
        }
    }

    public void OnRecordDirOpenClick(object? sender, RoutedEventArgs e)
    {
        var dir = _host.FindControl<TextBox>("RecordDir")?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    public void UpdateDiskSpaceLabel(string? dirPath)
    {
        if (_host.FindControl<TextBlock>("RecordDiskSpace") is not { } lbl) return;
        if (string.IsNullOrWhiteSpace(dirPath))
        {
            lbl.Text = "";
            return;
        }
        try
        {
            var root = Path.GetPathRoot(dirPath);
            if (string.IsNullOrWhiteSpace(root)) { lbl.Text = ""; return; }
            var drive = new DriveInfo(root);
            if (!drive.IsReady) { lbl.Text = ""; return; }
            var freeMb = drive.AvailableFreeSpace / (1024.0 * 1024.0);
            lbl.Text = freeMb >= 1024
                ? $"({freeMb / 1024.0:F1} Go)"
                : $"({freeMb:F0} Mo)";
        }
        catch { lbl.Text = ""; }
    }

    // ── Output name helpers ──────────────────────────────────────────

    private static string GetSafeOutputName(string clipPath)
    {
        var name = Path.GetFileNameWithoutExtension(clipPath);
        if (name.Contains('%'))
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(clipPath));
            if (!string.IsNullOrEmpty(parent))
                name = parent;
            else
                name = name.Replace("%", "_");
        }
        return name;
    }

    private static string? ResolveFirstImageFile(string sequencePattern)
    {
        var dir = Path.GetDirectoryName(sequencePattern);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
        var ext = Path.GetExtension(sequencePattern);
        if (string.IsNullOrEmpty(ext)) return null;
        return Directory.EnumerateFiles(dir, $"*{ext}", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool ImageHasAlpha(string imagePath)
    {
        try
        {
            using var fs = File.OpenRead(imagePath);
            var header = new byte[8];
            if (fs.Read(header, 0, 8) < 8) return false;

            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                fs.Seek(25, SeekOrigin.Begin);
                return fs.ReadByte() is 4 or 6;
            }

            bool le = header[0] == 'I' && header[1] == 'I';
            if (!le && !(header[0] == 'M' && header[1] == 'M')) return false;

            ushort U16(byte[] b, int o) => le
                ? (ushort)(b[o] | (b[o + 1] << 8))
                : (ushort)((b[o] << 8) | b[o + 1]);
            uint U32(byte[] b, int o) => le
                ? (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24))
                : (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

            long ifdOffset = U32(header, 4);
            fs.Seek(ifdOffset, SeekOrigin.Begin);

            var countBuf = new byte[2];
            if (fs.Read(countBuf, 0, 2) < 2) return false;
            int entryCount = U16(countBuf, 0);

            var entry = new byte[12];
            for (int i = 0; i < entryCount; i++)
            {
                if (fs.Read(entry, 0, 12) < 12) return false;
                if (U16(entry, 0) == 277)
                    return U16(entry, 8) >= 4;
            }
            return false;
        }
        catch { return false; }
    }

    // ── Overwrite confirmation ───────────────────────────────────────

    private async Task<bool> AskOverwriteAsync(string displayName)
    {
        var result = false;
        var yesBtn = new Button { Content = GetUiText("OverwriteYes"), HorizontalAlignment = HorizontalAlignment.Center };
        var noBtn  = new Button { Content = GetUiText("OverwriteNo"),  HorizontalAlignment = HorizontalAlignment.Center };

        var dialog = new Window
        {
            Title = GetUiText("OverwriteConfirmTitle"),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.Format(GetUiText("OverwriteConfirm"), displayName),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { yesBtn, noBtn }
                    }
                }
            }
        };

        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click  += (_, _) => dialog.Close();
        await dialog.ShowDialog(_host.Window);
        return result;
    }

    // ── FFmpeg args building ─────────────────────────────────────────

    private (string ffArgs, string outputPath)? BuildFfmpegArgs(
        Dictionary<string, string> encVals, string renderScriptPath, string outputDir, string sourceFileName,
        bool sourceHasAlpha = false)
    {
        var encoder   = encVals.GetValueOrDefault("encoder", "x264");
        var container = encVals.GetValueOrDefault("container", "mkv");
        var qualityMode = encVals.GetValueOrDefault("quality_mode", "crf");
        var crfValue  = encVals.GetValueOrDefault("crf", "18");
        var bitrateText = encVals.GetValueOrDefault("bitrate", "20");
        var chroma    = encVals.GetValueOrDefault("chroma", "yuv420p");
        var resize    = encVals.GetValueOrDefault("resize", "original");

        var needsEvenDim = encoder is "x264" or "x265";
        var scaleFilter = resize != "original"
            ? $"-vf scale=-2:{resize}"
            : needsEvenDim
                ? "-vf pad=ceil(iw/2)*2:ceil(ih/2)*2"
                : "";
        var isImageSeq = encoder is "tiff" or "png" or "jpg";
        var durationLimit = TrialMaxSeconds > 0 ? $"-t {TrialMaxSeconds}" : "";
        var inputArgs = $"-f avisynth -i \"{renderScriptPath}\"";

        string outputPath;
        string ffArgs;

        if (isImageSeq)
        {
            var ext = encoder switch { "tiff" => "tif", "jpg" => "jpg", _ => "png" };
            var seqDir = Path.Combine(outputDir, sourceFileName);
            try { Directory.CreateDirectory(seqDir); } catch { return null; }
            outputPath = Path.Combine(seqDir, $"%05d.{ext}");
            var pixFmt = sourceHasAlpha ? "rgba" : "rgb24";
            var imgCodecArgs = encoder switch
            {
                "tiff" => $"-c:v tiff -compression_algo raw -pix_fmt {pixFmt}",
                "png"  => $"-c:v png -compression_level 0 -pix_fmt {pixFmt}",
                "jpg"  => "-c:v mjpeg -q:v 2 -pix_fmt yuvj444p",
                _      => ""
            };
            ffArgs = $"-progress pipe:2 {inputArgs} {durationLimit} {scaleFilter} {imgCodecArgs} -y \"{outputPath}\"";
        }
        else
        {
            var baseName = sourceFileName;
            outputPath = Path.Combine(outputDir, $"{baseName}.{container}");
            int dup = 2;
            while (_usedOutputPaths.Contains(outputPath))
            {
                outputPath = Path.Combine(outputDir, $"{baseName}_{dup}.{container}");
                dup++;
            }
            _usedOutputPaths.Add(outputPath);

            var qualityArgs = "";
            if (encoder is "x264" or "x265")
            {
                qualityArgs = qualityMode == "bitrate"
                    ? $"-b:v {bitrateText}M"
                    : $"-crf {crfValue}";
            }

            var codecArgs = encoder switch
            {
                "x264"    => $"-c:v libx264 {qualityArgs} -preset medium -pix_fmt {chroma}",
                "x265"    => $"-c:v libx265 {qualityArgs} -preset medium -pix_fmt {chroma}",
                "ffv1"    => "-c:v ffv1 -level 3 -slicecrc 1",
                "utvideo" => "-c:v utvideo",
                "prores"  => $"-c:v prores_ks -profile:v 3 -pix_fmt {chroma}",
                _         => $"-c:v libx264 {qualityArgs} -preset medium -pix_fmt {chroma}"
            };
            var movFlags = container is "mp4" or "mov" ? "-movflags +faststart" : "";
            ffArgs = $"-progress pipe:2 {inputArgs} {durationLimit} {scaleFilter} {codecArgs} {movFlags} -y \"{outputPath}\"";
        }

        return (ffArgs, outputPath);
    }

    // ── Script preparation for encoding ──────────────────────────────

    private string? PrepareClipForEncoding(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= Clips.Count) return null;

        var sourcePath = Clips[clipIndex].Path;
        var normalized = _host.SourceService.NormalizeConfiguredPath(sourcePath);

        Config.Set("source", normalized);
        Config.Set("film", normalized);
        Config.Set("img", normalized);

        {
            var clipCfg = Clips[clipIndex].Config;
            foreach (var kv in clipCfg)
                Config.Set(kv.Key, kv.Value);
        }

        foreach (var cropField in new[] { "Crop_L", "Crop_T", "Crop_R", "Crop_B" })
            Config.Set(cropField, "0");
        Config.Set("enable_crop", "false");

        var isFilm = _host.SourceService.IsVideoSource(sourcePath);
        Config.Set("use_img", (!isFilm).ToString().ToLowerInvariant());

        _host.ScriptService.Generate(Config.Snapshot(), _host.CustomFilterService.Filters, _host.ViewModel.CurrentLanguageCode);

        return GenerateRenderScript();
    }

    // ── Encoding execution ───────────────────────────────────────────

    public async void OnRecordStartClick(object? sender, RoutedEventArgs e)
    {
        if (_encodingProcess is { HasExited: false })
        {
            _encodingCts?.Cancel();
            try { _encodingProcess.Kill(entireProcessTree: true); } catch { }
            SetRecordStartButtonState(idle: true);
            return;
        }

        var dir = _host.FindControl<TextBox>("RecordDir")?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(dir))
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"), GetUiText("RecordNoDirError"));
            return;
        }

        var jobs = new List<int>();
        for (int i = 0; i < Clips.Count; i++)
        {
            if (Clips[i].BatchSelected)
                jobs.Add(i);
        }
        if (jobs.Count == 0)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"), GetUiText("RecordNoClipSelected"));
            return;
        }

        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"), ex.Message);
            return;
        }

        var ffmpegPath = FindFfmpeg();
        if (ffmpegPath is null)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"), GetUiText("RecordFfmpegNotFound"));
            return;
        }

        var avsSupported = await CheckFfmpegAviSynthSupport(ffmpegPath);
        if (!avsSupported)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"),
                "ffmpeg does not support AviSynth input.\nPlease use a ffmpeg build with AviSynth support (e.g. gyan.dev full build).");
            return;
        }

        var shutdownAfter = _host.FindControl<CheckBox>("ShutdownCheckBox")?.IsChecked == true;
        var defaultEncoding = CaptureEncodingValues();

        _host.ClipManager.SaveActiveConfig();
        var savedConfig = Config.Snapshot();
        var savedClipIndex = _host.ClipManager.ActiveIndex;

        SetRecordStartButtonState(idle: false);
        SetRecordProgressVisible(true);
        SetEncodingLock(true);
        _usedOutputPaths.Clear();
        _encodingCts = new CancellationTokenSource();

        int successCount = 0;
        var errors = new List<string>();

        try
        {
            for (int jobIdx = 0; jobIdx < jobs.Count; jobIdx++)
            {
                if (_encodingCts.IsCancellationRequested) break;

                var clipIndex = jobs[jobIdx];
                var customName = Clips[clipIndex].OutputName;
                var sourceFileName = !string.IsNullOrEmpty(customName)
                    ? customName
                    : GetSafeOutputName(Clips[clipIndex].Path);
                var batchLabel = string.Format(GetUiText("BatchProgress"), jobIdx + 1, jobs.Count);
                UpdateRecordProgress(0, $"{batchLabel} — {sourceFileName}");

                var clipEncoding = defaultEncoding;
                var clipPresetName = Clips[clipIndex].BatchEncodingPreset;
                {
                    var effectiveName = clipPresetName ?? DefaultEncodingPresetName;
                    var preset = _host.EncodingPresetService.LoadPresets()
                        .FirstOrDefault(p => string.Equals(p.Name, effectiveName, StringComparison.OrdinalIgnoreCase));
                    if (preset is not null)
                        clipEncoding = new Dictionary<string, string>(preset.Values, StringComparer.OrdinalIgnoreCase);
                }

                var clipDir = clipEncoding.TryGetValue("output_dir", out var presetDir) && !string.IsNullOrWhiteSpace(presetDir)
                    ? presetDir : dir;
                try { if (!Directory.Exists(clipDir)) Directory.CreateDirectory(clipDir); } catch { }

                var renderScriptPath = PrepareClipForEncoding(clipIndex);
                if (renderScriptPath is null || !File.Exists(renderScriptPath))
                {
                    DebugLog($"Render script missing for clip {clipIndex}: {renderScriptPath}");
                    errors.Add($"{sourceFileName}: {GetUiText("RecordNoScriptError")}");
                    continue;
                }
                DebugLog($"Render script ready: {renderScriptPath} ({new FileInfo(renderScriptPath).Length} bytes)");

                var hasAlpha = false;
                if (_host.SourceService.IsImageSource(Clips[clipIndex].Path))
                {
                    var firstFile = ResolveFirstImageFile(_host.SourceService.NormalizeConfiguredPath(Clips[clipIndex].Path));
                    if (firstFile is not null) hasAlpha = ImageHasAlpha(firstFile);
                }

                var result = BuildFfmpegArgs(clipEncoding, renderScriptPath, clipDir, sourceFileName, hasAlpha);
                if (result is null)
                {
                    errors.Add($"{sourceFileName}: failed to build output path");
                    continue;
                }

                var (ffArgs, outputPath) = result.Value;

                var sourcePath = Clips[clipIndex].Path;
                var isImgSeq = clipEncoding.GetValueOrDefault("encoder") is "tiff" or "png" or "jpg";
                if (!isImgSeq && string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                {
                    await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"),
                        string.Format(GetUiText("OutputSameAsSource"), Path.GetFileName(sourcePath)));
                    continue;
                }
                if (isImgSeq)
                {
                    var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
                    var outputSeqDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                    if (string.Equals(sourceDir, outputSeqDir, StringComparison.OrdinalIgnoreCase))
                    {
                        await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"),
                            string.Format(GetUiText("OutputSameAsSource"), Path.GetFileName(sourcePath)));
                        continue;
                    }
                }

                var outputExists = isImgSeq
                    ? Directory.Exists(Path.GetDirectoryName(outputPath)) &&
                      Directory.EnumerateFiles(Path.GetDirectoryName(outputPath)!).Any()
                    : File.Exists(outputPath);

                if (outputExists)
                {
                    var displayName = isImgSeq
                        ? Path.GetFileName(Path.GetDirectoryName(outputPath))!
                        : Path.GetFileName(outputPath);
                    if (!await AskOverwriteAsync(displayName))
                        continue;
                }

                _lastStderrLine = string.Empty;
                _stderrLines.Clear();
                DebugLog($"ffmpeg: {ffmpegPath} {ffArgs}");

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffArgs,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(renderScriptPath) ?? ""
                    };

                    _encodingProcess = new Process
                    {
                        StartInfo = psi,
                        EnableRaisingEvents = true
                    };

                    _encodingProcess.Start();

                    var stderrTask = ReadFfmpegStderrAsync(
                        _encodingProcess.StandardError, TrialMaxSeconds, _encodingCts.Token,
                        batchLabel);

                    await _encodingProcess.WaitForExitAsync(_encodingCts.Token);
                    using (var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        try { await stderrTask.WaitAsync(drainCts.Token); }
                        catch (OperationCanceledException) { DebugLog("stderr drain timed out (5s)"); }
                    }

                    if (_encodingProcess.ExitCode == 0)
                    {
                        successCount++;
                    }
                    else
                    {
                        var errorLines = _stderrLines
                            .Where(l => !l.StartsWith("out_time", StringComparison.Ordinal)
                                     && !l.StartsWith("bitrate=", StringComparison.Ordinal)
                                     && !l.StartsWith("total_size=", StringComparison.Ordinal)
                                     && !l.StartsWith("speed=", StringComparison.Ordinal)
                                     && !l.StartsWith("progress=", StringComparison.Ordinal)
                                     && !l.StartsWith("frame=", StringComparison.Ordinal)
                                     && !l.StartsWith("fps=", StringComparison.Ordinal)
                                     && !l.StartsWith("stream_", StringComparison.Ordinal)
                                     && !l.StartsWith("dup_frames=", StringComparison.Ordinal)
                                     && !l.StartsWith("drop_frames=", StringComparison.Ordinal)
                                     && !string.IsNullOrWhiteSpace(l))
                            .TakeLast(5)
                            .ToList();
                        var msg = errorLines.Count > 0
                            ? string.Join("\n", errorLines)
                            : $"ffmpeg exit code {_encodingProcess.ExitCode}";
                        DebugLog($"ffmpeg error for {sourceFileName}:\n{string.Join("\n", _stderrLines)}");
                        errors.Add($"{sourceFileName}: {msg}");
                    }
                }
                finally
                {
                    _encodingProcess?.Dispose();
                    _encodingProcess = null;
                }
            }
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("ErrorTitle"), ex.Message);
        }
        finally
        {
            _encodingCts = null;
            SetRecordStartButtonState(idle: true);
            SetRecordProgressVisible(false);
            SetEncodingLock(false);

            Config.ReplaceAll(savedConfig);
            if (savedClipIndex >= 0 && savedClipIndex < Clips.Count)
            {
                _host.ClipManager.ActiveIndex = savedClipIndex;
                _host.RestoreClipConfig(savedClipIndex);
            }
            _host.RegenerateScript(showValidationError: false);
        }

        UpdateDiskSpaceLabel(dir);
        var doneMsg = string.Format(GetUiText("BatchDoneMsg"), successCount, jobs.Count);
        if (errors.Count > 0)
            doneMsg += "\n\n" + string.Join("\n", errors);
        await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("RecordBtn"), doneMsg);

        if (shutdownAfter && successCount > 0 && errors.Count == 0)
        {
            await _host.DialogService.ShowErrorAsync(_host.Window, GetUiText("RecordBtn"), GetUiText("BatchShutdownMsg"));
            Process.Start("shutdown", "/s /t 60");
        }
    }

    // ── FFmpeg stderr reading ────────────────────────────────────────

    private async Task ReadFfmpegStderrAsync(StreamReader stderr, double totalDuration, CancellationToken ct, string? batchLabel = null)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await stderr.ReadLineAsync(ct);
                if (line is null) break;

                _lastStderrLine = line;
                _stderrLines.Add(line);
                if (_stderrLines.Count > 30) _stderrLines.RemoveAt(0);

                if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    if (long.TryParse(line.AsSpan("out_time_us=".Length), out var us) && us >= 0)
                    {
                        var seconds = us / 1_000_000.0;
                        var elapsed = TimeSpan.FromSeconds(seconds);
                        string label;
                        double pct;
                        if (totalDuration > 0)
                        {
                            pct = Math.Min(100.0, seconds / totalDuration * 100.0);
                            label = $"{pct:F1}%  \u2014  {elapsed:hh\\:mm\\:ss}";
                        }
                        else
                        {
                            pct = 0;
                            label = $"{elapsed:hh\\:mm\\:ss}";
                        }
                        if (batchLabel is not null) label = $"{batchLabel}  {label}";
                        Dispatcher.UIThread.Post(() => UpdateRecordProgress(pct, label));
                    }
                }
                else if (line.Contains("time=", StringComparison.Ordinal))
                {
                    var idx = line.IndexOf("time=", StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        var timePart = line.AsSpan(idx + 5);
                        var spaceIdx = timePart.IndexOf(' ');
                        if (spaceIdx > 0) timePart = timePart[..spaceIdx];
                        if (TimeSpan.TryParse(timePart, CultureInfo.InvariantCulture, out var ts))
                        {
                            string label;
                            double pct;
                            if (totalDuration > 0)
                            {
                                pct = Math.Min(100.0, ts.TotalSeconds / totalDuration * 100.0);
                                label = $"{pct:F1}%  \u2014  {ts:hh\\:mm\\:ss}";
                            }
                            else
                            {
                                pct = 0;
                                label = $"{ts:hh\\:mm\\:ss}";
                            }
                            if (batchLabel is not null) label = $"{batchLabel}  {label}";
                            Dispatcher.UIThread.Post(() => UpdateRecordProgress(pct, label));
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    // ── Progress & UI state ──────────────────────────────────────────

    private void UpdateRecordProgress(double percent, string label)
    {
        if (_host.FindControl<ProgressBar>("RecordProgressBar") is { } bar)
            bar.Value = percent;
        if (_host.FindControl<TextBlock>("RecordProgressText") is { } txt)
            txt.Text = label;
    }

    private void SetRecordProgressVisible(bool visible)
    {
        if (_host.FindControl<StackPanel>("RecordProgressPanel") is { } panel)
            panel.IsVisible = visible;
    }

    private void SetRecordStartButtonState(bool idle)
    {
        if (_host.FindControl<Button>("RecordStartBtn") is not { } btn) return;
        if (idle)
        {
            btn.Content = GetUiText("RecordStartBtn");
            btn.Background = ThemeBrush("AccentGreen");
        }
        else
        {
            btn.Content = GetUiText("RecordStopBtn");
            btn.Background = new SolidColorBrush(Color.Parse("#C62828"));
        }
    }

    private void SetEncodingLock(bool locked)
    {
        _host.IsEncoding = locked;

        if (locked)
            _host.MpvService?.Stop();

        foreach (var name in new[] { "VdbBeginning", "VdbPrevFrame", "VdbPlay", "VdbNextFrame", "VdbEnd", "SpeedBtn", "HalfResBtn", "RecordBtn" })
        {
            if (_host.FindControl<Button>(name) is { } btn)
            {
                btn.IsEnabled = !locked;
                if (name == "RecordBtn")
                    btn.Opacity = locked ? 0.4 : 1.0;
            }
        }
        if (_host.FindControl<Slider>("SeekBar") is { } seek)
            seek.IsEnabled = !locked;

        if (_host.FindControl<ComboBox>("ClipPresetCombo") is { } clipPreset)
            clipPreset.IsEnabled = !locked;

        if (_host.FindControl<Border>("ClipTabsContainer") is { } clipTabs)
            clipTabs.IsEnabled = !locked;

        if (_host.FindControl<Menu>("MainMenu") is { } menu)
            menu.IsEnabled = !locked;

        if (_host.FindControl<Grid>("RecordSettingsGrid") is { } recGrid)
            recGrid.IsEnabled = !locked;
        if (_host.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
            shutCb.IsEnabled = !locked;
    }

    // ── Render script generation ─────────────────────────────────────

    private string? GenerateRenderScript()
    {
        var scriptPath = _host.ScriptService.GetPrimaryScriptPath();
        if (scriptPath is null || !File.Exists(scriptPath)) return null;

        var renderPath = Path.Combine(
            Path.GetDirectoryName(scriptPath)!,
            "ScriptRender.avs");

        var content = File.ReadAllText(scriptPath);
        content = _host.PreviewTrueRegex().Replace(content, "${1}false");
        content = _host.PreviewHalfTrueRegex().Replace(content, "${1}false");

        File.WriteAllText(renderPath, content);
        return renderPath;
    }

    // ── FFmpeg probing ───────────────────────────────────────────────

    public static async Task<(string AvsError, string FullStderr)> ProbeAvsScriptError(string avsPath)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null || !File.Exists(avsPath)) return ("", "");

        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-i \"{avsPath}\" -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                }
            };
            proc.Start();

            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(stderr)) return ("", "");

            var avsLines = new List<string>();
            int scriptLineNum = -1;
            foreach (var line in stderr.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.Contains("[avisynth", StringComparison.OrdinalIgnoreCase))
                {
                    var clean = Regex.Replace(
                        trimmed, @"^\[avisynth\s*@\s*[0-9a-fA-F]+\]\s*", "");
                    avsLines.Add(clean);

                    var lineMatch = Regex.Match(
                        clean, @"line\s+(\d+)", RegexOptions.IgnoreCase);
                    if (lineMatch.Success && scriptLineNum < 0)
                        scriptLineNum = int.Parse(lineMatch.Groups[1].Value);
                }
            }

            if (File.Exists(avsPath))
            {
                try
                {
                    var scriptLines = await File.ReadAllLinesAsync(avsPath);

                    if (scriptLineNum < 0 && avsLines.Count > 0)
                    {
                        var filterMatch = Regex.Match(avsLines[0], @"^(\w+)\s*:");
                        if (filterMatch.Success)
                        {
                            var filterName = filterMatch.Groups[1].Value;
                            for (var i = scriptLines.Length - 1; i >= 0; i--)
                            {
                                if (scriptLines[i].Contains(filterName, StringComparison.OrdinalIgnoreCase)
                                 && !scriptLines[i].TrimStart().StartsWith("#"))
                                {
                                    scriptLineNum = i + 1;
                                    break;
                                }
                            }
                        }
                    }

                    if (scriptLineNum > 0)
                    {
                        var from = Math.Max(0, scriptLineNum - 3);
                        var to   = Math.Min(scriptLines.Length, scriptLineNum + 2);
                        avsLines.Add("");
                        avsLines.Add($"\u2500\u2500 Script ligne {scriptLineNum} \u2500\u2500");
                        for (var i = from; i < to; i++)
                        {
                            var marker = (i + 1 == scriptLineNum) ? "\u25ba" : " ";
                            avsLines.Add($" {marker} {i + 1,4}\u2502 {scriptLines[i]}");
                        }
                    }
                }
                catch { /* ignore read errors */ }
            }

            var avsError = avsLines.Count > 0 ? string.Join("\n", avsLines) : "";
            return (avsError, stderr.Trim());
        }
        catch { return ("", ""); }
    }

    public static string? FindFfmpeg()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;

        var bundled = Path.Combine(exeDir, "Plugins", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundled)) return bundled;

        var local = Path.Combine(exeDir, "ffmpeg.exe");
        if (File.Exists(local)) return local;

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var d in pathDirs)
        {
            var candidate = Path.Combine(d.Trim(), "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static async Task<bool> CheckFfmpegAviSynthSupport(string ffmpegPath)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-demuxers",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            proc.Dispose();
            return output.Contains("avisynth", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── Debug logging ────────────────────────────────────────────────

    [Conditional("DEBUG")]
    private static void DebugLog(string msg)
    {
        Debug.WriteLine($"[CleanScan:Encode] {DateTime.Now:HH:mm:ss.fff} {msg}");
    }
}
