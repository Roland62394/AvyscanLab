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
        Func<string, Dictionary<string, string>, Task> applyCallback,
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
                        new Border
                        {
                            Background = new SolidColorBrush(Color.Parse("#1E2A3A")),
                            BorderBrush = new SolidColorBrush(Color.Parse("#3B82C4")),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(10, 6),
                            Margin = new Thickness(0, 2, 0, 4),
                            Child = new TextBlock
                            {
                                Text = vm.GetUiText("PresetGlobalWarning"),
                                Foreground = new SolidColorBrush(Color.Parse("#7EB8E0")),
                                FontSize = 11,
                                FontFamily = monoFont,
                                TextWrapping = TextWrapping.Wrap
                            }
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
                            Children = { saveButton, deleteButton, loadButton, closeButton }
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
            if (string.IsNullOrWhiteSpace(name)) return;

            var existing = presetList.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                existing.Values = presets.CaptureCurrentValues(config);
            else
                presetList.Add(new Preset(name, presets.CaptureCurrentValues(config)));

            presets.SavePresets(presetList);
            RefreshCombo();
            comboBox.SelectedItem = ordered.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
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
            if (target?.Values is not null)
                await applyCallback(target.Name, new Dictionary<string, string>(target.Values, StringComparer.OrdinalIgnoreCase));
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

    public async Task ShowFeedbackDialogAsync(Window owner, MainWindowViewModel vm)
    {
        const string FormspreeEndpoint = "https://formspree.io/f/mvzwglbv";

        var lang = vm.CurrentLanguageCode;
        string L(string en, string fr, string de, string es) => lang switch
        {
            "fr" => fr, "de" => de, "es" => es, _ => en
        };

        var monoFont = new FontFamily("Consolas,Cascadia Code,monospace");
        var bgField = new SolidColorBrush(Color.Parse("#1A2030"));
        var fgField = new SolidColorBrush(Color.Parse("#DBDBDB"));
        var borderField = new SolidColorBrush(Color.Parse("#252E42"));
        var fgLabel = new SolidColorBrush(Color.Parse("#F6F6F6"));

        TextBlock MakeLabel(string text) => new()
        {
            Text = text, Foreground = fgLabel, FontSize = 11,
            FontWeight = FontWeight.SemiBold, FontFamily = monoFont,
            Margin = new Thickness(0, 4, 0, 2)
        };

        TextBox MakeField(string placeholder = "", int height = 30) => new()
        {
            Watermark = placeholder, Height = height,
            Background = bgField, Foreground = fgField,
            BorderBrush = borderField, BorderThickness = new Thickness(1),
            FontFamily = monoFont, Padding = new Thickness(8, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var nameBox = MakeField(L("Your name", "Votre nom", "Ihr Name", "Su nombre"));
        var emailBox = MakeField(L("Your email", "Votre email", "Ihre E-Mail", "Su email"));

        // Category
        var categoryCombo = new ComboBox
        {
            Height = 30, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = bgField, Foreground = fgField,
            BorderBrush = borderField, BorderThickness = new Thickness(1),
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
                "To help fix a bug, your description must be as precise as possible.\nWhat action were you performing, on which window, what result did you expect, what did you get instead.\nAlso describe what type of video files you were processing, which filters were active if relevant, etc.",
                "Pour permettre la correction d\u2019un bug, votre description doit \u00eatre aussi pr\u00e9cise que possible.\nQuelle action \u00e9tiez-vous en train d\u2019entreprendre, sur quelle fen\u00eatre, \u00e0 quel r\u00e9sultat vous vous attendiez, qu\u2019avez-vous obtenu \u00e0 la place.\nD\u00e9crivez aussi quel type de fichiers vid\u00e9o \u00e9tiez-vous en train de traiter, si besoin quel filtre \u00e9tait actif, etc.",
                "Um einen Fehler beheben zu k\u00f6nnen, muss Ihre Beschreibung so genau wie m\u00f6glich sein.\nWelche Aktion haben Sie ausgef\u00fchrt, in welchem Fenster, welches Ergebnis haben Sie erwartet, was haben Sie stattdessen erhalten.\nBeschreiben Sie auch, welche Art von Videodateien Sie verarbeitet haben, welche Filter aktiv waren, usw.",
                "Para poder corregir un error, su descripci\u00f3n debe ser lo m\u00e1s precisa posible.\nQu\u00e9 acci\u00f3n estaba realizando, en qu\u00e9 ventana, qu\u00e9 resultado esperaba, qu\u00e9 obtuvo en su lugar.\nDescriba tambi\u00e9n qu\u00e9 tipo de archivos de v\u00eddeo estaba procesando, qu\u00e9 filtros estaban activos si es relevante, etc."),
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
            Background = bgField, Foreground = fgField,
            BorderBrush = borderField, BorderThickness = new Thickness(1),
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
            Background = new SolidColorBrush(Color.Parse("#1C2333")),
            Foreground = fgField, FontFamily = monoFont,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var dialog = new Window
        {
            Title = "Contact",
            Width = 480, SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#0F1319")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(16), Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.Parse("#161B24")),
                BorderBrush = borderField, BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        MakeLabel(L("Name", "Nom", "Name", "Nombre")),
                        nameBox,
                        MakeLabel(L("Email", "Email", "E-Mail", "Email")),
                        emailBox,
                        MakeLabel(L("Category", "Cat\u00e9gorie", "Kategorie", "Categor\u00eda")),
                        categoryCombo,
                        bugHelpText,
                        MakeLabel(L("Message", "Message", "Nachricht", "Mensaje")),
                        messageBox,
                        MakeLabel(L("Rating", "Note", "Bewertung", "Valoraci\u00f3n")),
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
            statusText.Foreground = new SolidColorBrush(Color.Parse("#7984A5"));
            statusText.Text = L("Sending...", "Envoi en cours...", "Wird gesendet...", "Enviando...");

            try
            {
                var category = categoryCombo.SelectedItem?.ToString() ?? "";
                var stars = selectedRating > 0 ? new string('★', selectedRating) + new string('☆', 5 - selectedRating) : "—";

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var payload = new Dictionary<string, string>
                {
                    ["_subject"] = $"CleanScan Feedback: {category}",
                    ["name"] = string.IsNullOrWhiteSpace(name) ? "Anonymous" : name,
                    ["email"] = string.IsNullOrWhiteSpace(email) ? "noreply@cleanscan.app" : email,
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
}
