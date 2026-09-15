using Spelljammer.Localization;
using Spelljammer.Settings;

namespace Spelljammer.Presentation;

internal sealed class GameSettingsApplyRequestedEventArgs(GameSettingsProfile profile) : EventArgs
{
    internal GameSettingsProfile Profile { get; } = profile;
}

/// <summary>
/// SpriteForge-rendered settings surface with bounded pointer controls.
/// </summary>
internal sealed class SpriteForgeSettingsView : SpriteForgeRenderSurface
{
    private const uint SurfaceWidth = 960;
    private const uint SurfaceHeight = 680;
    private const float ControlX = 520;
    private const float ControlWidth = 300;

    private static readonly HitBox LanguageControl = new(ControlX, 160, ControlWidth, 42, Control.Language);
    private static readonly HitBox Resolution = new(ControlX, 214, ControlWidth, 42, Control.Resolution);
    private static readonly HitBox Master = new(ControlX, 302, ControlWidth, 36, Control.Master);
    private static readonly HitBox Music = new(ControlX, 348, ControlWidth, 36, Control.Music);
    private static readonly HitBox Effects = new(ControlX, 394, ControlWidth, 36, Control.Effects);
    private static readonly HitBox Subtitles = new(ControlX, 468, 94, 38, Control.Subtitles);
    private static readonly HitBox ReducedMotion = new(626, 468, 94, 38, Control.ReducedMotion);
    private static readonly HitBox ScreenShake = new(732, 468, 94, 38, Control.ScreenShake);
    private static readonly HitBox UiScale = new(ControlX, 518, ControlWidth, 36, Control.UiScale);
    private static readonly HitBox Reset = new(360, 586, 140, 48, Control.Reset);
    private static readonly HitBox Cancel = new(515, 586, 140, 48, Control.Cancel);
    private static readonly HitBox Apply = new(670, 586, 155, 48, Control.Apply);
    private static readonly HitBox[] HitBoxes =
    [
        LanguageControl, Resolution, Master, Music, Effects, Subtitles, ReducedMotion,
        ScreenShake, UiScale, Reset, Cancel, Apply,
    ];

    private readonly GameText strings;
    private GameSettingsProfile draft;
    private Control hovered;
    private Control pressed;
    private string status;
    private bool statusIsError;
    private bool busy;

    internal SpriteForgeSettingsView(GameSettingsProfile initial, GameText strings)
        : base(SurfaceWidth, SurfaceHeight, strings.Culture)
    {
        draft = initial;
        this.strings = strings;
        status = strings.Get("settings.status.ready");
    }

    internal event EventHandler<GameSettingsApplyRequestedEventArgs>? ApplyRequested;
    internal event EventHandler? CancelRequested;

    internal void SetApplyFailure(GameSettingsDiagnostic diagnostic)
    {
        busy = false;
        status = strings.Format("settings.status.save-failed",
            LocalizationArgument.Text("code", GameSettingsDiagnostics.Stable(diagnostic)));
        statusIsError = true;
        RefreshSurface();
    }

    internal void SetBusy()
    {
        busy = true;
        status = strings.Get("settings.status.saving");
        statusIsError = false;
        RefreshSurface();
    }

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.Fill(0, 0, SurfaceWidth, SurfaceHeight, "#FF050914", -10);
        canvas.Fill(24, 20, 912, 640, "#FA0C1423", -5);
        canvas.Border(24, 20, 912, 640, 1, "#8860789C", -4);
        canvas.TextLine(strings.Get("settings.title"), 54, 48, 852, 50, 34, "#FFF2E9D8");
        canvas.TextLine(strings.Get("settings.introduction"), 54, 96, 820, 40, 14, "#FFB8C7DF");

        DrawSection(canvas, strings.Get("settings.heading.display"), 54, 160);
        DrawLabel(canvas, strings.Get("settings.label.language"), 300, 160);
        DrawButton(canvas, LanguageControl, strings.LanguageName(draft.Language));
        DrawLabel(canvas, strings.Get("settings.label.resolution"), 300, 214);
        DrawButton(canvas, Resolution, strings.ResolutionName(CurrentResolution()));

        DrawSection(canvas, strings.Get("settings.heading.audio"), 54, 302);
        DrawSlider(canvas, Master, strings.Get("settings.label.master-volume"), draft.MasterVolume, 0, 100);
        DrawSlider(canvas, Music, strings.Get("settings.label.music-volume"), draft.MusicVolume, 0, 100);
        DrawSlider(canvas, Effects, strings.Get("settings.label.effects-volume"), draft.EffectsVolume, 0, 100);

        DrawSection(canvas, strings.Get("settings.heading.accessibility"), 54, 468);
        DrawToggle(canvas, Subtitles, strings.Get("settings.label.subtitles"), draft.Subtitles);
        DrawToggle(canvas, ReducedMotion, strings.Get("settings.label.reduced-motion"), draft.ReducedMotion);
        DrawToggle(canvas, ScreenShake, strings.Get("settings.label.screen-shake"), draft.ScreenShake);
        DrawSlider(canvas, UiScale, strings.Get("settings.label.interface-scale"), draft.UiScalePercent, 75, 150);

        DrawButton(canvas, Reset, strings.Get("settings.button.reset"));
        DrawButton(canvas, Cancel, strings.Get("settings.button.cancel"));
        DrawButton(canvas, Apply, strings.Get("settings.button.apply"), primary: true);
        canvas.TextLine(status, 54, 638, 852, 22, 12, statusIsError ? "#FFF39A8D" : "#FF9FB3CE",
            SpriteForgeTextAlignment.Center);
    }

    protected override void PointerMoved(float x, float y)
    {
        Control next = Find(x, y);
        if (next != hovered)
        {
            hovered = next;
            RefreshSurface();
        }
    }

    protected override void PointerPressed(float x, float y)
    {
        if (!busy)
        {
            pressed = Find(x, y);
            RefreshSurface();
        }
    }

    protected override void PointerReleased(float x, float y)
    {
        if (busy)
        {
            return;
        }

        Control released = Find(x, y);
        Control action = released == pressed ? released : Control.None;
        pressed = Control.None;
        if (action is Control.Master or Control.Music or Control.Effects or Control.UiScale)
        {
            HitBox box = HitBoxes.First(value => value.Control == action);
            ChangeSlider(action, x, box);
        }
        else
        {
            Activate(action);
        }

        RefreshSurface();
    }

    protected override void KeyPressed(int virtualKey)
    {
        if (virtualKey == 0x1B && !busy)
        {
            Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        }
    }

    private void Activate(Control control)
    {
        switch (control)
        {
            case Control.Language:
                draft = draft with { Language = Cycle(GameSettingsChoices.Languages, draft.Language) };
                break;
            case Control.Resolution:
                int resolutionIndex = GameSettingsChoices.Resolutions.ToList().FindIndex(value => value.Id == draft.Resolution);
                draft = draft with {
                    Resolution = GameSettingsChoices.Resolutions[(resolutionIndex + 1) % GameSettingsChoices.Resolutions.Count].Id,
                };
                break;
            case Control.Subtitles:
                draft = draft with { Subtitles = !draft.Subtitles };
                break;
            case Control.ReducedMotion:
                draft = draft with { ReducedMotion = !draft.ReducedMotion };
                break;
            case Control.ScreenShake:
                draft = draft with { ScreenShake = !draft.ScreenShake };
                break;
            case Control.Reset:
                draft = GameSettingsProfile.Default;
                status = strings.Get("settings.status.reset");
                statusIsError = false;
                break;
            case Control.Cancel:
                Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
                break;
            case Control.Apply:
                GameSettingsProfile requested = draft;
                Dispatch(() => ApplyRequested?.Invoke(this, new GameSettingsApplyRequestedEventArgs(requested)));
                break;
        }
    }

    private void ChangeSlider(Control control, float x, HitBox box)
    {
        float ratio = Math.Clamp((x - box.X) / box.Width, 0, 1);
        switch (control)
        {
            case Control.Master:
                draft = draft with { MasterVolume = Step(ratio, 0, 100, 1) };
                break;
            case Control.Music:
                draft = draft with { MusicVolume = Step(ratio, 0, 100, 1) };
                break;
            case Control.Effects:
                draft = draft with { EffectsVolume = Step(ratio, 0, 100, 1) };
                break;
            case Control.UiScale:
                draft = draft with { UiScalePercent = Step(ratio, 75, 150, 5) };
                break;
        }
    }

    private void DrawSection(SpriteForgeCanvas canvas, string text, float x, float y)
    {
        canvas.TextLine(text, x, y, 220, 38, 18, "#FFE3B966");
    }

    private void DrawLabel(SpriteForgeCanvas canvas, string text, float x, float y)
    {
        canvas.TextLine(text, x, y, 200, 42, 14, "#FFB8C7DF");
    }

    private void DrawButton(SpriteForgeCanvas canvas, HitBox box, string text, bool primary = false)
    {
        string fill = primary ? "#FF80DED9" : pressed == box.Control ? "#FF17243A" : "#FF21334D";
        if (hovered == box.Control && !busy)
        {
            fill = primary ? "#FF9CF0EB" : "#FF314765";
        }

        canvas.Fill(box.X, box.Y, box.Width, box.Height, fill, 4);
        canvas.Border(box.X, box.Y, box.Width, box.Height, 1, primary ? "#FFB9FFFF" : "#FF60789C", 5);
        canvas.TextLine(text, box.X, box.Y, box.Width, box.Height, 13,
            primary ? "#FF07111E" : "#FFF2E9D8", SpriteForgeTextAlignment.Center);
    }

    private void DrawSlider(SpriteForgeCanvas canvas, HitBox box, string label, int value, int minimum, int maximum)
    {
        canvas.TextLine(label, 300, box.Y, 200, box.Height, 13, "#FFB8C7DF");
        canvas.Fill(box.X, box.Y + 16, box.Width, 4, "#FF263A55", 2);
        float ratio = (value - minimum) / (float)(maximum - minimum);
        canvas.Fill(box.X, box.Y + 16, box.Width * ratio, 4, "#FF80DED9", 3);
        canvas.Circle(box.X + box.Width * ratio, box.Y + 18, hovered == box.Control ? 17 : 13, "#FFB9FFFF", 5);
        canvas.TextLine(strings.Percent(value), box.X + box.Width + 12, box.Y, 70, box.Height, 13, "#FF80DED9");
    }

    private void DrawToggle(SpriteForgeCanvas canvas, HitBox box, string label, bool value)
    {
        canvas.TextLine(label, box.X, box.Y - 26, box.Width, 24, 11, "#FFB8C7DF",
            SpriteForgeTextAlignment.Center);
        DrawButton(canvas, box, strings.Get(value ? "settings.state.on" : "settings.state.off"), primary: value);
    }

    private GameResolutionChoice CurrentResolution()
    {
        if (!GameSettingsChoices.TryGetResolution(draft.Resolution, out GameResolutionChoice resolution))
        {
            throw new InvalidOperationException($"Unsupported draft display resolution '{draft.Resolution}'.");
        }

        return resolution;
    }

    private static string Cycle(IReadOnlyList<string> values, string current)
    {
        int index = values.ToList().FindIndex(value => string.Equals(value, current, StringComparison.Ordinal));
        return values[(index + 1) % values.Count];
    }

    private static int Step(float ratio, int minimum, int maximum, int increment)
    {
        int raw = minimum + (int)MathF.Round(ratio * (maximum - minimum));
        return Math.Clamp((int)MathF.Round(raw / (float)increment) * increment, minimum, maximum);
    }

    private static Control Find(float x, float y) =>
        HitBoxes.FirstOrDefault(value => value.Contains(x, y)).Control;

    private enum Control : byte
    {
        None,
        Language,
        Resolution,
        Master,
        Music,
        Effects,
        Subtitles,
        ReducedMotion,
        ScreenShake,
        UiScale,
        Reset,
        Cancel,
        Apply,
    }

    private readonly record struct HitBox(float X, float Y, float Width, float Height, Control Control)
    {
        internal bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}
