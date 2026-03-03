using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CleanScan.ViewModels;
using CleanScan.Views;

namespace CleanScan.Services;

public sealed class DialogService : IDialogService
{
    private const string AviSynthDownloadUrl = "https://github.com/AviSynth/AviSynthPlus/releases";

    public async Task ShowErrorAsync(Window owner, string title, string message)
    {
        var closeButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center };
        var dialog = BuildSimpleDialog(title, new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, closeButton }
        });
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public async Task ShowTextDialogAsync(Window owner, string title, string text)
    {
        var closeButton = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Center };

        var dialog = BuildSimpleDialog(title, new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            MaxWidth = 720,
            Children =
            {
                new ScrollViewer
                {
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    MaxHeight = 440,
                    Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
                },
                closeButton
            }
        });

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }


    public async Task ShowAboutDialogAsync(
        Window owner,
        string title,
        string company,
        string rights,
        string website,
        string version,
        string closeLabel,
        string imageUri)
    {
        var dialog = new AboutWindow();
        dialog.Configure(title, company, rights, website, version, closeLabel, imageUri);
        await dialog.ShowDialog(owner);
    }

    public async Task ShowScriptPreviewDialogAsync(
        Window owner,
        IScriptService scriptService,
        Action? onReload,
        MainWindowViewModel vm)
    {
        var scriptPath = scriptService.GetPrimaryScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            await ShowErrorAsync(owner, vm.GetUiText("ErrorTitle"),
                vm.GetLocalizedText(fr: "Le script ScriptUser.avs est introuvable.", en: "ScriptUser.avs was not found."));
            return;
        }

        var scriptEditor = new TextBox
        {
            Text = await File.ReadAllTextAsync(scriptPath),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = 900,
            MinHeight = 520,
            FontFamily = new FontFamily("Consolas, Courier New, monospace")
        };

        var reloadButton = MakeButton(vm.GetLocalizedText(fr: "Recharger", en: "Reload"), minWidth: 100);
        var closeButton  = MakeButton(vm.GetUiText("GamMacCloseButton"));

        var dialog = new Window
        {
            Title = vm.GetLocalizedText(fr: "Aperçu du script", en: "Script preview"),
            Width = 980,
            Height = 650,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(12),
                LastChildFill = true,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Children = { reloadButton, closeButton }
                    },
                    scriptEditor
                }
            }
        };

        reloadButton.Click += async (_, _) =>
        {
            if (File.Exists(scriptPath))
                scriptEditor.Text = await File.ReadAllTextAsync(scriptPath);
            onReload?.Invoke();
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public async Task ShowPresetDialogAsync(
        Window owner,
        IPresetService presets,
        ConfigStore config,
        Func<Dictionary<string, string>, Task> applyCallback,
        MainWindowViewModel vm)
    {
        var presetList = presets.LoadPresets();
        var ordered = presetList.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var monoFont = new FontFamily("Consolas,Cascadia Code,monospace");

        var comboBox = new ComboBox
        {
            Width = 300,
            ItemsSource = ordered,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Preset.Name)),
            Background = new SolidColorBrush(Color.Parse("#1A2030")),
            Foreground = new SolidColorBrush(Color.Parse("#7984A5")),
            BorderBrush = new SolidColorBrush(Color.Parse("#252E42")),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var nameBox = new TextBox
        {
            Width = 300,
            Background = new SolidColorBrush(Color.Parse("#1A2030")),
            Foreground = new SolidColorBrush(Color.Parse("#7984A5")),
            BorderBrush = new SolidColorBrush(Color.Parse("#252E42")),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            Height = 30,
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            TextAlignment = TextAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        Button MakePresetActionButton(string label, int minWidth = 92) => new()
        {
            Content = label,
            MinWidth = minWidth,
            Height = 30,
            Padding = new Thickness(8, 0),
            Background = new SolidColorBrush(Color.Parse("#1C2333")),
            Foreground = new SolidColorBrush(Color.Parse("#DBDBDB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#252E42")),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var saveButton = MakePresetActionButton(vm.GetUiText("PresetSaveButton"));
        var updateButton = MakePresetActionButton(vm.GetUiText("PresetUpdateButton"));
        var deleteButton = MakePresetActionButton(vm.GetUiText("PresetDeleteButton"));
        var loadButton = MakePresetActionButton(vm.GetUiText("PresetLoadButton"));
        var closeButton = MakePresetActionButton(vm.GetUiText("GamMacCloseButton"));

        void RefreshCombo()
        {
            ordered = presetList.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            comboBox.ItemsSource = ordered;
        }

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is Preset p) nameBox.Text = p.Name;
        };

        var dialog = new Window
        {
            Title = vm.GetUiText("PresetDialogTitle"),
            Width = 560,
            SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#0F1319")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.Parse("#161B24")),
                BorderBrush = new SolidColorBrush(Color.Parse("#252E42")),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = vm.GetUiText("PresetMenuItem"),
                            Foreground = new SolidColorBrush(Color.Parse("#F6F6F6")),
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold,
                            LetterSpacing = 1.5,
                            FontFamily = monoFont
                        },
                        comboBox,
                        new TextBlock
                        {
                            Text = vm.GetUiText("PresetNameLabel"),
                            Foreground = new SolidColorBrush(Color.Parse("#F6F6F6")),
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold,
                            LetterSpacing = 1.5,
                            FontFamily = monoFont,
                            Margin = new Thickness(0, 6, 0, 0)
                        },
                        nameBox,
                        new StackPanel
                        {
                            Margin = new Thickness(0, 6, 0, 0),
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { saveButton, updateButton, deleteButton, loadButton, closeButton }
                        }
                    }
                }
            }
        };

        string TrimmedName() => (nameBox.Text ?? string.Empty).Trim();
        Preset? FindPreset() =>
            comboBox.SelectedItem as Preset
            ?? presetList.FirstOrDefault(p => string.Equals(p.Name, TrimmedName(), StringComparison.OrdinalIgnoreCase));

        saveButton.Click += (_, _) =>
        {
            var name = TrimmedName();
            if (string.IsNullOrWhiteSpace(name) || presetList.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) return;
            presetList.Add(new Preset(name, presets.CaptureCurrentValues(config)));
            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = ordered.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        };

        updateButton.Click += (_, _) =>
        {
            var existing = presetList.FirstOrDefault(p => string.Equals(p.Name, TrimmedName(), StringComparison.OrdinalIgnoreCase));
            if (existing is null) return;
            existing.Values = presets.CaptureCurrentValues(config);
            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = ordered.FirstOrDefault(p => string.Equals(p.Name, existing.Name, StringComparison.OrdinalIgnoreCase));
        };

        deleteButton.Click += (_, _) =>
        {
            var target = FindPreset();
            if (target is null) return;
            presetList.RemoveAll(p => string.Equals(p.Name, target.Name, StringComparison.OrdinalIgnoreCase));
            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = null;
            nameBox.Text = string.Empty;
        };

        loadButton.Click += async (_, _) =>
        {
            var target = FindPreset();
            if (target is not null) await applyCallback(target.Values);
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public async Task<bool> ShowAviSynthMissingDialogAsync(Window owner, MainWindowViewModel vm)
    {
        var downloadButton = new Button { Content = vm.GetUiText("DownloadButton"), HorizontalAlignment = HorizontalAlignment.Center };
        var closeButton    = new Button { Content = vm.GetUiText("GamMacCloseButton"), HorizontalAlignment = HorizontalAlignment.Center };

        var dialog = BuildSimpleDialog(vm.GetUiText("AviSynthRequiredTitle"), new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = vm.GetUiText("AviSynthNotDetectedBody"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { downloadButton, closeButton }
                }
            }
        });

        downloadButton.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = AviSynthDownloadUrl, UseShellExecute = true }); } catch { }
            dialog.Close();
        };
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);

        // Return true: caller decides whether AviSynth is now installed
        return true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Window BuildSimpleDialog(string title, Control content) => new()
    {
        Title = title,
        SizeToContent = SizeToContent.WidthAndHeight,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = content
    };

    private static Button MakeButton(string label, int minWidth = 96) =>
        new() { Content = label, MinWidth = minWidth };
}
