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
using CleanScan.Services;
using CleanScan.Views;

namespace CleanScan
{
    public partial class App : Application
    {
        private const string AppDataFolder = "CleanScan";

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
            desktop.MainWindow = splash;
            splash.Show();

            try
            {
                await Task.Delay(3000);

                // Instantiate services
                var config             = new ConfigStore();
                var sourceService      = new SourceService();
                var aviService         = new AviService();
                var scriptService      = new ScriptService(sourceService);
                var presetService      = new PresetService(GetAppDataPath("presets.json"));
                var windowStateService = new WindowStateService(GetAppDataPath("window-settings.json"));
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

        private static string GetAppDataPath(string fileName) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder, fileName);

        private static void TryLogStartupException(Exception exception)
        {
            try
            {
                var appData    = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var logDir     = Path.Combine(appData, AppDataFolder);
                Directory.CreateDirectory(logDir);
                var logPath    = Path.Combine(logDir, "startup-error.log");
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
