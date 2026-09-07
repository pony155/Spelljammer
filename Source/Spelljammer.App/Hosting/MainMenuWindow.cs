using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Spelljammer.Presentation;
using Spelljammer.Settings;

namespace Spelljammer;

internal sealed class MainMenuWindow : Window
{
    private readonly GameSettingsRegistry settings;
    private readonly string settingsPath;
    private readonly GameText strings;
    private readonly Grid root;
    private readonly SpriteForgeMainMenuView menuView;
    private GameSettingsDialog? settingsDialog;
    private CharacterCreationScreen? characterCreation;
    private CharacterCreationSelection? newGameDraft;

    internal MainMenuWindow(
        GameSettingsRegistry settings,
        string settingsPath,
        GameSettingsDiagnostic startupDiagnostic,
        GameText strings)
    {
        this.settings = settings;
        this.settingsPath = settingsPath;
        this.strings = strings;
        Title = strings.Get("menu.window-title");
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        WindowState = WindowState.Maximized;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Black;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        menuView = new SpriteForgeMainMenuView(strings, GetApplicationVersion());
        menuView.NewGameRequested += MenuView_NewGameRequested;
        menuView.SettingsRequested += MenuView_SettingsRequested;
        menuView.QuitRequested += MenuView_QuitRequested;
        root = new Grid();
        root.Children.Add(menuView);
        Content = root;
        Closing += Window_Closing;
        Closed += Window_Closed;
        ApplyResolution(settings.Active.Resolution);

        if (startupDiagnostic is not GameSettingsDiagnostic.None and not GameSettingsDiagnostic.Missing)
        {
            menuView.SetStatus(strings.Diagnostic(
                "settings.status.load-failed",
                GameSettingsDiagnostics.Stable(startupDiagnostic)), isError: true);
        }
    }

    private void MenuView_NewGameRequested(object? sender, EventArgs e)
    {
        if (characterCreation is not null || settingsDialog is not null)
        {
            return;
        }

        characterCreation = new CharacterCreationScreen(strings, newGameDraft);
        characterCreation.Completed += CharacterCreation_Completed;
        characterCreation.Cancelled += CharacterCreation_Cancelled;
        menuView.IsEnabled = false;
        root.Children.Add(characterCreation);
    }

    private void CharacterCreation_Completed(object? sender, CharacterCreationCompletedEventArgs e)
    {
        newGameDraft = e.Selection;
        string captain = strings.Get($"creation.captain.{e.Selection.Choice.TextId}.name");
        CloseCharacterCreation();
        menuView.SetStatus(strings.Format(
            "creation.status.selected",
            Spelljammer.Localization.LocalizationArgument.Text("captain", captain)), isError: false);
        menuView.Focus();
    }

    private void CharacterCreation_Cancelled(object? sender, EventArgs e)
    {
        CloseCharacterCreation();
        menuView.Focus();
    }

    private void CloseCharacterCreation()
    {
        if (characterCreation is null)
        {
            return;
        }

        characterCreation.Completed -= CharacterCreation_Completed;
        characterCreation.Cancelled -= CharacterCreation_Cancelled;
        root.Children.Remove(characterCreation);
        characterCreation.Dispose();
        characterCreation = null;
        menuView.IsEnabled = true;
    }

    private void MenuView_SettingsRequested(object? sender, EventArgs e)
    {
        if (settingsDialog is not null)
        {
            return;
        }

        settingsDialog = new GameSettingsDialog(settings, settingsPath, strings);
        settingsDialog.Applied += SettingsDialog_Applied;
        settingsDialog.Cancelled += SettingsDialog_Cancelled;
        menuView.IsEnabled = false;
        root.Children.Add(settingsDialog);
    }

    private void SettingsDialog_Applied(object? sender, EventArgs e)
    {
        CloseSettingsDialog();
        strings.SetLanguage(settings.Active.Language);
        Title = strings.Get("menu.window-title");
        menuView.RefreshLanguage();
        ApplyResolution(settings.Active.Resolution);
        menuView.SetStatus(strings.Get("settings.status.saved"), isError: false);
        menuView.Focus();
    }

    private void SettingsDialog_Cancelled(object? sender, EventArgs e)
    {
        CloseSettingsDialog();
        menuView.Focus();
    }

    private void CloseSettingsDialog()
    {
        if (settingsDialog is null)
        {
            return;
        }

        settingsDialog.Applied -= SettingsDialog_Applied;
        settingsDialog.Cancelled -= SettingsDialog_Cancelled;
        root.Children.Remove(settingsDialog);
        settingsDialog.Dispose();
        settingsDialog = null;
        menuView.IsEnabled = true;
    }

    private static void MenuView_QuitRequested(object? sender, EventArgs e) =>
        Application.Current.Shutdown();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (settingsDialog?.ApplyInProgress == true)
        {
            e.Cancel = true;
        }
    }

    private static string GetApplicationVersion()
    {
        string version = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? throw new InvalidOperationException("The application version metadata is unavailable.");
        int buildMetadata = version.IndexOf('+', StringComparison.Ordinal);
        return buildMetadata >= 0 ? version[..buildMetadata] : version;
    }

    private void ApplyResolution(string resolutionId)
    {
        if (!GameSettingsChoices.TryGetResolution(resolutionId, out GameResolutionChoice resolution))
        {
            throw new InvalidOperationException($"Unsupported active display resolution '{resolutionId}'.");
        }

        if (resolution.IsDesktop)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        WindowState = WindowState.Normal;
        Rect workArea = SystemParameters.WorkArea;
        Width = Math.Min(resolution.Width, workArea.Width);
        Height = Math.Min(resolution.Height, workArea.Height);
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        CloseCharacterCreation();
        CloseSettingsDialog();
        menuView.NewGameRequested -= MenuView_NewGameRequested;
        menuView.SettingsRequested -= MenuView_SettingsRequested;
        menuView.QuitRequested -= MenuView_QuitRequested;
        menuView.Dispose();
        Closing -= Window_Closing;
        Closed -= Window_Closed;
    }
}
