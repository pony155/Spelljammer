using System.Windows.Controls;
using Spelljammer.Presentation;

namespace Spelljammer;

/// <summary>
/// Hosts character creation and translates presentation selections into a simulation request.
/// </summary>
/// <remarks>
/// Code flow: Available content becomes UI choices, player input produces a deterministic selection, and completion returns the selected character seed and definition IDs.
/// </remarks>
internal sealed class CharacterCreationScreen : Grid, IDisposable
{
    private readonly SpriteForgeCharacterCreationView creationView;
    private bool disposed;

    internal CharacterCreationScreen(GameText strings, CharacterCreationSelection initial)
    {
        creationView = new SpriteForgeCharacterCreationView(strings, initial);
        creationView.Completed += CreationView_Completed;
        creationView.CancelRequested += CreationView_CancelRequested;
        Children.Add(creationView);
    }

    internal event EventHandler<CharacterCreationCompletedEventArgs>? Completed;
    internal event EventHandler? Cancelled;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        creationView.Completed -= CreationView_Completed;
        creationView.CancelRequested -= CreationView_CancelRequested;
        creationView.Dispose();
        Children.Clear();
        GC.SuppressFinalize(this);
    }

    private void CreationView_Completed(object? sender, CharacterCreationCompletedEventArgs e) =>
        Completed?.Invoke(this, e);

    private void CreationView_CancelRequested(object? sender, EventArgs e) =>
        Cancelled?.Invoke(this, EventArgs.Empty);
}
