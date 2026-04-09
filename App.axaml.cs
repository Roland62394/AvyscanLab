using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AvyScanLab.Services;
using AvyScanLab.Views;

namespace AvyScanLab
{
    public partial class App : Application
    {

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // Emergency session save on unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                {
                    TrySaveSessionOnCrash(desktop);
                    if (args.ExceptionObject is Exception ex)
                        TryLogStartupException(ex);
                };
                TaskScheduler.UnobservedTaskException += (_, args) =>
                {
                    TrySaveSessionOnCrash(desktop);
                    TryLogStartupException(args.Exception);
                };

                _ = ShowSplashThenMainAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void TrySaveSessionOnCrash(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                if (desktop.MainWindow is MainWindow mainWindow)
                    mainWindow.EmergencySaveSession();
            }
            catch { /* Must never throw during crash handling */ }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
                BindingPlugins.DataValidators.Remove(plugin);
        }

        private static async Task ShowSplashThenMainAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();

            // Position the splash on the same screen as the saved main window position
            var savedSettings = new WindowStateService(AppConstants.GetAppDataPath("window-settings.json")).Load();
            if (savedSettings is not null)
            {
                splash.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.Manual;
                splash.Position = new Avalonia.PixelPoint(
                    savedSettings.X + (int)((savedSettings.Width - splash.Width) / 2),
                    savedSettings.Y + (int)((savedSettings.Height - splash.Height) / 2));
            }

            desktop.MainWindow = splash;
            splash.Show();

            try
            {
                await Task.Delay(6000);

                // Instantiate services
                var config             = new ConfigStore();
                var sourceService      = new SourceService();
                var aviService         = new AviService();
                var scriptService      = new ScriptService(sourceService);
                var presetService      = new PresetService(AppConstants.GetAppDataPath("presets.json"));
                var windowStateService = new WindowStateService(AppConstants.GetAppDataPath("window-settings.json"));
                var dialogService      = new DialogService();

                var mainWindow = new MainWindow(
                    config,
                    sourceService,
                    scriptService,
                    presetService,
                    windowStateService,
                    dialogService,
                    aviService);

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.ShowInTaskbar = true;
                mainWindow.Activate();
                splash.Close();
            }
            catch (Exception ex)
            {
                TryLogStartupException(ex);
                splash.Close();
                desktop.Shutdown();
            }
        }

        private static void TryLogStartupException(Exception exception)
        {
            try
            {
                var logPath = AppConstants.GetAppDataPath("startup-error.log");
                var logDir  = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDir)) Directory.CreateDirectory(logDir);
                var message    = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n";
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Logging should never block startup shutdown.
            }

            Debug.WriteLine(exception);
        }
    }
}
