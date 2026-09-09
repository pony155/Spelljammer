using System.Windows.Controls;
using System.Windows.Media;
using Spelljammer.Presentation;

namespace Spelljammer;

/// <summary>
/// Hosts the new-campaign galaxy generator as a full-window in-app screen.
/// </summary>
internal sealed class GalaxyMapGeneratorScreen : Grid, IDisposable
{
    private readonly GalaxyMapGeneratorView generatorView;
    private bool disposed;

    internal GalaxyMapGeneratorScreen(GameText strings, GalaxyMapSelection? initial)
    {
        Background = Brushes.Black;
        generatorView = new GalaxyMapGeneratorView(strings, initial);
        generatorView.Completed += GeneratorView_Completed;
        generatorView.CancelRequested += GeneratorView_CancelRequested;
        Children.Add(new Viewbox {
            Child = generatorView,
            Stretch = Stretch.Fill,
        });
    }

    internal event EventHandler<GalaxyMapGeneratorCompletedEventArgs>? Completed;
    internal event EventHandler? Cancelled;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        generatorView.Completed -= GeneratorView_Completed;
        generatorView.CancelRequested -= GeneratorView_CancelRequested;
        generatorView.Dispose();
        Children.Clear();
        GC.SuppressFinalize(this);
    }

    private void GeneratorView_Completed(object? sender, GalaxyMapGeneratorCompletedEventArgs e) =>
        Completed?.Invoke(this, e);

    private void GeneratorView_CancelRequested(object? sender, EventArgs e) =>
        Cancelled?.Invoke(this, EventArgs.Empty);
}
