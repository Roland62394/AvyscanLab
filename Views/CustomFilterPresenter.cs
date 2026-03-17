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

    // ── Public API called from MainWindow ────────────────────────────

    public void RebuildUI()
    {
        var list = _host.FindControl<StackPanel>("CustomFiltersList");
        if (list is null) return;
        list.Children.Clear();

        var container = _host.FindControl<StackPanel>("CustomParamPanels");
        container?.Children.Clear();

        foreach (var filter in _filterService.Filters)
        {
            AddFilterRow(filter, list);
            var panelName = $"CustomPanel_{filter.Id}";
            if (_host.OpenParamPanels.Contains(panelName))
                BuildParamPanel(filter);
        }
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

    // ── Private: row / panel construction ────────────────────────────

    private void AddFilterRow(CustomFilter filter, StackPanel list)
    {
        var row = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,10,28,4,20"),
            Tag = filter.Id
        };

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
}
