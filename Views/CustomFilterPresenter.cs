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
using CleanScan.Models;
using CleanScan.Services;
using CleanScan.ViewModels;

namespace CleanScan.Views;

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
}

public sealed class CustomFilterPresenter
{
    private readonly IFilterPresenterHost _host;
    private readonly CustomFilterService _filterService;

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

    public void RebuildUI()
    {
        var list = _host.FindControl<StackPanel>("CustomFiltersList");
        if (list is null) return;
        list.Children.Clear();

        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        container?.Children.Clear();

        // Sort filters by pipeline position for display
        var sorted = _filterService.Filters
            .OrderBy(f => ScriptService.InjectionPositionToOrder(f.Position))
            .ToList();

        foreach (var filter in sorted)
        {
            AddFilterRow(filter, list);
            var panelName = $"CustomPanel_{filter.Id}";
            if (_host.OpenParamPanels.Contains(panelName))
                BuildParamPanel(filter);
        }

        // Set up drag & drop on the list
        SetupDragDrop(list);

        // Ensure positions are in ConfigStore for preset capture
        SyncPositionsToConfig();
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
    }

    public async void OnAddClick(object? sender, RoutedEventArgs e)
    {
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
        var picker = await _host.Window.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = _host.GetUiText("CfDlgImportTitle"),
                AllowMultiple = false,
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

        // Sync enabled state from config (preset may have changed it)
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
        else
        {
            // First time: write current enabled state to config so presets can capture it
            _host.Config.Set(enabledKey, filter.Enabled.ToString().ToLowerInvariant());
        }

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
            toggleBtn.Tag = filter.Enabled;
            _host.UpdateToggleButtonPresentation(toggleBtn, filter.Enabled);
            _host.Config.Set(enabledKey, filter.Enabled.ToString().ToLowerInvariant());
            _host.OnCustomFilterValueChanged();
            _filterService.Save();
            _host.RegenerateScript(showValidationError: false);
            _ = _host.LoadScriptAsync();
        };
        Grid.SetColumn(toggleBtn, 0);
        row.Children.Add(toggleBtn);

        var expandBtn = new Button
        {
            Content = "▶",
            Classes = { "expand-btn" },
            Tag = $"CustomPanel_{filter.Id}"
        };
        if (filter.Controls.Count == 0)
        {
            // No params to expand — hide button but keep column space for alignment
            expandBtn.Opacity = 0;
            expandBtn.IsHitTestVisible = false;
        }
        expandBtn.Click += OnExpandClick;
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
            BorderBrush = _host.ThemeBrush("BorderSubtle"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(16, 14),
            MinWidth = 200
        };

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = filter.Name.ToUpperInvariant(),
            Classes = { "section-title" }
        });

        foreach (var ctrl in filter.Controls)
        {
            var configKey = ScriptService.GetCustomFilterConfigKey(filter.Id, ctrl.Placeholder);
            if (string.IsNullOrEmpty(_host.Config.Get(configKey)))
                _host.Config.Set(configKey, ctrl.Default);

            stack.Children.Add(ctrl.Type switch
            {
                "slider"   => BuildSlider(ctrl, configKey),
                "combo"    => BuildCombo(ctrl, configKey),
                "checkbox" => BuildCheckbox(ctrl, configKey),
                _          => BuildTextBox(ctrl, configKey),
            });
        }

        border.Child = stack;
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

    private Control BuildSlider(CustomFilterControl ctrl, string configKey)
    {
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,70") };

        var label = new TextBlock
        {
            Text = ctrl.Placeholder, Classes = { "param-label" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
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

    private Control BuildCombo(CustomFilterControl ctrl, string configKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = ctrl.Placeholder, Classes = { "param-label" },
            VerticalAlignment = VerticalAlignment.Center
        });

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

    private Control BuildCheckbox(CustomFilterControl ctrl, string configKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = ctrl.Placeholder, Classes = { "param-label" },
            VerticalAlignment = VerticalAlignment.Center
        });

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

    private Control BuildTextBox(CustomFilterControl ctrl, string configKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = ctrl.Placeholder, Classes = { "param-label" },
            VerticalAlignment = VerticalAlignment.Center
        });

        var textBox = new TextBox
        {
            Text = _host.Config.Get(configKey) ?? ctrl.Default,
            Width = 120
        };
        textBox.LostFocus += (_, _) => CommitValue(configKey, textBox.Text ?? "");
        row.Children.Add(textBox);
        return row;
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

        // Build current display order (sorted by position)
        var sorted = _filterService.Filters
            .OrderBy(f => ScriptService.InjectionPositionToOrder(f.Position))
            .ToList();

        var dragged = sorted.FirstOrDefault(f => f.Id == draggedId);
        if (dragged is null) return;

        sorted.Remove(dragged);
        var newIndex = Math.Clamp(insertIndex, 0, sorted.Count);
        sorted.Insert(newIndex, dragged);

        // Reassign numeric positions (100, 110, 120, ...) — must be >= 50
        // so filters land in __MODULE_PIPELINE__ (after src_matrix), not PRE
        for (var i = 0; i < sorted.Count; i++)
            sorted[i].Position = (100 + i * 10).ToString();

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
}
