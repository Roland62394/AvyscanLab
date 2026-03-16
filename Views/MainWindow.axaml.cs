using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
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
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using System.Runtime.InteropServices;
using CleanScan.Models;
using CleanScan.Services;
using CleanScan.ViewModels;
using static CleanScan.Services.FilterPresets;
using static CleanScan.Services.UiFieldDefinitions;

namespace CleanScan.Views
{
    public partial class MainWindow : Window, ITourHost, IFilterPresenterHost, IEncodeHost, IPlayerHost
    {
        #region Constants

        private const string AppDataFolder         = "CleanScan";
        private const string WindowSettingsFileName = "window-settings.json";
        private const string PresetsFileName        = "presets.json";
        private const string EncodingPresetsFileName = "encoding_presets.json";
        private const string GammacPresetsFileName   = "gammac_presets.json";
        private const string SessionFileName         = "session.json";
        private const string CustomFiltersFileName   = "custom_filters.json";
        private const string DefaultEncodingPresetName = "Default";

        /// <summary>Trial: max recording duration per clip in seconds. 0 = unlimited (full version).</summary>
        private const int TrialMaxSeconds = 30;
        private const string UseImageConfigName     = ScriptService.UseImageConfigName;

        #endregion

        #region Static data

        private static readonly string[] VideoExtensions = [".avi", ".mp4", ".mov", ".mkv", ".wmv", ".m4v", ".mpeg", ".mpg", ".webm"];
        private static readonly string[] ImageExtensions = [".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp"];

        private static readonly FilePickerFileType VideoFileType =
            new("Video") { Patterns = [.. VideoExtensions.Select(e => $"*{e}")] };
        private static readonly FilePickerFileType ImageFileType =
            new("Images") { Patterns = [.. ImageExtensions.Select(e => $"*{e}")] };

        // Filter presets, option arrays, tooltips — see Services/FilterPresets.cs
        // Slider/field specs — see Services/UiFieldDefinitions.cs

        [GeneratedRegex(@"^\d+$")]
        private static partial Regex NumericStemRegex();

        [GeneratedRegex(@"(?m)^(\s*preview\s*=\s*)true")]
        private static partial Regex PreviewTrueRegex();

        [GeneratedRegex(@"(?m)^(\s*preview_half\s*=\s*)true")]
        private static partial Regex PreviewHalfTrueRegex();

        #endregion

        #region Instance state

        private readonly ConfigStore          _config;
        private readonly SourceService        _sourceService;
        private readonly IScriptService       _scriptService;
        private readonly IPresetService       _presetService;
        private readonly PresetService        _encodingPresetService;
        private readonly PresetService        _gammacPresetService;
        private readonly IWindowStateService  _windowStateService;
        private readonly IDialogService       _dialogService;
        private readonly IAviService          _aviService;
        private readonly SessionService      _sessionService;
        private readonly CustomFilterService _customFilterService;
        private readonly ThemeService        _themeService = new();
        private readonly Debouncer            _refreshDebouncer = new(TimeSpan.FromMilliseconds(400));
        private readonly Debouncer            _windowStateDebouncer = new(TimeSpan.FromMilliseconds(120));
        private PlayerController _playerController = null!; // initialized in constructor

        private bool  _suppressTextEvents;
        private bool  _sliderSync;
        private bool  _loadingSourceFallback;

        private readonly Dictionary<string, (Slider Slider, SliderSpec Spec)> _sliderMap = [];
        private bool  _isClosing;
        private bool  _isInitializing;
        private bool  _layoutInitialized;
        private bool  _sourceValidationErrorVisible;
        private Grid? _mainGrid;

        private readonly ClipManager _clipManager = null!; // initialized in constructor
        private bool _applyingPreset;
        private bool _switchingClip;
        private int  _pendingClipSwitch = -1;

        // Convenience accessors over ClipManager
        private List<ClipState> Clips => _clipManager.Clips;
        private int ActiveClipIndex { get => _clipManager.ActiveIndex; set => _clipManager.ActiveIndex = value; }

        private EncodeController _encodeController = null!; // initialized in constructor
        private bool _isDroppingFiles;


        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

        #endregion

        #region Constructor & lifecycle

        public MainWindow()
        {
            _config             = new ConfigStore();
            _clipManager        = new ClipManager(_config);
            _sourceService      = new SourceService();
            _aviService         = new AviService();
            _scriptService      = new ScriptService(_sourceService);
            _presetService      = new PresetService(GetAppDataPath(PresetsFileName));
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _gammacPresetService  = new PresetService(GetAppDataPath(GammacPresetsFileName));
            _windowStateService = new WindowStateService(GetAppDataPath(WindowSettingsFileName));
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _customFilterService = new CustomFilterService(GetAppDataPath(CustomFiltersFileName));
            _dialogService      = new DialogService();

            InitializeWindow();
        }

        public MainWindow(
            ConfigStore         config,
            SourceService       sourceService,
            IScriptService      scriptService,
            IPresetService      presetService,
            IWindowStateService windowStateService,
            IDialogService      dialogService,
            IAviService         aviService)
        {
            _config             = config;
            _clipManager        = new ClipManager(config);
            _sourceService      = sourceService;
            _scriptService      = scriptService;
            _presetService      = presetService;
            _encodingPresetService = new PresetService(GetAppDataPath(EncodingPresetsFileName));
            _gammacPresetService  = new PresetService(GetAppDataPath(GammacPresetsFileName));
            _windowStateService = windowStateService;
            _sessionService     = new SessionService(GetAppDataPath(SessionFileName));
            _customFilterService = new CustomFilterService(GetAppDataPath(CustomFiltersFileName));
            _dialogService      = dialogService;
            _aviService         = aviService;

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            ConfigureMenuBar();
            InitTheme();
            PreApplyWindowPosition();
            Opened  += OnOpened;
            Closing += OnClosing;
            PositionChanged += OnPositionChanged;
            SizeChanged     += OnWindowSizeChanged;
            BottomPanel.SizeChanged += OnBottomPanelSizeChanged;
            InitializeChoiceFields();
            UpdateOptionColumnVisibility();
            RegisterChangeHandlers();
            InitSliders();
            _encodeController = new EncodeController(this);
            _encodeController.InitRecordPanel();
            _playerController = new PlayerController(this);
            _playerController.FilesDropped += OnPlayerFilesDropped;
            _playerController.InitPlayerControls();
            RebuildCustomFilterUI();
        }

        // Player methods delegated to PlayerController
        private void ShowPlayerStatus(string message) => _playerController?.ShowPlayerStatus(message);
        private static void DebugLog(string msg) => PlayerController.DebugLog(msg);
        private static string GetAviSynthDiagnostic() => PlayerController.GetAviSynthDiagnostic();
        private Task LoadScriptAsync(bool resetPosition = false) => _playerController?.LoadScriptAsync(resetPosition) ?? Task.CompletedTask;
        private void UpdateTimeLabel(double pos, double dur) => _playerController?.UpdateTimeLabel(pos, dur);
        private void ApplyTransportTooltips() => _playerController?.ApplyTransportTooltips();

        private void InitSliders()
        {
            foreach (var spec in SliderSpecs)
            {
                if (this.FindControl<Slider>("Slide_" + spec.Field) is not { } slider) continue;
                slider.Minimum     = spec.Min;
                slider.Maximum     = spec.Max;
                slider.SmallChange = spec.SmallChange;
                slider.LargeChange = spec.SmallChange * 10;
                _sliderMap[spec.Field] = (slider, spec);

                var captured = spec;
                var pressing = false;

                slider.ValueChanged += (_, _) => OnSliderValueChanged(captured);

                slider.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
                    pressing = true;
                    e.Pointer.Capture(slider);
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerMovedEvent, (_, e) =>
                {
                    if (!pressing) return;
                    MoveSliderToPointer(slider, e);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                slider.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (!pressing) return;
                    pressing = false;
                    e.Pointer.Capture(null);
                    CommitSliderField(captured.Field);
                    e.Handled = true;
                }, RoutingStrategies.Bubble, handledEventsToo: true);

                // Mouse wheel on TextBox
                if (this.FindControl<TextBox>(spec.Field) is { } tb)
                {
                    var capturedSpec = spec;
                    tb.PointerWheelChanged += (_, e) =>
                    {
                        e.Handled = true;
                        if (!_sliderMap.TryGetValue(capturedSpec.Field, out var entry)) return;
                        var delta = e.Delta.Y > 0 ? capturedSpec.SmallChange : -capturedSpec.SmallChange;
                        entry.Slider.Value = Math.Clamp(entry.Slider.Value + delta, entry.Spec.Min, entry.Spec.Max);
                        CommitSliderField(capturedSpec.Field);
                    };
                }
            }

            _config.Changed += OnConfigChangedForSlider;

            // Initialize overlap max based on current blksize
            if (_sliderMap.TryGetValue("degrain_blksize", out var blk))
                ClampOverlapToBlksize(SnapToNearest(blk.Slider.Value, ValidBlkSizes));
        }

        /// <summary>Snap a value to the nearest multiple of <paramref name="step"/> from <paramref name="min"/>.</summary>
        private static double SnapToStep(double value, double min, double step)
        {
            if (step <= 0) return value;
            return min + Math.Round((value - min) / step) * step;
        }

        private static void MoveSliderToPointer(Slider slider, PointerEventArgs e)
        {
            const double thumbHalf = 7.0;
            var w = slider.Bounds.Width;
            if (w <= thumbHalf * 2) return;
            var x     = e.GetCurrentPoint(slider).Position.X;
            var ratio = Math.Clamp((x - thumbHalf) / (w - thumbHalf * 2), 0.0, 1.0);
            var raw   = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
            slider.Value = SnapToStep(raw, slider.Minimum, slider.SmallChange);
        }

        private void OnSliderValueChanged(SliderSpec spec)
        {
            if (!_layoutInitialized || _sliderSync || _suppressTextEvents) return;
            if (!_sliderMap.TryGetValue(spec.Field, out var entry)) return;
            if (this.FindControl<TextBox>(spec.Field) is not { } tb) return;

            _sliderSync = true;
            try
            {
                var snapped = SnapToStep(entry.Slider.Value, spec.Min, spec.SmallChange);
                if (spec.Field == "degrain_blksize")
                    snapped = SnapToNearest(snapped, ValidBlkSizes);
                tb.Text = spec.IsFloat
                    ? snapped.ToString("F" + spec.Decimals, CultureInfo.InvariantCulture)
                    : ((int)Math.Round(snapped)).ToString();
            }
            finally { _sliderSync = false; }
        }


        /// <summary>Valid blksize values for MVTools2 MAnalyse (powers of 2).</summary>
        private static readonly int[] ValidBlkSizes = [4, 8, 16, 32, 64];

        /// <summary>Snap to nearest value in a sorted array.</summary>
        private static int SnapToNearest(double value, int[] validValues)
        {
            var best = validValues[0];
            var bestDist = Math.Abs(value - best);
            for (var i = 1; i < validValues.Length; i++)
            {
                var dist = Math.Abs(value - validValues[i]);
                if (dist < bestDist) { best = validValues[i]; bestDist = dist; }
            }
            return best;
        }

        private void CommitSliderField(string field)
        {
            if (!_sliderMap.TryGetValue(field, out var entry)) return;
            var snapped = SnapToStep(entry.Slider.Value, entry.Spec.Min, entry.Spec.SmallChange);
            snapped = Math.Clamp(snapped, entry.Spec.Min, entry.Spec.Max);

            // blksize: snap to nearest power of 2
            if (field == "degrain_blksize")
                snapped = SnapToNearest(snapped, ValidBlkSizes);

            entry.Slider.Value = snapped;
            var text = entry.Spec.IsFloat
                ? snapped.ToString("F" + entry.Spec.Decimals, CultureInfo.InvariantCulture)
                : ((int)Math.Round(snapped)).ToString();
            _ = ApplyFieldChangeAsync(field, text, showValidationError: true, refreshScriptPreview: false);

            // When blksize changes, cap overlap to blksize/2
            if (field == "degrain_blksize")
                ClampOverlapToBlksize((int)Math.Round(snapped));

            // Clear GamMac preset name when a GamMac parameter changes manually
            if (!_switchingClip && Array.IndexOf(EncodeController.GammacKeys, field) >= 0)
            {
                if (this.FindControl<ComboBox>("GammacPresetCombo") is { } gc)
                { gc.SelectedIndex = -1; gc.Text = null; }
                _config.Set("gammac_preset", string.Empty);
                if (ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count)
                    Clips[ActiveClipIndex].GammacPresetName = null;
            }
        }

        /// <summary>Ensures overlap ≤ blksize/2 and updates slider max accordingly.</summary>
        private void ClampOverlapToBlksize(int blksize)
        {
            if (!_sliderMap.TryGetValue("degrain_overlap", out var ov)) return;
            var maxOverlap = blksize / 2;
            ov.Slider.Maximum = maxOverlap;
            if (ov.Slider.Value > maxOverlap)
            {
                ov.Slider.Value = maxOverlap;
                CommitSliderField("degrain_overlap");
            }
        }

        private void OnConfigChangedForSlider(string key, string value)
        {
            if (!_sliderMap.TryGetValue(key, out var entry)) return;
            if (_sliderSync) return;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) return;
            var clamped = Math.Clamp(val, entry.Slider.Minimum, entry.Slider.Maximum);

            Dispatcher.UIThread.Post(() =>
            {
                if (Math.Abs(entry.Slider.Value - clamped) < 0.0001) return;
                _sliderSync = true;
                try { entry.Slider.Value = clamped; }
                finally { _sliderSync = false; }
            });
        }

        private void SyncAllSliders()
        {
            foreach (var (field, (slider, spec)) in _sliderMap)
            {
                var raw = _config.Get(field);
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) continue;
                var clamped = Math.Clamp(val, slider.Minimum, slider.Maximum);
                _sliderSync = true;
                try
                {
                    slider.Value = clamped;
                    // Also sync the TextBox (slider.Value setter won't fire ValueChanged if value unchanged)
                    if (this.FindControl<TextBox>(field) is { } tb)
                    {
                        tb.Text = spec.IsFloat
                            ? clamped.ToString("F" + spec.Decimals, CultureInfo.InvariantCulture)
                            : ((int)Math.Round(clamped)).ToString();
                    }
                }
                finally { _sliderSync = false; }
            }
        }

        // Positions the window BEFORE Show() is called, so the native window is
        // created directly at the right coordinates (no top-left flash on startup).
        private void PreApplyWindowPosition()
        {
            var saved = _windowStateService.Load();
            if (saved is null) return; // First launch: OnOpened handles it via SnapToBottomOfScreen

            WindowStartupLocation = WindowStartupLocation.Manual;
            Width    = ClampWindowWidth(saved.Width);
            Height   = Math.Clamp(saved.Height, MinHeight, MaxHeight);
            Position = new PixelPoint(saved.X, saved.Y);
            // IsSavedPositionVisible is validated later in ApplyStartupLayout (OnOpened).
            // If the position turns out to be off-screen, the window will correct itself
            // once on that session (rare: only after a screen-layout change).
        }

        private void InitializeChoiceFields()
        {
            SetComboSource("Sharp_Mode",        SharpModeOptions);
            SetComboSource("sharp_preset",      SharpPresetOptions);
            SetComboSource("degrain_preset",    DegrainPresetOptions);
            SetComboSource("degrain_mode",      DegrainModeOptions);
            SetComboSource("degrain_prefilter", DegrainPrefilterOptions);
            SetComboSource("denoise_preset",    DenoisePresetOptions);
            SetComboSource("denoise_mode",      DenoiseModeOptions);

            if (this.FindControl<ComboBox>("sharp_preset") is { } sharpPresetCombo)
                sharpPresetCombo.SelectedItem = "standard";
            if (this.FindControl<ComboBox>("degrain_preset") is { } presetCombo)
                presetCombo.SelectedItem = "standard";
            if (this.FindControl<ComboBox>("denoise_preset") is { } denoisePresetCombo)
                denoisePresetCombo.SelectedItem = "standard";

            // Persist default combo values to _config so they survive clip switching
            _config.Set("sharp_preset",      "standard");
            _config.Set("degrain_preset",    "standard");
            _config.Set("denoise_preset",    "standard");
            _config.Set("Sharp_Mode",        SharpModeOptions[0]);
            _config.Set("degrain_mode",      DegrainModeOptions[0]);
            _config.Set("degrain_prefilter", DegrainPrefilterOptions[0]);
            _config.Set("denoise_mode",      DenoiseModeOptions[0]);
        }

        private void SetComboSource(string name, string[] options)
        {
            if (this.FindControl<ComboBox>(name) is { } combo)
            {
                combo.ItemsSource  = options;
                combo.SelectedItem = options[0];
            }
        }

        // ── UIPI drag-drop fix for elevated processes ─────────────────────
        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial bool ChangeWindowMessageFilterEx(
            nint hwnd, uint message, uint action, nint pChangeFilterStruct);

        private const uint WM_DROPFILES_MSG      = 0x0233;
        private const uint WM_COPYGLOBALDATA_MSG  = 0x0049;
        private const uint MSGFLT_ALLOW_MSG       = 1;

        private void AllowDragDropThroughUipi()
        {
            var platformHandle = TryGetPlatformHandle();
            if (platformHandle is null) return;
            var hwnd = platformHandle.Handle;
            ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES_MSG, MSGFLT_ALLOW_MSG, 0);
            ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA_MSG, MSGFLT_ALLOW_MSG, 0);
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            AllowDragDropThroughUipi();
            _isInitializing = true;
            var settings = _windowStateService.Load();
            try
            {
                ViewModel.SetLanguage(settings?.Language ?? MainWindowViewModel.GetOsLanguageCodeOrEnglish());

                ApplyLanguage(ViewModel.CurrentLanguageCode, persist: false);
                _scriptService.EnsureScriptCopiesInOutputDir();
                ApplyConfigurationValues();
                RestoreSessionState(settings);

                // Restore saved session (clips + per-clip configs)
                RestoreSessionClips();

                // Rebuild batch clip list now that clips are loaded (RestoreSessionState may
                // have opened the Record panel before clips were available)
                if (_recordOpen)
                    RebuildBatchClipList();

                // Régénère toujours avec la bonne langue au démarrage (indépendamment de la validation source)
                _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
            }
            finally
            {
                _isInitializing = false;
            }

            if (TryValidateSourceSelection(out _))
                await LoadScriptAsync();

            Dispatcher.UIThread.Post(() => ApplyStartupLayout(settings), DispatcherPriority.Loaded);
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            _isClosing = true;
            _refreshDebouncer.Cancel();
            _config.Changed -= OnConfigChangedForSlider;

            // Kill any running encoding process (delegated to EncodeController — not needed,
            // but ensure encoding lock is released)
            // EncodeController's process is internal; it handles its own cleanup.

            SaveWindowSettings();
            SaveSession();
            _layoutInitialized = false;
            _playerController.Dispose();
        }

        #endregion

        #region Language & menu

        private void ConfigureMenuBar() => ApplyLanguage(ViewModel.CurrentLanguageCode, persist: false);

        private void OnLanguageClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: string code })
                ApplyLanguage(code, persist: true);
            CloseAllMenus();
        }

        private void ApplyLanguage(string languageCode, bool persist)
        {
            ViewModel.SetLanguage(languageCode);

            foreach (var (controlName, textKey) in new[]
            {
                ("InfosMenu",             "InfosMenu"),
                ("UserGuideMenuItem",     "UserGuideMenuItem"),
                ("ScriptPreviewMenuItem", "ScriptPreviewMenuItem"),
                ("GuidedTourMenuItem",   "GuidedTourMenuItem"),
                ("AboutMenuItem",         "AboutMenuItem"),
                ("FeedbackMenuItem",     "FeedbackMenuItem"),
                ("SettingsMenu",         "SettingsMenu"),
                ("ResetSettingsMenuItem", "ResetSettingsMenuItem"),
            })
            {
                if (this.FindControl<MenuItem>(controlName) is { } item)
                    item.Header = GetUiText(textKey);
            }

            if (this.FindControl<Button>("GlobalPresetBtn") is { } globalPresetBtn)
            {
                globalPresetBtn.Content = GetUiText("GlobalPresetButton");
                ToolTip.SetTip(globalPresetBtn, GetUiText("GlobalPresetTooltip"));
            }

            if (this.FindControl<MenuItem>("LanguagesMenu") is { } langMenu)
                langMenu.Header = languageCode.ToUpper();

            if (this.FindControl<TextBlock>("ThreadsLabel") is { } threadsLbl)
                threadsLbl.Text = GetUiText("ThreadsLabel");
            if (this.FindControl<TextBlock>("SourceLoaderLabel") is { } srcLbl)
                srcLbl.Text = GetUiText("SourceLoaderLabel");

            foreach (var expandName in new[] { "CropExpandBtn", "GammacExpandBtn", "DenoiseExpandBtn", "DegrainExpandBtn", "LumaExpandBtn", "SharpExpandBtn" })
            {
                if (this.FindControl<Button>(expandName) is { } expandBtn)
                    ToolTip.SetTip(expandBtn, GetUiText("ExpandBtnTooltip"));
            }

            SetLanguageMenuChecks();
            ApplyParamTooltips(languageCode);
            ApplyTransportTooltips();
            ApplyRecordLabels();
            ApplyThemeLabels();


            if (this.FindControl<TextBlock>("DropHintBar") is { IsVisible: true } dropBar)
                dropBar.Text = GetUiText("DropHintBar");

            if (this.FindControl<Button>("ImportCustomFilterBtn") is { } importBtn)
                ToolTip.SetTip(importBtn, GetUiText("CfDlgImportTitle"));

            if (persist && IsVisible)
            {
                _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
                SaveWindowSettings();
            }
        }

        private void ApplyRecordLabels()
        {
            if (this.FindControl<Button>("RecordBtn") is { } btn)
                btn.Content = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordOverlayTitle") is { } title)
                title.Text = "⏺ " + GetUiText("RecordBtn");
            if (this.FindControl<TextBlock>("RecordDirLabel") is { } dirLbl)
                dirLbl.Text = GetUiText("RecordDirLabel");
            if (this.FindControl<TextBlock>("RecordEncoderLabel") is { } encLbl)
                encLbl.Text = GetUiText("RecordEncoderLabel");
            if (this.FindControl<TextBlock>("RecordContainerLabel") is { } cntLbl)
                cntLbl.Text = GetUiText("RecordContainerLabel");
            if (this.FindControl<TextBlock>("RecordQualityModeLabel") is { } qmLbl)
                qmLbl.Text = GetUiText("RecordQualityModeLabel");
            if (this.FindControl<TextBlock>("RecordCrfLabel") is { } crfLbl)
                crfLbl.Text = GetUiText("RecordCrfLabel");
            if (this.FindControl<TextBlock>("RecordBitrateLabel") is { } brLbl)
                brLbl.Text = GetUiText("RecordBitrateLabel");
            if (this.FindControl<TextBlock>("RecordChromaLabel") is { } chLbl)
                chLbl.Text = GetUiText("RecordChromaLabel");
            if (this.FindControl<TextBlock>("RecordResizeLabel") is { } rsLbl)
                rsLbl.Text = GetUiText("RecordResizeLabel");
            if (this.FindControl<TextBlock>("RecordPresetLabel") is { } prLbl)
                prLbl.Text = GetUiText("RecordPresetLabel");
            if (this.FindControl<Button>("RecordPresetSaveBtn") is { } prSave)
                prSave.Content = GetUiText("RecordPresetSaveBtn");
            if (this.FindControl<Button>("RecordPresetDeleteBtn") is { } prDel)
                prDel.Content = GetUiText("RecordPresetDeleteBtn");
            if (this.FindControl<Button>("GammacPresetSaveBtn") is { } gmSave)
                gmSave.Content = GetUiText("RecordPresetSaveBtn");
            if (this.FindControl<Button>("GammacPresetDelBtn") is { } gmDel)
                gmDel.Content = GetUiText("RecordPresetDeleteBtn");
            if (this.FindControl<Button>("RecordStartBtn") is { } startBtn)
                startBtn.Content = GetUiText("RecordStartBtn");
            if (this.FindControl<CheckBox>("ShutdownCheckBox") is { } shutCb)
                shutCb.Content = GetUiText("ShutdownCheckBox");
            if (this.FindControl<TextBlock>("BatchClipListLabel") is { } batchLbl)
                batchLbl.Text = GetUiText("BatchClipListLabel");
            if (this.FindControl<CheckBox>("BatchSelectAllCheck") is { } allCb)
                allCb.Content = GetUiText("BatchSelectAll");
            if (this.FindControl<TextBlock>("BatchColOriginal") is { } colOrig)
                colOrig.Text = GetUiText("BatchColOriginal");
            if (this.FindControl<TextBlock>("BatchColRenamed") is { } colRenamed)
                colRenamed.Text = GetUiText("BatchColRenamed");
            if (this.FindControl<TextBlock>("BatchColPreset") is { } colPreset)
                colPreset.Text = GetUiText("BatchColPreset");
        }

        private void ApplyParamTooltips(string lang)
        {
            foreach (var (name, translations) in ParamTooltipTexts)
            {
                if (this.FindControl<TextBlock>(name) is not { } label) continue;
                var tip = translations.TryGetValue(lang, out var t) ? t
                        : translations.TryGetValue("en", out var en) ? en
                        : string.Empty;
                ToolTip.SetTip(label, tip);
            }
        }

        private void SetLanguageMenuChecks()
        {
            SetLanguageMenuItemChecked("LanguageEnglishMenuItem", "en");
            SetLanguageMenuItemChecked("LanguageFrenchMenuItem",  "fr");
            SetLanguageMenuItemChecked("LanguageGermanMenuItem",  "de");
            SetLanguageMenuItemChecked("LanguageSpanishMenuItem", "es");
        }

        private void SetLanguageMenuItemChecked(string menuName, string languageCode)
        {
            if (this.FindControl<MenuItem>(menuName) is not { } menuItem) return;

            var label = languageCode switch
            {
                "en" => "English",
                "fr" => "Français",
                "de" => "Deutsch",
                "es" => "Español",
                _    => languageCode
            };

            var isSelected = string.Equals(ViewModel.CurrentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase);
            menuItem.Header = isSelected ? $"✓ {label}" : label;
        }

        private string GetUiText(string key) => ViewModel.GetUiText(key);
        private string GetLocalizedText(string fr, string en) => ViewModel.GetLocalizedText(fr, en);

        private async void OnResetSettingsClick(object? sender, RoutedEventArgs e)
        {
            CloseSettingsMenu();

            // Confirmation dialog with Yes/No
            var result = false;
            var yesButton = new Button { Content = GetUiText("OkButton"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var noButton  = new Button { Content = GetUiText("GamMacCloseButton"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            var dialog = new Window
            {
                Title = GetUiText("ResetSettingsTitle"),
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = GetUiText("ResetSettingsConfirm"), TextWrapping = TextWrapping.Wrap, MaxWidth = 400 },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Children = { yesButton, noButton }
                        }
                    }
                }
            };

            yesButton.Click += (_, _) => { result = true; dialog.Close(); };
            noButton.Click  += (_, _) => dialog.Close();
            await dialog.ShowDialog(this);

            if (!result) return;

            // Delete entire AppData\CleanScan folder so the app leaves no trace
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder);
            try { if (Directory.Exists(appDataDir)) Directory.Delete(appDataDir, recursive: true); } catch { }

            // Restart application
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
            {
                System.Diagnostics.Process.Start(exePath);
                Environment.Exit(0);
            }
        }

        private void CloseSettingsMenu()
        {
            if (this.FindControl<MenuItem>("SettingsMenu") is { } menu)
                menu.Close();
        }

        private void CloseAllMenus()
        {
            if (this.FindControl<Menu>("MainMenu") is { } mainMenu)
                mainMenu.Close();
            if (this.FindControl<Menu>("MainMenuRight") is { } rightMenu)
                rightMenu.Close();
        }

        #endregion

        #region Theme

        private void InitTheme()
        {
            // Build accent swatch buttons
            if (this.FindControl<StackPanel>("AccentSwatchPanel") is { } panel)
            {
                foreach (var accent in ThemeService.AvailableAccents)
                {
                    var color = ThemeService.AccentSwatchColors[accent];
                    var btn = new Button
                    {
                        Tag = accent,
                        Width = 22,
                        Height = 22,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(11),
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color)),
                        BorderThickness = new Thickness(2),
                        BorderBrush = Avalonia.Media.Brushes.Transparent,
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                        Content = "",
                    };
                    btn.Click += OnAccentClick;
                    // Custom template: just a colored circle
                    btn.Template = new Avalonia.Controls.Templates.FuncControlTemplate<Button>((b, _) =>
                    {
                        var border = new Border
                        {
                            CornerRadius = new CornerRadius(11),
                            [!Border.BackgroundProperty] = b[!Button.BackgroundProperty],
                            [!Border.BorderBrushProperty] = b[!Button.BorderBrushProperty],
                            [!Border.BorderThicknessProperty] = b[!Button.BorderThicknessProperty],
                        };
                        return border;
                    });
                    panel.Children.Add(btn);
                }
            }

            ApplyTheme(_themeService.Theme, _themeService.Accent);
        }

        private void OnThemeClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string theme }) return;
            _themeService.SetTheme(theme);
            ApplyTheme(theme, _themeService.Accent);
            CloseSettingsMenu();
        }

        private void OnAccentClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string accent }) return;
            _themeService.SetAccent(accent);
            ApplyTheme(_themeService.Theme, accent);
            CloseSettingsMenu();
        }

        private void ApplyTheme(string theme, string accent)
        {
            var palette = ThemeService.GetPalette(theme, accent);

            // Update application-level resources so ALL windows inherit the palette
            if (Application.Current is { } app)
            {
                foreach (var (key, hex) in palette)
                    app.Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));

                // Update Avalonia theme variant (affects Fluent popup/menu surfaces)
                var variant = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
                    ? Avalonia.Styling.ThemeVariant.Light
                    : Avalonia.Styling.ThemeVariant.Dark; // Dark and Grey both use Dark variant
                app.RequestedThemeVariant = variant;
            }

            // Also set on this window for direct ThemeBrush() lookups
            foreach (var (key, hex) in palette)
                Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));

            // Update theme button visual states
            UpdateThemeButtonStates(theme);
            UpdateAccentSwatchStates(accent);
        }

        private void UpdateThemeButtonStates(string theme)
        {
            foreach (var name in new[] { "ThemeDarkBtn", "ThemeGreyBtn", "ThemeLightBtn" })
            {
                if (this.FindControl<Button>(name) is not { Tag: string tag } btn) continue;
                var active = string.Equals(tag, theme, StringComparison.OrdinalIgnoreCase);
                btn.Foreground = active ? ThemeBrush("TextLabel") : ThemeBrush("TextPrimary");
                btn.BorderBrush = active ? ThemeBrush("AccentBlue") : ThemeBrush("BorderSubtle");
            }
        }

        private void UpdateAccentSwatchStates(string accent)
        {
            if (this.FindControl<StackPanel>("AccentSwatchPanel") is not { } panel) return;
            foreach (var child in panel.Children)
            {
                if (child is not Button btn || btn.Tag is not string tag) continue;
                btn.BorderBrush = string.Equals(tag, accent, StringComparison.OrdinalIgnoreCase)
                    ? Avalonia.Media.Brushes.White
                    : Avalonia.Media.Brushes.Transparent;
            }
        }

        private SolidColorBrush ThemeBrush(string key) =>
            Resources.TryGetValue(key, out var val) && val is SolidColorBrush b
                ? b
                : new SolidColorBrush(Colors.Magenta);

        private void ApplyThemeLabels()
        {
            if (this.FindControl<TextBlock>("ThemeLabel") is { } lbl)
                lbl.Text = GetUiText("ThemeLabel");
            if (this.FindControl<TextBlock>("AccentLabel") is { } albl)
                albl.Text = GetUiText("AccentLabel");
            if (this.FindControl<Button>("ThemeDarkBtn") is { } darkBtn)
                darkBtn.Content = GetUiText("ThemeDark");
            if (this.FindControl<Button>("ThemeGreyBtn") is { } greyBtn)
                greyBtn.Content = GetUiText("ThemeGrey");
            if (this.FindControl<Button>("ThemeLightBtn") is { } lightBtn)
                lightBtn.Content = GetUiText("ThemeLight");
        }

        #endregion

        #region Window settings / layout

        private const int WindowBottomPadding = 8;

        private void ApplyStartupLayout(WindowSettings? saved = null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width  = GetStartupWidth(saved);
            Height = GetStartupHeight(saved);

            if (saved is { X: var sx, Y: var sy } && IsSavedPositionVisible(sx, sy))
                Position = new PixelPoint(sx, sy);
            else if (saved is null)
                CenterOnScreen();   // First launch → centre the window nicely
            else
                SnapToBottomOfScreen();

            if (saved?.BottomPanelHeight is { } bph)
                MainGrid.RowDefinitions[2].Height = new GridLength(Math.Clamp(bph, 60, 800), GridUnitType.Pixel);

            _layoutInitialized = true;

            if (saved?.TourCompleted != true && Clips.Count == 0)
                Dispatcher.UIThread.Post(() => _ = ShowGuidedTourAsync(), DispatcherPriority.Background);
        }

        private double GetStartupHeight(WindowSettings? saved)
        {
            if (saved is not null)
                return Math.Clamp(saved.Height, MinHeight, MaxHeight);

            return GetCompactStartupHeight();
        }

        private double GetStartupWidth(WindowSettings? saved)
        {
            if (saved is not null)
                return ClampWindowWidth(saved.Width);

            // First launch: use ~2/3 of screen width
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is not null)
            {
                var available = screen.WorkingArea.Width / screen.Scaling;
                var target = available * 2.0 / 3.0;
                return Math.Clamp(target, MinWidth, available);
            }
            return Width;
        }

        private double ClampWindowWidth(double width)
        {
            var maxWidth = double.IsFinite(MaxWidth) ? MaxWidth : double.MaxValue;
            return Math.Clamp(width, MinWidth, maxWidth);
        }

        private double GetCompactStartupHeight()
        {
            // First launch: use ~70% of screen height for a comfortable view
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is not null)
            {
                var available = screen.WorkingArea.Height / screen.Scaling;
                var target = available * 0.70;
                return Math.Clamp(target, MinHeight, available);
            }
            return MinHeight;
        }

        private void CenterOnScreen()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null) { SnapToBottomOfScreen(); return; }

            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            // Use Width/Height properties (Bounds may not be updated yet)
            var w = (int)Math.Ceiling(Width * scaling);
            var h = (int)Math.Ceiling(Height * scaling);

            var x = wa.X + Math.Max(0, (wa.Width  - w) / 2);
            var y = wa.Y + Math.Max(0, (wa.Height - h) / 2);
            Position = new PixelPoint(x, y);
        }

        private PixelPoint GetBottomAnchoredPosition(Screen screen, int requestedX)
        {
            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            var w  = (int)Math.Ceiling(Bounds.Width * scaling);
            var h  = Math.Min((int)Math.Ceiling(Height * scaling), wa.Height);

            var x = Math.Clamp(requestedX, wa.X, Math.Max(wa.X, wa.Right - w));
            var y = Math.Clamp(wa.Bottom - h - WindowBottomPadding, wa.Y, Math.Max(wa.Y, wa.Bottom - h));

            return new PixelPoint(x, y);
        }

        private WindowSettings? _lastGoodSettings;

        private void CaptureWindowSettings()
        {
            if (WindowState != WindowState.Normal) return;
            if (_isInitializing || !_layoutInitialized) return;
            var bottomH = BottomPanel.Bounds.Height is > 0 and var bh ? (double?)bh : null;
            _lastGoodSettings = new WindowSettings(Bounds.Width, Bounds.Height, Position.X, Position.Y, ViewModel.CurrentLanguageCode, bottomH);
        }

        private void SaveWindowSettings()
        {
            CaptureWindowSettings();
            if (_lastGoodSettings is { } s)
            {
                var panels = _openParamPanels.Count > 0 ? _openParamPanels.ToArray() : null;
                var lastDir = this.FindControl<TextBox>("RecordDir")?.Text?.Trim();
                var prevTour = _windowStateService.Load()?.TourCompleted;
                _windowStateService.Save(s with { Language = ViewModel.CurrentLanguageCode, OpenPanels = panels, LastOutputDir = lastDir, AutoSaveEncodingPreset = _encodeController.AutoSaveEncodingPreset ? true : null, RecordPanelOpen = _recordOpen ? true : null, TourCompleted = prevTour });
            }
        }

        private async void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing || WindowState != WindowState.Normal) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        private async void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing || WindowState != WindowState.Normal) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        private async void OnBottomPanelSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_layoutInitialized || _isInitializing || _isClosing) return;
            await _windowStateDebouncer.DebounceAsync(() =>
            {
                CaptureWindowSettings();
                return Task.CompletedTask;
            });
        }

        #endregion

        #region Configuration loading & UI binding

        private void ApplyConfigurationValues()
        {
            var scriptValues    = _scriptService.LoadScriptValues();
            var resourceManager = new ResourceManager("CleanScan.Resources.ConfigValues", typeof(MainWindow).Assembly);
            var newValues       = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in ScriptService.TextFieldNames)
            {
                var value = scriptValues.TryGetValue(name, out var sv) ? sv : resourceManager.GetString(name);
                if (value is null) continue;

                var isPath  = IsPathField(name);
                var uiValue = isPath ? _sourceService.NormalizeConfiguredPath(value) : value;

                if (this.FindControl<Control>(name) is TextBox tb)
                    SetTextSafely(tb, uiValue);
                else if (this.FindControl<Control>(name) is ComboBox cb)
                    uiValue = ApplyComboChoice(cb, name, uiValue);

                newValues[name] = uiValue;
            }

            var useImage = scriptValues.TryGetValue(UseImageConfigName, out var uiv)
                && bool.TryParse(uiv, out var parsedUseImage) && parsedUseImage;

            var legacySource = useImage
                ? (scriptValues.TryGetValue("img",  out var iv) ? iv : resourceManager.GetString("img"))
                : (scriptValues.TryGetValue("film", out var fv) ? fv : resourceManager.GetString("film"));

            if (!string.IsNullOrWhiteSpace(legacySource))
                SetDetectedSourceValue(legacySource, newValues);

            UpdateSourceSelection(isFilmSelected: !useImage, updateConfig: false, currentValues: newValues);

            foreach (var name in ScriptService.BoolFieldNames)
            {
                var raw = scriptValues.TryGetValue(name, out var bsv) ? bsv : resourceManager.GetString(name);
                if (raw is null || !bool.TryParse(raw, out var parsed)) continue;
                SetOptionToggleValue(name, parsed);
                newValues[name] = parsed.ToString().ToLowerInvariant();
            }

            UpdateOptionColumnVisibility();
            _config.ReplaceAll(newValues);
            SyncAllSliders();
            SyncForceSourceCombo(newValues);
        }

        private void RestoreSessionState(WindowSettings? settings)
        {
            // Restore half-res button visual from config
            _playerController.RestoreHalfResVisual();

            // Restore expanded filter panels
            if (settings?.OpenPanels is { Length: > 0 } panels)
            {
                foreach (var panelName in panels)
                {
                    if (!IsParamPanelEnabled(panelName)) continue;
                    _openParamPanels.Add(panelName);
                    if (FindPanelByName(panelName) is { } panel) panel.IsVisible = true;

                    // Update the matching expand button
                    var btnName = panelName.Replace("Params", "ExpandBtn");
                    if (this.FindControl<Button>(btnName) is { } expandBtn)
                    {
                        expandBtn.Content = "▶";
                        expandBtn.Classes.Add("active");
                    }
                }
                UpdateParamsPlaceholderVisibility();
            }

            // Restore last output directory
            if (!string.IsNullOrWhiteSpace(settings?.LastOutputDir))
            {
                if (this.FindControl<TextBox>("RecordDir") is { } dirTb)
                    dirTb.Text = settings.LastOutputDir;
            }

            // Restore "auto-save encoding preset" preference
            if (settings?.AutoSaveEncodingPreset == true)
                _encodeController.AutoSaveEncodingPreset = true;

            // Restore Record panel visibility
            if (settings?.RecordPanelOpen == true)
            {
                _recordOpen = true;
                if (this.FindControl<Button>("RecordBtn") is { } recBtn)
                {
                    recBtn.Background = new SolidColorBrush(Color.Parse("#C62828"));
                    recBtn.Foreground = Brushes.White;
                }
                if (this.FindControl<Border>("RecordOverlay") is { } overlay)
                    overlay.IsVisible = true;
                RebuildBatchClipList();
                UpdateDiskSpaceLabel(this.FindControl<TextBox>("RecordDir")?.Text);
            }
        }

        /// <summary>Called by App on unhandled exceptions to save session state before crash.</summary>
        public void EmergencySaveSession()
        {
            try { SaveSession(); } catch { }
        }

        private void SaveSession()
        {
            // Ensure the active clip's config is up to date
            _clipManager.SaveActiveConfig();

            var clips = new List<ClipSession>();
            for (int i = 0; i < Clips.Count; i++)
            {
                var c = Clips[i];
                clips.Add(new ClipSession(
                    Path:               c.Path,
                    FilterConfig:       c.Config,
                    PresetName:         c.PresetName,
                    BatchSelected:      c.BatchSelected,
                    BatchEncodingPreset: c.BatchEncodingPreset,
                    OutputName:         c.OutputName));
            }

            var encPresetName = (this.FindControl<ComboBox>("RecordPresetCombo")?.SelectedItem as string)?.Trim();
            _sessionService.Save(new SessionState(ActiveClipIndex, clips, _encodeController.CaptureCurrentEncodingValues(), encPresetName));
        }

        private void RestoreSessionClips()
        {
            var session = _sessionService.Load();
            if (session?.Clips is not { Count: > 0 } clips) return;

            // Filter out clips whose source files no longer exist
            var validClips = new List<(ClipSession Clip, int OriginalIndex)>();
            for (int i = 0; i < clips.Count; i++)
            {
                var clipPath = clips[i].Path;
                if (File.Exists(clipPath)
                    || _sourceService.IsImageSource(clipPath) && Directory.Exists(Path.GetDirectoryName(clipPath)))
                    validClips.Add((clips[i], i));
            }
            if (validClips.Count == 0) return;

            // Rebuild clip state from session
            Clips.Clear();

            foreach (var (clip, _) in validClips)
            {
                Clips.Add(new ClipState
                {
                    Path = clip.Path,
                    Config = new Dictionary<string, string>(clip.FilterConfig, StringComparer.OrdinalIgnoreCase),
                    PresetName = clip.PresetName,
                    OutputName = clip.OutputName,
                    BatchSelected = clip.BatchSelected,
                    BatchEncodingPreset = clip.BatchEncodingPreset,
                });
            }

            // Determine the active clip index
            var targetIndex = session.ActiveClipIndex;
            var newIndex = validClips.FindIndex(v => v.OriginalIndex == targetIndex);
            if (newIndex < 0) newIndex = 0;
            ActiveClipIndex = newIndex;

            // Restore active clip's filter config into _config and UI
            RestoreClipConfig(ActiveClipIndex);

            // Set source directly (without going through ApplyDetectedSourceAndRefreshAsync
            // which calls AddOrActivateClip and would corrupt the restored clip lists)
            var sourcePath = Clips[ActiveClipIndex].Path;
            var normalized = _sourceService.NormalizeConfiguredPath(sourcePath);
            _config.Set("source", normalized);
            _config.Set("film",   normalized);
            _config.Set("img",    normalized);
            if (this.FindControl<TextBox>("source") is { } srcTb)
                SetTextSafely(srcTb, normalized);

            // Detect film vs image mode
            var isImage = _sourceService.IsImageSource(normalized);
            UpdateSourceSelection(isFilmSelected: !isImage);

            // Rebuild UI
            RestoreClipPresetCombo();
            RebuildClipTabs();

            // Restore encoding parameters
            if (session.EncodingValues is { Count: > 0 } encVals)
                _encodeController.ApplyCurrentEncodingValues(encVals);

            // Restore encoding preset combo selection
            if (!string.IsNullOrWhiteSpace(session.EncodingPresetName)
                && this.FindControl<ComboBox>("RecordPresetCombo") is { } encCombo)
            {
                _encodeController.RefreshEncodingPresetCombo();
                encCombo.SelectedItem = session.EncodingPresetName;
            }
        }

        private static bool IsPathField(string name) =>
            string.Equals(name, "source", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "film", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "img",  StringComparison.OrdinalIgnoreCase);

        private static string ApplyComboChoice(ComboBox cb, string name, string rawValue)
        {
            if (string.Equals(name, "Sharp_Mode",    StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, SharpModeOptions);   return cb.SelectedItem?.ToString() ?? SharpModeOptions[0]; }
            if (string.Equals(name, "degrain_mode",      StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DegrainModeOptions);      return cb.SelectedItem?.ToString() ?? DegrainModeOptions[0]; }
            if (string.Equals(name, "degrain_prefilter", StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DegrainPrefilterOptions); return cb.SelectedItem?.ToString() ?? DegrainPrefilterOptions[0]; }
            if (string.Equals(name, "denoise_mode",  StringComparison.OrdinalIgnoreCase)) { SetComboBoxChoice(cb, rawValue, DenoiseModeOptions);  return cb.SelectedItem?.ToString() ?? DenoiseModeOptions[0]; }
            cb.SelectedItem = rawValue;
            return rawValue;
        }

        #endregion

        #region Change handlers registration

        private void RegisterChangeHandlers()
        {
            foreach (var spec in FieldSpecs)
            {
                if (this.FindControl<Control>(spec.Name) is TextBox textBox)
                {
                    RegisterTextBoxHandler(textBox, spec);
                    continue;
                }

                if (this.FindControl<Control>(spec.Name) is ComboBox combo)
                {
                    combo.SelectionChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, combo.SelectedItem?.ToString() ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: false);
                    };
                }
            }

            if (this.FindControl<ComboBox>("sharp_preset") is { } sharpPresetHandler)
            {
                sharpPresetHandler.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (sharpPresetHandler.SelectedItem is string preset)
                        ApplySharpPreset(preset);
                };
            }

            if (this.FindControl<ComboBox>("degrain_preset") is { } presetCombo)
            {
                presetCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (presetCombo.SelectedItem is string preset)
                        ApplyDegrainPreset(preset);
                };
            }

            if (this.FindControl<ComboBox>("denoise_preset") is { } denoisePresetCombo)
            {
                denoisePresetCombo.SelectionChanged += (_, _) =>
                {
                    if (_suppressTextEvents) return;
                    if (denoisePresetCombo.SelectedItem is string preset)
                        ApplyDenoisePreset(preset);
                };
            }

            foreach (var name in ScriptService.BoolFieldNames)
            {
                if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) continue;
                btn.Tag = false;
                UpdateToggleButtonPresentation(btn, isEnabled: false);
            }

            if (this.FindControl<TextBox>("threads") is { } threadsTextBox)
            {
                threadsTextBox.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
                {
                    threadsTextBox.Focus();
                    e.Handled = true;
                }, RoutingStrategies.Tunnel, handledEventsToo: true);

                threadsTextBox.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                        CloseSettingsMenu();
                };
                threadsTextBox.LostFocus += (_, _) => CloseSettingsMenu();
            }

            RegisterPathPickers();
        }

        private void RegisterTextBoxHandler(TextBox textBox, FieldSpec spec)
        {
            switch (spec.Mode)
            {
                case UpdateMode.Debounced:
                    textBox.TextChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: false);
                    };
                    break;

                case UpdateMode.OnLostFocus:
                    textBox.LostFocus += async (_, _) =>
                    {
                        if (_suppressTextEvents) return;
                        if (spec.Name.Equals("source", StringComparison.OrdinalIgnoreCase))
                        {
                            var raw = textBox.Text ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                UpdateSourceSelection(isFilmSelected: _sourceService.IsVideoSource(raw), updateConfig: true);
                                SetDetectedSourceValue(NormalizeSourceValue(raw));
                            }
                        }
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: true, refreshScriptPreview: true);
                    };
                    break;

                case UpdateMode.OnEnter:
                    textBox.KeyDown += async (_, e) =>
                    {
                        if (e.Key != Key.Enter || _suppressTextEvents) return;
                        e.Handled = true;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: true, refreshScriptPreview: true);
                        Focus();
                    };
                    break;

                case UpdateMode.Immediate:
                    textBox.TextChanged += async (_, _) =>
                    {
                        if (_suppressTextEvents || _sliderSync) return;
                        await ApplyFieldChangeAsync(spec.Name, textBox.Text ?? string.Empty,
                            showValidationError: spec.ValidateOnChange, refreshScriptPreview: true);
                    };
                    break;
            }
        }

        private void ApplySharpPreset(string preset)
        {
            if (!SharpPresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("sharp_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        private void ApplyDegrainPreset(string preset)
        {
            if (!DegrainPresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("degrain_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        private void ApplyDenoisePreset(string preset)
        {
            if (!DenoisePresets.TryGetValue(preset, out var values)) return;

            _applyingPreset = true;
            _suppressTextEvents = true;
            try
            {
                foreach (var kv in values)
                {
                    var ctrl = this.FindControl<Control>(kv.Key);
                    if (ctrl is TextBox tb)
                        tb.Text = kv.Value;
                    else if (ctrl is ComboBox cb)
                        cb.SelectedItem = kv.Value;
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }

            foreach (var kv in values)
                UpdateConfigurationValue(kv.Key, kv.Value, showValidationError: false);
            _config.Set("denoise_preset", preset);
            _applyingPreset = false;
            MarkClipAsPerso();
        }

        /// <summary>Renames the active clip to the next "persoN" if it isn't already one.</summary>
        private void MarkClipAsPerso()
        {
            if (ActiveClipIndex < 0 || ActiveClipIndex >= Clips.Count) return;
            var currentName = Clips[ActiveClipIndex].PresetName;
            if (currentName is not null && currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase)) return;
            Clips[ActiveClipIndex].PresetName = _clipManager.GetNextPersoName();
            RestoreClipPresetCombo();
            RebuildClipTabs();
        }

        private void RegisterPathPickers()
        {
            if (this.FindControl<Border>("ClipTabsContainer") is { } container)
            {
                container.AddHandler(DragDrop.DragOverEvent, OnSourceDragOver, RoutingStrategies.Bubble);
                container.AddHandler(DragDrop.DropEvent,     OnSourceDrop,     RoutingStrategies.Bubble);
            }
        }


        #endregion

        #region Source management

        private async Task ApplyDetectedSourceAndRefreshAsync(string rawValue, bool skipLoad = false)
        {
            rawValue ??= string.Empty;
            SetDetectedSourceValue(NormalizeSourceValue(rawValue));

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                _playerController.Unload();
                RegenerateScript(showValidationError: false);
                return;
            }

            // Stop mpv playback and reset player state for the new clip.
            _playerController.ResetPlayerState();

            // Reset crop values to 0.
            _suppressTextEvents = true;
            _sliderSync = true; // Block OnConfigChangedForSlider from posting stale async updates
            try
            {
                foreach (var cropField in new[] { "Crop_L", "Crop_T", "Crop_R", "Crop_B" })
                {
                    _config.Set(cropField, "0");
                    if (this.FindControl<TextBox>(cropField) is { } tb)
                        tb.Text = "0";
                }
            }
            finally
            {
                _sliderSync = false;
                _suppressTextEvents = false;
            }
            SyncAllSliders();

            var isFilm = _sourceService.IsVideoSource(rawValue);
            UpdateSourceSelection(isFilmSelected: isFilm, updateConfig: true);

            if (!isFilm)
            {
                var dir = Path.GetDirectoryName(_sourceService.NormalizeConfiguredPath(rawValue));
                if (!string.IsNullOrWhiteSpace(dir))
                    UpdateImageRangeFields(dir);
            }

            RegenerateScript(showValidationError: !skipLoad);

            if (skipLoad) return;

            if (TryValidateSourceSelection(out var msg))
            {
                _refreshDebouncer.Cancel();
                await LoadScriptAsync(resetPosition: true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(msg))
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), msg);
        }

        private void OnSourceDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedFilePaths(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnSourceDrop(object? sender, DragEventArgs e)
        {
            if (_isDroppingFiles) return;
            var paths = GetDroppedFilePaths(e);
            if (paths.Count == 0)
            {
                await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), GetUiText("DropInvalidFileType"));
                return;
            }

            _isDroppingFiles = true;
            try
            {
                // Activate the first dropped file
                await ApplyDetectedSourceAndRefreshAsync(paths[0]);

                // Add remaining dropped files without activating
                for (int i = 1; i < paths.Count; i++)
                {
                    var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(paths[i]));
                    if (!Clips.Any(c => string.Equals(c.Path, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        Clips.Add(new ClipState { Path = normalized, Config = _clipManager.CaptureConfig() });
                    }
                }
                if (paths.Count > 1)
                    RebuildClipTabs();
            }
            catch (Exception ex) { DebugLog($"OnSourceDrop error: {ex.Message}"); }
            finally { _isDroppingFiles = false; }
        }

        private async void OnPlayerFilesDropped(List<string> paths)
        {
            if (_isDroppingFiles) return;
            var valid = new List<string>();
            foreach (var p in paths)
            {
                // Directory dropped → find first image inside
                if (Directory.Exists(p))
                {
                    var firstImage = FindFirstImageInDirectory(p);
                    if (firstImage is not null)
                        valid.Add(firstImage);
                    continue;
                }
                var ext = Path.GetExtension(p);
                if (!string.IsNullOrWhiteSpace(ext) &&
                    (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                     ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)))
                    valid.Add(p);
            }

            if (valid.Count == 0) return;

            _isDroppingFiles = true;
            try
            {
                // Activate the first dropped file
                await ApplyDetectedSourceAndRefreshAsync(valid[0]);

                // Add remaining files without activating
                for (int i = 1; i < valid.Count; i++)
                {
                    var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(valid[i]));
                    if (!Clips.Any(c => string.Equals(c.Path, normalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        Clips.Add(new ClipState { Path = normalized, Config = _clipManager.CaptureConfig() });
                    }
                }
                if (valid.Count > 1)
                    RebuildClipTabs();
            }
            catch (Exception ex) { DebugLog($"OnPlayerFilesDropped error: {ex.Message}"); }
            finally { _isDroppingFiles = false; }
        }

        private static List<string> GetDroppedFilePaths(DragEventArgs e)
        {
#pragma warning disable CS0618
            if (!e.Data.Contains(DataFormats.Files)) return [];
            var items = e.Data.GetFiles()?.ToList();
#pragma warning restore CS0618
            if (items is null || items.Count == 0) return [];

            var paths = new List<string>();
            foreach (var item in items)
            {
                var path = item.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path)) continue;

                // Directory dropped → find first image inside
                if (Directory.Exists(path))
                {
                    var firstImage = FindFirstImageInDirectory(path);
                    if (firstImage is not null)
                        paths.Add(firstImage);
                    continue;
                }

                var ext = Path.GetExtension(path);
                if (!string.IsNullOrWhiteSpace(ext) &&
                    (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ||
                     ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)))
                    paths.Add(path);
            }
            return paths;
        }

        /// <summary>Scan a directory for the first image file (sorted by name) matching supported extensions.</summary>
        private static string? FindFirstImageInDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return null;
            return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return !string.IsNullOrWhiteSpace(ext)
                        && ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }


        private static IReadOnlyList<FilePickerFileType> BuildSourceFileTypeFilter(string? currentValue)
        {
            var dir = GetDirectoryPath(currentValue);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return [VideoFileType, ImageFileType];

            bool hasTiff = false, hasVideo = false, hasImage = false;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (string.IsNullOrWhiteSpace(ext)) continue;

                if (ext.Equals(".tif",  StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase)) { hasTiff = true; hasImage = true; }
                else if (VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) hasVideo = true;
                else if (ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) hasImage = true;

                if ((hasTiff || hasImage) && hasVideo) break;
            }

            return hasTiff || (hasImage && !hasVideo)
                ? [ImageFileType, VideoFileType]
                : [VideoFileType, ImageFileType];
        }

        private static string? GetDirectoryPath(string? currentValue)
        {
            var path = currentValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Directory.Exists(path)) return path;
            if (File.Exists(path)) return Path.GetDirectoryName(path);
            var parent = Path.GetDirectoryName(path);
            return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) ? parent : null;
        }

        private void UpdateSourceSelection(bool isFilmSelected, bool updateConfig = true,
            Dictionary<string, string>? currentValues = null)
        {
            var useImageValue = (!isFilmSelected).ToString().ToLowerInvariant();

            SetPanelVisibility("img_start_panel", !isFilmSelected);
            SetPanelVisibility("img_end_panel",   !isFilmSelected);
            SetPanelVisibility("play_speed_panel", isFilmSelected);

            if (updateConfig)
            {
                _config.Set(UseImageConfigName, useImageValue);
            }
            else
            {
                currentValues?[UseImageConfigName] = useImageValue;
            }
        }

        private void SetPanelVisibility(string name, bool visible)
        {
            if (this.FindControl<StackPanel>(name) is { } p)
                p.IsVisible = visible;
        }

        private void SetDetectedSourceValue(string rawValue, Dictionary<string, string>? currentValues = null)
        {
            var normalized = _sourceService.NormalizeConfiguredPath(rawValue);

            if (currentValues is not null)
            {
                currentValues["source"] = normalized;
                currentValues["film"]   = normalized;
                currentValues["img"]    = normalized;
            }
            else
            {
                _config.Set("source", normalized);
                _config.Set("film",   normalized);
                _config.Set("img",    normalized);
            }

            if (this.FindControl<TextBox>("source") is { } tb)
                SetTextSafely(tb, normalized);

            AddOrActivateClip(normalized);
        }

        private void AddOrActivateClip(string path)
        {
            _clipManager.AddOrActivate(path, _clipManager.CaptureConfig());
            RebuildClipTabs();
            if (_recordOpen) RebuildBatchClipList();
        }

        // CaptureClipConfig() and SaveActiveClipConfig() moved to ClipManager

        /// <summary>Restores a clip's filter config into _config and refreshes all UI controls.</summary>
        private void RestoreClipConfig(int index)
        {
            if (index < 0 || index >= Clips.Count) return;
            var clipCfg = Clips[index].Config;

            _suppressTextEvents = true;
            _applyingPreset = true;
            _sliderSync = true; // Block OnConfigChangedForSlider from posting stale async updates
            try
            {
                foreach (var name in ScriptService.TextFieldNames)
                {
                    if (PresetService.ExcludedKeys.Contains(name)) continue;
                    if (!clipCfg.TryGetValue(name, out var value)) continue;

                    if (this.FindControl<Control>(name) is TextBox tb) tb.Text = value;
                    else if (this.FindControl<Control>(name) is ComboBox cb) ApplyComboChoice(cb, name, value);

                    _config.Set(name, value);
                }

                foreach (var name in ScriptService.BoolFieldNames)
                {
                    if (!clipCfg.TryGetValue(name, out var v) || !bool.TryParse(v, out var parsed)) continue;
                    SetOptionToggleValue(name, parsed);
                    _config.Set(name, parsed.ToString().ToLowerInvariant());
                }

                // Restore filter preset combo selections AND their config values
                foreach (var presetKey in new[] { "sharp_preset", "degrain_preset", "denoise_preset" })
                {
                    var presetVal = clipCfg.TryGetValue(presetKey, out var pv) ? pv : string.Empty;
                    _config.Set(presetKey, presetVal);

                    if (this.FindControl<ComboBox>(presetKey) is { } presetCombo)
                    {
                        if (!string.IsNullOrEmpty(presetVal))
                            presetCombo.SelectedItem = presetVal;
                        else
                            presetCombo.SelectedIndex = -1;
                    }
                }

                // Restore GammacPresetCombo the same way (config key → combo)
                {
                    var gVal = clipCfg.TryGetValue("gammac_preset", out var gv) ? gv : string.Empty;
                    if (string.IsNullOrEmpty(gVal))
                        gVal = Clips[index].GammacPresetName ?? string.Empty;

                    _config.Set("gammac_preset", gVal);
                    _encodeController.RestoreGammacPresetSelection(!string.IsNullOrEmpty(gVal) ? gVal : null);
                }

                // Restore custom filter config keys (cf_*) so dynamic panels pick up correct values
                foreach (var (key, value) in clipCfg)
                {
                    if (key.StartsWith("cf_", StringComparison.OrdinalIgnoreCase))
                        _config.Set(key, value);
                }
            }
            finally
            {
                _sliderSync = false;
                _suppressTextEvents = false;
                _applyingPreset = false;
            }

            SyncAllSliders();
            SyncAllCombos();
            UpdateOptionColumnVisibility();

            // Rebuild custom filter param panels so ComboBoxes reflect restored values
            RebuildCustomFilterUI();
        }

        /// <summary>Re-applies all ComboBox selections from _config after a clip restore.</summary>
        private void SyncAllCombos()
        {
            _suppressTextEvents = true;
            try
            {
                // Standard filter combos (degrain_mode, degrain_prefilter, denoise_mode, Sharp_Mode)
                foreach (var name in ScriptService.TextFieldNames)
                {
                    if (this.FindControl<Control>(name) is not ComboBox cb) continue;
                    var value = _config.Get(name);
                    if (!string.IsNullOrEmpty(value))
                        ApplyComboChoice(cb, name, value);
                }

                // Filter preset combos
                foreach (var presetKey in new[] { "sharp_preset", "degrain_preset", "denoise_preset" })
                {
                    if (this.FindControl<ComboBox>(presetKey) is not { } presetCombo) continue;
                    var value = _config.Get(presetKey);
                    if (!string.IsNullOrEmpty(value))
                        presetCombo.SelectedItem = value;
                    else
                        presetCombo.SelectedIndex = -1;
                }

                // GammacPresetCombo
                {
                    var gVal = _config.Get("gammac_preset");
                    _encodeController.RestoreGammacPresetSelection(!string.IsNullOrEmpty(gVal) ? gVal : null);
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }
        }

        private void RebuildClipTabs()
        {
            if (this.FindControl<WrapPanel>("ClipTabsPanel") is not { } panel) return;

            // Keep only the "+" button (last child)
            var addBtn = this.FindControl<Button>("AddClipBtn");
            panel.Children.Clear();

            for (int i = 0; i < Clips.Count; i++)
            {
                var index = i;
                var path = Clips[i].Path;
                var filename = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(filename)) filename = path;
                var isActive = i == ActiveClipIndex;

                var presetName = Clips[i].PresetName;
                var presetSuffix = presetName is not null
                    ? $"  [{presetName}]"
                    : string.Empty;

                var label = new TextBlock
                {
                    Text = filename + presetSuffix,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (isActive)
                    label.Foreground = Brushes.White;

                var closeBtn = new Button
                {
                    Content = "\u00d7",
                    FontSize = 12,
                    Background = Brushes.Transparent,
                    Foreground = isActive
                        ? new SolidColorBrush(Color.Parse("#FFFFFFA0"))
                        : ThemeBrush("TextPrimary"),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 0, 0, 0),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 0,
                    MinHeight = 0,
                };
                closeBtn.Click += (_, _) => RemoveClip(index);

                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                };
                stack.Children.Add(label);
                stack.Children.Add(closeBtn);

                var tab = new Border
                {
                    Background = isActive
                        ? ThemeBrush("AccentBlue")
                        : ThemeBrush("BgInput"),
                    BorderBrush = isActive
                        ? new SolidColorBrush(Color.Parse("#4A9AD4"))
                        : new SolidColorBrush(Color.Parse("#3A4660")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 4),
                    Margin = new Thickness(0, 0, 6, 4),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = stack,
                };
                tab.PointerPressed += (_, _) => SwitchToClip(index);
                ToolTip.SetTip(tab, path);

                panel.Children.Add(tab);
            }

            // Re-add the "+" button at the end
            if (addBtn is not null)
                panel.Children.Add(addBtn);

            UpdateActivePresetNameDisplay();
        }

        private async void SwitchToClip(int index)
        {
            if (index < 0 || index >= Clips.Count || index == ActiveClipIndex) return;

            if (_switchingClip)
            {
                // A switch is already in progress — remember the latest request
                // so we jump to it once the current switch finishes.
                _pendingClipSwitch = index;
                return;
            }

            _switchingClip = true;
            try
            {
                // Save current clip's filter config (includes all preset keys in _config)
                _clipManager.SaveActiveConfig();

                // Switch source (this sets ActiveClipIndex via AddOrActivateClip).
                // skipLoad: true — don't load the script yet; we'll restore the
                // clip's config first, regenerate once, and load once.  This avoids
                // a double write+load of ScriptUser.avs that can segfault when
                // AviSynth is still reading the first version.
                await ApplyDetectedSourceAndRefreshAsync(Clips[index].Path, skipLoad: true);

                // Restore the target clip's filter config (includes all filter preset combos + Gammac)
                RestoreClipConfig(ActiveClipIndex);

                // Restore per-clip preset selection
                RestoreClipPresetCombo();

                RegenerateScript(showValidationError: false);

                if (TryValidateSourceSelection(out _))
                    await LoadScriptAsync(resetPosition: true);

            }
            finally
            {
                // Defer resetting _switchingClip so that async continuations
                // (e.g. LostFocus handlers on TextBoxes that fire during the await)
                // still see _switchingClip=true and don't trigger preset deselection.
                // Also re-sync combos at Background priority: this runs AFTER any stale
                // async continuations (which resume at Normal priority) that may have
                // cleared a preset combo despite the _switchingClip guard.
                Dispatcher.UIThread.Post(() =>
                {
                    _switchingClip = false;
                    SyncAllCombos();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }

            // If the user clicked another clip while we were switching,
            // honour the last requested index now.
            if (_pendingClipSwitch >= 0)
            {
                var next = _pendingClipSwitch;
                _pendingClipSwitch = -1;
                SwitchToClip(next);
            }
        }

        private void UpdateActivePresetNameDisplay()
        {
            if (this.FindControl<TextBox>("ActivePresetNameBox") is not { } box) return;
            var name = ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count
                ? Clips[ActiveClipIndex].PresetName
                : null;

            var isPerso = !string.IsNullOrWhiteSpace(name)
                && name.StartsWith("perso", StringComparison.OrdinalIgnoreCase);
            box.Text = string.IsNullOrWhiteSpace(name) || isPerso ? string.Empty : name;
        }

        /// <summary>Restores the per-clip preset ComboBox selection without triggering the change handler.</summary>
        private void RestoreClipPresetCombo()
        {
            if (this.FindControl<ComboBox>("ClipPresetCombo") is not { } combo) return;
            _suppressClipPresetChange = true;
            try
            {
                var presetName = ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count
                    ? Clips[ActiveClipIndex].PresetName
                    : null;

                var isPerso = presetName?.StartsWith("perso", StringComparison.OrdinalIgnoreCase) == true;

                var presets = _presetService.LoadPresets()
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(p => p.Name)
                    .ToList();
                combo.ItemsSource = presets;

                if (presetName is not null && !isPerso && presets.Contains(presetName, StringComparer.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = presetName;
                    combo.PlaceholderText = null;
                }
                else
                {
                    combo.SelectedIndex = -1;
                    combo.PlaceholderText = presetName; // shows "perso2" as placeholder
                }
            }
            finally { _suppressClipPresetChange = false; }

            UpdateActivePresetNameDisplay();
        }

        private async void RemoveClip(int index)
        {
            var result = _clipManager.Remove(index);
            if (!result.Removed) return;

            if (Clips.Count == 0)
            {
                RebuildClipTabs();
                if (_recordOpen) RebuildBatchClipList();
                await ApplyDetectedSourceAndRefreshAsync(string.Empty);
                return;
            }

            if (result.WasActive)
            {
                await ApplyDetectedSourceAndRefreshAsync(Clips[ActiveClipIndex].Path);
                RestoreClipConfig(ActiveClipIndex);
                RestoreClipPresetCombo();
                RegenerateScript(showValidationError: false);
                if (TryValidateSourceSelection(out _))
                    await LoadScriptAsync();
            }
            else
            {
                RebuildClipTabs();
            }

            if (_recordOpen) RebuildBatchClipList();
        }

        private async void OnAddClipClick(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider is not { } sp) return;

            var currentSource = _config.Get("source");
            var suggestedLocation = await GetSuggestedStartLocationAsync(sp, currentSource);
            var filter = BuildSourceFileTypeFilter(currentSource);
            var results = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = GetUiText("PickSourceTitle"),
                AllowMultiple = true,
                SuggestedStartLocation = suggestedLocation,
                FileTypeFilter = filter
            });

            if (results.Count == 0) return;

            var newPaths = new List<string>();
            foreach (var file in results)
            {
                var filePath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(filePath)) continue;
                var displayPath = _sourceService.IsImageSource(filePath)
                    ? _sourceService.BuildImageSequenceSourcePath(filePath)
                    : filePath;
                newPaths.Add(displayPath);
            }

            if (newPaths.Count == 0) return;

            // Activate the first selected file
            await ApplyDetectedSourceAndRefreshAsync(newPaths[0]);

            // Add remaining files without activating
            for (int i = 1; i < newPaths.Count; i++)
            {
                var normalized = _sourceService.NormalizeConfiguredPath(NormalizeSourceValue(newPaths[i]));
                _clipManager.AddOrActivate(normalized, _clipManager.CaptureConfig());
            }
            if (newPaths.Count > 1)
                RebuildClipTabs();
        }

        private void UpdateImageRangeFields(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

            int? min = null, max = null;
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (string.IsNullOrWhiteSpace(ext)
                    || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var stem = Path.GetFileNameWithoutExtension(file);
                if (!NumericStemRegex().IsMatch(stem) || !int.TryParse(stem, out var v)) continue;

                min = min.HasValue ? Math.Min(min.Value, v) : v;
                max = max.HasValue ? Math.Max(max.Value, v) : v;
            }

            if (min.HasValue && this.FindControl<TextBox>("img_start") is { } s) SetTextSafely(s, min.Value.ToString());
            if (max.HasValue && this.FindControl<TextBox>("img_end")   is { } e) SetTextSafely(e, max.Value.ToString());
        }

        private static async Task<IStorageFolder?> GetSuggestedStartLocationAsync(IStorageProvider sp, string? currentValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue)) return null;
            var path = File.Exists(currentValue) ? Path.GetDirectoryName(currentValue) : currentValue;
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
                ? await sp.TryGetFolderFromPathAsync(path)
                : null;
        }

        private string NormalizeSourceValue(string rawValue)
        {
            var normalized = _sourceService.NormalizeConfiguredPath(rawValue);
            return _sourceService.IsImageSource(normalized)
                ? _sourceService.BuildImageSequenceSourcePath(normalized)
                : normalized;
        }

        private bool IsImageSourceEnabled() =>
            bool.TryParse(_config.Get(UseImageConfigName), out var b) && b;

        #endregion

        #region Field change pipeline

        private async Task ApplyFieldChangeAsync(string key, string rawValue, bool showValidationError, bool refreshScriptPreview)
        {
            if (_isClosing || _isInitializing) return;

            rawValue ??= string.Empty;

            Func<string, string>? normalize = IsPathField(key) ? NormalizeSourceValue : null;
            var changed = _config.Set(key, rawValue, normalize);

            if (!changed && !string.Equals(key, "source", StringComparison.OrdinalIgnoreCase))
                return;

            // Rename the active clip's preset to a unique "persoN" when a filter value is manually changed
            // Skip during clip switching — restored values are not manual changes
            if (changed && !_applyingPreset && !_switchingClip && !IsPathField(key)
                && !PresetService.ExcludedKeys.Contains(key)
                && ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count)
            {
                var currentName = Clips[ActiveClipIndex].PresetName;
                if (currentName is null || !currentName.StartsWith("perso", StringComparison.OrdinalIgnoreCase))
                {
                    Clips[ActiveClipIndex].PresetName = _clipManager.GetNextPersoName();
                    RestoreClipPresetCombo();
                    RebuildClipTabs();
                }

                // Deselect filter preset combo when a field belonging to that filter is manually changed
                if (FieldToFilterPresetCombo.TryGetValue(key, out var filterPresetCombo)
                    && this.FindControl<ComboBox>(filterPresetCombo) is { } filterCombo)
                {
                    _suppressTextEvents = true;
                    try { filterCombo.SelectedIndex = -1; }
                    finally { _suppressTextEvents = false; }
                    _config.Set(filterPresetCombo, string.Empty);
                }

                // Deselect GammacPresetCombo when a Gammac parameter is manually changed
                if (Array.IndexOf(EncodeController.GammacKeys, key) >= 0
                    && this.FindControl<ComboBox>("GammacPresetCombo") is { } gc)
                {
                    gc.SelectedIndex = -1;
                    gc.Text = null;
                    _config.Set("gammac_preset", string.Empty);
                    if (ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count)
                        Clips[ActiveClipIndex].GammacPresetName = null;
                }
            }

            if (key.Equals("source", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = _config.Get("source");
                _config.Set("film", normalized);
                _config.Set("img",  normalized);
            }
            else if (key.Equals("img_start", StringComparison.OrdinalIgnoreCase)
                  || key.Equals("img_end",   StringComparison.OrdinalIgnoreCase))
            {
            }

            RegenerateScript(showValidationError);

            if (!TryValidateSourceSelection(out var message))
            {
                if (showValidationError && !string.IsNullOrWhiteSpace(message))
                    await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), message);
                return;
            }

            if (!ShouldRefreshPreviewForField(key)) return;

            if (refreshScriptPreview)
            {
                _refreshDebouncer.Cancel();
                await LoadScriptAsync();
            }
            else
            {
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
            }
        }

        private void UpdateConfigurationValue(string name, string value, bool showValidationError = true) =>
            _ = ApplyFieldChangeAsync(name, value, showValidationError, refreshScriptPreview: false);

        private static bool ShouldRefreshPreviewForField(string key) =>
            ScriptService.TextFieldNames.Contains(key, StringComparer.OrdinalIgnoreCase)
            || ScriptService.BoolFieldNames.Contains(key, StringComparer.OrdinalIgnoreCase)
            || key.Equals(UseImageConfigName, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Script generation

        private void RegenerateScript(bool showValidationError = true)
        {
            if (!TryValidateSourceSelection(out var errorMessage))
            {
                if (showValidationError)
                    ShowSourceValidationError(errorMessage);
                return;
            }

            _scriptService.Generate(_config.Snapshot(), _customFilterService.Filters, ViewModel.CurrentLanguageCode);
        }

        #endregion

        #region Option toggles & column visibility

#pragma warning disable IDE0028
        private readonly HashSet<string> _openParamPanels = new(StringComparer.Ordinal);
#pragma warning restore IDE0028

        private static readonly string[] AllParamPanels =
            ["CropParams", "DegrainParams", "DenoiseParams", "LumaParams", "GammacParams", "SharpParams"];

        private static readonly string[] AllExpandBtns =
            ["CropExpandBtn", "DegrainExpandBtn", "DenoiseExpandBtn", "LumaExpandBtn", "GammacExpandBtn", "SharpExpandBtn"];

        private static readonly Dictionary<string, string> ParamPanelToOptionToggle = new(StringComparer.Ordinal)
        {
            ["CropParams"] = "enable_crop",
            ["DegrainParams"] = "enable_degrain",
            ["DenoiseParams"] = "enable_denoise",
            ["LumaParams"] = "enable_luma_levels",
            ["GammacParams"] = "enable_gammac",
            ["SharpParams"] = "enable_sharp"
        };

        private bool IsParamPanelEnabled(string panelName) =>
            ParamPanelToOptionToggle.TryGetValue(panelName, out var optionName) && IsOptionEnabled(optionName);

        private void HideAllParamPanelsAndResetExpandButtons()
        {
            _openParamPanels.Clear();
            foreach (var name in AllParamPanels)
                if (this.FindControl<Control>(name) is { } p) p.IsVisible = false;

            foreach (var name in AllExpandBtns)
            {
                if (this.FindControl<Button>(name) is not { } b) continue;
                b.Content = "▶";
                b.Classes.Remove("active");
            }
        }

        private void UpdateParamsPlaceholderVisibility()
        {
            if (this.FindControl<TextBlock>("ParamsPlaceholder") is { } ph)
                ph.IsVisible = _openParamPanels.Count == 0;
        }

        private Control? FindPanelByName(string name) =>
            this.FindControl<Control>(name);

        private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string targetName) return;

            if (_openParamPanels.Remove(targetName))
            {
                // Panel is open → collapse it
                if (FindPanelByName(targetName) is { } panel) panel.IsVisible = false;
                btn.Content = "▶";
                btn.Classes.Remove("active");
            }
            else
            {
                // Panel is closed → if filter is off, activate it first
                if (!IsParamPanelEnabled(targetName))
                {
                    if (ParamPanelToOptionToggle.TryGetValue(targetName, out var toggleName) &&
                        this.FindControl<Button>(toggleName) is { } toggleBtn)
                    {
                        toggleBtn.Tag = true;
                        UpdateToggleButtonPresentation(toggleBtn, true);
                        UpdateConfigurationValue(toggleName, "true", showValidationError: true);
                        UpdateOptionColumnVisibility();
                    }
                }

                _openParamPanels.Add(targetName);
                if (FindPanelByName(targetName) is { } panel) panel.IsVisible = true;
                btn.Content = "▶";
                btn.Classes.Add("active");
            }

            UpdateParamsPlaceholderVisibility();
        }

        private void SnapToBottomOfScreen()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen is null) return;

            var wa      = screen.WorkingArea;
            var scaling = screen.Scaling;
            var w  = (int)Math.Ceiling(Bounds.Width * scaling);
            var h  = Math.Min((int)Math.Ceiling(Height * scaling), wa.Height);

            var x = Math.Clamp(wa.X + Math.Max(0, (wa.Width - w) / 2), wa.X, Math.Max(wa.X, wa.Right - w));
            var y = Math.Clamp(wa.Bottom - h - WindowBottomPadding, wa.Y, Math.Max(wa.Y, wa.Bottom - h));
            Position = new PixelPoint(x, y);
        }

        private bool IsSavedPositionVisible(int x, int y)
        {
            // Check if the top-left area of the title bar is on any screen.
            // Using the left edge (not the center) avoids false negatives when
            // the window is wide or positioned near the right edge of the screen.
            var titleBarLeft = new PixelPoint(x, y + 10);
            return Screens.All.Any(s => s.WorkingArea.Contains(titleBarLeft));
        }

        private void OnOptionToggleButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Name: { } name } btn) return;

            var updated = !(btn.Tag is bool v && v);
            btn.Tag = updated;
            UpdateToggleButtonPresentation(btn, updated);
            UpdateConfigurationValue(name, updated.ToString().ToLowerInvariant(), showValidationError: true);

            if (IsOptionToggle(name))
            {
                UpdateOptionColumnVisibility();
                SyncActiveParamPanelWithFilters();
            }
        }

        private void SyncActiveParamPanelWithFilters()
        {
            if (_openParamPanels.Count == 0) return;

            var toClose = _openParamPanels.Where(p => !IsParamPanelEnabled(p)).ToList();
            foreach (var panel in toClose)
            {
                _openParamPanels.Remove(panel);
                if (FindPanelByName(panel) is { } c) c.IsVisible = false;
                var idx = Array.IndexOf(AllParamPanels, panel);
                if (idx >= 0 && idx < AllExpandBtns.Length &&
                    this.FindControl<Button>(AllExpandBtns[idx]) is { } btn)
                {
                    btn.Content = "▶";
                    btn.Classes.Remove("active");
                }
            }

            UpdateParamsPlaceholderVisibility();
        }

        private void UpdateOptionColumnVisibility()
        {
            _mainGrid ??= this.FindControl<Grid>("MainGrid");

            var crop    = IsOptionEnabled("enable_crop");
            var degrain = IsOptionEnabled("enable_degrain");
            var denoise = IsOptionEnabled("enable_denoise");
            var luma    = IsOptionEnabled("enable_luma_levels");
            var gammac  = IsOptionEnabled("enable_gammac");
            var sharp   = IsOptionEnabled("enable_sharp");

            SetColumnEnabled(crop,              "CropScrollViewer");
            SetColumnEnabled(crop,              "CropSplitterBefore", "CropSplitterAfter");
            SetColumnEnabled(true,              "DegrainColumn");
            SetColumnEnabled(degrain,           "DegrainScrollViewer");
            SetColumnEnabled(degrain || denoise,"DegrainDenoiseSplitter");
            SetColumnEnabled(true,              "DenoiseColumn");
            SetColumnEnabled(denoise,           "DenoiseScrollViewer");
            SetColumnEnabled(luma,              "LumaSplitterBefore", "LumaLevelsScrollViewer");
            SetColumnEnabled(luma && gammac,    "LumaGammacSplitter");
            SetColumnEnabled(gammac,            "GammacScrollViewer");
            SetColumnEnabled(gammac && sharp,   "GammacSplitterAfter");
            SetColumnEnabled(sharp,             "SharpenScrollViewer");
        }

        private bool IsOptionEnabled(string name) => GetOptionToggleValue(name);

        private bool GetOptionToggleValue(string name)
        {
            if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) return false;
            return btn.Tag is bool v && v;
        }

        private void SetOptionToggleValue(string name, bool isEnabled)
        {
            if (this.FindControl<Button>(GetBoolControlName(name)) is not { } btn) return;
            btn.Tag = isEnabled;
            UpdateToggleButtonPresentation(btn, isEnabled);
        }

        private void UpdateToggleButtonPresentation(Button btn, bool isEnabled)
        {
            if (btn.Name is { Length: > 0 } n && OptionButtonLabels.TryGetValue(n, out var l))
                btn.Content = l;
            // else: keep existing Content (e.g. custom filter name set by caller)

            btn.Background  = isEnabled ? ThemeBrush("AccentGreen") : ThemeBrush("BorderAccent");
            btn.BorderBrush = ThemeBrush("BorderAccent");
            btn.Foreground  = isEnabled ? Brushes.White : ThemeBrush("TextLabel");
        }

        private void SetColumnEnabled(bool isEnabled, params string[] names)
        {
            foreach (var n in names)
                if (this.FindControl<Control>(n) is { } c)
                    c.IsEnabled = isEnabled;
        }

        private static bool IsOptionToggle(string name) =>
            name is "enable_crop" or "enable_degrain" or "enable_denoise"
                 or "enable_luma_levels" or "enable_gammac" or "enable_sharp";

        private static string GetBoolControlName(string name) =>
            string.Equals(name, "Show", StringComparison.OrdinalIgnoreCase) ? "ShowPreview" : name;

        #endregion

        #region Custom filters

        private CustomFilterPresenter? _customFilterPresenter;

        private CustomFilterPresenter CustomFilters =>
            _customFilterPresenter ??= new CustomFilterPresenter(this, _customFilterService);

        private void RebuildCustomFilterUI() => CustomFilters.RebuildUI();
        private void OnCustomExpandClick(object? sender, RoutedEventArgs e) => CustomFilters.OnExpandClick(sender, e);
        private void OnAddCustomFilterClick(object? sender, RoutedEventArgs e) => CustomFilters.OnAddClick(sender, e);
        private void OnImportCustomFilterClick(object? sender, RoutedEventArgs e) => CustomFilters.OnImportClick(sender, e);

        #endregion

        #region Player / script preview (delegated to PlayerController)

        private void OnVdbBeginningClick(object? sender, RoutedEventArgs e) => _playerController.OnVdbBeginningClick(sender, e);
        private void OnVdbPrevFrameClick(object? sender, RoutedEventArgs e) => _playerController.OnVdbPrevFrameClick(sender, e);
        private void OnVdbPlayClick(object? sender, RoutedEventArgs e) => _playerController.OnVdbPlayClick(sender, e);
        private void OnVdbNextFrameClick(object? sender, RoutedEventArgs e) => _playerController.OnVdbNextFrameClick(sender, e);
        private void OnVdbEndClick(object? sender, RoutedEventArgs e) => _playerController.OnVdbEndClick(sender, e);
        private void OnSpeedClick(object? sender, RoutedEventArgs e) => _playerController.OnSpeedClick(sender, e);
        private void OnHalfResClick(object? sender, RoutedEventArgs e) => _playerController.OnHalfResClick(sender, e);
        private void OnMaxViewerClick(object? sender, RoutedEventArgs e) => _playerController.OnMaxViewerClick(sender, e);
        private void ToggleViewerMaximized() => _playerController.ToggleViewerMaximized();
        private void OnWindowKeyDown(object? sender, KeyEventArgs e) => _playerController.OnWindowKeyDown(sender, e);
        private void SyncForceSourceCombo(Dictionary<string, string> values) => _playerController.SyncForceSourceCombo(values);
        private void OnForceSourceChanged(object? sender, SelectionChangedEventArgs e) => _playerController.OnForceSourceChanged(sender, e);

        private bool _isEncoding;
        private bool _recordOpen;

        // ── Encoding delegations to EncodeController ──
        private void OnRecordClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordClick(sender, e);
        private void RebuildBatchClipList() => _encodeController.RebuildBatchClipList();
        private void OnBatchSelectAllClick(object? sender, RoutedEventArgs e) => _encodeController.OnBatchSelectAllClick(sender, e);
        private void OnRecordDirPickClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordDirPickClick(sender, e);
        private void OnRecordDirOpenClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordDirOpenClick(sender, e);
        private void OnRecordStartClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordStartClick(sender, e);
        private void OnRecordPresetSaveClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetSaveClick(sender, e);
        private void OnRecordPresetLoadClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetLoadClick(sender, e);
        private void OnRecordPresetDeleteClick(object? sender, RoutedEventArgs e) => _encodeController.OnRecordPresetDeleteClick(sender, e);
        private void OnGammacPresetSaveClick(object? sender, RoutedEventArgs e) => _encodeController.OnGammacPresetSaveClick(sender, e);
        private void OnGammacPresetDeleteClick(object? sender, RoutedEventArgs e) => _encodeController.OnGammacPresetDeleteClick(sender, e);
        private void OnGammacPresetSelectionChanged(object? sender, SelectionChangedEventArgs e) => _encodeController.OnGammacPresetSelectionChanged(sender, e);
        private void UpdateDiskSpaceLabel(string? dirPath) => _encodeController.UpdateDiskSpaceLabel(dirPath);
        private void RefreshEncodingPresetCombo() => _encodeController.RefreshEncodingPresetCombo();
        private void RefreshGammacPresetCombo() => _encodeController.RefreshGammacPresetCombo();

        #endregion

        #region Validation

        private bool TryValidateSourceSelection(out string errorMessage)
        {
            var useImg = IsImageSourceEnabled();
            var raw = _config.Get("source");
            if (string.IsNullOrWhiteSpace(raw))
                raw = useImg ? _config.Get("img") : _config.Get("film");

            if (string.IsNullOrWhiteSpace(raw))
            {
                errorMessage = string.Empty;
                return false;
            }

            var path = _sourceService.NormalizeConfiguredPath(raw);

            if (useImg && !_sourceService.ImageSequenceExists(path))
            {
                errorMessage = GetLocalizedText(
                    fr: "Les images référencées dans source sont introuvables.",
                    en: "The image sequence referenced in source was not found.");
                return false;
            }

            if (!useImg && !File.Exists(path))
            {
                errorMessage = GetLocalizedText(
                    fr: "Le fichier référencé dans source est introuvable.",
                    en: "The file referenced in source was not found.");
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private void ShowSourceValidationError(string message)
        {
            if (_sourceValidationErrorVisible || string.IsNullOrWhiteSpace(message)) return;
            _sourceValidationErrorVisible = true;
            Dispatcher.UIThread.Post(async () =>
            {
                try { await _dialogService.ShowErrorAsync(this, GetUiText("ErrorTitle"), message); }
                finally { _sourceValidationErrorVisible = false; }
            });
        }

        #endregion

        #region Presets

        private async void OnPresetClick(object? sender, RoutedEventArgs e)
        {
            await _dialogService.ShowPresetDialogAsync(this, _presetService, _config, ApplyPresetSelectionAsync, ViewModel);

            var savedPresetNames = _presetService.LoadPresets()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var clip in Clips)
            {
                if (string.IsNullOrWhiteSpace(clip.PresetName))
                    continue;

                var isPerso = clip.PresetName.StartsWith("perso", StringComparison.OrdinalIgnoreCase);
                if (!isPerso && !savedPresetNames.Contains(clip.PresetName))
                    clip.PresetName = null;
            }

            RestoreClipPresetCombo();
            RebuildClipTabs();
        }

        /// <summary>Applies preset values either to active clip or all clips.</summary>
        private async Task ApplyPresetSelectionAsync(string presetName, Dictionary<string, string> values, bool applyToAllClips)
        {
            await ApplyPresetValuesAsync(values);

            if (ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count)
                Clips[ActiveClipIndex].PresetName = presetName;

            if (applyToAllClips)
            {
                var filterSnap = _clipManager.CaptureConfig();
                for (int i = 0; i < Clips.Count; i++)
                {
                    Clips[i].Config = new Dictionary<string, string>(filterSnap, StringComparer.OrdinalIgnoreCase);
                    Clips[i].PresetName = presetName;
                }
            }
        }

        /// <summary>Applies preset values to the current UI/config only (per-clip).</summary>
        private async Task ApplyPresetValuesAsync(Dictionary<string, string> values)
        {
            _applyingPreset = true;
            try
            {
            // Ignore source files and crop values from presets
            foreach (var key in PresetService.ExcludedKeys)
                values.Remove(key);

            foreach (var name in ScriptService.TextFieldNames)
            {
                if (!values.TryGetValue(name, out var value)) continue;

                if (this.FindControl<Control>(name) is TextBox tb) SetTextSafely(tb, value);
                else if (this.FindControl<Control>(name) is ComboBox cb) value = ApplyComboChoice(cb, name, value);

                _config.Set(name, value);
            }

            foreach (var name in ScriptService.BoolFieldNames)
            {
                if (!values.TryGetValue(name, out var v) || !bool.TryParse(v, out var parsed)) continue;
                SetOptionToggleValue(name, parsed);
                _config.Set(name, parsed.ToString().ToLowerInvariant());
            }

            var useImage = values.TryGetValue(UseImageConfigName, out var uiv)
                && bool.TryParse(uiv, out var parsedUseImage) && parsedUseImage;
            UpdateSourceSelection(isFilmSelected: !useImage, updateConfig: true);
            SyncAllSliders();
            RegenerateScript(showValidationError: true);
            UpdateOptionColumnVisibility();

            // Save into current clip config
            _clipManager.SaveActiveConfig();

            if (TryValidateSourceSelection(out _))
                await _refreshDebouncer.DebounceAsync(() => LoadScriptAsync());
            }
            finally { _applyingPreset = false; }
        }

        // GetNextPersoName() moved to ClipManager

        /// <summary>Populates the per-clip preset ComboBox from saved presets.</summary>
        private void RefreshClipPresetCombo()
        {
            if (this.FindControl<ComboBox>("ClipPresetCombo") is not { } combo) return;
            var presets = _presetService.LoadPresets()
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Name)
                .ToList();

            var current = combo.SelectedItem as string;
            combo.ItemsSource = presets;
            if (current is not null && presets.Contains(current))
                combo.SelectedItem = current;
            else
                combo.SelectedIndex = -1;
        }

        private bool _suppressClipPresetChange;

        private async void OnClipPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressClipPresetChange) return;
            if (sender is not ComboBox combo) return;
            var name = combo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name)) return;

            var preset = _presetService.LoadPresets()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (preset?.Values is null) return;

            // Store preset name for this clip
            if (ActiveClipIndex >= 0 && ActiveClipIndex < Clips.Count)
                Clips[ActiveClipIndex].PresetName = name;

            await ApplyPresetValuesAsync(new Dictionary<string, string>(preset.Values, StringComparer.OrdinalIgnoreCase));
            RebuildClipTabs();
        }

        #endregion

        #region Info dialogs

        private void OnUserGuideClick(object? sender, RoutedEventArgs e)
        {
            var lang = ViewModel.CurrentLanguageCode;
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
            var guidePath = System.IO.Path.Combine(exeDir, "Users Guide", $"CleanScan_Guide_{lang}.pdf");
            if (!System.IO.File.Exists(guidePath))
                guidePath = System.IO.Path.Combine(exeDir, "Users Guide", "CleanScan_Guide_en.pdf");
            if (System.IO.File.Exists(guidePath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = guidePath, UseShellExecute = true });
        }

        private async void OnScriptPreviewClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowScriptPreviewDialogAsync(this, _scriptService,
                () => _ = LoadScriptAsync(), ViewModel);

        private async void OnFeedbackClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowFeedbackDialogAsync(this, ViewModel);

        private async void OnAboutClick(object? sender, RoutedEventArgs e) =>
            await _dialogService.ShowAboutDialogAsync(
                this,
                GetUiText("AboutMenuItem"),
                GetUiText("AboutCompany"),
                GetUiText("AllRightsReserved"),
                GetUiText("AboutWebsite"),
                GetUiText("AboutVersion"),
                GetUiText("GamMacCloseButton"),
                "avares://CleanScan/Assets/Logo.png");

        #endregion

        #region UI helpers

        private void SetTextSafely(TextBox textBox, string text)
        {
            try { _suppressTextEvents = true; textBox.Text = text; }
            finally { _suppressTextEvents = false; }
        }

        private static void SetComboBoxChoice(ComboBox cb, string? rawValue, string[] choices)
        {
            var normalized = NormalizeChoiceValue(rawValue);
            string? found = null;
            for (var i = 0; i < choices.Length; i++)
            {
                if (string.Equals(choices[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    found = choices[i];
                    break;
                }
            }
            cb.SelectedItem = found ?? (choices.Length > 0 ? choices[0] : string.Empty);
        }

        private void UpdateMinimumWidthFromConfiguration()
        {
            if (this.FindControl<Border>("ConfigurationBorder") is not { } border) return;
            Dispatcher.UIThread.Post(() =>
            {
                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var w = border.DesiredSize.Width;
                if (_mainGrid is { } g) w += g.Margin.Left + g.Margin.Right;
                MinWidth = Math.Max(MinWidth, w);
            });
        }

        private static string NormalizeChoiceValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var cleaned = value.Trim();
            var hash    = cleaned.IndexOf('#');
            if (hash >= 0) cleaned = cleaned[..hash].TrimEnd();
            return cleaned.Trim().Trim('"');
        }

        private static string GetAppDataPath(string fileName) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder, fileName);

        private void OnGuidedTourClick(object? sender, RoutedEventArgs e)
        {
            _ = ShowGuidedTourAsync();
        }

        #endregion

        #region Guided Tour

        private readonly GuidedTourService _guidedTourService = new();

        private async Task ShowGuidedTourAsync() => await _guidedTourService.RunAsync(this);

        #endregion

        #region Interface implementations (ITourHost, IFilterPresenterHost)

        // ── ITourHost ────────────────────────────────────────────────
        T? ITourHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        SolidColorBrush ITourHost.ThemeBrush(string key) => ThemeBrush(key);
        string ITourHost.GetUiText(string key) => GetUiText(key);
        string ITourHost.CurrentLanguageCode => ViewModel.CurrentLanguageCode;
        Size ITourHost.ClientSize => ClientSize;
        bool ITourHost.IsRecordPanelOpen => _recordOpen;
        void ITourHost.ToggleRecordPanel() => OnRecordClick(null, new RoutedEventArgs());
        void ITourHost.ExpandPanel(string expandButtonName)
        {
            if (this.FindControl<Button>(expandButtonName) is { } btn)
                OnExpandButtonClick(btn, new RoutedEventArgs());
        }
        void ITourHost.ApplyLanguage(string code, bool persist) => ApplyLanguage(code, persist);
        void ITourHost.MarkTourCompleted()
        {
            var settings = _windowStateService.Load();
            if (settings is not null)
                _windowStateService.Save(settings with { TourCompleted = true });
        }

        // ── IFilterPresenterHost ─────────────────────────────────────
        T? IFilterPresenterHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        Window IFilterPresenterHost.Window => this;
        SolidColorBrush IFilterPresenterHost.ThemeBrush(string key) => ThemeBrush(key);
        string IFilterPresenterHost.GetUiText(string key) => GetUiText(key);
        MainWindowViewModel IFilterPresenterHost.ViewModel => ViewModel;
        ConfigStore IFilterPresenterHost.Config => _config;
        double IFilterPresenterHost.WindowHeight => Bounds.Height;
        ThemeService? IFilterPresenterHost.ThemeService => _themeService;
        HashSet<string> IFilterPresenterHost.OpenParamPanels => _openParamPanels;
        void IFilterPresenterHost.UpdateToggleButtonPresentation(Button btn, bool isEnabled) =>
            UpdateToggleButtonPresentation(btn, isEnabled);
        void IFilterPresenterHost.UpdateParamsPlaceholderVisibility() =>
            UpdateParamsPlaceholderVisibility();
        void IFilterPresenterHost.RegenerateScript(bool showValidationError) =>
            RegenerateScript(showValidationError);
        Task IFilterPresenterHost.LoadScriptAsync(bool resetPosition) =>
            LoadScriptAsync(resetPosition);

        // ── IEncodeHost ────────────────────────────────────────────────
        T? IEncodeHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        Window IEncodeHost.Window => this;
        SolidColorBrush IEncodeHost.ThemeBrush(string key) => ThemeBrush(key);
        string IEncodeHost.GetUiText(string key) => GetUiText(key);
        MainWindowViewModel IEncodeHost.ViewModel => ViewModel;
        ConfigStore IEncodeHost.Config => _config;
        IScriptService IEncodeHost.ScriptService => _scriptService;
        SourceService IEncodeHost.SourceService => _sourceService;
        IDialogService IEncodeHost.DialogService => _dialogService;
        PresetService IEncodeHost.EncodingPresetService => _encodingPresetService;
        PresetService IEncodeHost.GammacPresetService => _gammacPresetService;
        CustomFilterService IEncodeHost.CustomFilterService => _customFilterService;
        ClipManager IEncodeHost.ClipManager => _clipManager;
        MpvService? IEncodeHost.MpvService => _playerController.MpvService;
        ThemeService IEncodeHost.ThemeService => _themeService;
        IStorageProvider IEncodeHost.StorageProvider => StorageProvider;
        bool IEncodeHost.RecordOpen { get => _recordOpen; set => _recordOpen = value; }
        bool IEncodeHost.IsEncoding { get => _isEncoding; set => _isEncoding = value; }
        bool IEncodeHost.IsInitializing => _isInitializing;
        bool IEncodeHost.IsClosing => _isClosing;
        bool IEncodeHost.IsSwitchingClip => _switchingClip;
        void IEncodeHost.RegenerateScript(bool showValidationError) => RegenerateScript(showValidationError);
        Task IEncodeHost.LoadScriptAsync(bool resetPosition) => LoadScriptAsync(resetPosition);
        void IEncodeHost.RestoreClipConfig(int index) => RestoreClipConfig(index);
        Regex IEncodeHost.PreviewTrueRegex() => PreviewTrueRegex();
        Regex IEncodeHost.PreviewHalfTrueRegex() => PreviewHalfTrueRegex();
        void IEncodeHost.MoveSliderToPointer(Slider slider, PointerEventArgs e) => MoveSliderToPointer(slider, e);

        // ── IPlayerHost ─────────────────────────────────────────────────
        T? IPlayerHost.FindControl<T>(string name) where T : class => this.FindControl<T>(name);
        Window IPlayerHost.Window => this;
        SolidColorBrush IPlayerHost.ThemeBrush(string key) => ThemeBrush(key);
        string IPlayerHost.GetUiText(string key) => GetUiText(key);
        string IPlayerHost.GetLocalizedText(string fr, string en) => GetLocalizedText(fr, en);
        MainWindowViewModel IPlayerHost.ViewModel => ViewModel;
        ConfigStore IPlayerHost.Config => _config;
        IScriptService IPlayerHost.ScriptService => _scriptService;
        SourceService IPlayerHost.SourceService => _sourceService;
        IDialogService IPlayerHost.DialogService => _dialogService;
        bool IPlayerHost.IsClosing => _isClosing;
        bool IPlayerHost.IsEncoding => _isEncoding;
        bool IPlayerHost.LoadingSourceFallback { get => _loadingSourceFallback; set => _loadingSourceFallback = value; }
        bool IPlayerHost.TryValidateSourceSelection(out string errorMessage) => TryValidateSourceSelection(out errorMessage);
        Action? IPlayerHost.AdvanceOnClipLoaded => _guidedTourService.AdvanceOnClipLoaded;
        void IPlayerHost.RegenerateScript(bool showValidationError) => RegenerateScript(showValidationError);
        void IPlayerHost.UpdateConfigurationValue(string name, string value, bool showValidationError) =>
            UpdateConfigurationValue(name, value, showValidationError);
        void IPlayerHost.CloseSettingsMenu() => CloseSettingsMenu();

        #endregion
    }
}
