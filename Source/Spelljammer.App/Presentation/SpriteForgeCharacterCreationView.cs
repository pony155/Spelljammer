using System.Globalization;
using Spelljammer.Localization;

namespace Spelljammer.Presentation;

internal sealed class CharacterCreationCompletedEventArgs(CharacterCreationSelection selection) : EventArgs
{
    internal CharacterCreationSelection Selection { get; } = selection;
}

/// <summary>
/// SpriteForge-rendered captain selection screen.
/// </summary>
internal sealed class SpriteForgeCharacterCreationView : SpriteForgeRenderSurface
{
    private const uint SurfaceWidth = 1600;
    private const uint SurfaceHeight = 900;
    private const string BackgroundUri = "pack://application:,,,/Assets/UI/MainMenu/Background.png";
    private static readonly HitBox Back = new(42, 828, 180, 52, -1);
    private static readonly HitBox Confirm = new(1330, 828, 228, 52, -2);
    private static readonly HitBox[] Choices = CharacterCreationChoices.All
        .Select((_, index) => new HitBox(64, 202 + index * 49, 316, 42, index))
        .ToArray();

    private readonly GameText strings;
    private readonly ulong seed;
    private int choiceIndex;
    private int hovered = int.MinValue;
    private int pressed = int.MinValue;

    internal SpriteForgeCharacterCreationView(GameText strings, CharacterCreationSelection initial)
        : base(SurfaceWidth, SurfaceHeight, strings.Culture)
    {
        this.strings = strings;
        seed = initial.Seed;
        choiceIndex = FindChoiceIndex(initial.Choice.CharacterId);
    }

    internal event EventHandler<CharacterCreationCompletedEventArgs>? Completed;
    internal event EventHandler? CancelRequested;

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.ImageCover(BackgroundUri, 0, 0, SurfaceWidth, SurfaceHeight);
        canvas.Fill(0, 0, SurfaceWidth, SurfaceHeight, "#C9050912", -8);
        canvas.Fill(34, 30, 1532, 850, "#F20A1220", -7);
        canvas.Border(34, 30, 1532, 850, 1, "#88556F91", -6);

        CharacterCreationChoice choice = CurrentChoice;
        string captain = strings.Get($"creation.captain.{choice.TextId}.name");
        string race = strings.Get($"creation.race.{choice.TextId}.name");
        string heritage = strings.Get($"creation.heritage.{choice.TextId}.name");
        string background = strings.Get("creation.background.expedition-veteran.name");

        canvas.TextLine(strings.Get("creation.title"), 64, 52, 900, 52, 31, "#FFF2E9D8");
        canvas.TextLine(strings.Get("creation.introduction"), 64, 104, 1100, 36, 13, "#FF93A1BE");
        canvas.TextLine(string.Format(strings.Culture, "{0:00} / {1:00}", choiceIndex + 1, CharacterCreationChoices.All.Count),
            1320, 60, 200, 40, 15, "#FFD7AF70", SpriteForgeTextAlignment.Right);

        canvas.Fill(50, 160, 348, 618, "#E6121E31", -2);
        canvas.Border(50, 160, 348, 618, 1, "#66556F91", -1);
        canvas.TextLine(strings.Get("creation.accessibility.screen"), 64, 168, 316, 30, 15, "#FFD7AF70");
        for (int index = 0; index < Choices.Length; ++index)
        {
            HitBox box = Choices[index];
            bool selected = index == choiceIndex;
            string fill = selected ? "#FF29465C" : hovered == index ? "#FF1E3048" : "#B3142033";
            canvas.Fill(box.X, box.Y, box.Width, box.Height, fill, 2);
            if (selected || hovered == index)
            {
                canvas.Border(box.X, box.Y, box.Width, box.Height, selected ? 2 : 1,
                    selected ? "#FF80DED9" : "#FF60789C", 3);
            }

            string name = strings.Get($"creation.captain.{CharacterCreationChoices.All[index].TextId}.name");
            canvas.TextLine(name, box.X + 18, box.Y, box.Width - 36, box.Height, 13, "#FFF2E9D8");
        }

        canvas.Fill(430, 160, 1086, 618, "#D9101A2B", -2);
        canvas.Border(430, 160, 1086, 618, 1, "#66556F91", -1);
        canvas.Circle(716, 328, 210, "#FF233A54", 1);
        canvas.Circle(716, 328, 184, "#FF102035", 2);
        canvas.TextLine(StringInfo.GetNextTextElement(captain), 626, 238, 180, 180, 76, "#FF80DED9",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(captain, 520, 448, 392, 48, 25, "#FFF2E9D8", SpriteForgeTextAlignment.Center);

        canvas.TextLine(strings.Get("creation.accessibility.details"), 960, 202, 470, 36, 18, "#FFF2E9D8");
        DrawDetail(canvas, strings.Get("creation.label.race"), race, 960, 262);
        DrawDetail(canvas, strings.Get("creation.label.heritage"), heritage, 960, 352);
        DrawDetail(canvas, strings.Get("creation.label.background"), background, 960, 442);
        canvas.TextLine(strings.Format("creation.summary",
            LocalizationArgument.Text("race", race),
            LocalizationArgument.Text("heritage", heritage),
            LocalizationArgument.Text("background", background)),
            960, 532, 470, 72, 14, "#FFB8C7DF");
        canvas.TextLine(strings.Get("creation.label.seed"), 960, 622, 470, 24, 12, "#FFD7AF70");
        canvas.TextLine(strings.Format("creation.value.seed", LocalizationArgument.Unsigned("seed", seed)),
            960, 646, 470, 34, 16, "#FF80DED9");

        canvas.TextLine(strings.Get("creation.status.ready"), 300, 828, 980, 52, 13, "#FF93A1BE",
            SpriteForgeTextAlignment.Center);
        DrawButton(canvas, Back, strings.Get("creation.button.back"));
        DrawButton(canvas, Confirm, strings.Get("creation.button.confirm"), primary: true);
    }

    protected override void PointerMoved(float x, float y)
    {
        int next = Find(x, y);
        if (next != hovered)
        {
            hovered = next;
            RefreshSurface();
        }
    }

    protected override void PointerPressed(float x, float y)
    {
        pressed = Find(x, y);
        RefreshSurface();
    }

    protected override void PointerReleased(float x, float y)
    {
        int released = Find(x, y);
        int action = released == pressed ? released : int.MinValue;
        pressed = int.MinValue;
        if (action >= 0)
        {
            choiceIndex = action;
        }
        else if (action == -1)
        {
            Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        }
        else if (action == -2)
        {
            CharacterCreationSelection selection = new(CurrentChoice, seed);
            Dispatch(() => Completed?.Invoke(this, new CharacterCreationCompletedEventArgs(selection)));
        }

        RefreshSurface();
    }

    protected override void KeyPressed(int virtualKey)
    {
        if (virtualKey == 0x1B)
        {
            Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        }
        else if (virtualKey == 0x0D)
        {
            CharacterCreationSelection selection = new(CurrentChoice, seed);
            Dispatch(() => Completed?.Invoke(this, new CharacterCreationCompletedEventArgs(selection)));
        }
    }

    private CharacterCreationChoice CurrentChoice => CharacterCreationChoices.All[choiceIndex];

    private static int FindChoiceIndex(Spelljammer.Simulation.Content.CharacterId id)
    {
        for (int index = 0; index < CharacterCreationChoices.All.Count; ++index)
        {
            if (CharacterCreationChoices.All[index].CharacterId == id)
            {
                return index;
            }
        }

        return 0;
    }

    private static int Find(float x, float y)
    {
        HitBox choice = Choices.FirstOrDefault(value => value.Contains(x, y));
        if (choice.Index >= 0 && choice.Contains(x, y))
        {
            return choice.Index;
        }

        if (Back.Contains(x, y))
        {
            return -1;
        }

        return Confirm.Contains(x, y) ? -2 : int.MinValue;
    }

    private void DrawDetail(SpriteForgeCanvas canvas, string label, string value, float x, float y)
    {
        canvas.TextLine(label, x, y, 470, 24, 12, "#FFD7AF70");
        canvas.TextLine(value, x, y + 24, 470, 40, 19, "#FFF2E9D8");
        canvas.Fill(x, y + 70, 470, 1, "#44556F91", 1);
    }

    private void DrawButton(SpriteForgeCanvas canvas, HitBox box, string text, bool primary = false)
    {
        string fill = primary ? "#FF80DED9" : "#FF17243A";
        if (hovered == box.Index)
        {
            fill = primary ? "#FFA0F3EE" : "#FF293C58";
        }

        canvas.Fill(box.X, box.Y, box.Width, box.Height, fill, 4);
        canvas.Border(box.X, box.Y, box.Width, box.Height, 1, primary ? "#FFB9FFFF" : "#FF60789C", 5);
        canvas.TextLine(text, box.X, box.Y, box.Width, box.Height, 14,
            primary ? "#FF07111E" : "#FFF2E9D8", SpriteForgeTextAlignment.Center);
    }

    private readonly record struct HitBox(float X, float Y, float Width, float Height, int Index)
    {
        internal bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}
