using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvyscanLab.Services;
using AvyscanLab.ViewModels;

namespace AvyscanLab.Views;

/// <summary>Contract the player controller needs from the host window.</summary>
public interface IPlayerHost
{
    T? FindControl<T>(string name) where T : Control;
    Window Window { get; }
    SolidColorBrush ThemeBrush(string key);
    string GetUiText(string key);
    string GetLocalizedText(string fr, string en);
    MainWindowViewModel ViewModel { get; }
    ConfigStore Config { get; }
    IScriptService ScriptService { get; }
    SourceService SourceService { get; }
    IDialogService DialogService { get; }

    bool IsClosing { get; }
    bool IsEncoding { get; }
    bool LoadingSourceFallback { get; set; }

    bool TryValidateSourceSelection(out string errorMessage);
    Action? AdvanceOnClipLoaded { get; }

    void RegenerateScript(bool showValidationError = true);
    void UpdateConfigurationValue(string name, string value, bool showValidationError = true);
    void CloseSettingsMenu();
}

/// <summary>
/// Manages all player/transport-bar/mpv-related logic extracted from MainWindow.
/// </summary>
public sealed class PlayerController
{
    private readonly IPlayerHost _host;

    // ── mpv service ───────────────────────────────────────────────────
    private MpvService? _mpvService;
    public MpvService? MpvService => _mpvService;

    // ── Seek state ────────────────────────────────────────────────────
    private bool _seekDragging;
    private double _seekDuration;
    private int _totalFrames;
    private double _fps;
    private double _pendingSeekPos;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    // ── Auto-resume state ──────────────────────────────────────────────
    private bool _resumeAfterLoad;

    // ── Speed / view state ────────────────────────────────────────────
    private static readonly double[] PlaybackSpeeds = [0.25, 0.5, 1.0];
    private int _speedIndex = 2; // default 1x
    private bool _viewerMaximized;
    private GridLength _savedBottomPanelRow = new(360);
    private bool _halfRes;
    private Grid? _mainGrid;
    private CancellationTokenSource? _pulseAnimCts;
    private DispatcherTimer? _pulseTimer;
    private double _pulsePhase;

    // ── ForceSource state ─────────────────────────────────────────────
    private bool _syncingForceSource;

    // ── Debug log ─────────────────────────────────────────────────────
    private static readonly string _logPath =
        Path.Combine(Path.GetTempPath(), "avyscanlab_debug.txt");

    public PlayerController(IPlayerHost host)
    {
        _host = host;
    }

    // ══════════════════════════════════════════════════════════════════
    // Initialisation
    // ══════════════════════════════════════════════════════════════════

    public void InitPlayerControls()
    {
        DebugLog("InitPlayerControls start");
        _mpvService = new MpvService();

        if (_host.FindControl<MpvHost>("VideoHost") is { } host)
        {
            DebugLog("VideoHost found");
            host.HandleReady += hwnd =>
            {
                DebugLog($"HandleReady hwnd={hwnd}");
                _mpvService.Initialize(hwnd);
                DebugLog($"After Initialize, IsReady={_mpvService.IsReady}");
                if (!_mpvService.IsReady)
                {
                    ShowPlayerStatus("mpv non disponible.\nVérifiez que libmpv-2.dll est présent dans le dossier mpv/.");
                    return;
                }

                // Check AviSynth at startup so the user sees the status immediately.
                var avsCheck = GetAviSynthDiagnostic();
                DebugLog("AviSynth startup check: " + avsCheck);
                if (!avsCheck.Contains("chargeable OK", StringComparison.Ordinal))
                    ShowPlayerStatus("AviSynth — " + avsCheck);

                var srcOk = _host.TryValidateSourceSelection(out _);
                DebugLog($"TryValidateSourceSelection={srcOk}");
                if (!srcOk)
                {
                    return;
                }
                _ = LoadScriptAsync();
            };
            host.FilesDropped += OnPlayerFilesDroppedInternal;
        }
        else
        {
            DebugLog("VideoHost NOT found — FindControl returned null");
        }

        _mpvService.PositionChanged    += pos => Dispatcher.UIThread.Post(() => OnMpvPosition(pos));
        _mpvService.DurationChanged    += dur => Dispatcher.UIThread.Post(() => OnMpvDuration(dur));
        _mpvService.PauseChanged       += p   => Dispatcher.UIThread.Post(() => OnMpvPauseChanged(p));
        _mpvService.FileLoaded         += ()  => Dispatcher.UIThread.Post(() => OnMpvFileLoaded());
        _mpvService.PlaybackRestart    += ()  => Dispatcher.UIThread.Post(OnMpvPlaybackRestart);
        _mpvService.LoadFailed         += msg => Dispatcher.UIThread.Post(() => OnMpvLoadFailed(msg));
        _mpvService.UnexpectedShutdown += ()  => Dispatcher.UIThread.Post(OnMpvUnexpectedShutdown);

        if (_host.FindControl<Slider>("SeekBar") is { } seekBar)
        {
            seekBar.AddHandler(InputElement.PointerPressedEvent, (_, _) => { _seekDragging = true; },
                RoutingStrategies.Bubble, handledEventsToo: true);
            seekBar.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
                {
                    _seekDragging = false;
                    var pos = seekBar.Value;
                    if (_totalFrames > 0 && _fps > 0)
                        pos = Math.Min(pos, (_totalFrames - 1.0) / _fps);
                    else if (_seekDuration > 0)
                        pos = Math.Min(pos, _seekDuration - 0.001);
                    _mpvService?.Seek(pos);
                },
                RoutingStrategies.Bubble, handledEventsToo: true);
        }

        if (_host.FindControl<Button>("HistogramBtn") is { } histBtn)
            histBtn.AddHandler(InputElement.PointerReleasedEvent,
                new EventHandler<PointerReleasedEventArgs>(OnHistogramRightClick),
                RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Event forwarded by the host when files are dropped on the player area.
    /// The host hooks FilesDropped on MpvHost; we just expose a delegate for MainWindow to call.</summary>
    public event Action<System.Collections.Generic.List<string>>? FilesDropped;

    private void OnPlayerFilesDroppedInternal(System.Collections.Generic.List<string> paths) =>
        FilesDropped?.Invoke(paths);

    // ══════════════════════════════════════════════════════════════════
    // mpv event handlers
    // ══════════════════════════════════════════════════════════════════

    private void OnMpvPosition(double pos)
    {
        if (_seekDragging || _seekDuration <= 0) return;
        if (_host.FindControl<Slider>("SeekBar") is { } s) s.Value = pos;
        UpdateTimeLabel(pos, _seekDuration);
    }

    private void OnMpvDuration(double dur)
    {
        _seekDuration = dur;
        if (_host.FindControl<Slider>("SeekBar") is { } s)
        {
            s.Maximum   = dur > 0 ? dur : 1;
            s.IsEnabled = dur > 0;
        }
        UpdateTimeLabel(_mpvService?.GetPosition() ?? 0, dur);
    }

    private void OnMpvPauseChanged(bool paused)
    {
        if (_host.FindControl<Button>("VdbPlay") is { } btn)
            btn.Content = paused ? "\u25B6" : "\u23F8";
    }

    private void OnMpvFileLoaded()
    {
        DebugLog("OnMpvFileLoaded — file loaded successfully");
        _host.Window.Title = "Avyscan Lab";

        if (_host.LoadingSourceFallback)
        {
            ShowPlayerStatus("Mode d\u00e9grad\u00e9 : lecture directe de la source (AviSynth+ non install\u00e9).");
        }
        else if (_host.FindControl<Border>("PlayerErrorBanner") is { } banner)
        {
            banner.IsVisible = false;
        }

        if (_host.FindControl<TextBlock>("DropHintBar") is { } dropHint)
            dropHint.IsVisible = false;

        if (_host.FindControl<Slider>("SeekBar") is { } s) s.Value = _pendingSeekPos;

        _host.AdvanceOnClipLoaded?.Invoke();

        if (_mpvService is { IsReady: true })
        {
            _totalFrames = _mpvService.GetTotalFrames();
            _fps         = _mpvService.GetFps();

            if (_totalFrames > 0 && _fps > 0 && _host.FindControl<Slider>("SeekBar") is { } bar)
            {
                bar.Maximum   = (_totalFrames - 1.0) / _fps;
                bar.IsEnabled = true;
            }

            // Reapply vf filters after file load (mpv clears vf on stop/loadfile)
            _mpvService.ReapplyHistogramFilter(IsPreviewMode());
            _mpvService.ReapplyRegionOverlay();
        }
    }

    private void OnMpvPlaybackRestart()
    {
        StopPulseAnimation();
        if (_host.FindControl<Button>("VdbPlay") is { } btn)
        {
            btn.Opacity = 1.0;
            btn.Background = _host.ThemeBrush("BgInput");
        }

        // Auto-resume playback if user was playing before the script reload
        if (_resumeAfterLoad)
        {
            _resumeAfterLoad = false;
            DebugLog("OnMpvPlaybackRestart: auto-resuming playback");
            _mpvService?.Play();
        }
    }

    private void StopPulseAnimation()
    {
        _pulseTimer?.Stop();
        _pulseTimer = null;
        _pulseAnimCts?.Cancel();
        _pulseAnimCts = null;
    }

    private void SetPlayButtonProcessing()
    {
        if (_host.FindControl<Button>("VdbPlay") is not { } btn) return;

        btn.Background = new SolidColorBrush(Color.Parse("#FFCC00"));

        StopPulseAnimation();
        _pulsePhase = 0;
        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _pulseTimer.Tick += (_, _) =>
        {
            // Sine wave oscillation between 0.45 and 1.0 over ~700ms per half-cycle
            _pulsePhase += 0.030 / 0.700 * Math.PI;
            btn.Opacity = 0.725 + 0.275 * Math.Sin(_pulsePhase);
        };
        _pulseTimer.Start();
    }

    private void OnMpvUnexpectedShutdown()
    {
        _seekDragging = false;
        _seekDuration = 0;
        if (_host.FindControl<Slider>("SeekBar") is { } s)
        {
            s.Value     = 0;
            s.Maximum   = 1;
            s.IsEnabled = false;
        }

        _mpvService?.Reinitialize();

        if (_host.TryValidateSourceSelection(out _))
            _ = LoadScriptAsync();
    }

    private void OnMpvLoadFailed(string errorMsg)
    {
        DebugLog("OnMpvLoadFailed: " + errorMsg);

        if (!_host.LoadingSourceFallback
         && (errorMsg.Contains("unknown file format", StringComparison.OrdinalIgnoreCase)
          || errorMsg.Contains("unrecognized file format", StringComparison.OrdinalIgnoreCase)))
        {
            var diag = GetAviSynthDiagnostic();
            DebugLog("AviSynth diag: " + diag);

            if (diag.Contains("chargeable", StringComparison.OrdinalIgnoreCase))
            {
                ShowPlayerStatus("Erreur de script AviSynth");
                _ = ShowAvsScriptErrorAsync();
                return;
            }

            var raw = _host.Config.Get("source");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var path = _host.SourceService.NormalizeConfiguredPath(raw);
                if (File.Exists(path))
                {
                    _host.LoadingSourceFallback = true;
                    ShowPlayerStatus($"AviSynth+ non d\u00e9tect\u00e9 ({diag}).\nLecture directe de la source (sans filtres).");
                    _mpvService?.LoadFile(path, 0);
                    return;
                }
            }
            ShowPlayerStatus($"AviSynth+ non d\u00e9tect\u00e9.\n{diag}");
            return;
        }

        _host.LoadingSourceFallback = false;
        ShowPlayerStatus($"Erreur de lecture : {errorMsg}");
    }

    // ══════════════════════════════════════════════════════════════════
    // UI helpers
    // ══════════════════════════════════════════════════════════════════

    public void UpdateTimeLabel(double pos, double dur)
    {
        static string Fmt(double s) =>
            TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
        if (_host.FindControl<TextBlock>("TimeLabel") is { } lbl)
            lbl.Text = $"{Fmt(pos)} / {Fmt(dur)}";

        if (_host.FindControl<TextBlock>("FrameLabel") is { } fl)
        {
            var currentFrame = _fps > 0 ? (int)(pos * _fps) : 0;
            fl.Text = $"{currentFrame} / {_totalFrames}";
        }
    }

    public void ShowPlayerStatus(string message)
    {
        DebugLog("ShowPlayerStatus: " + message.Replace('\n', ' '));
        _host.Window.Title = "Avyscan Lab";

        if (_host.FindControl<Border>("PlayerErrorBanner") is { } banner
         && _host.FindControl<TextBlock>("PlayerErrorText")  is { } text)
        {
            text.Text        = message;
            banner.IsVisible = true;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Transport bar click handlers
    // ══════════════════════════════════════════════════════════════════

    public void OnVdbBeginningClick(object? sender, RoutedEventArgs e) =>
        _mpvService?.Stop();

    public void OnVdbPrevFrameClick(object? sender, RoutedEventArgs e) =>
        _mpvService?.FrameBackStep();

    public void OnVdbPlayClick(object? sender, RoutedEventArgs e) =>
        _mpvService?.TogglePlayPause();

    public void OnVdbNextFrameClick(object? sender, RoutedEventArgs e) =>
        _mpvService?.FrameStep();

    public void OnVdbEndClick(object? sender, RoutedEventArgs e)
    {
        if (_totalFrames > 0 && _fps > 0)
            _mpvService?.Seek((_totalFrames - 1.0) / _fps);
        else if (_seekDuration > 0)
            _mpvService?.Seek(_seekDuration - 0.001);
    }

    // ══════════════════════════════════════════════════════════════════
    // Speed / view
    // ══════════════════════════════════════════════════════════════════

    public void OnSpeedClick(object? sender, RoutedEventArgs e)
    {
        _speedIndex = (_speedIndex + 1) % PlaybackSpeeds.Length;
        var speed = PlaybackSpeeds[_speedIndex];
        _mpvService?.SetSpeed(speed);
        if (_host.FindControl<Button>("SpeedBtn") is { } btn)
            btn.Content = speed < 1.0 ? $"{speed:G}x" : "1x";
    }

    public bool HalfRes => _halfRes;
    public bool ViewerMaximized => _viewerMaximized;

    public void OnHistogramClick(object? sender, RoutedEventArgs e)
    {
        _mpvService?.ToggleHistogram(IsPreviewMode());
        UpdateHistogramButtonVisual();
    }

    public void OnHistogramRightClick(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right) return;
        if (sender is not Button btn) return;

        var menu = new ContextMenu();
        foreach (var (type, label) in new[]
        {
            (ScopeType.Histogram,      "Histogram"),
            (ScopeType.HistogramParade, "Histogram Parade"),
            (ScopeType.Waveform,       "Waveform"),
            (ScopeType.Vectorscope,    "Vectorscope"),
        })
        {
            var item = new MenuItem { Header = label };
            var current = _mpvService?.CurrentScopeType;
            if (current == type)
                item.Icon = new TextBlock { Text = "\u2713" };
            var t = type;
            item.Click += (_, _) =>
            {
                _mpvService?.SetScopeType(t, IsPreviewMode());
                UpdateHistogramButtonVisual();
            };
            menu.Items.Add(item);
        }
        menu.Open(btn);
        e.Handled = true;
    }

    /// <summary>Called when the preview toggle changes so the histogram crop adapts.</summary>
    public void OnPreviewModeChanged()
    {
        _mpvService?.UpdateHistogramFilter(IsPreviewMode());
    }

    private bool IsPreviewMode()
    {
        return _host.FindControl<Button>("preview") is { Tag: true };
    }

    private void UpdateHistogramButtonVisual()
    {
        var on = _mpvService?.HistogramEnabled ?? false;
        if (_host.FindControl<Button>("HistogramBtn") is { } btn)
        {
            btn.Background = on ? _host.ThemeBrush("AccentGreen") : _host.ThemeBrush("BgInput");
            btn.Foreground = on ? Brushes.White : _host.ThemeBrush("TextLabel");
        }
    }

    public void OnHalfResClick(object? sender, RoutedEventArgs e)
    {
        _halfRes = !_halfRes;
        if (_host.FindControl<Button>("HalfResBtn") is { } btn)
        {
            btn.Background = _halfRes ? _host.ThemeBrush("AccentGreen") : new SolidColorBrush(Color.Parse("#3B4C64"));
            btn.Foreground = Brushes.White;
        }
        UpdateConfigurationValue("preview_half", _halfRes.ToString().ToLowerInvariant());
    }

    public void RestoreHalfResVisual()
    {
        var halfResValue = _host.Config.Get("preview_half");
        // Default to true (half-res on) for new users who have no saved preference
        var shouldEnable = string.IsNullOrEmpty(halfResValue) || (bool.TryParse(halfResValue, out var parsed) && parsed);
        if (shouldEnable)
        {
            _halfRes = true;
            if (_host.FindControl<Button>("HalfResBtn") is { } btn)
            {
                btn.Background = _host.ThemeBrush("AccentGreen");
                btn.Foreground = Brushes.White;
            }
        }
    }

    public void OnMaxViewerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b)
            ToolTip.SetIsOpen(b, false);
        ToggleViewerMaximized();
        e.Handled = true;
    }

    public void ToggleViewerMaximized()
    {
        _mainGrid ??= _host.FindControl<Grid>("MainGrid");
        _viewerMaximized = !_viewerMaximized;

        if (_host.FindControl<Border>("TopBar") is { } topBar)
            topBar.IsVisible = !_viewerMaximized;

        if (_host.FindControl<Border>("BottomPanel") is { } bottomPanel)
            bottomPanel.IsVisible = !_viewerMaximized;
        if (_host.FindControl<GridSplitter>("MainSplitter") is { } splitter)
            splitter.IsVisible = !_viewerMaximized;

        if (_mainGrid is not null && _mainGrid.RowDefinitions.Count >= 3)
        {
            if (_viewerMaximized)
            {
                _savedBottomPanelRow = _mainGrid.RowDefinitions[2].Height;
                _mainGrid.RowDefinitions[1].Height = new GridLength(0);
                _mainGrid.RowDefinitions[2].Height = new GridLength(0);
            }
            else
            {
                _mainGrid.RowDefinitions[1].Height = new GridLength(4);
                _mainGrid.RowDefinitions[2].Height = _savedBottomPanelRow;
            }
        }

        if (_host.FindControl<Button>("MaxViewerBtn") is { } btn)
        {
            btn.Content = _viewerMaximized ? "\u26F6" : "\u26F6";
            var tooltipKey = _viewerMaximized ? "RestoreViewerBtn" : "MaxViewerBtn";
            ToolTip.SetTip(btn, _host.GetUiText(tooltipKey));
            btn.Background = _viewerMaximized
                ? _host.ThemeBrush("AccentGreen")
                : _host.ThemeBrush("BgInput");
            btn.Foreground = _viewerMaximized
                ? Brushes.White
                : _host.ThemeBrush("TextLabel");
        }

        Dispatcher.UIThread.Post(_host.Window.InvalidateVisual, DispatcherPriority.Render);
    }

    // ══════════════════════════════════════════════════════════════════
    // ForceSource combo
    // ══════════════════════════════════════════════════════════════════

    public void SyncForceSourceCombo(System.Collections.Generic.Dictionary<string, string> values)
    {
        if (_host.FindControl<ComboBox>("ForceSourceCombo") is not { } cb) return;
        _syncingForceSource = true;
        var current = values.TryGetValue("force_source", out var v) ? v.Trim().Trim('"') : "FFMS2";
        for (var i = 0; i < cb.ItemCount; i++)
        {
            if (cb.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), current, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedIndex = i;
                _syncingForceSource = false;
                return;
            }
        }
        cb.SelectedIndex = 1; // default FFMS2
        _syncingForceSource = false;
    }

    public void OnForceSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingForceSource) return;
        if (sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item) return;
        var value = item.Tag?.ToString() ?? "";
        UpdateConfigurationValue("force_source", value);
        _host.CloseSettingsMenu();
    }

    // ══════════════════════════════════════════════════════════════════
    // Keyboard shortcuts
    // ══════════════════════════════════════════════════════════════════

    public void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_host.IsEncoding) return;
        if (_host.Window.FocusManager?.GetFocusedElement() is TextBox) return;

        switch ((e.Key, e.KeyModifiers))
        {
            case (Key.Space, KeyModifiers.None):
                e.Handled = true;
                _mpvService?.TogglePlayPause();
                break;

            case (Key.Left, KeyModifiers.None):
                e.Handled = true;
                _mpvService?.FrameBackStep();
                break;

            case (Key.Left, KeyModifiers.Control):
                e.Handled = true;
                _mpvService?.Stop();
                break;

            case (Key.Right, KeyModifiers.None):
                e.Handled = true;
                _mpvService?.FrameStep();
                break;

            case (Key.Right, KeyModifiers.Control):
                e.Handled = true;
                if (_totalFrames > 0 && _fps > 0)
                    _mpvService?.Seek((_totalFrames - 1.0) / _fps);
                else if (_seekDuration > 0)
                    _mpvService?.Seek(_seekDuration - 0.001);
                break;

            case (Key.F11, KeyModifiers.None):
                e.Handled = true;
                ToggleViewerMaximized();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // LoadScriptAsync — core script loading into mpv
    // ══════════════════════════════════════════════════════════════════

    public async Task LoadScriptAsync(bool resetPosition = false)
    {
        if (_host.IsClosing || _mpvService is null) return;

        await _refreshGate.WaitAsync();
        try
        {
            if (!_host.TryValidateSourceSelection(out _))
            {
                return;
            }

            var scriptPath = _host.ScriptService.GetPrimaryScriptPath();
            if (string.IsNullOrWhiteSpace(scriptPath)) return;

            double pos;
            if (resetPosition)
                pos = 0.0;
            else if (_mpvService.IsReady)
            {
                var cur = _mpvService.GetPosition();
                pos = (cur < 0.5 && _pendingSeekPos > 0.5) ? _pendingSeekPos : cur;
            }
            else
                pos = _pendingSeekPos;
            _pendingSeekPos = pos;
            _totalFrames = 0;
            _fps         = 0;
            _host.LoadingSourceFallback = false;
            // Save play state BEFORE LoadFile pauses mpv
            _resumeAfterLoad = !_mpvService.IsPaused();
            DebugLog($"LoadFile: {scriptPath}, pos={pos:F2}, IsReady={_mpvService.IsReady}, resumeAfterLoad={_resumeAfterLoad}");
            try
            {
                var content = File.ReadAllText(scriptPath);
                DebugLog($"Script ({content.Length} chars): {content[..Math.Min(400, content.Length)].Replace('\n', '|').Replace('\r', ' ')}");
            }
            catch (Exception ex) { DebugLog($"Script read error: {ex.Message}"); }
            SetPlayButtonProcessing();
            ShowPlayerStatus("Chargement\u2026");
            _mpvService.LoadFile(scriptPath, pos);
        }
        finally { _refreshGate.Release(); }
    }

    // ══════════════════════════════════════════════════════════════════
    // Player reset (called when switching clips)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Resets seek state and slider to prepare for a new clip load.</summary>
    public void ResetPlayerState()
    {
        _mpvService?.Unload();
        _seekDragging = false;
        _seekDuration = 0;
        _totalFrames  = 0;
        _fps          = 0;
        if (_host.FindControl<Slider>("SeekBar") is { } seekBar)
        {
            seekBar.Value     = 0;
            seekBar.Maximum   = 1;
            seekBar.IsEnabled = false;
        }
        UpdateTimeLabel(0, 0);
    }

    /// <summary>Unloads the current file from mpv (no source).</summary>
    public void Unload() => _mpvService?.Unload();

    public void Dispose() => _mpvService?.Dispose();

    // ══════════════════════════════════════════════════════════════════
    // Transport tooltip refresh
    // ══════════════════════════════════════════════════════════════════

    public void ApplyTransportTooltips()
    {
        foreach (var (controlName, textKey) in new[]
        {
            ("VdbBeginning", "VdbBeginning"),
            ("VdbPrevFrame", "VdbPrevFrame"),
            ("VdbPlay",      "VdbPlay"),
            ("VdbStop",      "VdbStop"),
            ("VdbNextFrame", "VdbNextFrame"),
            ("VdbEnd",       "VdbEnd"),
            ("SpeedBtn",     "SpeedBtn"),
            ("HalfResBtn",   "HalfResBtn"),
            ("HistogramBtn", "HistogramBtn"),
            ("RecordBtn",    "RecordBtn"),
        })
        {
            if (_host.FindControl<Button>(controlName) is { } btn)
                ToolTip.SetTip(btn, _host.GetUiText(textKey));
        }

        if (_host.FindControl<Button>("MaxViewerBtn") is { } maxBtn)
            ToolTip.SetTip(maxBtn, _host.GetUiText(_viewerMaximized ? "RestoreViewerBtn" : "MaxViewerBtn"));
    }

    // ══════════════════════════════════════════════════════════════════
    // Static helpers
    // ══════════════════════════════════════════════════════════════════

    public static string GetAviSynthDiagnostic()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var dllPath  = Path.Combine(system32, "AviSynth.dll");
        if (!File.Exists(dllPath))
            return $"AviSynth.dll absent de {system32}";
        try
        {
            if (System.Runtime.InteropServices.NativeLibrary.TryLoad(dllPath, out var h))
            {
                System.Runtime.InteropServices.NativeLibrary.Free(h);
                return "AviSynth.dll pr\u00e9sent et chargeable OK";
            }
            return "AviSynth.dll pr\u00e9sent dans System32 mais non chargeable (mauvaise architecture ?)";
        }
        catch (Exception ex) { return $"AviSynth.dll erreur : {ex.Message}"; }
    }

    public static void DebugLog(string msg)
    {
        try { File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff}  {msg}\n"); }
        catch { }
    }

    private async Task ShowAvsScriptErrorAsync()
    {
        var scriptPath = _host.ScriptService.GetPrimaryScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath)) return;

        var (avsError, fullStderr) = await EncodeController.ProbeAvsScriptError(scriptPath);
        var primary = !string.IsNullOrWhiteSpace(avsError)
            ? avsError
            : "Erreur inconnue dans le script AviSynth.\nOuvrez le script dans AvsPmod pour diagnostiquer.";
        ShowPlayerStatus(primary);
        await _host.DialogService.ShowErrorAsync(_host.Window, "Erreur AviSynth", primary, fullStderr);
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void UpdateConfigurationValue(string name, string value, bool showValidationError = true) =>
        _host.UpdateConfigurationValue(name, value, showValidationError);
}
