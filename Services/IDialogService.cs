using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using CleanScan.ViewModels;

namespace CleanScan.Services;

public interface IDialogService
{
    Task ShowErrorAsync(Window owner, string title, string message);
    Task ShowTextDialogAsync(Window owner, string title, string text);
    Task ShowAboutDialogAsync(Window owner, string title, string company, string rights, string website, string version, string closeLabel, string imageUri);
    Task ShowScriptPreviewDialogAsync(Window owner, IScriptService scriptService, Action? onReload, MainWindowViewModel vm);
    Task ShowPresetDialogAsync(Window owner, IPresetService presets, ConfigStore config, Func<string, Dictionary<string, string>, Task> applyCallback, MainWindowViewModel vm);
    Task<bool> ShowAviSynthMissingDialogAsync(Window owner, MainWindowViewModel vm);
}
