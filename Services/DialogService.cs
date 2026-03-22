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
using AvyscanLab.ViewModels;
using AvyscanLab.Views;

namespace AvyscanLab.Services;

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
                    ["_subject"] = $"Avyscan Lab Feedback: {category}",
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
        new() { Content = label, MinWidth = minWidth };

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
            Text = L("Thank you for using Avyscan Lab!",
                "Merci d\u2019utiliser Avyscan Lab\u00a0!",
                "Vielen Dank f\u00fcr die Nutzung von Avyscan Lab!",
                "\u00a1Gracias por usar Avyscan Lab!"),
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
                $"A new version of Avyscan Lab is available: v{latestVersion}\nYou are currently using v{UpdateService.CurrentVersion}.",
                $"Une nouvelle version de Avyscan Lab est disponible : v{latestVersion}\nVous utilisez actuellement la v{UpdateService.CurrentVersion}.",
                $"Eine neue Version von Avyscan Lab ist verf\u00fcgbar: v{latestVersion}\nSie verwenden derzeit v{UpdateService.CurrentVersion}.",
                $"Una nueva versi\u00f3n de Avyscan Lab est\u00e1 disponible: v{latestVersion}\nActualmente usa la v{UpdateService.CurrentVersion}."),
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
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { closeBtn, downloadBtn }
        };

        var panel = new StackPanel
        {
            Width = 420,
            Margin = new Thickness(20),
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
}
