using System.Windows;
using Spelljammer.Presentation;

namespace Spelljammer;

/// <summary>
/// Borderless startup host whose complete client area is rendered by SpriteForge.
/// </summary>
internal sealed class StartupSplashWindow : Window, IDisposable
{
    private readonly SpriteForgeStartupSplashView splashView;
    private bool disposed;

    internal StartupSplashWindow()
    {
        Title = "Spelljammer";
        Width = 800;
        Height = 450;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        splashView = new SpriteForgeStartupSplashView();
        Content = splashView;
        Closed += StartupSplashWindow_Closed;
    }

    internal void SetProgress(float value) => splashView.SetProgress(value);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Closed -= StartupSplashWindow_Closed;
        splashView.Dispose();
        Content = null;
        GC.SuppressFinalize(this);
    }

    private void StartupSplashWindow_Closed(object? sender, EventArgs e) => Dispose();
}
