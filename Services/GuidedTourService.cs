using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvyScanLab.Services;

/// <summary>Minimal contract the tour needs from the host window.</summary>
public interface ITourHost
{
    T? FindControl<T>(string name) where T : Control;
    SolidColorBrush ThemeBrush(string key);
    string GetUiText(string key);
    string CurrentLanguageCode { get; }
    Size ClientSize { get; }
    bool IsRecordPanelOpen { get; }
    void ToggleRecordPanel();
    void ExpandPanel(string expandButtonName);
    void ApplyLanguage(string code, bool persist);
    void MarkTourCompleted();
}

public sealed class GuidedTourService
{
    private static readonly (string? TargetName, string TitleKey, string BodyKey, string Emoji, string? BeforeAction)[] Steps =
    [
        (null,                "TourWelcomeTitle",       "TourWelcomeBody",       "\ud83c\udfac", null),
        ("AddClipBtn",        "TourAddClipTitle",       "TourAddClipBody",       "\u2795",       null),
        ("CustomFiltersList", "TourFiltersTitle",       "TourFiltersBody",       "\ud83d\udd27", null),
        ("CustomParamPanels", "TourParamsTitle",        "TourParamsBody",        "\ud83c\udf9a", null),
        ("VdbPlay",           "TourPreviewTitle",       "TourPreviewBody",       "\u25b6\ufe0f", null),
        ("RecordBtn",         "TourRecordTitle",        "TourRecordBody",        "\ud83d\udcbe", null),
        ("RecordDirPickBtn",  "TourOutputDirTitle",     "TourOutputDirBody",     "\ud83d\udcc1", "OpenRecord"),
        ("RecordStartBtn",    "TourStartEncodingTitle", "TourStartEncodingBody", "\ud83d\ude80", "OpenRecord"),
    ];

    private bool _active;

    /// <summary>Set by the tour; called by the host when a clip finishes loading (step 1 advance).</summary>
    public Action? AdvanceOnClipLoaded { get; private set; }

    public async Task RunAsync(ITourHost host)
    {
        if (_active) return;
        _active = true;
        AdvanceOnClipLoaded = null;

        try
        {
            if (host.IsRecordPanelOpen)
                host.ToggleRecordPanel();

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(300);

            int step = 0;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── UI elements (created once, updated per step) ──────────

            var titleTb = new TextBlock
            {
                FontSize = 18, FontWeight = FontWeight.SemiBold,
                Foreground = host.ThemeBrush("TextLabel"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            };
            var bodyTb = new TextBlock
            {
                FontSize = 13, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = host.ThemeBrush("TextSecondary"),
                MaxWidth = 340, LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 18),
            };

            var dots = new Border[Steps.Length];
            var dotsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6, Margin = new Thickness(0, 0, 0, 14),
            };
            for (int i = 0; i < Steps.Length; i++)
            {
                dots[i] = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4) };
                dotsPanel.Children.Add(dots[i]);
            }

            var skipBtn = new Button
            {
                MinWidth = 60, Background = Brushes.Transparent,
                Foreground = host.ThemeBrush("TextPrimary"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var prevBtn = new Button
            {
                MinWidth = 70,
                Background = host.ThemeBrush("BorderSubtle"),
                Foreground = host.ThemeBrush("TextSecondary"),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var nextBtn = new Button
            {
                MinWidth = 90,
                Background = new SolidColorBrush(Color.Parse("#3B82F6")),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            var buttonsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
            };
            buttonsRow.Children.Add(skipBtn);
            buttonsRow.Children.Add(prevBtn);
            buttonsRow.Children.Add(nextBtn);

            var stepLabel = new TextBlock
            {
                FontSize = 11,
                Foreground = host.ThemeBrush("TextPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            };

            // Language selector (welcome step only)
            var langLabel = new TextBlock
            {
                FontSize = 12,
                Foreground = host.ThemeBrush("TextPrimary"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var langRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6, Margin = new Thickness(0, 0, 0, 12),
            };
            langRow.Children.Add(langLabel);
            Action? refreshStep = null;
            foreach (var (code, label) in new[] { ("en", "English"), ("fr", "Français"), ("de", "Deutsch"), ("es", "Español") })
            {
                var btn = new Button
                {
                    Content = label, Tag = code, MinWidth = 70, FontSize = 12,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Foreground = host.ThemeBrush("TextSecondary"),
                };
                btn.Click += (_, _) =>
                {
                    host.ApplyLanguage(code, persist: true);
                    refreshStep?.Invoke();
                };
                langRow.Children.Add(btn);
            }

            var card = new Border
            {
                Background = host.ThemeBrush("BgPanel"),
                BorderBrush = host.ThemeBrush("AccentBlue"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(28, 24),
                MinWidth = 340, MaxWidth = 400,
                BoxShadow = BoxShadows.Parse("0 8 30 0 #A0000000"),
                Child = new StackPanel
                {
                    Spacing = 0,
                    Children = { titleTb, bodyTb, langRow, dotsPanel, buttonsRow, stepLabel }
                }
            };

            var mainGrid = host.FindControl<Grid>("MainGrid");
            if (mainGrid is null) { done.TrySetResult(); return; }

            var tourPopup = new Popup
            {
                PlacementTarget = mainGrid,
                Placement = PlacementMode.TopEdgeAlignedLeft,
                IsLightDismissEnabled = false,
                Child = card,
            };

            // ── Highlight state ──────────────────────────────────────

            Control? highlightedTarget = null;
            IBrush? addClipBg = null, addClipFg = null, addClipBorder = null;
            CancellationTokenSource? hoverCts = null;
            EventHandler<PointerEventArgs>? enterHandler = null, leaveHandler = null;
            EventHandler<RangeBaseValueChangedEventArgs>? sliderHandler = null;
            EventHandler<RoutedEventArgs>? clickHandler = null;
            Control? interactionTarget = null;
            EventHandler<PointerPressedEventArgs>? highlightClickHandler = null;

            void ClearInteraction()
            {
                hoverCts?.Cancel(); hoverCts = null;
                if (interactionTarget is not null)
                {
                    if (enterHandler is not null) interactionTarget.PointerEntered -= enterHandler;
                    if (leaveHandler is not null) interactionTarget.PointerExited -= leaveHandler;
                    if (sliderHandler is not null && interactionTarget is Slider sl) sl.ValueChanged -= sliderHandler;
                    if (clickHandler is not null && interactionTarget is Button bt) bt.Click -= clickHandler;
                    enterHandler = leaveHandler = null;
                    sliderHandler = null; clickHandler = null;
                    interactionTarget = null;
                }
            }

            void ClearHighlight()
            {
                ClearInteraction();
                if (highlightedTarget is Button { Name: "AddClipBtn" } addClip)
                {
                    if (addClipBg is not null) addClip.Background = addClipBg;
                    if (addClipFg is not null) addClip.Foreground = addClipFg;
                    if (addClipBorder is not null) addClip.BorderBrush = addClipBorder;
                }
                if (highlightedTarget is not null)
                {
                    if (highlightClickHandler is not null)
                    {
                        highlightedTarget.RemoveHandler(InputElement.PointerPressedEvent, highlightClickHandler);
                        highlightClickHandler = null;
                    }
                    highlightedTarget.Classes.Remove("tour-highlight");
                    highlightedTarget = null;
                }
            }

            void CloseTour()
            {
                ClearHighlight();
                AdvanceOnClipLoaded = null;
                tourPopup.IsOpen = false;
                host.MarkTourCompleted();
                done.TrySetResult();
            }

            // ── Positioning helpers ──────────────────────────────────

            static int VerticalNudge(int s) => s switch
            {
                1 => 70, 2 => 70, 3 => 50, 6 => 100, _ => 0,
            };
            static int HorizontalNudge(int s) => s switch
            {
                0 => 120, 1 => 80, 6 => 120, _ => 0,
            };

            // ── UpdateStep ───────────────────────────────────────────

            async void UpdateStep()
            {
                try { await UpdateStepCore(); } catch { /* prevent async void crash */ }
            }

            async Task UpdateStepCore()
            {
                var (targetName, titleKey, bodyKey, emoji, beforeAction) = Steps[step];
                bool isFirst = step == 0;
                bool isLast = step == Steps.Length - 1;

                // Pre-action: open/close panels
                bool needsLayout = false;
                if (beforeAction == "OpenRecord")
                {
                    if (!host.IsRecordPanelOpen) { host.ToggleRecordPanel(); needsLayout = true; }
                }
                else if (host.IsRecordPanelOpen)
                {
                    host.ToggleRecordPanel();
                }

                if (needsLayout)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Task.Delay(100);
                }

                // Update text
                titleTb.Text = $"{emoji}  {host.GetUiText(titleKey)}";
                bodyTb.Text = host.GetUiText(bodyKey);
                skipBtn.Content = host.GetUiText("TourSkipBtn");
                prevBtn.Content = host.GetUiText("TourPrevBtn");
                nextBtn.Content = isLast ? host.GetUiText("TourFinishBtn") : host.GetUiText("TourNextBtn");
                stepLabel.Text = $"{step + 1} / {Steps.Length}";
                prevBtn.IsVisible = !isFirst;

                // Language row (welcome only)
                langRow.IsVisible = isFirst;
                if (isFirst)
                {
                    langLabel.Text = host.GetUiText("TourLanguageLabel");
                    var curLang = host.CurrentLanguageCode;
                    foreach (var child in langRow.Children)
                    {
                        if (child is not Button lb || lb.Tag is not string code) continue;
                        bool cur = string.Equals(code, curLang, StringComparison.OrdinalIgnoreCase);
                        lb.Background = cur ? host.ThemeBrush("AccentBlue") : host.ThemeBrush("BorderSubtle");
                        lb.Foreground = cur ? Brushes.White : host.ThemeBrush("TextSecondary");
                    }
                }

                // Dots
                for (int i = 0; i < dots.Length; i++)
                    dots[i].Background = i == step ? host.ThemeBrush("AccentBlue")
                        : i < step ? host.ThemeBrush("BorderAccent")
                        : host.ThemeBrush("BorderSubtle");

                // ── Position card ────────────────────────────────────
                double ww = host.ClientSize.Width;
                double wh = host.ClientSize.Height;
                card.Measure(new Size(ww, wh));
                double cardW = Math.Max(card.DesiredSize.Width, 340);
                double cardH = Math.Max(card.DesiredSize.Height, 100);
                const double gap = 16;

                ClearHighlight();

                double cLeft, cTop;
                if (targetName is not null && host.FindControl<Control>(targetName) is { } target)
                {
                    target.Classes.Add("tour-highlight");
                    highlightedTarget = target;

                    // Special AddClipBtn highlight
                    if (target is Button { Name: "AddClipBtn" } addClip)
                    {
                        addClipBg ??= addClip.Background;
                        addClipFg ??= addClip.Foreground;
                        addClipBorder ??= addClip.BorderBrush;
                        addClip.Background = new SolidColorBrush(Color.Parse("#2A3755"));
                        addClip.Foreground = Brushes.White;
                        addClip.BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
                    }

                    var pos = target.TranslatePoint(new Point(0, 0), mainGrid);
                    double tx = pos?.X ?? 0, ty = pos?.Y ?? 0;
                    double tw = target.Bounds.Width, th = target.Bounds.Height;

                    if (tx + tw + gap + cardW + 10 < ww)
                        (cLeft, cTop) = (tx + tw + gap, Math.Clamp(ty, 10, Math.Max(10, wh - cardH - 10)));
                    else if (ty + th + gap + cardH + 10 < wh)
                        (cLeft, cTop) = (Math.Clamp(tx, 10, Math.Max(10, ww - cardW - 10)), ty + th + gap);
                    else if (ty - gap - cardH > 0)
                        (cLeft, cTop) = (Math.Clamp(tx, 10, Math.Max(10, ww - cardW - 10)), ty - gap - cardH);
                    else
                        (cLeft, cTop) = (Math.Max(10, (ww - cardW) / 2), Math.Max(10, (wh - cardH) / 2));
                }
                else
                {
                    (cLeft, cTop) = (Math.Max(10, (ww - cardW) / 2), Math.Max(10, (wh - cardH) / 2));
                }

                cLeft = Math.Clamp(cLeft + HorizontalNudge(step), 10, Math.Max(10, ww - cardW - 10));
                cTop = Math.Clamp(cTop + VerticalNudge(step), 10, Math.Max(10, wh - cardH - 10));
                tourPopup.HorizontalOffset = cLeft;
                tourPopup.VerticalOffset = cTop;

                // Clip-loaded advance (step 1)
                AdvanceOnClipLoaded = step == 1
                    ? () =>
                    {
                        if (!_active || step != 1) return;
                        step++;
                        if (step >= Steps.Length) CloseTour(); else UpdateStep();
                    }
                    : null;

                // Auto-advance interactions (steps 2–7)
                if (step is >= 2 and <= 7 && targetName is not null
                    && host.FindControl<Control>(targetName) is { } ttTarget)
                {
                    var capturedStep = step;
                    interactionTarget = ttTarget;

                    void AdvanceAfterDelay()
                    {
                        if (hoverCts is not null) return;
                        hoverCts = new CancellationTokenSource();
                        var token = hoverCts.Token;
                        _ = Task.Delay(8000, token).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!_active || step != capturedStep || token.IsCancellationRequested) return;
                                step++;
                                if (step >= Steps.Length) CloseTour(); else UpdateStep();
                            });
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }

                    if (step == 4 && ttTarget is Button bt)
                    {
                        // VdbPlay: click → auto-advance after 8s
                        clickHandler = (_, _) => AdvanceAfterDelay();
                        bt.Click += clickHandler;
                    }
                    else if (step is 5 or 6 && ttTarget is Button clickBtn)
                    {
                        // RecordBtn, OutputDir: click → advance
                        clickHandler = (_, _) =>
                        {
                            if (!_active) return;
                            step++;
                            if (step >= Steps.Length) CloseTour(); else UpdateStep();
                        };
                        clickBtn.Click += clickHandler;
                    }
                    else if (step == 7 && ttTarget is Button lastBtn)
                    {
                        // StartEncoding: click → close tour
                        clickHandler = (_, _) => { if (_active) CloseTour(); };
                        lastBtn.Click += clickHandler;
                    }
                    else
                    {
                        // Steps 2-3 (FiltersList, ParamPanels): hover → auto-advance after 8s
                        enterHandler = (_, _) => AdvanceAfterDelay();
                        leaveHandler = (_, _) => { hoverCts?.Cancel(); hoverCts = null; };
                        ttTarget.PointerEntered += enterHandler;
                        ttTarget.PointerExited += leaveHandler;
                    }
                }

                tourPopup.IsOpen = true;
            }

            // ── Navigation ───────────────────────────────────────────

            nextBtn.Click += (_, _) =>
            {
                step++;
                if (step >= Steps.Length) CloseTour(); else UpdateStep();
            };
            prevBtn.Click += (_, _) =>
            {
                if (step > 0) { step--; UpdateStep(); }
            };
            skipBtn.Click += (_, _) => CloseTour();

            refreshStep = UpdateStep;
            UpdateStep();
            await done.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Guided tour failed: {ex}");
        }
        finally
        {
            AdvanceOnClipLoaded = null;
            _active = false;
        }
    }
}
