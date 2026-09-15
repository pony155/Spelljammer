using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Spelljammer.Presentation;
using Spelljammer.Settings;

namespace Spelljammer;

/// <summary>
/// Main application entry point for Spelljammer.
/// </summary>
/// <remarks>
/// Handles application startup by:
/// 1. Loading user settings from persistent storage
/// 2. Loading localization strings in the user's language
/// 3. Initializing and displaying the main menu window
/// 4. Managing application lifecycle
/// Code flow: Startup publishes settings and localization dependencies before constructing the main window, while
/// shutdown disposes native-backed services on their owning UI thread.
/// </remarks>
public partial class App : Application
{
    private SpriteForgeAudioService? audio;

    /// <summary>
    /// Handles application startup initialization.
    /// </summary>
    /// <remarks>
    /// Shows the SpriteForge startup presentation, loads settings asynchronously, publishes application services,
    /// and transitions to the main-window shutdown mode only after the menu is ready.
    /// </remarks>
    /// <param name="e">Startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        StartupSplashWindow splash = new();
        splash.Show();

        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            long splashStarted = Stopwatch.GetTimestamp();
            splash.SetProgress(0.2f);
            string settingsPath = GameSettingsPath.CurrentUser;

            // Load settings asynchronously on a background thread while the SpriteForge splash remains responsive.
            (GameSettingsRegistry registry, GameSettingsDiagnostic diagnostic) =
                await Task.Run(() => GameSettingsRegistry.Load(settingsPath));
            splash.SetProgress(0.48f);
            await Dispatcher.Yield(DispatcherPriority.Render);

            GameText strings = GameText.Load(registry.Active.Language);
            splash.SetProgress(0.7f);
            await Dispatcher.Yield(DispatcherPriority.Render);

            audio = SpriteForgeAudioService.Create(registry.Active);
            splash.SetProgress(0.86f);
            await Dispatcher.Yield(DispatcherPriority.Render);

            MainMenuWindow window = new(registry, settingsPath, diagnostic, strings, audio);
            MainWindow = window;
            splash.SetProgress(1);

            TimeSpan minimumVisibleTime = TimeSpan.FromMilliseconds(750);
            TimeSpan visibleTime = Stopwatch.GetElapsedTime(splashStarted);
            if (visibleTime < minimumVisibleTime)
            {
                await Task.Delay(minimumVisibleTime - visibleTime);
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            splash.Close();
        }
        catch
        {
            splash.Close();
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        audio?.Dispose();
        audio = null;
        base.OnExit(e);
    }
}
