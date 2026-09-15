namespace Spelljammer.Presentation;

/// <summary>
/// SpriteForge-rendered main menu. WPF supplies only the containing window.
/// </summary>
internal sealed class SpriteForgeMainMenuView : SpriteForgeRenderSurface
{
    internal const uint SurfaceWidth = 1280;
    internal const uint SurfaceHeight = 720;
    private const string BackgroundUri = "pack://application:,,,/Assets/UI/MainMenu/Background.png";

    private static readonly HitBox NewGameButton = new(860, 242, 300, 68);
    private static readonly HitBox SettingsButton = new(860, 326, 300, 68);
    private static readonly HitBox QuitButton = new(860, 410, 300, 68);

    private readonly GameText strings;
    private readonly string version;
    private HitBox? pressed;
    private HitBox? hovered;
    private string status = string.Empty;
    private bool statusIsError;

    internal SpriteForgeMainMenuView(GameText strings, string version)
        : base(SurfaceWidth, SurfaceHeight, strings.Culture)
    {
        this.strings = strings;
        this.version = version;
    }

    internal event EventHandler? NewGameRequested;
    internal event EventHandler? SettingsRequested;
    internal event EventHandler? QuitRequested;

    internal void SetStatus(string text, bool isError)
    {
        status = text;
        statusIsError = isError;
        RefreshSurface();
    }

    internal void RefreshLanguage() => ResetText(strings.Culture);

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.ImageCover(BackgroundUri, 0, 0, SurfaceWidth, SurfaceHeight);
        canvas.Fill(800, 70, 420, 580, "#D9080C17");
        canvas.Border(800, 70, 420, 580, 1, "#66556F91");
        canvas.TextLine(strings.Get("menu.title"), 830, 120, 360, 72, 48, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(strings.Get("menu.subtitle"), 830, 190, 360, 44, 16, "#FFC4BBD1",
            SpriteForgeTextAlignment.Center);

        DrawButton(canvas, NewGameButton, strings.Get("menu.button.new-game"));
        DrawButton(canvas, SettingsButton, strings.Get("menu.button.settings"));
        DrawButton(canvas, QuitButton, strings.Get("menu.button.quit"));

        if (!string.IsNullOrEmpty(status))
        {
            canvas.TextLine(status, 835, 485, 350, 58, 13,
                statusIsError ? "#FFF39A8D" : "#FFB8C7DF", SpriteForgeTextAlignment.Center);
        }

        canvas.TextLine(strings.Version(version), 1010, 674, 240, 26, 13, "#FFC4BBD1",
            SpriteForgeTextAlignment.Right);
    }

    protected override void PointerMoved(float x, float y)
    {
        HitBox? next = FindButton(x, y);
        if (next != hovered)
        {
            hovered = next;
            RefreshSurface();
        }
    }

    protected override void PointerPressed(float x, float y)
    {
        pressed = FindButton(x, y);
        RefreshSurface();
    }

    protected override void PointerReleased(float x, float y)
    {
        HitBox? released = FindButton(x, y);
        HitBox? activated = pressed == released ? released : null;
        pressed = null;
        RefreshSurface();
        if (activated == NewGameButton)
        {
            Dispatch(() => NewGameRequested?.Invoke(this, EventArgs.Empty));
        }
        else if (activated == SettingsButton)
        {
            Dispatch(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        }
        else if (activated == QuitButton)
        {
            Dispatch(() => QuitRequested?.Invoke(this, EventArgs.Empty));
        }
    }

    private void DrawButton(SpriteForgeCanvas canvas, HitBox box, string text)
    {
        string fill = pressed == box ? "#FF132238" : hovered == box ? "#F23A526F" : "#D91B2C47";
        canvas.Fill(box.X, box.Y, box.Width, box.Height, fill, 5);
        canvas.Border(box.X, box.Y, box.Width, box.Height, hovered == box ? 2 : 1,
            hovered == box ? "#FF80DED9" : "#99647B9D", 6);
        canvas.TextLine(text, box.X, box.Y, box.Width, box.Height, 20, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center, 100);
    }

    private static HitBox? FindButton(float x, float y)
    {
        if (NewGameButton.Contains(x, y))
        {
            return NewGameButton;
        }

        if (SettingsButton.Contains(x, y))
        {
            return SettingsButton;
        }

        return QuitButton.Contains(x, y) ? QuitButton : null;
    }

    private readonly record struct HitBox(float X, float Y, float Width, float Height)
    {
        internal bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}
