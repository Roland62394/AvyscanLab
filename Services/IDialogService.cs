using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvyscanLab.ViewModels;

namespace AvyscanLab.Services;

public interface IDialogService
{
    Task ShowErrorAsync(Window owner, string title, string message, string? details = null);
    Task ShowInfoAsync(Window owner, string title, string message);
    Task ShowTextDialogAsync(Window owner, string title, string text);
    Task ShowAboutDialogAsync(Window owner, string title, string company, string rights, string website, string version, string closeLabel, string imageUri);
    Task ShowScriptPreviewDialogAsync(Window owner, IScriptService scriptService, Action? onReload, MainWindowViewModel vm);
    Task<PresetDialogResult> ShowPresetDialogAsync(Window owner, IPresetService presets, ConfigStore config, MainWindowViewModel vm, string? activePresetName = null);
    Task<bool> ShowAviSynthMissingDialogAsync(Window owner, MainWindowViewModel vm);
    Task ShowFeedbackDialogAsync(Window owner, MainWindowViewModel vm);
    Task<(bool OpenContact, bool DontShowAgain)> ShowExitFeedbackDialogAsync(Window owner, MainWindowViewModel vm);
    Task ShowUpdateAvailableDialogAsync(Window owner, MainWindowViewModel vm, string latestVersion, string downloadUrl);
}
