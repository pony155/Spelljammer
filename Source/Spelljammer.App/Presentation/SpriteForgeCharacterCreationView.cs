using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spelljammer.Interop;
using Spelljammer.Localization;

namespace Spelljammer.Presentation;

internal sealed class CharacterCreationCompletedEventArgs(CharacterCreationSelection selection) : EventArgs
{
    internal CharacterCreationSelection Selection { get; } = selection;
}

internal sealed class SpriteForgeCharacterCreationView : FrameworkElement, IDisposable
{
    internal const double LogicalWidth = 1600;
    internal const double LogicalHeight = 900;
    private const uint ElementCapacity = 64;
    private const uint ActionCapacity = 32;
    private const uint NonEditableTextCapacity = 1;

    private static readonly ulong RootKey = StableKey("spelljammer.creation.root");
    private static readonly ulong ModalKey = StableKey("spelljammer.creation.modal");
    private static readonly ulong HeaderPanelKey = StableKey("spelljammer.creation.header-panel");
    private static readonly ulong HeaderRuleKey = StableKey("spelljammer.creation.header-rule");
    private static readonly ulong TitleKey = StableKey("spelljammer.creation.title");
    private static readonly ulong IntroductionKey = StableKey("spelljammer.creation.introduction");
    private static readonly ulong SelectionCounterKey = StableKey("spelljammer.creation.selection-counter");
    private static readonly ulong RosterPanelKey = StableKey("spelljammer.creation.roster-panel");
    private static readonly ulong RosterHeadingKey = StableKey("spelljammer.creation.roster-heading");
    private static readonly ulong PreviewPanelKey = StableKey("spelljammer.creation.preview-panel");
    private static readonly ulong PortraitKey = StableKey("spelljammer.creation.portrait");
    private static readonly ulong CaptainKey = StableKey("spelljammer.creation.captain");
    private static readonly ulong DetailPanelKey = StableKey("spelljammer.creation.details");
    private static readonly ulong DetailHeadingKey = StableKey("spelljammer.creation.details-heading");
    private static readonly ulong RaceLabelKey = StableKey("spelljammer.creation.race-label");
    private static readonly ulong RaceValueKey = StableKey("spelljammer.creation.race-value");
    private static readonly ulong HeritageLabelKey = StableKey("spelljammer.creation.heritage-label");
    private static readonly ulong HeritageValueKey = StableKey("spelljammer.creation.heritage-value");
    private static readonly ulong BackgroundLabelKey = StableKey("spelljammer.creation.background-label");
    private static readonly ulong BackgroundValueKey = StableKey("spelljammer.creation.background-value");
    private static readonly ulong SummaryKey = StableKey("spelljammer.creation.summary");
    private static readonly ulong SeedLabelKey = StableKey("spelljammer.creation.seed-label");
    private static readonly ulong SeedValueKey = StableKey("spelljammer.creation.seed-value");
    private static readonly ulong FooterPanelKey = StableKey("spelljammer.creation.footer-panel");
    private static readonly ulong StatusKey = StableKey("spelljammer.creation.status");
    private static readonly ulong BackKey = StableKey("spelljammer.creation.back");
    private static readonly ulong RerollKey = StableKey("spelljammer.creation.reroll");
    private static readonly ulong ConfirmKey = StableKey("spelljammer.creation.confirm");
    private static readonly ulong CancelAction = StableKey("spelljammer.creation.action.cancel");
    private static readonly ulong[] CaptainChoiceKeys =
        CharacterCreationChoices.All.Select((_, index) =>
            StableKey($"spelljammer.creation.captain-choice.{index}")).ToArray();

    private readonly ulong[] elementKeys;
    private readonly Dictionary<ulong, EngineUiElementSnapshot> snapshots = [];
    private readonly EngineUiPresentationCommand[] presentation = new EngineUiPresentationCommand[ElementCapacity];
    private readonly EngineUiAction[] actions = new EngineUiAction[ActionCapacity];
    private readonly GameText strings;
    private readonly BitmapSource background;
    private nint context;
    private ulong document;
    private ulong revision;
    private ulong inputSequence;
    private int choiceIndex;
    private ulong seed;
    private bool disposed;

    internal SpriteForgeCharacterCreationView(GameText strings, CharacterCreationSelection? initial)
    {
        this.strings = strings;
        choiceIndex = initial is null ? 0 : FindChoiceIndex(initial.Choice.CharacterId);
        seed = initial?.Seed ?? NewSeed();
        background = LoadBackground();
        elementKeys =
        [
            ModalKey, HeaderPanelKey, HeaderRuleKey, TitleKey, IntroductionKey, SelectionCounterKey,
            RosterPanelKey, RosterHeadingKey, PreviewPanelKey, PortraitKey, CaptainKey,
            DetailPanelKey, DetailHeadingKey, RaceLabelKey, RaceValueKey, HeritageLabelKey,
            HeritageValueKey, BackgroundLabelKey, BackgroundValueKey, SummaryKey, SeedLabelKey,
            SeedValueKey, FooterPanelKey, StatusKey, BackKey, RerollKey, ConfirmKey,
            .. CaptainChoiceKeys,
        ];
        Focusable = true;
        SnapsToDevicePixels = true;
        CreateNativeDocument();
        Loaded += View_Loaded;
        Unloaded += View_Unloaded;
    }

    internal event EventHandler<CharacterCreationCompletedEventArgs>? Completed;
    internal event EventHandler? CancelRequested;

    protected override Size MeasureOverride(Size availableSize) => new(LogicalWidth, LogicalHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        DrawBackdrop(drawingContext);
        if (context == nint.Zero)
        {
            return;
        }

        RefreshSnapshots();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIBuildPresentation(
            context, document, presentation, (uint)presentation.Length,
            out uint requiredCommands, out uint commandCount, out _),
            "build the character-creation presentation");
        RequireCompleteCopy(requiredCommands, commandCount, "character-creation presentation");
        for (int index = 0; index < commandCount; ++index)
        {
            EngineUiPresentationCommand command = presentation[index];
            if (command.Type != EngineUiPresentationType.SolidQuad)
            {
                continue;
            }

            drawingContext.DrawRectangle(ToBrush(command.Color), null, ScaleAndClip(command));
        }

        CharacterCreationChoice choice = CurrentChoice;
        string captain = strings.Get($"creation.captain.{choice.TextId}.name");
        string race = strings.Get($"creation.race.{choice.TextId}.name");
        string heritage = strings.Get($"creation.heritage.{choice.TextId}.name");
        string characterBackground = strings.Get("creation.background.expedition-veteran.name");

        DrawRosterSelection(drawingContext);
        DrawPortrait(drawingContext, StringInfo.GetNextTextElement(captain));
        strings.BeginFrame();
        DrawText(drawingContext, TitleKey, strings.Get("creation.title"), 31, "#F2E9D8",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, IntroductionKey, strings.Get("creation.introduction"), 13, "#93A1BE",
            TextAlignment.Left, FontWeights.Normal);
        DrawText(drawingContext, SelectionCounterKey,
            string.Format(strings.Culture, "{0:00} / {1:00}", choiceIndex + 1, CharacterCreationChoices.All.Count),
            15, "#D7AF70", TextAlignment.Right, FontWeights.SemiBold);
        DrawText(drawingContext, RosterHeadingKey, strings.Get("creation.accessibility.screen"), 15, "#D7AF70",
            TextAlignment.Left, FontWeights.SemiBold);
        for (int index = 0; index < CharacterCreationChoices.All.Count; ++index)
        {
            string name = strings.Get($"creation.captain.{CharacterCreationChoices.All[index].TextId}.name");
            DrawText(drawingContext, CaptainChoiceKeys[index], name, 13, "#F2E9D8",
                TextAlignment.Left, index == choiceIndex ? FontWeights.SemiBold : FontWeights.Normal, 18);
        }

        DrawText(drawingContext, CaptainKey, captain, 25, "#F2E9D8",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, DetailHeadingKey, strings.Get("creation.accessibility.details"), 18, "#F2E9D8",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, RaceLabelKey, strings.Get("creation.label.race"), 12, "#D7AF70",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, RaceValueKey, race, 19, "#F2E9D8",
            TextAlignment.Left, FontWeights.Normal);
        DrawText(drawingContext, HeritageLabelKey, strings.Get("creation.label.heritage"), 12, "#D7AF70",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, HeritageValueKey, heritage, 19, "#F2E9D8",
            TextAlignment.Left, FontWeights.Normal);
        DrawText(drawingContext, BackgroundLabelKey, strings.Get("creation.label.background"), 12, "#D7AF70",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, BackgroundValueKey, characterBackground, 19, "#F2E9D8",
            TextAlignment.Left, FontWeights.Normal);
        DrawText(drawingContext, SummaryKey, strings.Format(
            "creation.summary",
            LocalizationArgument.Text("race", race),
            LocalizationArgument.Text("heritage", heritage),
            LocalizationArgument.Text("background", characterBackground)),
            14, "#B8C7DF", TextAlignment.Left, FontWeights.Normal);
        DrawText(drawingContext, SeedLabelKey, strings.Get("creation.label.seed"), 12, "#D7AF70",
            TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, SeedValueKey, strings.Format(
            "creation.value.seed", LocalizationArgument.Unsigned("seed", seed)),
            16, "#80DED9", TextAlignment.Left, FontWeights.SemiBold);
        DrawText(drawingContext, RerollKey, strings.Get("creation.button.reroll"), 13, "#F2E9D8",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, StatusKey, strings.Get("creation.status.ready"), 13, "#93A1BE",
            TextAlignment.Center, FontWeights.Normal);
        DrawText(drawingContext, BackKey, strings.Get("creation.button.back"), 14, "#F2E9D8",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, ConfirmKey, strings.Get("creation.button.confirm"), 14, "#F2E9D8",
            TextAlignment.Center, FontWeights.SemiBold);

        foreach (EngineUiElementSnapshot snapshot in snapshots.Values.Where(value => value.IsFocused))
        {
            drawingContext.DrawRectangle(null, new Pen(Brush("#80DED9"), 2),
                Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SendPointer(EngineUiInputType.PointerMoved, e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        SendPointer(EngineUiInputType.PointerDown, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        SendPointer(EngineUiInputType.PointerUp, e.GetPosition(this));
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        EngineUiNavigation navigation = e.Key switch
        {
            Key.Tab when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) => EngineUiNavigation.Previous,
            Key.Tab => EngineUiNavigation.Next,
            Key.Left => EngineUiNavigation.Left,
            Key.Right => EngineUiNavigation.Right,
            Key.Up => EngineUiNavigation.Up,
            Key.Down => EngineUiNavigation.Down,
            Key.Enter or Key.Space => EngineUiNavigation.Accept,
            Key.Escape => EngineUiNavigation.Cancel,
            _ => EngineUiNavigation.None,
        };
        if (navigation == EngineUiNavigation.None)
        {
            base.OnKeyDown(e);
            return;
        }

        Process([new EngineUiInput
        {
            Type = EngineUiInputType.Navigation,
            Navigation = navigation,
            Sequence = ++inputSequence,
            Source = EngineInputDeviceKind.Keyboard,
            InsideViewport = 1,
        }]);
        e.Handled = true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DestroyNativeDocument();
        Loaded -= View_Loaded;
        Unloaded -= View_Unloaded;
        GC.SuppressFinalize(this);
    }

    private CharacterCreationChoice CurrentChoice => CharacterCreationChoices.All[choiceIndex];

    private static int FindChoiceIndex(Spelljammer.Simulation.Content.CharacterId characterId)
    {
        for (int index = 0; index < CharacterCreationChoices.All.Count; ++index)
        {
            if (CharacterCreationChoices.All[index].CharacterId == characterId)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(characterId));
    }

    private void CreateNativeDocument()
    {
        EngineUiDocumentDescription description = new()
        {
            RootKey = RootKey,
            LogicalWidth = (uint)LogicalWidth,
            LogicalHeight = (uint)LogicalHeight,
            MaximumElements = ElementCapacity,
            MaximumActions = ActionCapacity,
            Theme = new EngineUiTheme
            {
                Panel = Color(0.040f, 0.059f, 0.098f, 0.96f),
                Button = Color(0.105f, 0.145f, 0.216f, 0.96f),
                ButtonHovered = Color(0.180f, 0.259f, 0.353f, 0.98f),
                ButtonPressed = Color(0.067f, 0.098f, 0.157f),
                ButtonFocused = Color(0.180f, 0.306f, 0.384f),
                ButtonDisabled = Color(0.055f, 0.067f, 0.094f),
            },
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_CreateUIContext(
            in description, out context, out document), "create the character-creation UI document");

        List<nint> allocatedNames = [];
        try
        {
            EngineUiElementDescription[] elements = BuildElements(allocatedNames);
            EngineUiMutation[] mutations = elements.Select(static element => new EngineUiMutation
            {
                Type = EngineUiMutationType.Create,
                Element = element,
            }).ToArray();
            ThrowIfFailed(SpriteForgeNative.SpriteForge_UICommit(
                context, document, 1, mutations, (uint)mutations.Length, out EngineUiCommitReport report),
                "commit the character-creation UI document");
            if (report.Created != (uint)mutations.Length)
            {
                throw new InvalidOperationException(
                    "SpriteForge did not create the complete character-creation UI document.");
            }

            revision = report.Revision;
        }
        catch
        {
            DestroyNativeDocument();
            throw;
        }
        finally
        {
            foreach (nint name in allocatedNames)
            {
                Marshal.FreeCoTaskMem(name);
            }
        }
    }

    private EngineUiElementDescription[] BuildElements(List<nint> names)
    {
        CharacterCreationChoice choice = CurrentChoice;
        string captain = strings.Get($"creation.captain.{choice.TextId}.name");
        string race = strings.Get($"creation.race.{choice.TextId}.name");
        string heritage = strings.Get($"creation.heritage.{choice.TextId}.name");
        string characterBackground = strings.Get("creation.background.expedition-veteran.name");
        string summary = strings.Format(
            "creation.summary",
            LocalizationArgument.Text("race", race),
            LocalizationArgument.Text("heritage", heritage),
            LocalizationArgument.Text("background", characterBackground));

        List<EngineUiElementDescription> elements =
        [
            Element(ModalKey, RootKey, 0, 0, 1600, 900, EngineUiBehavior.None,
                strings.Get("creation.accessibility.screen"), names, modal: true, dismissAction: CancelAction,
                customColor: true, color: Color(0.018f, 0.027f, 0.047f, 0.88f)),
            Panel(HeaderPanelKey, 0, 0, 1600, 124, strings.Get("creation.accessibility.screen"), names,
                Color(0.025f, 0.039f, 0.065f, 0.96f)),
            Text(TitleKey, 54, 22, 920, 48, strings.Get("creation.title"), names),
            Text(IntroductionKey, 54, 70, 1040, 30, strings.Get("creation.introduction"), names),
            Text(SelectionCounterKey, 1350, 38, 194, 34,
                string.Format(strings.Culture, "{0:00} / {1:00}", choiceIndex + 1,
                    CharacterCreationChoices.All.Count), names),
            Panel(HeaderRuleKey, 54, 112, 1492, 2, strings.Get("creation.accessibility.screen"), names,
                Color(0.843f, 0.686f, 0.439f)),
            Panel(RosterPanelKey, 42, 142, 360, 650, strings.Get("creation.accessibility.screen"), names,
                Color(0.025f, 0.041f, 0.071f, 0.98f)),
            Text(RosterHeadingKey, 64, 158, 316, 34, strings.Get("creation.accessibility.screen"), names),
            Panel(PreviewPanelKey, 426, 142, 570, 650, captain, names,
                Color(0.021f, 0.035f, 0.062f, 0.97f)),
            Element(PortraitKey, ModalKey, 456, 172, 510, 452, EngineUiBehavior.None,
                captain, names, customColor: true, color: Color(0.035f, 0.059f, 0.102f, 0.96f)),
            Text(CaptainKey, 456, 638, 510, 54, captain, names),
            Panel(DetailPanelKey, 1020, 142, 538, 650, strings.Get("creation.accessibility.details"), names,
                Color(0.025f, 0.041f, 0.071f, 0.98f)),
            Text(DetailHeadingKey, 1052, 164, 474, 38,
                strings.Get("creation.accessibility.details"), names),
            Text(RaceLabelKey, 1052, 222, 474, 22, strings.Get("creation.label.race"), names),
            Text(RaceValueKey, 1052, 244, 474, 38, race, names),
            Text(HeritageLabelKey, 1052, 302, 474, 22, strings.Get("creation.label.heritage"), names),
            Text(HeritageValueKey, 1052, 324, 474, 38, heritage, names),
            Text(BackgroundLabelKey, 1052, 382, 474, 22,
                strings.Get("creation.label.background"), names),
            Text(BackgroundValueKey, 1052, 404, 474, 38, characterBackground, names),
            Text(SummaryKey, 1052, 466, 474, 82, summary, names),
            Text(SeedLabelKey, 1052, 568, 180, 24, strings.Get("creation.label.seed"), names),
            Text(SeedValueKey, 1052, 594, 474, 34,
                strings.Format("creation.value.seed", LocalizationArgument.Unsigned("seed", seed)), names),
            Button(RerollKey, 1052, 646, 210, 46, 20, strings.Get("creation.button.reroll"), names),
            Panel(FooterPanelKey, 0, 808, 1600, 92, strings.Get("creation.accessibility.screen"), names,
                Color(0.025f, 0.039f, 0.065f, 0.98f)),
            Button(BackKey, 42, 828, 180, 52, 21, strings.Get("creation.button.back"), names),
            Text(StatusKey, 250, 830, 900, 48, strings.Get("creation.status.ready"), names),
            Button(ConfirmKey, 1330, 828, 228, 52, 22, strings.Get("creation.button.confirm"), names),
        ];

        for (int index = 0; index < CharacterCreationChoices.All.Count; ++index)
        {
            string name = strings.Get($"creation.captain.{CharacterCreationChoices.All[index].TextId}.name");
            elements.Add(Button(CaptainChoiceKeys[index], 64, 202 + index * 49, 316, 42,
                index, name, names, selected: index == choiceIndex));
        }

        return [.. elements];
    }

    private static EngineUiElementDescription Text(
        ulong key, float x, float y, float width, float height, string name, List<nint> names) =>
        Element(key, ModalKey, x, y, width, height, EngineUiBehavior.None, name, names,
            kind: EngineUiElementKind.Text, customColor: true, color: Color(0, 0, 0, 0));

    private static EngineUiElementDescription Panel(
        ulong key, float x, float y, float width, float height, string name, List<nint> names,
        EngineUiColor color) =>
        Element(key, ModalKey, x, y, width, height, EngineUiBehavior.None, name, names,
            customColor: true, color: color);

    private static EngineUiElementDescription Button(
        ulong key, float x, float y, float width, float height, int tabOrder, string name,
        List<nint> names, bool selected = false) =>
        Element(key, ModalKey, x, y, width, height, EngineUiBehavior.Button, name, names,
            tabOrder: tabOrder, selected: selected);

    private static EngineUiElementDescription Element(
        ulong key,
        ulong parent,
        float x,
        float y,
        float width,
        float height,
        EngineUiBehavior behavior,
        string accessibleName,
        List<nint> allocatedNames,
        int tabOrder = int.MaxValue,
        bool modal = false,
        ulong dismissAction = 0,
        EngineUiElementKind kind = EngineUiElementKind.Container,
        bool customColor = false,
        EngineUiColor color = default,
        bool selected = false)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(accessibleName);
        nint name = Marshal.StringToCoTaskMemUTF8(accessibleName);
        allocatedNames.Add(name);
        bool interactive = behavior != EngineUiBehavior.None;
        return new EngineUiElementDescription
        {
            Key = key,
            ParentKey = parent,
            Action = interactive ? key : 0,
            DismissAction = dismissAction,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            SliderMinimum = 0,
            SliderMaximum = 1,
            SliderValue = 0,
            SliderStep = 0.1f,
            TabOrder = tabOrder,
            Kind = kind,
            Behavior = behavior,
            AccessibilityRole = AccessibilityRole(kind, behavior),
            ChildLayout = EngineUiLayoutMode.Absolute,
            WidthKind = EngineUiSizeKind.Fixed,
            HeightKind = EngineUiSizeKind.Fixed,
            Visible = 1,
            Enabled = 1,
            HitTestable = interactive ? 1u : 0u,
            Modal = modal ? 1u : 0u,
            Focusable = interactive ? 1u : 0u,
            Selected = selected ? 1u : 0u,
            CustomColor = customColor ? 1u : 0u,
            TextMaximumBytes = NonEditableTextCapacity,
            Color = color,
            AccessibleNameUtf8 = name,
            AccessibleNameBytes = (uint)encoded.Length,
        };
    }

    private void SendPointer(EngineUiInputType type, Point physical)
    {
        if (context == nint.Zero || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Process([new EngineUiInput
        {
            Type = type,
            X = (float)(physical.X * LogicalWidth / ActualWidth),
            Y = (float)(physical.Y * LogicalHeight / ActualHeight),
            Sequence = ++inputSequence,
            PointerId = 1,
            Source = EngineInputDeviceKind.Mouse,
            Button = EngineMouseButton.Left,
            InsideViewport = physical.X >= 0 && physical.Y >= 0 &&
                physical.X < ActualWidth && physical.Y < ActualHeight ? 1u : 0u,
        }]);
    }

    private void Process(EngineUiInput[] input)
    {
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIProcessInput(
            context, document, input, (uint)input.Length), "process character-creation input");
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIConsumeActions(
            context, document, actions, (uint)actions.Length, null, 0,
            out uint requiredActions, out uint actionCount,
            out uint requiredUtf8Bytes, out uint writtenUtf8Bytes),
            "consume character-creation actions");
        RequireCompleteCopy(requiredActions, actionCount, "character-creation actions");
        RequireCompleteCopy(requiredUtf8Bytes, writtenUtf8Bytes, "character-creation action text");
        for (int index = 0; index < actionCount; ++index)
        {
            EngineUiAction action = actions[index];
            int selectedIndex = Array.IndexOf(CaptainChoiceKeys, action.Source);
            if (selectedIndex >= 0)
            {
                choiceIndex = selectedIndex;
                Recreate(action.Source);
            }
            else if (action.Source == RerollKey)
            {
                seed = NewSeed();
                Recreate(RerollKey);
            }
            else if (action.Source == ConfirmKey)
            {
                Completed?.Invoke(this, new CharacterCreationCompletedEventArgs(
                    new CharacterCreationSelection(CurrentChoice, seed)));
            }
            else if (action.Source == BackKey || action.Type == CancelAction)
            {
                CancelRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        InvalidateVisual();
    }

    private void Recreate(ulong focusKey)
    {
        DestroyNativeDocument();
        CreateNativeDocument();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UISetFocus(
            context, document, revision, focusKey, out EngineUiFocusResult focus),
            "restore character-creation focus");
        if (focus.FocusedKey != focusKey)
        {
            throw new InvalidOperationException("SpriteForge did not restore character-creation focus.");
        }

        InvalidateVisual();
    }

    private void RefreshSnapshots()
    {
        EngineUiElementSnapshot[] values = new EngineUiElementSnapshot[elementKeys.Length];
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIGetElementSnapshots(
            context, document, elementKeys, (uint)elementKeys.Length, values, (uint)values.Length,
            out uint required, out uint count),
            "copy character-creation element snapshots");
        RequireCompleteCopy(required, count, "character-creation element snapshots");
        snapshots.Clear();
        for (int index = 0; index < count; ++index)
        {
            snapshots.Add(values[index].Key, values[index]);
        }
    }

    private void DrawBackdrop(DrawingContext drawingContext)
    {
        double imageScale = Math.Max(ActualWidth / background.PixelWidth, ActualHeight / background.PixelHeight);
        double imageWidth = background.PixelWidth * imageScale;
        double imageHeight = background.PixelHeight * imageScale;
        drawingContext.DrawImage(background, new Rect(
            (ActualWidth - imageWidth) / 2,
            (ActualHeight - imageHeight) / 2,
            imageWidth,
            imageHeight));
        drawingContext.DrawRectangle(Brush("#D90A0E19"), null, new Rect(RenderSize));
    }

    private void DrawRosterSelection(DrawingContext drawingContext)
    {
        if (!snapshots.TryGetValue(CaptainChoiceKeys[choiceIndex], out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        drawingContext.DrawRectangle(Brush("#66456274"), new Pen(Brush("#D7AF70"), 1), bounds);
        drawingContext.DrawRectangle(Brush("#D7AF70"), null,
            new Rect(bounds.X, bounds.Y, Math.Max(4, 5 * ActualWidth / LogicalWidth), bounds.Height));
    }

    private void DrawPortrait(DrawingContext drawingContext, string monogram)
    {
        if (!snapshots.TryGetValue(PortraitKey, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        Point center = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height * 0.48);
        double radius = Math.Min(bounds.Width, bounds.Height) * 0.31;
        RadialGradientBrush aura = new(
            System.Windows.Media.Color.FromArgb(180, 55, 105, 130),
            System.Windows.Media.Color.FromArgb(0, 9, 16, 29));
        aura.Freeze();
        drawingContext.DrawEllipse(aura, new Pen(Brush("#48677B"), 2), center, radius * 1.28, radius * 1.28);
        drawingContext.DrawEllipse(Brush("#152A43"), new Pen(Brush("#D7AF70"), 3), center, radius, radius);
        drawingContext.DrawEllipse(null, new Pen(Brush("#80DED9"), 1), center, radius * 0.83, radius * 0.83);

        FormattedText formatted = new(
            monogram,
            strings.Culture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Light"),
            126 * ActualHeight / LogicalHeight,
            Brush("#80DED9"),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(formatted, new Point(
            center.X - formatted.Width / 2,
            center.Y - formatted.Height / 2));
    }

    private void DrawText(
        DrawingContext drawingContext,
        ulong key,
        string text,
        double fontSize,
        string color,
        TextAlignment alignment,
        FontWeight weight,
        double horizontalInset = 0)
    {
        if (!snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        double inset = horizontalInset * ActualWidth / LogicalWidth;
        FormattedText formatted = new(
            text,
            strings.Culture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize * ActualHeight / LogicalHeight,
            Brush(color),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, bounds.Width - inset * 2),
            MaxTextHeight = Math.Max(1, bounds.Height),
            TextAlignment = alignment,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        double y = bounds.Y + Math.Max(0, (bounds.Height - formatted.Height) / 2);
        drawingContext.DrawText(formatted, new Point(bounds.X + inset, y));
    }

    private Rect Scale(float x, float y, float width, float height) => new(
        x * ActualWidth / LogicalWidth,
        y * ActualHeight / LogicalHeight,
        width * ActualWidth / LogicalWidth,
        height * ActualHeight / LogicalHeight);

    private Rect ScaleAndClip(EngineUiPresentationCommand command)
    {
        Rect logical = new(command.X, command.Y, command.Width, command.Height);
        if (command.Flags.HasFlag(EngineUiPresentationFlags.Clipped))
        {
            logical.Intersect(new Rect(command.ClipX, command.ClipY, command.ClipWidth, command.ClipHeight));
        }

        return logical.IsEmpty ? new Rect() : Scale(
            (float)logical.X, (float)logical.Y, (float)logical.Width, (float)logical.Height);
    }

    private static BitmapSource LoadBackground()
    {
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri("pack://application:,,,/Assets/UI/MainMenu/Background.png", UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return value == 0 ? 1 : value;
    }

    private static EngineUiColor Color(float red, float green, float blue, float alpha = 1) =>
        new() { Red = red, Green = green, Blue = blue, Alpha = alpha };

    private static SolidColorBrush ToBrush(EngineUiColor color)
    {
        Color value = System.Windows.Media.Color.FromArgb(
            Channel(color.Alpha), Channel(color.Red), Channel(color.Green), Channel(color.Blue));
        SolidColorBrush brush = new(value);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Brush(string value)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static byte Channel(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue);

    private static EngineUiAccessibilityRole AccessibilityRole(
        EngineUiElementKind kind, EngineUiBehavior behavior) => behavior switch
        {
            EngineUiBehavior.Button => EngineUiAccessibilityRole.Button,
            EngineUiBehavior.Toggle => EngineUiAccessibilityRole.Toggle,
            EngineUiBehavior.Slider => EngineUiAccessibilityRole.Slider,
            EngineUiBehavior.Scroll => EngineUiAccessibilityRole.ScrollArea,
            EngineUiBehavior.Selection => EngineUiAccessibilityRole.ListItem,
            EngineUiBehavior.TextEdit => EngineUiAccessibilityRole.TextField,
            _ when kind == EngineUiElementKind.Text => EngineUiAccessibilityRole.Text,
            _ when kind == EngineUiElementKind.Image => EngineUiAccessibilityRole.Image,
            _ => EngineUiAccessibilityRole.Panel,
        };

    private static void RequireCompleteCopy(uint required, uint written, string operation)
    {
        if (required != written)
        {
            throw new InvalidOperationException(
                $"SpriteForge returned {written} of {required} records while copying the {operation}.");
        }
    }

    private static ulong StableKey(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte item in Encoding.UTF8.GetBytes(value))
        {
            hash ^= item;
            hash *= prime;
        }

        return hash == 0 ? 1 : hash;
    }

    private void View_Loaded(object sender, RoutedEventArgs e) => Focus();

    private void View_Unloaded(object sender, RoutedEventArgs e) => Dispose();

    private void DestroyNativeDocument()
    {
        if (context == nint.Zero)
        {
            return;
        }

        SpriteForgeNative.SpriteForge_DestroyUIContext(context);
        context = nint.Zero;
        document = 0;
        revision = 0;
        snapshots.Clear();
    }

    private static void ThrowIfFailed(EngineStatus status, string operation)
    {
        if (status != EngineStatus.Success)
        {
            throw new InvalidOperationException(
                $"SpriteForge.dll could not {operation} ({status}, {(int)status}).");
        }
    }
}
