using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Spelljammer.Presentation;
using Spelljammer.Settings;

namespace Spelljammer;

internal sealed class GameSettingsDialog : Grid, IDisposable
{
    private readonly GameSettingsRegistry registry;
    private readonly string settingsPath;
    private readonly SpriteForgeSettingsView settingsView;
    private bool applyInProgress;
    private bool disposed;

    internal GameSettingsDialog(
        GameSettingsRegistry registry,
        string settingsPath,
        GameText strings)
    {
        this.registry = registry;
        this.settingsPath = settingsPath;
        Background = new SolidColorBrush(Color.FromArgb(184, 3, 6, 14));
        Focusable = true;

        settingsView = new SpriteForgeSettingsView(registry.Active, strings);
        Viewbox viewbox = new()
        {
            Child = settingsView,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };
        Children.Add(viewbox);

        settingsView.ApplyRequested += SettingsView_ApplyRequested;
        settingsView.CancelRequested += SettingsView_CancelRequested;
    }

    internal event EventHandler? Applied;
    internal event EventHandler? Cancelled;

    internal bool ApplyInProgress => applyInProgress;

    private async void SettingsView_ApplyRequested(object? sender, GameSettingsApplyRequestedEventArgs e)
    {
        if (applyInProgress)
        {
            return;
        }

        applyInProgress = true;
        settingsView.SetBusy();
        GameSettingsApplyResult result = await Task.Run(() => registry.Apply(settingsPath, e.Profile));
        applyInProgress = false;
        if (!result.Applied)
        {
            settingsView.SetApplyFailure(result.Diagnostic);
            return;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsView_CancelRequested(object? sender, EventArgs e)
    {
        if (!applyInProgress)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        settingsView.ApplyRequested -= SettingsView_ApplyRequested;
        settingsView.CancelRequested -= SettingsView_CancelRequested;
        settingsView.Dispose();
        Children.Clear();
        GC.SuppressFinalize(this);
    }
}
