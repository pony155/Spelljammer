using System.Windows;
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
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// Handles application startup initialization.
    /// </summary>
    /// <remarks>
    /// Loads game settings and localization asynchronously to prevent UI blocking.
    /// Sets up the main menu window and transitions to window-close shutdown mode.
    /// </remarks>
    /// <param name="e">Startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string settingsPath = GameSettingsPath.CurrentUser;

        // Load settings asynchronously on a background thread to prevent UI blocking.
        // Tuple deconstruction unpacks both the loaded registry and diagnostic info (which may contain load errors).
        (GameSettingsRegistry registry, GameSettingsDiagnostic diagnostic) =
            await Task.Run(() => GameSettingsRegistry.Load(settingsPath));

        // Load localized UI strings based on the user's preferred language setting.
        GameText strings = GameText.Load(registry.Active.Language);

        MainMenuWindow window = new(registry, settingsPath, diagnostic, strings);
        MainWindow = window;

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }
}
