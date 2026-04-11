using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvyScanLab.Models;
using AvyScanLab.Services;
using AvyScanLab.ViewModels;

namespace AvyScanLab.Views;

/// <summary>Contract the custom filter presenter needs from the host window.</summary>
public interface IFilterPresenterHost
{
    T? FindControl<T>(string name) where T : Control;
    Window Window { get; }
    SolidColorBrush ThemeBrush(string key);
    string GetUiText(string key);
    MainWindowViewModel ViewModel { get; }
    ConfigStore Config { get; }
    double WindowHeight { get; }
    ThemeService? ThemeService { get; }
    HashSet<string> OpenParamPanels { get; }
    void UpdateToggleButtonPresentation(Button btn, bool isEnabled);
    void UpdateParamsPlaceholderVisibility();
    void RegenerateScript(bool showValidationError = true);
    Task LoadScriptAsync(bool resetPosition = false);
    void OnCustomFilterValueChanged();
    void ShowRegionOverlay(string filterId, double x, double y, double w, double h);
    void RefreshRegionOverlay(string filterId);
    void SetRegionDrawMode(string filterId, bool active);
}

public sealed class CustomFilterPresenter
{
    private readonly IFilterPresenterHost _host;
    private readonly CustomFilterService _filterService;

    /// <summary>The filter ID currently in region draw mode (null if none).</summary>
    private string? _regionDrawFilterId;
    private Button? _regionDrawButton;

    public CustomFilterPresenter(IFilterPresenterHost host, CustomFilterService filterService)
    {
        _host = host;
        _filterService = filterService;
    }

    private const string PositionKeySuffix = "_position";

    // ── Public API called from MainWindow ────────────────────────────

    /// <summary>Writes all filter positions to ConfigStore so presets capture them.</summary>
    public void SyncPositionsToConfig()
    {
        foreach (var f in _filterService.Filters)
        {
            var key = ScriptService.GetCustomFilterConfigKey(f.Id, "position");
            _host.Config.Set(key, f.Position);
        }
    }

    /// <summary>Reads filter positions from ConfigStore (after preset apply) and updates filters.</summary>
    public void ApplyPositionsFromConfig()
    {
        var changed = false;
        foreach (var f in _filterService.Filters)
        {
            var key = ScriptService.GetCustomFilterConfigKey(f.Id, "position");
            var pos = _host.Config.Get(key);
            if (!string.IsNullOrEmpty(pos) && pos != f.Position)
            {
                f.Position = pos;
                changed = true;
            }
        }
        if (changed) _filterService.Save();
    }

    private bool _inactiveExpanded = true;

    public void CollapseInactive() => _inactiveExpanded = false;

    public void RebuildUI()
    {
        var list = _host.FindControl<StackPanel>("CustomFiltersList");
        if (list is null) return;
        list.Children.Clear();

        var inactiveList = _host.FindControl<StackPanel>("InactiveFiltersList");
        inactiveList?.Children.Clear();

        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        container?.Children.Clear();

        // Sort filters by pipeline position for display
        var sorted = _filterService.Filters
            .OrderBy(f => ScriptService.InjectionPositionToOrder(f.Position))
            .ToList();

        var inactiveCount = 0;
        foreach (var filter in sorted)
        {
            // Sync enabled state from config before deciding section
            var enabledKey = ScriptService.GetCustomFilterEnabledKey(filter.Id);
            var cfgEnabled = _host.Config.Get(enabledKey);
            if (!string.IsNullOrEmpty(cfgEnabled) && bool.TryParse(cfgEnabled, out var parsedEnabled))
            {
                if (filter.Enabled != parsedEnabled)
                {
                    filter.Enabled = parsedEnabled;
                    _filterService.Save();
                }
            }

            if (filter.Enabled)
            {
                AddFilterRow(filter, list);
                var panelName = $"CustomPanel_{filter.Id}";
                if (_host.OpenParamPanels.Contains(panelName))
                    BuildParamPanel(filter);
            }
            else
            {
                inactiveCount++;
                if (inactiveList is not null)
                    AddFilterRow(filter, inactiveList);
            }
        }

        // Set up drag & drop on both lists
        SetupDragDrop(list);
        if (inactiveList is not null)
            SetupDragDrop(inactiveList);

        // Update inactive section header
        UpdateInactiveHeader(inactiveCount);

        // Ensure positions are in ConfigStore for preset capture
        SyncPositionsToConfig();
    }

    private void UpdateInactiveHeader(int inactiveCount)
    {
        var header = _host.FindControl<Border>("InactiveFiltersHeader");
        var label = _host.FindControl<TextBlock>("InactiveFiltersLabel");
        var inactiveList = _host.FindControl<StackPanel>("InactiveFiltersList");
        if (header is null || label is null || inactiveList is null) return;

        if (inactiveCount == 0)
        {
            header.IsVisible = false;
            inactiveList.IsVisible = false;
            return;
        }

        var arrow = _inactiveExpanded ? "▾" : "▸";
        label.Text = $"{arrow} {_host.GetUiText("InactiveFilters")} ({inactiveCount})";
        header.IsVisible = true;
        inactiveList.IsVisible = _inactiveExpanded;

        // Wire click (re-wired each rebuild, but PointerPressed on Border is fine)
        header.PointerPressed -= OnInactiveHeaderClick;
        header.PointerPressed += OnInactiveHeaderClick;
    }

    private void OnInactiveHeaderClick(object? sender, PointerPressedEventArgs e)
    {
        _inactiveExpanded = !_inactiveExpanded;
        var inactiveList = _host.FindControl<StackPanel>("InactiveFiltersList");
        var label = _host.FindControl<TextBlock>("InactiveFiltersLabel");
        if (inactiveList is null || label is null) return;

        inactiveList.IsVisible = _inactiveExpanded;
        var count = inactiveList.Children.Count;
        var arrow = _inactiveExpanded ? "▾" : "▸";
        label.Text = $"{arrow} {_host.GetUiText("InactiveFilters")} ({count})";
    }

    public void OnExpandClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string panelName) return;
        var filterId = panelName.Replace("CustomPanel_", "");
        var filter = _filterService.GetById(filterId);
        if (filter is null) return;

        if (_host.OpenParamPanels.Remove(panelName))
        {
            RemoveParamPanel(panelName);
            btn.Content = "▶";
            btn.Classes.Remove("active");
        }
        else
        {
            _host.OpenParamPanels.Add(panelName);
            BuildParamPanel(filter);
            btn.Content = "▶";
            btn.Classes.Add("active");
        }
        _host.UpdateParamsPlaceholderVisibility();

        // Deactivate region draw if its panel was just collapsed
        if (filter.RegionDraw && !_host.OpenParamPanels.Contains(panelName))
            DeactivateRegionDraw();
    }

    public async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!LicenseService.IsLicensed) return;
        var filter = new CustomFilter { Name = "Custom " + _filterService.Filters.Count };
        var dialog = new CustomFilterDialog(filter, isNew: true, vm: _host.ViewModel,
            ownerHeight: _host.WindowHeight, themeService: _host.ThemeService)
        {
            OnPreview = f =>
            {
                _filterService.Add(f);
                _host.RegenerateScript(showValidationError: false);
                _ = _host.LoadScriptAsync();
                _filterService.Remove(f.Id);
            }
        };
        await dialog.ShowDialog(_host.Window);

        if (dialog.Saved)
        {
            filter.Enabled = true;
            _filterService.Add(filter);
            _host.Config.Set(ScriptService.GetCustomFilterEnabledKey(filter.Id), "true");
            RebuildUI();
            _host.RegenerateScript(showValidationError: false);
            await _host.LoadScriptAsync();
        }
    }

    public async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (!LicenseService.IsLicensed) return;
        // Default to the Filters/ directory next to the executable
        Avalonia.Platform.Storage.IStorageFolder? startDir = null;
        var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            var filtersDir = System.IO.Path.Combine(exeDir, "Filters");
            if (System.IO.Directory.Exists(filtersDir))
                startDir = await _host.Window.StorageProvider.TryGetFolderFromPathAsync(new Uri(filtersDir));
        }

        var picker = await _host.Window.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = _host.GetUiText("CfDlgImportTitle"),
                AllowMultiple = false,
                SuggestedStartLocation = startDir,
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

        if (picker.Count == 0) return;
        var imported = CustomFilterDialog.ImportFromFile(picker[0].Path.LocalPath);
        if (imported.Count == 0) return;

        foreach (var f in imported)
        {
            f.Enabled = true;
            _filterService.Add(f);
            _host.Config.Set(ScriptService.GetCustomFilterEnabledKey(f.Id), "true");
        }
        RebuildUI();
        _host.RegenerateScript(showValidationError: false);
        await _host.LoadScriptAsync();
    }

    public void OnResetOrderClick()
    {
        // Restore default positions from Filters/*.json for built-in filters
        var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
        var filtersDir = exeDir is not null ? System.IO.Path.Combine(exeDir, "Filters") : null;
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (filtersDir is not null && System.IO.Directory.Exists(filtersDir))
        {
            foreach (var jsonFile in System.IO.Directory.GetFiles(filtersDir, "*.json"))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(jsonFile);
                    var f = System.Text.Json.JsonSerializer.Deserialize<CustomFilter>(json);
                    if (f is not null && !string.IsNullOrWhiteSpace(f.Id))
                        defaults[f.Id] = f.Position;
                }
                catch { /* skip */ }
            }
        }

        // Also restore stab8mm1 default position
        if (!defaults.ContainsKey("stab8mm1"))
            defaults["stab8mm1"] = "BeforePipeline";

        foreach (var filter in _filterService.Filters)
        {
            if (defaults.TryGetValue(filter.Id, out var defaultPos))
                filter.Position = defaultPos;
            // User-created filters without a default keep their current position
        }

        _filterService.Save();
        SyncPositionsToConfig();
        RebuildUI();
        _host.RegenerateScript(showValidationError: false);
        _ = _host.LoadScriptAsync();
    }

    // ── Private: row / panel construction ────────────────────────────

    private Point? _dragStartPos;
    private string? _dragFilterId;

    private void AddFilterRow(CustomFilter filter, StackPanel list)
    {
        var row = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,10,28,4,20"),
            Tag = filter.Id,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        // Drag reordering — pure pointer tracking (no DragDrop API)
        row.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;
            _dragStartPos = e.GetPosition(list);
            _dragFilterId = filter.Id;
            _isDragging = false;
        }, RoutingStrategies.Tunnel);

        row.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (_dragStartPos is null || _dragFilterId != filter.Id) return;
            var pos = e.GetPosition(list);
            var dist = Math.Abs(pos.Y - _dragStartPos.Value.Y);
            if (!_isDragging && dist < 14) return;

            if (!_isDragging)
            {
                _isDragging = true;
                _draggedRow = row;
                row.Opacity = 0.4;
                e.Pointer.Capture(list); // capture to list so we track moves beyond row bounds
            }

            var insertIndex = GetInsertIndexFromY(list, pos.Y, filter.Id);
            ShowDropIndicator(list, insertIndex);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        row.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (_isDragging && _dragFilterId == filter.Id)
            {
                var pos = e.GetPosition(list);
                var insertIndex = GetInsertIndexFromY(list, pos.Y, filter.Id);
                FinishDrag(list, filter.Id, insertIndex);
                e.Handled = true;
            }
            _dragStartPos = null;
            _isDragging = false;
        }, RoutingStrategies.Tunnel);

        var enabledKey = ScriptService.GetCustomFilterEnabledKey(filter.Id);
        // Ensure config has the enabled state (for first time / presets)
        if (string.IsNullOrEmpty(_host.Config.Get(enabledKey)))
            _host.Config.Set(enabledKey, filter.Enabled.ToString().ToLowerInvariant());

        var panelTag = $"CustomPanel_{filter.Id}";
        Button? expandBtnRef = null;

        var toggleBtn = new Button
        {
            Content = filter.Name,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Classes = { "toggle" },
            Tag = filter.Enabled
        };
        _host.UpdateToggleButtonPresentation(toggleBtn, filter.Enabled);
        toggleBtn.Click += (_, _) =>
        {
            filter.Enabled = !filter.Enabled;
            _host.Config.Set(enabledKey, filter.Enabled.ToString().ToLowerInvariant());
            _host.OnCustomFilterValueChanged();
            _filterService.Save();

            // Collapse param panel when filter is disabled
            if (!filter.Enabled)
                _host.OpenParamPanels.Remove(panelTag);

            RebuildUI();
            _host.UpdateParamsPlaceholderVisibility();
            _host.RegenerateScript(showValidationError: false);
            _ = _host.LoadScriptAsync();

            // Deactivate region draw if its filter was just disabled
            if (filter.RegionDraw && !filter.Enabled)
                DeactivateRegionDraw();
        };
        Grid.SetColumn(toggleBtn, 0);
        row.Children.Add(toggleBtn);

        var expandBtn = new Button
        {
            Content = "▶",
            Classes = { "expand-btn" },
            Tag = panelTag
        };
        if (filter.Controls.Count == 0)
        {
            // No params to expand — hide button but keep column space for alignment
            expandBtn.Opacity = 0;
            expandBtn.IsHitTestVisible = false;
        }
        else if (_host.OpenParamPanels.Contains(panelTag))
        {
            expandBtn.Classes.Add("active");
        }
        expandBtn.Click += OnExpandClick;
        expandBtnRef = expandBtn;
        Grid.SetColumn(expandBtn, 2);
        row.Children.Add(expandBtn);

        var editBtn = new Button
        {
            Content = "✎", FontSize = 12,
            Width = 20, Height = 20,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = _host.ThemeBrush("TextPrimary"),
            BorderThickness = new Thickness(0)
        };
        editBtn.Click += async (_, _) => await OpenDialog(filter);
        Grid.SetColumn(editBtn, 4);
        row.Children.Add(editBtn);

        list.Children.Add(row);
    }

    private async Task OpenDialog(CustomFilter filter)
    {
        var dialog = new CustomFilterDialog(filter, vm: _host.ViewModel,
            ownerHeight: _host.WindowHeight, themeService: _host.ThemeService)
        {
            OnPreview = f =>
            {
                _host.RegenerateScript(showValidationError: false);
                var task = _host.LoadScriptAsync();
            }
        };
        await dialog.ShowDialog(_host.Window);

        if (dialog.Deleted)
        {
            // Remove config keys for this filter (enabled + all placeholders)
            RemoveFilterConfigKeys(filter);
            _filterService.Remove(filter.Id);
            RebuildUI();
            _host.RegenerateScript(showValidationError: false);
            await _host.LoadScriptAsync();
        }
        else if (dialog.Duplicated && dialog.DuplicatedFilter is { } clone)
        {
            // Insert clone right after the original filter
            var idx = -1;
            for (var i = 0; i < _filterService.Filters.Count; i++)
                if (ReferenceEquals(_filterService.Filters[i], filter)) { idx = i; break; }
            if (idx >= 0)
                _filterService.InsertAt(idx + 1, clone);
            else
                _filterService.Add(clone);
            RebuildUI();
            _host.RegenerateScript(showValidationError: false);
            await _host.LoadScriptAsync();
        }
        else if (dialog.Saved)
        {
            _filterService.Save();
            RebuildUI();
            _host.RegenerateScript(showValidationError: false);
            await _host.LoadScriptAsync();
        }
        else
        {
            _filterService.Load();
            _host.RegenerateScript(showValidationError: false);
            _ = _host.LoadScriptAsync();
        }
    }

    private void BuildParamPanel(CustomFilter filter)
    {
        var panelName = $"CustomPanel_{filter.Id}";
        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        if (container is null) return;

        RemoveParamPanel(panelName);
        if (filter.Controls.Count == 0) return;

        var border = new Border
        {
            Name = panelName,
            BorderBrush = _host.ThemeBrush("SepLight"),
            BorderThickness = new Thickness(0, 0, 2, 0),
            Padding = new Thickness(10, 10),
            MinWidth = 140
        };

        var stack = new StackPanel { Spacing = 6 };

        // Title row: filter name (left) + reset button (right)
        var titleRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto") };
        var titleBlock = new TextBlock
        {
            Text = filter.Name.ToUpperInvariant(),
            Classes = { "section-title" },
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        Grid.SetColumn(titleBlock, 0);
        titleRow.Children.Add(titleBlock);

        var resetBtn = new Button
        {
            Content = "↺",
            FontSize = 14,
            Width = 22, Height = 22,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = _host.ThemeBrush("TextPrimary"),
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(resetBtn, _host.GetUiText("ResetFilterDefaults"));
        resetBtn.Click += (_, _) => ResetFilterToDefaults(filter);
        Grid.SetColumn(resetBtn, 1);
        titleRow.Children.Add(resetBtn);

        stack.Children.Add(titleRow);

        // Add "Draw" toggle button for RegionDraw filters
        if (filter.RegionDraw)
        {
            var drawBtn = new Button
            {
                Content = _host.GetUiText("DrawRegion"),
                Classes = { "toggle" },
                Tag = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _host.UpdateToggleButtonPresentation(drawBtn, false);

            var filterId = filter.Id;
            drawBtn.Click += (_, _) =>
            {
                var isActive = _regionDrawFilterId == filterId;
                if (isActive)
                    DeactivateRegionDraw();
                else
                    ActivateRegionDraw(filterId, drawBtn);
            };

            // Restore active state if this filter was already in draw mode
            if (_regionDrawFilterId == filterId)
            {
                _regionDrawButton = drawBtn;
                drawBtn.Tag = true;
                _host.UpdateToggleButtonPresentation(drawBtn, true);
            }

            stack.Children.Add(drawBtn);
        }

        var tooltipId = filter.SourceId ?? filter.Id;
        foreach (var ctrl in filter.Controls)
        {
            var configKey = ScriptService.GetCustomFilterConfigKey(filter.Id, ctrl.Placeholder);
            if (string.IsNullOrEmpty(_host.Config.Get(configKey)))
                _host.Config.Set(configKey, ctrl.Default);

            stack.Children.Add(ctrl.Type switch
            {
                "slider"   => BuildSlider(ctrl, configKey, tooltipId),
                "combo"    => BuildCombo(ctrl, configKey, tooltipId),
                "checkbox" => BuildCheckbox(ctrl, configKey, tooltipId),
                _          => BuildTextBox(ctrl, configKey, tooltipId),
            });
        }

        border.Child = stack;

        // Drag & drop via title to reorder param panels
        Point? panelDragStart = null;
        var dragging = false;

        titleBlock.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(titleBlock).Properties.IsLeftButtonPressed) return;
            panelDragStart = e.GetPosition(container);
            dragging = false;
            e.Pointer.Capture(titleBlock);
            e.Handled = true;
        };

        titleBlock.PointerMoved += (_, e) =>
        {
            if (panelDragStart is null) return;
            var pos = e.GetPosition(container);
            if (!dragging && Math.Abs(pos.X - panelDragStart.Value.X) < 14) return;
            dragging = true;
            border.Opacity = 0.5;
            var insertIdx = GetPanelInsertIndex(container, pos.X, border);
            ShowPanelDropIndicator(container, insertIdx, border);
            e.Handled = true;
        };

        titleBlock.PointerReleased += (_, e) =>
        {
            if (panelDragStart is null) return;
            e.Pointer.Capture(null);
            var wasDragging = dragging;
            panelDragStart = null;
            dragging = false;
            border.Opacity = 1.0;
            RemovePanelDropIndicator(container);

            if (wasDragging)
            {
                var pos = e.GetPosition(container);
                var insertIdx = GetPanelInsertIndex(container, pos.X, border);
                FinishPanelDrag(container, border, insertIdx);
            }
            e.Handled = true;
        };

        container.Children.Add(border);
    }

    private void RemoveParamPanel(string panelName)
    {
        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        if (container is null) return;
        for (var i = container.Children.Count - 1; i >= 0; i--)
            if (container.Children[i] is Border b && b.Name == panelName)
                container.Children.RemoveAt(i);
    }

    // ── Control builders ─────────────────────────────────────────────

    private TextBlock MakeParamLabel(CustomFilterControl ctrl, string filterId, Thickness? margin = null)
    {
        var label = new TextBlock
        {
            Text = ctrl.Placeholder,
            Classes = { "param-label" },
            VerticalAlignment = VerticalAlignment.Center
        };
        if (margin is { } m) label.Margin = m;

        string? tip = null;

        // Prefer localized lookup (ParamTooltipKeyMap → ParamTooltipTexts) so that
        // a language change instantly updates the tooltip. The JSON "Description"
        // field is only used as an English fallback for filters that are not
        // present in the localized map.
        var mapKey = $"{filterId}.{ctrl.Placeholder}";
        if (FilterPresets.ParamTooltipKeyMap.TryGetValue(mapKey, out var labelKey))
        {
            // Set Name so ApplyParamTooltips can update on language change
            label.Name = labelKey;
            var lang = _host.ViewModel.CurrentLanguageCode ?? "en";
            if (FilterPresets.ParamTooltipTexts.TryGetValue(labelKey, out var translations))
                tip = translations.TryGetValue(lang, out var t) ? t
                    : translations.TryGetValue("en", out var en) ? en
                    : null;
        }

        if (string.IsNullOrWhiteSpace(tip) && !string.IsNullOrWhiteSpace(ctrl.Description))
        {
            tip = ctrl.Description;
        }

        if (!string.IsNullOrWhiteSpace(tip))
        {
            ToolTip.SetTip(label, tip);
            label.Cursor = new Cursor(StandardCursorType.Help);
            label.TextDecorations = TextDecorations.Underline;
        }
        return label;
    }

    private Control BuildSlider(CustomFilterControl ctrl, string configKey, string filterId)
    {
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,70") };

        var label = MakeParamLabel(ctrl, filterId, new Thickness(0, 0, 8, 0));
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var isFloat = ctrl.Step < 1 || ctrl.Step != Math.Floor(ctrl.Step);
        var slider = new Slider
        {
            Minimum = ctrl.Min, Maximum = ctrl.Max,
            SmallChange = ctrl.Step, LargeChange = ctrl.Step * 10,
            Classes = { "param-slider" },
            Value = ParseDouble(_host.Config.Get(configKey) ?? ctrl.Default, ctrl.Min)
        };
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);

        var textBox = new TextBox
        {
            Text = FormatValue(slider.Value, isFloat),
            Width = 70
        };
        Grid.SetColumn(textBox, 2);
        grid.Children.Add(textBox);

        var syncing = false;
        slider.ValueChanged += (_, args) =>
        {
            if (syncing) return;
            syncing = true;
            textBox.Text = FormatValue(SnapToStep(args.NewValue, ctrl.Min, ctrl.Step), isFloat);
            syncing = false;
        };

        var pressing = false;
        slider.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(slider).Properties.IsLeftButtonPressed) return;
            pressing = true;
            e.Pointer.Capture(slider);
            MoveSliderToPointer(slider, e);
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        slider.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!pressing) return;
            MoveSliderToPointer(slider, e);
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        slider.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (!pressing) return;
            pressing = false;
            e.Pointer.Capture(null);
            var snapped = Math.Clamp(SnapToStep(slider.Value, ctrl.Min, ctrl.Step), ctrl.Min, ctrl.Max);
            slider.Value = snapped;
            CommitValue(configKey, FormatValue(snapped, isFloat));
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        textBox.LostFocus += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            var parsed = Math.Clamp(SnapToStep(ParseDouble(textBox.Text ?? "", ctrl.Min), ctrl.Min, ctrl.Step), ctrl.Min, ctrl.Max);
            slider.Value = parsed;
            var val = FormatValue(parsed, isFloat);
            textBox.Text = val;
            CommitValue(configKey, val);
            syncing = false;
        };

        textBox.PointerWheelChanged += (_, e) =>
        {
            e.Handled = true;
            var delta = e.Delta.Y > 0 ? ctrl.Step : -ctrl.Step;
            slider.Value = Math.Clamp(SnapToStep(slider.Value + delta, ctrl.Min, ctrl.Step), ctrl.Min, ctrl.Max);
            CommitValue(configKey, FormatValue(slider.Value, isFloat));
        };

        return grid;
    }

    private Control BuildCombo(CustomFilterControl ctrl, string configKey, string filterId)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(MakeParamLabel(ctrl, filterId));

        var combo = new ComboBox();
        foreach (var opt in ctrl.Options) combo.Items.Add(opt);
        var current = _host.Config.Get(configKey) ?? ctrl.Default;
        combo.SelectedItem = ctrl.Options.Contains(current) ? current : ctrl.Options.FirstOrDefault();

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string val)
                CommitValue(configKey, val);
        };
        row.Children.Add(combo);
        return row;
    }

    private Control BuildCheckbox(CustomFilterControl ctrl, string configKey, string filterId)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(MakeParamLabel(ctrl, filterId));

        var current = _host.Config.Get(configKey) ?? ctrl.Default;
        var isOn = string.Equals(current, ctrl.OnValue, StringComparison.OrdinalIgnoreCase);

        var toggleBtn = new Button
        {
            Width = 70, Classes = { "toggle" },
            Content = isOn ? ctrl.OnValue : ctrl.OffValue,
            Tag = isOn
        };
        _host.UpdateToggleButtonPresentation(toggleBtn, isOn);
        toggleBtn.Content = isOn ? ctrl.OnValue : ctrl.OffValue;

        toggleBtn.Click += (_, _) =>
        {
            isOn = !isOn;
            toggleBtn.Tag = isOn;
            toggleBtn.Content = isOn ? ctrl.OnValue : ctrl.OffValue;
            _host.UpdateToggleButtonPresentation(toggleBtn, isOn);
            toggleBtn.Content = isOn ? ctrl.OnValue : ctrl.OffValue;
            CommitValue(configKey, isOn ? ctrl.OnValue : ctrl.OffValue);
        };
        row.Children.Add(toggleBtn);
        return row;
    }

    private Control BuildTextBox(CustomFilterControl ctrl, string configKey, string filterId)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(MakeParamLabel(ctrl, filterId));

        var textBox = new TextBox
        {
            Text = _host.Config.Get(configKey) ?? ctrl.Default,
            Width = 120
        };
        textBox.LostFocus += (_, _) => CommitValue(configKey, textBox.Text ?? "");
        row.Children.Add(textBox);
        return row;
    }

    // ── Region draw mode ────────────────────────────────────────────

    private void ActivateRegionDraw(string filterId, Button drawBtn)
    {
        // Deactivate any previous draw mode
        DeactivateRegionDraw();

        _regionDrawFilterId = filterId;
        _regionDrawButton = drawBtn;
        drawBtn.Tag = true;
        _host.UpdateToggleButtonPresentation(drawBtn, true);
        _host.SetRegionDrawMode(filterId, true);
    }

    private void DeactivateRegionDraw()
    {
        if (_regionDrawFilterId is null) return;

        _host.SetRegionDrawMode(_regionDrawFilterId, false);

        if (_regionDrawButton is not null)
        {
            _regionDrawButton.Tag = false;
            _host.UpdateToggleButtonPresentation(_regionDrawButton, false);
            _regionDrawButton = null;
        }
        _regionDrawFilterId = null;
    }

    /// <summary>Called by the host after a region has been drawn and committed.</summary>
    public void OnRegionDrawCompleted()
    {
        DeactivateRegionDraw();
    }

    /// <summary>
    /// Rebuilds a single filter's parameter panel in place, preserving its
    /// position among sibling panels. Use this instead of a full RebuildUI()
    /// when only one panel's values have changed (e.g. after region draw commit).
    /// </summary>
    public void RebuildPanelInPlace(string filterId)
    {
        var filter = _filterService.GetById(filterId);
        if (filter is null) return;

        var panelName = $"CustomPanel_{filterId}";
        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        var index = -1;
        if (container is not null)
        {
            for (var i = 0; i < container.Children.Count; i++)
            {
                if (container.Children[i] is Border b && b.Name == panelName)
                {
                    index = i;
                    container.Children.RemoveAt(i);
                    break;
                }
            }
        }

        BuildParamPanel(filter);

        // Move the newly appended panel back to its original position
        if (container is not null && index >= 0 && index < container.Children.Count)
        {
            var newPanel = container.Children[^1];
            container.Children.RemoveAt(container.Children.Count - 1);
            container.Children.Insert(index, newPanel);
        }
    }

    // ── Reset filter defaults ───────────────────────────────────────

    private void ResetFilterToDefaults(CustomFilter filter)
    {
        foreach (var ctrl in filter.Controls)
        {
            var configKey = ScriptService.GetCustomFilterConfigKey(filter.Id, ctrl.Placeholder);
            _host.Config.Set(configKey, ctrl.Default);
        }

        // Rebuild the panel in place (preserve position in container)
        var panelName = $"CustomPanel_{filter.Id}";
        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        var index = -1;
        if (container is not null)
        {
            for (var i = 0; i < container.Children.Count; i++)
            {
                if (container.Children[i] is Border b && b.Name == panelName)
                {
                    index = i;
                    container.Children.RemoveAt(i);
                    break;
                }
            }
        }

        BuildParamPanel(filter);

        // Move the newly appended panel back to its original position
        if (container is not null && index >= 0 && index < container.Children.Count)
        {
            var newPanel = container.Children[^1];
            container.Children.RemoveAt(container.Children.Count - 1);
            container.Children.Insert(index, newPanel);
        }

        _host.OnCustomFilterValueChanged();
        _host.RegenerateScript(showValidationError: false);
        _ = _host.LoadScriptAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void RemoveFilterConfigKeys(CustomFilter filter)
    {
        var prefix = $"{ScriptService.CustomFilterConfigPrefix}{filter.Id}_";
        var enabledKey = ScriptService.GetCustomFilterEnabledKey(filter.Id);
        var snapshot = _host.Config.Snapshot();
        foreach (var key in snapshot.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || key.Equals(enabledKey, StringComparison.OrdinalIgnoreCase))
                _host.Config.Remove(key);
        }
    }

    private void CommitValue(string configKey, string value)
    {
        _host.Config.Set(configKey, value);
        _host.OnCustomFilterValueChanged();
        _host.RegenerateScript(showValidationError: false);

        // If this config key belongs to a RegionDraw filter's region placeholders, update the overlay
        var regionFilter = _filterService.Filters.FirstOrDefault(f =>
        {
            if (!f.RegionDraw) return false;
            var (p0, p1, p2, p3) = f.GetRegionPlaceholders();
            var regionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { p0, p1, p2, p3 };
            return f.Controls.Any(c =>
                regionKeys.Contains(c.Placeholder)
                && ScriptService.GetCustomFilterConfigKey(f.Id, c.Placeholder)
                    .Equals(configKey, StringComparison.OrdinalIgnoreCase));
        });
        if (regionFilter is not null)
            _host.RefreshRegionOverlay(regionFilter.Id);

        _ = _host.LoadScriptAsync();
    }

    private static string FormatValue(double value, bool isFloat) =>
        isFloat
            ? value.ToString("F2", CultureInfo.InvariantCulture)
            : ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    private static double ParseDouble(string text, double fallback)
    {
        var normalized = (text ?? "").Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

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
        var x = e.GetCurrentPoint(slider).Position.X;
        var ratio = Math.Clamp((x - thumbHalf) / (w - thumbHalf * 2), 0.0, 1.0);
        var raw = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);
        slider.Value = SnapToStep(raw, slider.Minimum, slider.SmallChange);
    }

    // ── Pointer-based drag reordering ──────────────────────────────────

    private Border? _dropIndicator;
    private bool _isDragging;
    private Grid? _draggedRow;

    private void SetupDragDrop(StackPanel list)
    {
        // Capture PointerMoved/Released on the list itself so we track the pointer
        // even when it leaves the dragged row (pointer is captured to list).
        list.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!_isDragging || _dragFilterId is null) return;
            var pos = e.GetPosition(list);
            var insertIndex = GetInsertIndexFromY(list, pos.Y, _dragFilterId);
            ShowDropIndicator(list, insertIndex);
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        list.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (!_isDragging || _dragFilterId is null) return;
            var pos = e.GetPosition(list);
            var insertIndex = GetInsertIndexFromY(list, pos.Y, _dragFilterId);
            FinishDrag(list, _dragFilterId, insertIndex);
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        // Cancel drag if pointer leaves the list area
        list.PointerExited += (_, _) =>
        {
            if (!_isDragging) return;
            CancelDrag(list);
        };
    }

    private int GetInsertIndexFromY(StackPanel list, double y, string draggedId)
    {
        var index = 0;
        foreach (var child in list.Children)
        {
            if (child == _dropIndicator) continue;
            if (child is Grid g && g.Tag is string id && id == draggedId) continue;

            if (child is Control c)
            {
                var mid = c.Bounds.Top + c.Bounds.Height / 2;
                if (y < mid) return index;
            }
            index++;
        }
        return index;
    }

    private void ShowDropIndicator(StackPanel list, int logicalIndex)
    {
        RemoveDropIndicator(list);

        _dropIndicator = new Border
        {
            Height = 2,
            Background = _host.ThemeBrush("AccentGreen"),
            Margin = new Thickness(0, -1, 0, -1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Convert logical index to visual index (skip the dragged row)
        var visualIndex = 0;
        var logical = 0;
        foreach (var child in list.Children)
        {
            if (logical >= logicalIndex) break;
            if (child == _draggedRow) { visualIndex++; continue; }
            logical++;
            visualIndex++;
        }

        list.Children.Insert(Math.Min(visualIndex, list.Children.Count), _dropIndicator);
    }

    private void RemoveDropIndicator(StackPanel list)
    {
        if (_dropIndicator is not null && list.Children.Contains(_dropIndicator))
            list.Children.Remove(_dropIndicator);
        _dropIndicator = null;
    }

    private void FinishDrag(StackPanel list, string draggedId, int insertIndex)
    {
        RemoveDropIndicator(list);
        if (_draggedRow is not null) _draggedRow.Opacity = 1.0;
        _draggedRow = null;
        _isDragging = false;
        _dragStartPos = null;

        // Collect filter IDs visible in THIS list (active or inactive section)
        var listIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in list.Children)
            if (child is Grid g && g.Tag is string id)
                listIds.Add(id);

        // Build ordered subset of filters that are in this list
        var subset = _filterService.Filters
            .Where(f => listIds.Contains(f.Id))
            .OrderBy(f => ScriptService.InjectionPositionToOrder(f.Position))
            .ToList();

        var dragged = subset.FirstOrDefault(f => f.Id == draggedId);
        if (dragged is null) return;

        subset.Remove(dragged);
        var newIndex = Math.Clamp(insertIndex, 0, subset.Count);
        subset.Insert(newIndex, dragged);

        // Reassign numeric positions for ALL filters, keeping relative order
        // of the other section intact
        var otherSection = _filterService.Filters
            .Where(f => !listIds.Contains(f.Id))
            .OrderBy(f => ScriptService.InjectionPositionToOrder(f.Position))
            .ToList();

        // Merge: active filters first (sorted), then inactive (sorted)
        // Determine which section we're dragging in
        var activeList = _host.FindControl<StackPanel>("CustomFiltersList");
        List<CustomFilter> merged;
        if (list == activeList)
            merged = [..subset, ..otherSection];
        else
            merged = [..otherSection, ..subset];

        for (var i = 0; i < merged.Count; i++)
            merged[i].Position = ((i + 1) * 10).ToString();

        _filterService.Save();
        SyncPositionsToConfig();
        RebuildUI();
        _host.RegenerateScript(showValidationError: false);
        _ = _host.LoadScriptAsync();
    }

    private void CancelDrag(StackPanel list)
    {
        RemoveDropIndicator(list);
        if (_draggedRow is not null) _draggedRow.Opacity = 1.0;
        _draggedRow = null;
        _isDragging = false;
        _dragStartPos = null;
    }

    // ── Param panel drag reordering ──────────────────────────────────

    private Border? _panelDropIndicator;

    private int GetPanelInsertIndex(StackPanel container, double x, Border dragged)
    {
        var index = 0;
        foreach (var child in container.Children)
        {
            if (child == _panelDropIndicator || child == dragged) continue;
            if (child is Control c)
            {
                var mid = c.Bounds.Left + c.Bounds.Width / 2;
                if (x < mid) return index;
            }
            index++;
        }
        return index;
    }

    private void ShowPanelDropIndicator(StackPanel container, int logicalIndex, Border dragged)
    {
        RemovePanelDropIndicator(container);

        _panelDropIndicator = new Border
        {
            Width = 3,
            Background = _host.ThemeBrush("AccentGreen"),
            Margin = new Thickness(-1, 0, -1, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var visualIndex = 0;
        var logical = 0;
        foreach (var child in container.Children)
        {
            if (logical >= logicalIndex) break;
            if (child == dragged) { visualIndex++; continue; }
            logical++;
            visualIndex++;
        }

        container.Children.Insert(Math.Min(visualIndex, container.Children.Count), _panelDropIndicator);
    }

    private void RemovePanelDropIndicator(StackPanel container)
    {
        if (_panelDropIndicator is not null && container.Children.Contains(_panelDropIndicator))
            container.Children.Remove(_panelDropIndicator);
        _panelDropIndicator = null;
    }

    private void FinishPanelDrag(StackPanel container, Border dragged, int insertIndex)
    {
        // Get current panel order (excluding indicators)
        var panels = container.Children.OfType<Border>()
            .Where(b => b != _panelDropIndicator && b.Name is { Length: > 0 })
            .ToList();

        var currentIdx = panels.IndexOf(dragged);
        if (currentIdx < 0) return;

        panels.RemoveAt(currentIdx);
        var newIdx = Math.Clamp(insertIndex, 0, panels.Count);
        panels.Insert(newIdx, dragged);

        // Rebuild container in new order
        container.Children.Clear();
        foreach (var p in panels)
            container.Children.Add(p);
    }
}
