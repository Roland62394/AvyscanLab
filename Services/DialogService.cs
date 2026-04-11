using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using AvyScanLab.ViewModels;
using AvyScanLab.Views;

namespace AvyScanLab.Services;

public sealed class DialogService : IDialogService
{
    private const string AviSynthDownloadUrl = "https://github.com/AviSynth/AviSynthPlus/releases";

    private static SolidColorBrush TB(string key) => ThemeService.Brush(key);

    public async Task ShowErrorAsync(Window owner, string title, string message, string? details = null)
    {
        var monoFont = UiConstants.MonoFont;
        var fgError  = new SolidColorBrush(Color.Parse("#FF6B6B"));

        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = fgError,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            FontFamily = monoFont,
            Margin = new Thickness(0, 0, 0, 6)
        };

        // Primary error message — prominent
        var messageBlock = new TextBlock
        {
            Text = message,
            Foreground = TB("TextSecondary"),
            FontFamily = monoFont,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(titleBlock);
        content.Children.Add(messageBlock);

        // Optional details section — collapsible
        if (!string.IsNullOrWhiteSpace(details))
        {
            var detailsBox = new TextBox
            {
                Text = details,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 250,
                FontFamily = monoFont,
                FontSize = 11,
                Background = TB("BgDeep"),
                Foreground = TB("TextPrimary"),
                BorderBrush = TB("BorderSubtle"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8),
            };

            var expander = new Expander
            {
                Header = "Détails (sortie ffmpeg)",
                IsExpanded = false,
                Foreground = TB("TextPrimary"),
                FontFamily = monoFont,
                FontSize = 11,
                Content = detailsBox,
                Margin = new Thickness(0, 4, 0, 0)
            };
            content.Children.Add(expander);
        }

        var copyButton = new Button
        {
            Content = "Copier",
            MinWidth = 96, Height = 30,
            Background = TB("BgHeader"),
            Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var closeButton = new Button
        {
            Content = "OK",
            MinWidth = 96, Height = 30,
            Background = TB("BgHeader"),
            Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        copyButton.Click += async (_, _) =>
        {
            if (owner.Clipboard is { } clipboard)
            {
                var fullText = details != null ? message + "\n\n--- Détails ---\n" + details : message;
                await clipboard.SetTextAsync(fullText);
                copyButton.Content = "Copié !";
            }
        };

        var buttonBar = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(copyButton, Dock.Left);
        DockPanel.SetDock(closeButton, Dock.Right);
        buttonBar.Children.Add(copyButton);
        buttonBar.Children.Add(closeButton);
        content.Children.Add(buttonBar);

        var dialog = new Window
        {
            Title = title,
            Width = 580,
            SizeToContent = SizeToContent.Height,
            Background = TB("BgDeep"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(16),
                Background = TB("BgPanel"),
                BorderBrush = new SolidColorBrush(Color.Parse("#C62828")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = content
            }
        };
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public async Task ShowInfoAsync(Window owner, string title, string message)
    {
        var messageBlock = new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TB("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var closeBtn = MakeButton("OK");

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { closeBtn }
        };

        var panel = new StackPanel
        {
            Width = 400,
            Margin = new Thickness(20),
            Children = { messageBlock, buttonPanel }
        };

        var dlg = BuildSimpleDialog(title, panel);
        closeBtn.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(owner);
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
            FontFamily = UiConstants.CodeFont
        };

        var copyButton   = MakeButton(vm.GetLocalizedText(fr: "Copier dans le presse-papier", en: "Copy to clipboard"), minWidth: 200);
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
                        Children = { copyButton, reloadButton, closeButton }
                    },
                    scriptEditor
                }
            }
        };

        copyButton.Click += async (_, _) =>
        {
            if (dialog.Clipboard is { } clipboard && !string.IsNullOrEmpty(scriptEditor.Text))
            {
                await clipboard.SetTextAsync(scriptEditor.Text);
                copyButton.Content = vm.GetLocalizedText(fr: "✓ Copié !", en: "✓ Copied!");
                await Task.Delay(1500);
                copyButton.Content = vm.GetLocalizedText(fr: "Copier dans le presse-papier", en: "Copy to clipboard");
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

    public async Task<PresetDialogResult> ShowPresetDialogAsync(
        Window owner,
        IPresetService presets,
        ConfigStore config,
        MainWindowViewModel vm,
        string? activePresetName = null)
    {
        var result = new PresetDialogResult();
        var presetList = presets.LoadPresets();
        var ordered = presetList.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var monoFont = UiConstants.MonoFont;

        var comboBox = new ComboBox
        {
            ItemsSource = ordered,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Preset.Name)),
            Background = TB("BgInput"),
            Foreground = TB("TextPrimary"),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            Height = 32,
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        Button MakePresetActionButton(string label, int minWidth = 92) => new()
        {
            Content = label,
            MinWidth = minWidth,
            Height = 32,
            Padding = new Thickness(12, 0),
            Background = TB("BgHeader"),
            Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        var newButton = MakePresetActionButton(vm.GetUiText("PresetNewButton"));
        var updateButton = MakePresetActionButton(vm.GetUiText("PresetUpdateButton"));
        updateButton.IsEnabled = false;
        var applyButton = new Button
        {
            Content = vm.GetUiText("PresetApplyButton"),
            MinWidth = 110,
            Height = 32,
            Padding = new Thickness(12, 0),
            Background = new SolidColorBrush(Color.Parse("#2A5A8C")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3B82C4")),
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            IsEnabled = false
        };
        var closeButton = MakePresetActionButton(vm.GetUiText("GamMacCloseButton"));

        // Red cross button to delete the selected preset
        var deleteXButton = new Button
        {
            Content = "✕",
            Width = 32, Height = 32,
            Padding = new Thickness(0),
            Background = TB("BgInput"),
            Foreground = new SolidColorBrush(Color.Parse("#E53935")),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            FontSize = 14, FontWeight = FontWeight.Bold,
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        // Checkbox: apply to all clips
        var applyAllCheckBox = new CheckBox
        {
            Content = vm.GetUiText("PresetApplyAllCheckbox"),
            Foreground = TB("TextSecondary"),
            FontFamily = monoFont,
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };

        void RefreshCombo()
        {
            ordered = presetList.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            comboBox.ItemsSource = ordered;
        }

        var dialogTitle = new TextBlock
        {
            Text = vm.GetUiText("PresetDialogTitle"),
            Foreground = new SolidColorBrush(Color.Parse("#64B5F6")),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            FontFamily = monoFont,
            Margin = new Thickness(0, 0, 0, 2)
        };

        // Row: combobox (stretch) + delete cross
        var comboRow = new DockPanel
        {
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Right,
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(6, 0, 0, 0),
                    Children = { deleteXButton }
                },
                comboBox
            }
        };

        var dialog = new Window
        {
            Title = vm.GetUiText("PresetDialogTitle"),
            Width = 620,
            SizeToContent = SizeToContent.Height,
            Background = TB("BgDeep"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(14),
                Padding = new Thickness(16),
                Background = TB("BgPanel"),
                BorderBrush = TB("BorderSubtle"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        dialogTitle,
                        comboRow,
                        applyAllCheckBox,
                        new Rectangle { Height = 1, Fill = TB("BorderSubtle"), Margin = new Thickness(0, 4, 0, 4) },
                        new DockPanel
                        {
                            Children =
                            {
                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 8,
                                    [DockPanel.DockProperty] = Dock.Right,
                                    Children = { closeButton }
                                },
                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 8,
                                    Children = { applyButton, newButton, updateButton }
                                }
                            }
                        }
                    }
                }
            }
        };

        async Task<(bool Created, string Name)> PromptCreatePresetAsync()
        {
            var nameBox = new TextBox
            {
                Width = 320,
                Background = TB("BgInput"),
                Foreground = TB("TextSecondary"),
                BorderBrush = TB("BorderSubtle"),
                BorderThickness = new Thickness(1),
                FontFamily = monoFont,
                Height = 30,
                Padding = new Thickness(8, 0),
                TextAlignment = TextAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            bool created = false;
            var createButton = MakePresetActionButton(vm.GetUiText("PresetCreateConfirmButton"), 180);
            var cancelButton = MakePresetActionButton(vm.GetUiText("GamMacCloseButton"), 120);

            var createTitle = new TextBlock
            {
                Text = vm.GetUiText("PresetCreateTitle"),
                Foreground = new SolidColorBrush(Color.Parse("#64B5F6")),
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                FontFamily = monoFont,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var createDialog = new Window
            {
                Title = vm.GetUiText("PresetCreateTitle"),
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = TB("BgDeep"),
                Content = new Border
                {
                    Margin = new Thickness(16),
                    Padding = new Thickness(14),
                    Background = TB("BgPanel"),
                    BorderBrush = TB("BorderSubtle"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            createTitle,
                            new TextBlock { Text = vm.GetUiText("PresetCreateNameLabel"), FontFamily = monoFont, Foreground = TB("TextLabel") },
                            nameBox,
                            new TextBlock { Text = vm.GetUiText("PresetCreateHint"), TextWrapping = TextWrapping.Wrap, FontFamily = monoFont, Foreground = TB("TextSecondary"), MaxWidth = 420, Margin = new Thickness(0,4,0,4) },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 8,
                                Children = { createButton, cancelButton }
                            }
                        }
                    }
                }
            };

            createButton.Click += async (_, _) =>
            {
                var name = (nameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                var exists = presetList.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    await ShowErrorAsync(createDialog, vm.GetUiText("PresetDuplicateNameTitle"), string.Format(vm.GetUiText("PresetDuplicateNameMessage"), name));
                    return;
                }

                created = true;
                createDialog.Close();
            };
            cancelButton.Click += (_, _) => createDialog.Close();

            await createDialog.ShowDialog(dialog);

            var newName = (nameBox.Text ?? string.Empty).Trim();
            return (created && !string.IsNullOrWhiteSpace(newName), newName);
        }

        async Task<bool> AskConfirmAsync(string title, string message)
        {
            bool confirmed = false;
            var confirmBtn = MakePresetActionButton(vm.GetUiText("PresetConfirmButton"), 120);
            var cancelBtn = MakePresetActionButton(vm.GetUiText("GamMacCloseButton"), 120);

            var confirmDialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = TB("BgDeep"),
                Content = new Border
                {
                    Margin = new Thickness(16),
                    Padding = new Thickness(14),
                    Background = TB("BgPanel"),
                    BorderBrush = TB("BorderSubtle"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child = new StackPanel
                    {
                        Spacing = 12,
                        MaxWidth = 420,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                Foreground = new SolidColorBrush(Color.Parse("#64B5F6")),
                                FontSize = 15, FontWeight = FontWeight.SemiBold,
                                FontFamily = monoFont,
                                Margin = new Thickness(0, 0, 0, 6)
                            },
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = TB("TextSecondary"),
                                FontFamily = monoFont
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 8,
                                Children = { confirmBtn, cancelBtn }
                            }
                        }
                    }
                }
            };

            confirmBtn.Click += (_, _) => { confirmed = true; confirmDialog.Close(); };
            cancelBtn.Click += (_, _) => confirmDialog.Close();
            await confirmDialog.ShowDialog(dialog);
            return confirmed;
        }

        // Sync update button enabled state with combobox selection
        comboBox.SelectionChanged += (_, _) =>
        {
            var hasSelection = comboBox.SelectedItem is Preset;
            updateButton.IsEnabled = hasSelection;
            applyButton.IsEnabled = hasSelection;
        };

        // Pre-select active preset without triggering anything
        if (!string.IsNullOrWhiteSpace(activePresetName))
        {
            comboBox.SelectedItem = ordered.FirstOrDefault(p => string.Equals(p.Name, activePresetName, StringComparison.OrdinalIgnoreCase));
        }

        // "Set preset" — save current settings as a new preset
        newButton.Click += async (_, _) =>
        {
            var result = await PromptCreatePresetAsync();
            if (!result.Created) return;

            var captured = presets.CaptureCurrentValues(config);
            presetList.Add(new Preset(result.Name, captured));
            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = ordered.FirstOrDefault(p => string.Equals(p.Name, result.Name, StringComparison.OrdinalIgnoreCase));
        };

        // "Update preset" — overwrite selected preset with current settings
        updateButton.Click += async (_, _) =>
        {
            if (comboBox.SelectedItem is not Preset target) return;
            var confirmed = await AskConfirmAsync(
                vm.GetUiText("PresetUpdateConfirmTitle"),
                string.Format(vm.GetUiText("PresetUpdateConfirmMessage"), target.Name));
            if (!confirmed) return;

            var captured = presets.CaptureCurrentValues(config);
            target.Values = new Dictionary<string, string>(captured, StringComparer.OrdinalIgnoreCase);
            presets.SavePresets(presetList);
            result.UpdatedPresets[target.Name] = new Dictionary<string, string>(captured, StringComparer.OrdinalIgnoreCase);
        };

        // Red cross — delete selected preset with confirmation
        deleteXButton.Click += async (_, _) =>
        {
            if (comboBox.SelectedItem is not Preset target) return;
            var confirmed = await AskConfirmAsync(
                vm.GetUiText("PresetDeleteConfirmTitle"),
                string.Format(vm.GetUiText("PresetDeleteConfirmMessage"), target.Name));
            if (!confirmed) return;

            presetList.RemoveAll(p => string.Equals(p.Name, target.Name, StringComparison.OrdinalIgnoreCase));
            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = null;
        };

        // "Apply" — apply selected preset (to active clip, or all clips if checkbox checked)
        applyButton.Click += async (_, _) =>
        {
            if (comboBox.SelectedItem is not Preset p || p.Values is null) return;
            var applyToAll = applyAllCheckBox.IsChecked == true;

            if (applyToAll)
            {
                var confirmed = await AskConfirmAsync(
                    vm.GetUiText("PresetApplyAllConfirmTitle"),
                    string.Format(vm.GetUiText("PresetApplyAllConfirmMessage"), p.Name));
                if (!confirmed) return;
            }

            result.Apply = (p.Name, new Dictionary<string, string>(p.Values, StringComparer.OrdinalIgnoreCase), applyToAll);
            dialog.Close();
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
        return result;
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

    public async Task ShowFeedbackDialogAsync(Window owner, MainWindowViewModel vm)
    {
        const string FormspreeEndpoint = "https://formspree.io/f/mvzwglbv";

        var lang = vm.CurrentLanguageCode;
        string L(string en, string fr, string de, string es) => lang switch
        {
            "fr" => fr, "de" => de, "es" => es, _ => en
        };

        var monoFont = UiConstants.MonoFont;

        TextBlock MakeLabel(string text) => new()
        {
            Text = text, Foreground = TB("TextLabel"), FontSize = 11,
            FontWeight = FontWeight.SemiBold, FontFamily = monoFont,
            Margin = new Thickness(0, 4, 0, 2)
        };

        TextBox MakeField(string placeholder = "", int height = 30) => new()
        {
            Watermark = placeholder, Height = height,
            Background = TB("BgInput"), Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"), BorderThickness = new Thickness(1),
            FontFamily = monoFont, Padding = new Thickness(8, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var nameBox = MakeField(L("Your name", "Votre nom", "Ihr Name", "Su nombre"));
        var emailBox = MakeField(L("Your email", "Votre email", "Ihre E-Mail", "Su email"));

        // Category
        var categoryCombo = new ComboBox
        {
            Height = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = TB("BgInput"), Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"), BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            ItemsSource = new[]
            {
                L("Bug report", "Signalement de bug", "Fehlerbericht", "Reporte de error"),
                L("Feature request", "Demande de fonctionnalité", "Funktionswunsch", "Solicitud de función"),
                L("General feedback", "Avis général", "Allgemeines Feedback", "Comentario general")
            },
            SelectedIndex = 0
        };

        // Bug help text (shown when "Bug report" category is selected)
        var bugHelpText = new TextBlock
        {
            Text = L(
                "A few details help us fix things faster:\nWhat were you doing? What happened vs. what you expected?\nAny info about your video files or active filters is a bonus!",
                "Quelques d\u00e9tails nous aident \u00e0 corriger plus vite :\nQue faisiez-vous\u00a0? Que s\u2019est-il pass\u00e9 par rapport \u00e0 ce que vous attendiez\u00a0?\nToute info sur vos fichiers vid\u00e9o ou filtres actifs est un plus\u00a0!",
                "Ein paar Details helfen uns, schneller zu helfen:\nWas haben Sie gemacht? Was ist passiert vs. was Sie erwartet haben?\nInfos zu Ihren Videodateien oder aktiven Filtern sind ein Bonus!",
                "Unos detalles nos ayudan a resolver m\u00e1s r\u00e1pido:\n\u00bfQu\u00e9 estabas haciendo? \u00bfQu\u00e9 pas\u00f3 vs. lo que esperabas?\n\u00a1Cualquier info sobre tus archivos de v\u00eddeo o filtros activos es un plus!"),
            FontSize = 11, FontFamily = monoFont,
            Foreground = new SolidColorBrush(Color.Parse("#D4A846")),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = true, // Bug is index 0 by default
            Margin = new Thickness(0, 2, 0, 4)
        };

        categoryCombo.SelectionChanged += (_, _) =>
        {
            bugHelpText.IsVisible = categoryCombo.SelectedIndex == 0;
        };

        // Star rating
        var starButtons = new Button[5];
        var starPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        int selectedRating = 0;

        void UpdateStars(int rating)
        {
            selectedRating = rating;
            for (int i = 0; i < 5; i++)
                starButtons[i].Content = i < rating ? "★" : "☆";
        }

        for (int i = 0; i < 5; i++)
        {
            int starIndex = i + 1;
            starButtons[i] = new Button
            {
                Content = "☆", FontSize = 22, Padding = new Thickness(2, 0),
                Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.Parse("#FFD700")),
                BorderThickness = new Thickness(0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            starButtons[i].Click += (_, _) => UpdateStars(starIndex);
            starPanel.Children.Add(starButtons[i]);
        }

        // Message
        var messageBox = new TextBox
        {
            Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Watermark = L("Describe your issue or suggestion...",
                          "Décrivez votre problème ou suggestion...",
                          "Beschreiben Sie Ihr Problem oder Ihren Vorschlag...",
                          "Describa su problema o sugerencia..."),
            Background = TB("BgInput"), Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"), BorderThickness = new Thickness(1),
            FontFamily = monoFont, Padding = new Thickness(8, 6),
            VerticalContentAlignment = VerticalAlignment.Top
        };

        // Status text
        var statusText = new TextBlock
        {
            Text = "", FontSize = 11, FontFamily = monoFont,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var sendButton = new Button
        {
            Content = L("Send", "Envoyer", "Senden", "Enviar"),
            MinWidth = 120, Height = 32, Padding = new Thickness(16, 0),
            Background = new SolidColorBrush(Color.Parse("#35C156")),
            Foreground = Brushes.White, FontWeight = FontWeight.SemiBold,
            FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var closeButton = new Button
        {
            Content = L("Close", "Fermer", "Schließen", "Cerrar"),
            MinWidth = 96, Height = 32, Padding = new Thickness(16, 0),
            Background = TB("BgHeader"),
            Foreground = TB("TextSecondary"), FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var dialog = new Window
        {
            Title = "Contact",
            Width = 480, SizeToContent = SizeToContent.Height,
            Background = TB("BgDeep"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(16), Padding = new Thickness(14),
                Background = TB("BgPanel"),
                BorderBrush = TB("BorderSubtle"), BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        MakeLabel(L("Name", "Nom", "Name", "Nombre")),
                        nameBox,
                        MakeLabel(L("Email", "Email", "E-Mail", "Email")),
                        emailBox,
                        MakeLabel(L("Category", "Catégorie", "Kategorie", "Categoría")),
                        categoryCombo,
                        bugHelpText,
                        MakeLabel(L("Message", "Message", "Nachricht", "Mensaje")),
                        messageBox,
                        MakeLabel(L("Rating", "Note", "Bewertung", "Valoración")),
                        starPanel,
                        statusText,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8, Margin = new Thickness(0, 8, 0, 0),
                            Children = { sendButton, closeButton }
                        }
                    }
                }
            }
        };

        sendButton.Click += async (_, _) =>
        {
            var name = nameBox.Text?.Trim() ?? "";
            var email = emailBox.Text?.Trim() ?? "";
            var message = messageBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(message))
            {
                statusText.Foreground = new SolidColorBrush(Color.Parse("#C62828"));
                statusText.Text = L("Please enter a message.", "Veuillez saisir un message.",
                    "Bitte geben Sie eine Nachricht ein.", "Por favor, introduzca un mensaje.");
                return;
            }

            sendButton.IsEnabled = false;
            statusText.Foreground = TB("TextPrimary");
            statusText.Text = L("Sending...", "Envoi en cours...", "Wird gesendet...", "Enviando...");

            try
            {
                var category = categoryCombo.SelectedItem?.ToString() ?? "";
                var stars = selectedRating > 0 ? new string('★', selectedRating) + new string('☆', 5 - selectedRating) : "—";

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var payload = new Dictionary<string, string>
                {
                    ["_subject"] = $"AvyScan Lab Feedback: {category}",
                    ["name"] = string.IsNullOrWhiteSpace(name) ? "Anonymous" : name,
                    ["email"] = string.IsNullOrWhiteSpace(email) ? "noreply@avyscanlab.app" : email,
                    ["category"] = category,
                    ["rating"] = stars,
                    ["message"] = message
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync(FormspreeEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    statusText.Foreground = new SolidColorBrush(Color.Parse("#35C156"));
                    statusText.Text = L("Sent successfully! Thank you.",
                        "Envoyé avec succès ! Merci.",
                        "Erfolgreich gesendet! Danke.",
                        "Enviado con éxito! Gracias.");
                    sendButton.Content = L("Sent", "Envoyé", "Gesendet", "Enviado");
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    statusText.Foreground = new SolidColorBrush(Color.Parse("#C62828"));
                    statusText.Text = L($"Send failed ({response.StatusCode}). {body}",
                        $"Échec de l'envoi ({response.StatusCode}). {body}",
                        $"Senden fehlgeschlagen ({response.StatusCode}). {body}",
                        $"Error al enviar ({response.StatusCode}). {body}");
                    sendButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                statusText.Foreground = new SolidColorBrush(Color.Parse("#C62828"));
                statusText.Text = L($"Connection error: {ex.Message}",
                    $"Erreur de connexion : {ex.Message}",
                    $"Verbindungsfehler: {ex.Message}",
                    $"Error de conexión: {ex.Message}");
                sendButton.IsEnabled = true;
            }
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
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
        new()
        {
            Content = label,
            MinWidth = minWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    public async Task<(bool OpenContact, bool DontShowAgain)> ShowExitFeedbackDialogAsync(Window owner, MainWindowViewModel vm)
    {
        var lang = vm.CurrentLanguageCode;
        string L(string en, string fr, string de, string es) => lang switch
        {
            "fr" => fr, "de" => de, "es" => es, _ => en
        };

        var openContact = false;
        var dontShow = false;

        var titleText = new TextBlock
        {
            Text = L("Thank you for using AvyScan Lab!",
                "Merci d\u2019utiliser AvyScan Lab\u00a0!",
                "Vielen Dank f\u00fcr die Nutzung von AvyScan Lab!",
                "\u00a1Gracias por usar AvyScan Lab!"),
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = TB("TextLabel"),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var messageText = new TextBlock
        {
            Text = L(
                "Your feedback helps us improve this software.\nFeel free to share any issues, suggestions or ideas via the Contact form.",
                "Vos retours nous aident \u00e0 am\u00e9liorer ce logiciel.\nN\u2019h\u00e9sitez pas \u00e0 partager vos remarques, probl\u00e8mes ou id\u00e9es via le formulaire Contact.",
                "Ihr Feedback hilft uns, diese Software zu verbessern.\nTeilen Sie uns gerne Probleme, Vorschl\u00e4ge oder Ideen \u00fcber das Kontaktformular mit.",
                "Tus comentarios nos ayudan a mejorar este software.\nNo dudes en compartir problemas, sugerencias o ideas a trav\u00e9s del formulario de Contacto."),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TB("TextPrimary"),
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var separator = new Border
        {
            Height = 1,
            Background = TB("BorderSubtle"),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var checkBox = new CheckBox
        {
            Content = L(
                "Don\u2019t show this message again",
                "Ne plus afficher ce message",
                "Diese Meldung nicht mehr anzeigen",
                "No mostrar este mensaje de nuevo"),
            FontSize = 12,
            Foreground = TB("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 0)
        };

        var closeBtn = new Button
        {
            Content = L("Close", "Fermer", "Schlie\u00dfen", "Cerrar"),
            MinWidth = 80,
            Height = 28,
            FontSize = 12,
            Padding = new Thickness(12, 4),
            Background = Brushes.Transparent,
            Foreground = TB("TextSecondary"),
            BorderBrush = TB("BorderSubtle"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var contactBtn = new Button
        {
            Content = L("Contact", "Contact", "Kontakt", "Contacto"),
            MinWidth = 80,
            Height = 28,
            FontSize = 12,
            Padding = new Thickness(12, 4),
            Background = TB("AccentGreen"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var buttonGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { closeBtn, contactBtn }
        };

        var bottomRow = new DockPanel
        {
            Margin = new Thickness(0, 16, 0, 0)
        };
        DockPanel.SetDock(buttonGroup, Dock.Right);
        bottomRow.Children.Add(buttonGroup);
        bottomRow.Children.Add(checkBox);

        var panel = new StackPanel
        {
            Width = 460,
            Margin = new Thickness(24, 24, 24, 20),
            Children = { titleText, messageText, separator, bottomRow }
        };

        var dlg = BuildSimpleDialog(
            L("Before you go\u2026", "Avant de partir\u2026", "Bevor Sie gehen\u2026", "Antes de irte\u2026"),
            panel);

        closeBtn.Click += (_, _) => { dontShow = checkBox.IsChecked == true; dlg.Close(); };
        contactBtn.Click += (_, _) => { openContact = true; dontShow = checkBox.IsChecked == true; dlg.Close(); };

        await dlg.ShowDialog(owner);
        return (openContact, dontShow);
    }

    public async Task ShowUpdateAvailableDialogAsync(Window owner, MainWindowViewModel vm, string latestVersion, string downloadUrl)
    {
        var lang = vm.CurrentLanguageCode;
        string L(string en, string fr, string de, string es) => lang switch
        {
            "fr" => fr, "de" => de, "es" => es, _ => en
        };

        var messageText = new TextBlock
        {
            Text = L(
                $"A new version of AvyScan Lab is available: v{latestVersion}\nYou are currently using v{UpdateService.CurrentVersion}.",
                $"Une nouvelle version de AvyScan Lab est disponible : v{latestVersion}\nVous utilisez actuellement la v{UpdateService.CurrentVersion}.",
                $"Eine neue Version von AvyScan Lab ist verf\u00fcgbar: v{latestVersion}\nSie verwenden derzeit v{UpdateService.CurrentVersion}.",
                $"Una nueva versi\u00f3n de AvyScan Lab est\u00e1 disponible: v{latestVersion}\nActualmente usa la v{UpdateService.CurrentVersion}."),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TB("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var closeBtn = MakeButton(L("Later", "Plus tard", "Sp\u00e4ter", "M\u00e1s tarde"));
        var downloadBtn = MakeButton(L("Download", "T\u00e9l\u00e9charger", "Herunterladen", "Descargar"), 140);
        downloadBtn.Background = TB("AccentGreen");
        downloadBtn.Foreground = Brushes.White;

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12,
            Children = { closeBtn, downloadBtn }
        };

        var panel = new StackPanel
        {
            Width = 340,
            Margin = new Thickness(24, 20, 24, 16),
            Children = { messageText, buttonPanel }
        };

        var dlg = BuildSimpleDialog(
            L("Update available", "Mise \u00e0 jour disponible", "Update verf\u00fcgbar", "Actualizaci\u00f3n disponible"),
            panel);

        closeBtn.Click += (_, _) => dlg.Close();
        downloadBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true }); }
            catch { /* ignore */ }
            dlg.Close();
        };

        await dlg.ShowDialog(owner);
    }

    // ── User Guide Editor ──────────────────────────────────────────────────

    public async Task ShowUserGuideEditorAsync(Window owner, MainWindowViewModel vm)
    {
        var lang = vm.CurrentLanguageCode;
        string L(string en, string fr, string de, string es) => lang switch
        {
            "fr" => fr, "de" => de, "es" => es, _ => en
        };

        var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
        var languages = new[] { "fr", "en", "de", "es" };
        var langLabels = new Dictionary<string, string>
        {
            ["fr"] = "Français", ["en"] = "English", ["de"] = "Deutsch", ["es"] = "Español"
        };

        // ── State ──
        var currentLang = lang;
        var isEditMode = false;
        var hasUnsavedChanges = false;
        var isWrap = false;

        // ── Helpers to resolve file paths ──
        string OriginalPath(string lg) =>
            System.IO.Path.Combine(exeDir, "Users Guide", $"AvyScanLab_Guide_{lg}.txt");

        string UserNotesPath(string lg) =>
            AppConstants.GetAppDataPath($"UserGuide_{lg}.txt");

        string LoadText(string lg)
        {
            var userPath = UserNotesPath(lg);
            if (File.Exists(userPath)) return File.ReadAllText(userPath, Encoding.UTF8);
            var origPath = OriginalPath(lg);
            return File.Exists(origPath) ? File.ReadAllText(origPath, Encoding.UTF8) : string.Empty;
        }

        bool HasUserNotes(string lg) => File.Exists(UserNotesPath(lg));

        void SaveUserNotes(string lg, string text)
        {
            var dir = System.IO.Path.GetDirectoryName(AppConstants.GetAppDataPath("_"))!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(UserNotesPath(lg), text, Encoding.UTF8);
        }

        // ── Highlight data per language ──
        string HighlightPath(string lg) =>
            AppConstants.GetAppDataPath($"UserGuide_{lg}_highlights.json");

        List<(int Start, int Length, int ColorIdx)> LoadHighlights(string lg)
        {
            var path = HighlightPath(lg);
            if (!File.Exists(path)) return new();
            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<int[]>>(
                    File.ReadAllText(path, Encoding.UTF8));
                return items?.Select(a => (a[0], a[1], a[2])).ToList() ?? new();
            }
            catch { return new(); }
        }

        void SaveHighlights(string lg, List<(int Start, int Length, int ColorIdx)> list)
        {
            var dir = System.IO.Path.GetDirectoryName(AppConstants.GetAppDataPath("_"))!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = list.Select(h => new[] { h.Start, h.Length, h.ColorIdx }).ToList();
            File.WriteAllText(HighlightPath(lg),
                System.Text.Json.JsonSerializer.Serialize(data), Encoding.UTF8);
        }

        var currentHighlights = LoadHighlights(currentLang);

        // ── Highlight colours ──
        var highlightColors = new (string Label, string Hex)[]
        {
            ("Yellow",  "#FFFF00"),
            ("Green",   "#90EE90"),
            ("Cyan",    "#00FFFF"),
            ("Orange",  "#FFB347"),
            ("Pink",    "#FFB6C1"),
        };

        // ── Background colours ──
        var bgColors = new (string Label, string Hex, string FgHex)[]
        {
            ("White",       "#FFFFFF", "#000000"),
            ("Light gray",  "#F0F0F0", "#1A1A1A"),
            ("Sepia",       "#FDF6E3", "#3B2E1A"),
            ("Light blue",  "#EBF5FB", "#1A1A2E"),
            ("Dark",        "#1E1E1E", "#D4D4D4"),
            ("Black",       "#000000", "#E0E0E0"),
        };

        // ══════════════════════════════════════════════════════════════════
        //  CONTROLS
        // ══════════════════════════════════════════════════════════════════

        // ── Language ──
        var langCombo = new ComboBox
        {
            ItemsSource = languages.Select(lg => langLabels[lg]).ToList(),
            SelectedIndex = Array.IndexOf(languages, currentLang) is var idx && idx >= 0 ? idx : 1,
            MinWidth = 95, FontSize = 12, Padding = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        // ── Font ──
        var fontNames = new[] { "Consolas", "Cascadia Code", "Courier New", "Segoe UI", "Arial", "Verdana", "Times New Roman" };
        var fontCombo = new ComboBox
        {
            ItemsSource = fontNames,
            SelectedIndex = 0,
            MinWidth = 115, FontSize = 12, Padding = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var fontSizeSlider = new Slider
        {
            Minimum = 9, Maximum = 24, Value = 13, Width = 80,
            VerticalAlignment = VerticalAlignment.Center,
            TickFrequency = 1, IsSnapToTickEnabled = true
        };
        var fontSizeLabel = new TextBlock
        {
            Text = "13", VerticalAlignment = VerticalAlignment.Center,
            Foreground = TB("TextPrimary"), MinWidth = 18, FontSize = 12
        };

        // ── Background ──
        var bgCombo = new ComboBox
        {
            ItemsSource = bgColors.Select(c => c.Label).ToList(),
            SelectedIndex = 0,
            MinWidth = 90, FontSize = 12, Padding = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        // ── Toggles ──
        var editToggle = new Avalonia.Controls.Primitives.ToggleButton
        {
            Content = L("Read mode", "Mode lecture", "Lesemodus", "Modo lectura"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3), FontSize = 12
        };
        var wrapToggle = new Avalonia.Controls.Primitives.ToggleButton
        {
            Content = L("Wrap", "Retour ligne", "Umbruch", "Ajuste"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3), FontSize = 12
        };

        // ── Indent / Unindent / Bullets (edit mode) ──
        var indentBtn = new Button
        {
            Content = L("Indent →", "Retrait →", "Einrücken →", "Sangría →"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3), FontSize = 12, IsEnabled = false,
            Focusable = false
        };
        var unindentBtn = new Button
        {
            Content = L("← Unindent", "← Dés-retrait", "← Ausrücken", "← Quitar"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3), FontSize = 12, IsEnabled = false,
            Focusable = false
        };
        var bulletBtn = new Button
        {
            Content = L("- → •", "- → •", "- → •", "- → •"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 3), FontSize = 12, IsEnabled = false,
            Focusable = false
        };

        // ── Highlight ──
        var highlightCombo = new ComboBox
        {
            ItemsSource = highlightColors.Select(c => c.Label).ToList(),
            SelectedIndex = 0, MinWidth = 75, FontSize = 12, Padding = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var highlightBtn = new Button { Content = L("Highlight", "Surligner", "Markieren", "Resaltar"), Padding = new Thickness(6, 3), FontSize = 12 };
        var clearHighlightBtn = new Button { Content = L("Clear HL", "Eff. surl.", "HL löschen", "Borrar HL"), Padding = new Thickness(6, 3), FontSize = 12 };
        // Prevent highlight buttons from stealing focus → preserves text selection
        highlightBtn.Focusable = false;
        clearHighlightBtn.Focusable = false;

        // ── Search ──
        var searchBox = new TextBox
        {
            Watermark = L("Search…", "Rechercher…", "Suchen…", "Buscar…"),
            MinWidth = 150, FontSize = 12, Padding = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var searchPrev = new Button { Content = "◀", MinWidth = 24, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 2), FontSize = 11, Focusable = false };
        var searchNext = new Button { Content = "▶", MinWidth = 24, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 2), FontSize = 11, Focusable = false };
        var searchCount = new TextBlock
        {
            Text = "", VerticalAlignment = VerticalAlignment.Center,
            Foreground = TB("TextSecondary"), FontSize = 11, MinWidth = 50
        };

        // ── Status ──
        var statusText = new TextBlock
        {
            Text = HasUserNotes(currentLang)
                ? L("User notes loaded", "Notes utilisateur chargées", "Benutzernotizen geladen", "Notas del usuario cargadas")
                : "",
            Foreground = TB("TextSecondary"), FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        // ══════════════════════════════════════════════════════════════════
        //  TEXT AREA — single TextBox + SelectableTextBlock in a Panel
        // ══════════════════════════════════════════════════════════════════

        var richDisplay = new SelectableTextBlock
        {
            FontFamily = new FontFamily(fontNames[0]),
            FontSize = 13,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(10),
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        var richScroll = new ScrollViewer
        {
            Content = richDisplay,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var textEditor = new TextBox
        {
            Text = LoadText(currentLang),
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily(fontNames[0]),
            FontSize = 13,
            Padding = new Thickness(10),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            CaretBrush = Brushes.Black
        };

        void RebuildRichDisplay(string text, List<(int Start, int Length, int ColorIdx)> highlights)
        {
            richDisplay.Inlines!.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var defaultFg = richDisplay.Foreground;

            var sorted = highlights
                .Where(h => h.Start >= 0 && h.Start < text.Length)
                .OrderBy(h => h.Start).ToList();

            var pos = 0;
            foreach (var hl in sorted)
            {
                var hlEnd = Math.Min(hl.Start + hl.Length, text.Length);
                if (hl.Start < pos) continue;
                if (hl.Start > pos)
                    richDisplay.Inlines!.Add(new Avalonia.Controls.Documents.Run(text[pos..hl.Start]));
                var colorIdx = Math.Clamp(hl.ColorIdx, 0, highlightColors.Length - 1);
                richDisplay.Inlines!.Add(new Avalonia.Controls.Documents.Run(text[hl.Start..hlEnd])
                {
                    Background = new SolidColorBrush(Color.Parse(highlightColors[colorIdx].Hex)),
                    Foreground = Brushes.Black
                });
                pos = hlEnd;
            }
            if (pos < text.Length)
                richDisplay.Inlines!.Add(new Avalonia.Controls.Documents.Run(text[pos..]));
        }

        // Initialize rich display
        RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);

        // Container: richScroll for read mode, textEditor for edit mode
        var editorContainer = new Panel
        {
            Children = { richScroll, textEditor }
        };
        // Start in read mode → show rich display
        textEditor.IsVisible = false;
        richScroll.IsVisible = true;

        // ── Track richDisplay selection (focus loss resets it before button handler) ──
        var lastRichSelStart = 0;
        var lastRichSelEnd = 0;
        richDisplay.PropertyChanged += (_, args) =>
        {
            if (args.Property == SelectableTextBlock.SelectionStartProperty
                || args.Property == SelectableTextBlock.SelectionEndProperty)
            {
                lastRichSelStart = richDisplay.SelectionStart;
                lastRichSelEnd = richDisplay.SelectionEnd;
            }
        };

        // ── Bottom buttons ──
        var saveBtn = new Button { Content = L("Save notes", "Sauver notes", "Notizen speichern", "Guardar notas"), Padding = new Thickness(8, 4), FontSize = 12 };
        saveBtn.IsEnabled = false;
        var resetBtn = new Button { Content = L("Reset to original", "Revenir à l'original", "Original wiederherstellen", "Restaurar original"), Padding = new Thickness(8, 4), FontSize = 12 };
        resetBtn.IsEnabled = HasUserNotes(currentLang);
        var copyBtn = new Button { Content = L("Copy", "Copier", "Kopieren", "Copiar"), Padding = new Thickness(8, 4), FontSize = 12 };
        var closeBtn = new Button { Content = L("Close", "Fermer", "Schließen", "Cerrar"), Padding = new Thickness(8, 4), FontSize = 12 };

        // ══════════════════════════════════════════════════════════════════
        //  LABELS REFRESH
        // ══════════════════════════════════════════════════════════════════

        void RefreshLabels()
        {
            string Lc(string en, string fr, string de, string es) => currentLang switch
            {
                "fr" => fr, "de" => de, "es" => es, _ => en
            };
            editToggle.Content = isEditMode
                ? Lc("Edit mode", "Mode édition", "Bearbeitungsmodus", "Modo edición")
                : Lc("Read mode", "Mode lecture", "Lesemodus", "Modo lectura");
            wrapToggle.Content = Lc("Wrap", "Retour ligne", "Umbruch", "Ajuste");
            indentBtn.Content = Lc("Indent →", "Retrait →", "Einrücken →", "Sangría →");
            unindentBtn.Content = Lc("← Unindent", "← Dés-retrait", "← Ausrücken", "← Quitar");
            highlightBtn.Content = Lc("Highlight", "Surligner", "Markieren", "Resaltar");
            clearHighlightBtn.Content = Lc("Clear HL", "Eff. surl.", "HL löschen", "Borrar HL");
            searchBox.Watermark = Lc("Search…", "Rechercher…", "Suchen…", "Buscar…");
            saveBtn.Content = Lc("Save notes", "Sauver notes", "Notizen speichern", "Guardar notas");
            resetBtn.Content = Lc("Reset to original", "Revenir à l'original", "Original wiederherstellen", "Restaurar original");
            copyBtn.Content = Lc("Copy", "Copier", "Kopieren", "Copiar");
            closeBtn.Content = Lc("Close", "Fermer", "Schließen", "Cerrar");
            statusText.Text = HasUserNotes(currentLang)
                ? Lc("User notes loaded", "Notes utilisateur chargées", "Benutzernotizen geladen", "Notas del usuario cargadas")
                : "";
            resetBtn.IsEnabled = HasUserNotes(currentLang);
        }

        // ══════════════════════════════════════════════════════════════════
        //  SEARCH
        // ══════════════════════════════════════════════════════════════════

        var searchPositions = new List<int>();
        var searchCurrentIndex = -1;

        void DoSearch()
        {
            searchPositions.Clear();
            searchCurrentIndex = -1;
            var query = searchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(textEditor.Text))
            { searchCount.Text = ""; return; }
            var text = textEditor.Text;
            var pos = 0;
            while ((pos = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            { searchPositions.Add(pos); pos += query.Length; }
            searchCount.Text = searchPositions.Count > 0 ? $"{searchPositions.Count}" : "0";
            if (searchPositions.Count > 0) NavigateSearch(0);
        }

        void NavigateSearch(int index)
        {
            if (searchPositions.Count == 0) return;
            searchCurrentIndex = (index % searchPositions.Count + searchPositions.Count) % searchPositions.Count;
            var query = searchBox.Text?.Trim() ?? "";
            // Search works in textEditor (switch view temporarily if needed)
            if (!textEditor.IsVisible)
            {
                textEditor.IsVisible = true;
                richScroll.IsVisible = false;
            }
            textEditor.SelectionStart = searchPositions[searchCurrentIndex];
            textEditor.SelectionEnd = searchPositions[searchCurrentIndex] + query.Length;
            searchCount.Text = $"{searchCurrentIndex + 1}/{searchPositions.Count}";
        }

        // ══════════════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════════════

        // ── Background colour ──
        void ApplyBackground(int bgIdx)
        {
            if (bgIdx < 0 || bgIdx >= bgColors.Length) return;
            var bg = new SolidColorBrush(Color.Parse(bgColors[bgIdx].Hex));
            var fg = new SolidColorBrush(Color.Parse(bgColors[bgIdx].FgHex));

            // Override Avalonia theme resources so hover/focus don't reset to dark
            textEditor.Resources["TextControlBackground"] = bg;
            textEditor.Resources["TextControlBackgroundPointerOver"] = bg;
            textEditor.Resources["TextControlBackgroundFocused"] = bg;
            textEditor.Resources["TextControlBackgroundDisabled"] = bg;
            textEditor.Resources["TextControlForeground"] = fg;
            textEditor.Resources["TextControlForegroundPointerOver"] = fg;
            textEditor.Resources["TextControlForegroundFocused"] = fg;
            textEditor.Resources["TextControlBorderBrush"] = Brushes.Transparent;
            textEditor.Resources["TextControlBorderBrushPointerOver"] = Brushes.Transparent;
            textEditor.Resources["TextControlBorderBrushFocused"] = Brushes.Transparent;
            textEditor.Background = bg;
            textEditor.Foreground = fg;
            textEditor.CaretBrush = fg;

            richDisplay.Background = bg;
            richDisplay.Foreground = fg;
            richScroll.Background = bg;
            if (!isEditMode) RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
        }
        bgCombo.SelectionChanged += (_, _) => ApplyBackground(bgCombo.SelectedIndex);
        // Apply initial background
        ApplyBackground(0);

        // ── Font ──
        fontCombo.SelectionChanged += (_, _) =>
        {
            if (fontCombo.SelectedItem is string name)
            {
                var ff = new FontFamily(name);
                textEditor.FontFamily = ff;
                richDisplay.FontFamily = ff;
            }
        };
        fontSizeSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == "Value")
            {
                var sz = (int)fontSizeSlider.Value;
                fontSizeLabel.Text = sz.ToString();
                textEditor.FontSize = sz;
                richDisplay.FontSize = sz;
            }
        };

        // ── Track TextBox selection continuously (buttons steal focus → selection lost) ──
        var lastEditorSelStart = 0;
        var lastEditorSelEnd = 0;
        textEditor.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.SelectionStartProperty
                || args.Property == TextBox.SelectionEndProperty)
            {
                lastEditorSelStart = textEditor.SelectionStart;
                lastEditorSelEnd = textEditor.SelectionEnd;
            }
        };

        // ── Indent / Unindent / Bullets logic ──
        const string IndentStr = "    "; // 4 spaces

        // Returns (lineStart, lineEnd) covering all lines touched by the cached selection
        (int Start, int End) GetSelectedLineRange(string text)
        {
            if (text.Length == 0) return (0, 0);
            var selStart = Math.Min(lastEditorSelStart, lastEditorSelEnd);
            var selEnd = Math.Max(lastEditorSelStart, lastEditorSelEnd);
            selStart = Math.Clamp(selStart, 0, text.Length);
            selEnd = Math.Clamp(selEnd, 0, text.Length);

            // Walk back to start of first selected line
            var lineStart = selStart;
            while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;

            // Walk forward to end of last selected line
            var lineEnd = selEnd;
            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r') lineEnd++;

            return (lineStart, lineEnd);
        }

        // Split text block into lines, stripping \r from each
        static string[] SplitLines(string block)
        {
            var raw = block.Split('\n');
            for (var i = 0; i < raw.Length; i++)
                raw[i] = raw[i].TrimEnd('\r');
            return raw;
        }

        void ApplyLineTransform(Func<string[], string[]> transform)
        {
            var text = textEditor.Text ?? "";
            if (text.Length == 0) return;
            var (lineStart, lineEnd) = GetSelectedLineRange(text);
            if (lineStart == lineEnd) return;

            var lines = SplitLines(text[lineStart..lineEnd]);
            var result = transform(lines);
            var newBlock = string.Join('\n', result);
            textEditor.Text = string.Concat(text.AsSpan(0, lineStart), newBlock, text.AsSpan(lineEnd));

            // Re-select the transformed block and give focus back to the editor
            textEditor.SelectionStart = lineStart;
            textEditor.SelectionEnd = lineStart + newBlock.Length;
            lastEditorSelStart = lineStart;
            lastEditorSelEnd = lineStart + newBlock.Length;
            textEditor.Focus();
        }

        indentBtn.Click += (_, _) => ApplyLineTransform(lines =>
            lines.Select(l => IndentStr + l).ToArray());

        unindentBtn.Click += (_, _) => ApplyLineTransform(lines =>
            lines.Select(l =>
            {
                var removed = 0;
                var j = 0;
                while (j < l.Length && l[j] == ' ' && removed < 4) { j++; removed++; }
                return l[j..];
            }).ToArray());

        bulletBtn.Click += (_, _) => ApplyLineTransform(lines =>
        {
            var hasDash = lines.Any(l => l.TrimStart().StartsWith("- "));
            return lines.Select(l =>
            {
                var trimmed = l.TrimStart();
                var leading = l[..(l.Length - trimmed.Length)];
                if (hasDash && trimmed.StartsWith("- "))
                    return leading + "• " + trimmed[2..];
                if (!hasDash && trimmed.StartsWith("• "))
                    return leading + "- " + trimmed[2..];
                return l;
            }).ToArray();
        });

        // ── Edit mode toggle ──
        editToggle.Click += (_, _) =>
        {
            isEditMode = editToggle.IsChecked == true;
            if (isEditMode)
            {
                textEditor.IsReadOnly = false;
                textEditor.IsVisible = true;
                richScroll.IsVisible = false;
            }
            else
            {
                textEditor.IsReadOnly = true;
                textEditor.IsVisible = false;
                richScroll.IsVisible = true;
                RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
            }
            RefreshLabels();
            saveBtn.IsEnabled = isEditMode && hasUnsavedChanges;
            highlightBtn.IsEnabled = !isEditMode;
            clearHighlightBtn.IsEnabled = !isEditMode;
            highlightCombo.IsEnabled = !isEditMode;
            indentBtn.IsEnabled = isEditMode;
            unindentBtn.IsEnabled = isEditMode;
            bulletBtn.IsEnabled = isEditMode;
        };

        // ── Word wrap toggle ──
        wrapToggle.Click += (_, _) =>
        {
            isWrap = wrapToggle.IsChecked == true;
            textEditor.TextWrapping = isWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            richDisplay.TextWrapping = isWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            richScroll.HorizontalScrollBarVisibility = isWrap
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        };

        // ── Highlight (uses saved selection — focus loss safe) ──
        highlightBtn.Click += (_, _) =>
        {
            var selStart = lastRichSelStart;
            var selEnd = lastRichSelEnd;
            if (selStart == selEnd) return;

            var start = Math.Min(selStart, selEnd);
            var length = Math.Abs(selEnd - selStart);
            var colorIdx = highlightCombo.SelectedIndex;
            if (colorIdx < 0) colorIdx = 0;

            currentHighlights.Add((start, length, colorIdx));
            SaveHighlights(currentLang, currentHighlights);
            RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
        };

        clearHighlightBtn.Click += (_, _) =>
        {
            currentHighlights.Clear();
            var path = HighlightPath(currentLang);
            if (File.Exists(path)) File.Delete(path);
            RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
        };

        // ── Text changed ──
        textEditor.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == "Text" && isEditMode)
            { hasUnsavedChanges = true; saveBtn.IsEnabled = true; }
        };

        // ── Language switch ──
        langCombo.SelectionChanged += (_, _) =>
        {
            var newIdx = langCombo.SelectedIndex;
            if (newIdx < 0 || newIdx >= languages.Length) return;
            var newLang = languages[newIdx];
            if (newLang == currentLang) return;
            if (hasUnsavedChanges && isEditMode)
            { SaveUserNotes(currentLang, textEditor.Text ?? ""); hasUnsavedChanges = false; }
            currentLang = newLang;
            textEditor.Text = LoadText(currentLang);
            currentHighlights = LoadHighlights(currentLang);
            if (!isEditMode) RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
            hasUnsavedChanges = false;
            saveBtn.IsEnabled = false;
            RefreshLabels();
        };

        // ── Search ──
        searchBox.PropertyChanged += (_, args) => { if (args.Property.Name == "Text") DoSearch(); };
        searchNext.Click += (_, _) => NavigateSearch(searchCurrentIndex + 1);
        searchPrev.Click += (_, _) => NavigateSearch(searchCurrentIndex - 1);

        // ── Save / Reset / Copy ──
        saveBtn.Click += (_, _) =>
        {
            SaveUserNotes(currentLang, textEditor.Text ?? "");
            hasUnsavedChanges = false; saveBtn.IsEnabled = false; RefreshLabels();
        };
        resetBtn.Click += (_, _) =>
        {
            var userPath = UserNotesPath(currentLang);
            if (File.Exists(userPath)) File.Delete(userPath);
            var hlPath = HighlightPath(currentLang);
            if (File.Exists(hlPath)) File.Delete(hlPath);
            currentHighlights.Clear();
            textEditor.Text = File.Exists(OriginalPath(currentLang))
                ? File.ReadAllText(OriginalPath(currentLang), Encoding.UTF8) : string.Empty;
            if (!isEditMode) RebuildRichDisplay(textEditor.Text ?? "", currentHighlights);
            hasUnsavedChanges = false; saveBtn.IsEnabled = false; RefreshLabels();
        };
        copyBtn.Click += async (_, _) =>
        {
            if (owner.Clipboard is { } clipboard && !string.IsNullOrEmpty(textEditor.Text))
            {
                await clipboard.SetTextAsync(textEditor.Text);
                var prev = copyBtn.Content; copyBtn.Content = "✓";
                await Task.Delay(1200); copyBtn.Content = prev;
            }
        };

        // ══════════════════════════════════════════════════════════════════
        //  LAYOUT — grouped toolbar with separators
        // ══════════════════════════════════════════════════════════════════

        static Border Separator() => new()
        {
            Width = 1, Height = 18,
            Background = new SolidColorBrush(Color.Parse("#666666")),
            Margin = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock Lbl(string text) => new()
        {
            Text = text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = TB("TextLabel"), Margin = new Thickness(0, 0, 4, 0)
        };

        var lblLang = Lbl(L("Language:", "Langue :", "Sprache:", "Idioma:"));
        var lblFont = Lbl(L("Font:", "Police :", "Schrift:", "Fuente:"));
        var lblSize = Lbl(L("Size:", "Taille :", "Größe:", "Tamaño:"));
        var lblBg   = Lbl(L("Background:", "Fond :", "Hintergrund:", "Fondo:"));
        var lblHl   = Lbl(L("Color:", "Couleur :", "Farbe:", "Color:"));

        // Row 1 : Language | Font + Size | Background | Mode toggles
        var row1 = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                lblLang, langCombo,
                Separator(),
                lblFont, fontCombo, lblSize, fontSizeSlider, fontSizeLabel,
                Separator(),
                lblBg, bgCombo,
                Separator(),
                editToggle, new Border { Width = 8 }, wrapToggle
            }
        };

        // Row 2 : Highlight | Indent | Search | Status
        var row2 = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                highlightBtn, new Border { Width = 8 }, lblHl, highlightCombo,
                new Border { Width = 10 }, clearHighlightBtn,
                Separator(),
                indentBtn, new Border { Width = 4 }, unindentBtn, new Border { Width = 4 }, bulletBtn,
                Separator(),
                searchBox, new Border { Width = 4 },
                searchPrev, searchNext, new Border { Width = 4 }, searchCount,
                new Border { Width = 10 }, statusText
            }
        };

        // Header border grouping
        var headerPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 2),
            Children =
            {
                row1,
                new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#444444")), Margin = new Thickness(0, 1, 0, 3) },
                row2,
                new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#444444")), Margin = new Thickness(0, 1, 0, 1) },
            }
        };

        // Bottom
        var bottomBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { saveBtn, resetBtn, copyBtn, closeBtn }
        };

        var mainPanel = new DockPanel
        {
            Margin = new Thickness(12),
            LastChildFill = true,
            Children = { headerPanel, bottomBar, editorContainer }
        };
        DockPanel.SetDock(headerPanel, Dock.Top);
        DockPanel.SetDock(bottomBar, Dock.Bottom);

        var dialog = new Window
        {
            Title = L("User Guide — AvyScan Lab", "Guide utilisateur — AvyScan Lab",
                       "Benutzerhandbuch — AvyScan Lab", "Guía del usuario — AvyScan Lab"),
            Width = 1050, Height = 740,
            MinWidth = 600, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Content = mainPanel
        };

        closeBtn.Click += (_, _) =>
        {
            if (hasUnsavedChanges && isEditMode)
                SaveUserNotes(currentLang, textEditor.Text ?? "");
            dialog.Close();
        };

        // ── Secret shortcut: Ctrl+Shift+Alt+S → save edits to ORIGINAL guide file ──
        dialog.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.S
                && args.KeyModifiers == (Avalonia.Input.KeyModifiers.Control
                    | Avalonia.Input.KeyModifiers.Shift
                    | Avalonia.Input.KeyModifiers.Alt))
            {
                var origPath = OriginalPath(currentLang);
                if (!string.IsNullOrEmpty(textEditor.Text))
                {
                    File.WriteAllText(origPath, textEditor.Text, Encoding.UTF8);
                    // Brief visual confirmation on the title bar
                    var origTitle = dialog.Title;
                    dialog.Title = origTitle + "  ✓ SAVED TO ORIGINAL";
                    Task.Delay(2000).ContinueWith(_ =>
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => dialog.Title = origTitle));
                }
                args.Handled = true;
            }
        };

        await dialog.ShowDialog(owner);
    }
}
