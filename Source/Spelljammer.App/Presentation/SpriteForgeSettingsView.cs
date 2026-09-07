using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Spelljammer.Interop;
using Spelljammer.Settings;

namespace Spelljammer.Presentation;

internal sealed class GameSettingsApplyRequestedEventArgs(GameSettingsProfile profile) : EventArgs
{
    internal GameSettingsProfile Profile { get; } = profile;
}

internal sealed class SpriteForgeSettingsView : FrameworkElement, IDisposable
{
    internal const double LogicalWidth = 900;
    internal const double LogicalHeight = 650;
    private const uint ElementCapacity = 48;
    private const uint ActionCapacity = 32;
    private const uint NonEditableTextCapacity = 1;

    private static readonly ulong RootKey = Key("spelljammer.settings.root");
    private static readonly ulong ModalKey = Key("spelljammer.settings.modal");
    private static readonly ulong TitleKey = Key("spelljammer.settings.title");
    private static readonly ulong IntroductionKey = Key("spelljammer.settings.introduction");
    private static readonly ulong SidebarKey = Key("spelljammer.settings.sidebar");
    private static readonly ulong ContentKey = Key("spelljammer.settings.content");
    private static readonly ulong GeneralCategoryKey = Key("spelljammer.settings.category.general");
    private static readonly ulong AudioCategoryKey = Key("spelljammer.settings.category.audio");
    private static readonly ulong InterfaceCategoryKey = Key("spelljammer.settings.category.interface");
    private static readonly ulong PageHeadingKey = Key("spelljammer.settings.page-heading");
    private static readonly ulong LanguageLabelKey = Key("spelljammer.settings.language-label");
    private static readonly ulong LanguageButtonKey = Key("spelljammer.settings.language-button");
    private static readonly ulong ResolutionLabelKey = Key("spelljammer.settings.resolution-label");
    private static readonly ulong ResolutionButtonKey = Key("spelljammer.settings.resolution-button");
    private static readonly ulong MasterLabelKey = Key("spelljammer.settings.master-label");
    private static readonly ulong MasterSliderKey = Key("spelljammer.settings.master-slider");
    private static readonly ulong MasterValueKey = Key("spelljammer.settings.master-value");
    private static readonly ulong MusicLabelKey = Key("spelljammer.settings.music-label");
    private static readonly ulong MusicSliderKey = Key("spelljammer.settings.music-slider");
    private static readonly ulong MusicValueKey = Key("spelljammer.settings.music-value");
    private static readonly ulong EffectsLabelKey = Key("spelljammer.settings.effects-label");
    private static readonly ulong EffectsSliderKey = Key("spelljammer.settings.effects-slider");
    private static readonly ulong EffectsValueKey = Key("spelljammer.settings.effects-value");
    private static readonly ulong SubtitlesLabelKey = Key("spelljammer.settings.subtitles-label");
    private static readonly ulong SubtitlesToggleKey = Key("spelljammer.settings.subtitles-toggle");
    private static readonly ulong MotionLabelKey = Key("spelljammer.settings.motion-label");
    private static readonly ulong MotionToggleKey = Key("spelljammer.settings.motion-toggle");
    private static readonly ulong ShakeLabelKey = Key("spelljammer.settings.shake-label");
    private static readonly ulong ShakeToggleKey = Key("spelljammer.settings.shake-toggle");
    private static readonly ulong ScaleLabelKey = Key("spelljammer.settings.scale-label");
    private static readonly ulong ScaleSliderKey = Key("spelljammer.settings.scale-slider");
    private static readonly ulong ScaleValueKey = Key("spelljammer.settings.scale-value");
    private static readonly ulong StatusKey = Key("spelljammer.settings.status");
    private static readonly ulong ResetButtonKey = Key("spelljammer.settings.reset");
    private static readonly ulong CancelButtonKey = Key("spelljammer.settings.cancel");
    private static readonly ulong ApplyButtonKey = Key("spelljammer.settings.apply");
    private static readonly ulong PopupPanelKey = Key("spelljammer.settings.popup.panel");
    private static readonly ulong PopupTitleKey = Key("spelljammer.settings.popup.title");
    private static readonly ulong CancelAction = Key("spelljammer.settings.action.cancel");
    private static readonly ulong PopupCancelAction = Key("spelljammer.settings.action.popup-cancel");
    private static readonly ulong[] LanguageChoiceKeys =
    [
        Key("spelljammer.settings.language.en-us"),
        Key("spelljammer.settings.language.fr-fr"),
        Key("spelljammer.settings.language.zh-hant-tw"),
    ];
    private static readonly ulong[] ResolutionChoiceKeys =
    [
        Key("spelljammer.settings.resolution.desktop"),
        Key("spelljammer.settings.resolution.1280x720"),
        Key("spelljammer.settings.resolution.1600x900"),
        Key("spelljammer.settings.resolution.1920x1080"),
        Key("spelljammer.settings.resolution.2560x1440"),
    ];

    private readonly Dictionary<ulong, EngineUiElementSnapshot> snapshots = [];
    private readonly EngineUiPresentationCommand[] presentation = new EngineUiPresentationCommand[ElementCapacity];
    private readonly EngineUiAction[] actions = new EngineUiAction[ActionCapacity];
    private readonly GameText strings;
    private ulong[] elementKeys = [];
    private nint context;
    private ulong document;
    private ulong revision;
    private ulong inputSequence;
    private GameSettingsProfile draft;
    private SettingsCategory category;
    private ChoicePopup popup;
    private string status;
    private bool statusIsError;
    private bool disposed;

    internal SpriteForgeSettingsView(GameSettingsProfile initial, GameText strings)
    {
        draft = initial;
        this.strings = strings;
        status = strings.Get("settings.status.ready");
        Focusable = true;
        SnapsToDevicePixels = true;
        CreateNativeDocument();
        Loaded += View_Loaded;
        Unloaded += View_Unloaded;
    }

    internal event EventHandler<GameSettingsApplyRequestedEventArgs>? ApplyRequested;
    internal event EventHandler? CancelRequested;

    protected override Size MeasureOverride(Size availableSize) => new(LogicalWidth, LogicalHeight);
    protected override Size ArrangeOverride(Size finalSize) => finalSize;
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Brush("#080D17"), null, new Rect(RenderSize));
        if (context == nint.Zero)
        {
            return;
        }

        RefreshSnapshots();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIBuildPresentation(
            context, document, presentation, (uint)presentation.Length,
            out uint requiredCommands, out uint commandCount, out _),
            "build the settings presentation");
        RequireCompleteCopy(requiredCommands, commandCount, "settings presentation");
        for (int index = 0; index < commandCount; ++index)
        {
            EngineUiPresentationCommand command = presentation[index];
            if (command.Type != EngineUiPresentationType.SolidQuad || IsPopupElement(command.Source))
            {
                continue;
            }

            drawingContext.DrawRectangle(
                ToBrush(command.Color), null, ScaleAndClip(command));
        }

        DrawCategorySelection(drawingContext);
        DrawPageControls(drawingContext);
        strings.BeginFrame();
        DrawText(drawingContext, TitleKey, strings.Get("settings.title"), 29, "#F2E9D8", false);
        DrawText(drawingContext, IntroductionKey, strings.Get("settings.introduction"), 13, "#93A1BE", false);
        DrawText(drawingContext, GeneralCategoryKey, strings.Get("settings.heading.display"), 15, "#F2E9D8", false, 16);
        DrawText(drawingContext, AudioCategoryKey, strings.Get("settings.heading.audio"), 15, "#F2E9D8", false, 16);
        DrawText(drawingContext, InterfaceCategoryKey, strings.Get("settings.heading.accessibility"), 15, "#F2E9D8", false, 16);
        DrawText(drawingContext, PageHeadingKey, PageHeading(), 19, "#D7AF70", false);
        DrawPageText(drawingContext);
        DrawText(drawingContext, StatusKey, status, 12, statusIsError ? "#F39A8D" : "#93A1BE", false);
        DrawText(drawingContext, ResetButtonKey, strings.Get("settings.button.reset"), 13, "#F2E9D8", true);
        DrawText(drawingContext, CancelButtonKey, strings.Get("settings.button.cancel"), 13, "#F2E9D8", true);
        DrawText(drawingContext, ApplyButtonKey, strings.Get("settings.button.apply"), 13, "#F2E9D8", true);

        if (popup != ChoicePopup.None)
        {
            for (int index = 0; index < commandCount; ++index)
            {
                EngineUiPresentationCommand command = presentation[index];
                if (command.Type != EngineUiPresentationType.SolidQuad || !IsPopupElement(command.Source))
                {
                    continue;
                }

                drawingContext.DrawRectangle(
                    ToBrush(command.Color), null, ScaleAndClip(command));
            }

            DrawPopup(drawingContext);
        }

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
            System.Windows.Input.Key.Tab when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) => EngineUiNavigation.Previous,
            System.Windows.Input.Key.Tab => EngineUiNavigation.Next,
            System.Windows.Input.Key.Left => EngineUiNavigation.Left,
            System.Windows.Input.Key.Right => EngineUiNavigation.Right,
            System.Windows.Input.Key.Up => EngineUiNavigation.Up,
            System.Windows.Input.Key.Down => EngineUiNavigation.Down,
            System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space => EngineUiNavigation.Accept,
            System.Windows.Input.Key.Escape => EngineUiNavigation.Cancel,
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
            InsideViewport = 1,
        }]);
        e.Handled = true;
    }

    internal void SetApplyFailure(GameSettingsDiagnostic diagnostic)
    {
        status = strings.Diagnostic("settings.status.save-failed", GameSettingsDiagnostics.Stable(diagnostic));
        statusIsError = true;
        IsEnabled = true;
        Focus();
        InvalidateVisual();
    }

    internal void SetBusy()
    {
        popup = ChoicePopup.None;
        status = strings.Get("settings.status.saving");
        statusIsError = false;
        IsEnabled = false;
        InvalidateVisual();
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
                Panel = Color(0.055f, 0.078f, 0.125f),
                Button = Color(0.145f, 0.204f, 0.302f),
                ButtonHovered = Color(0.220f, 0.310f, 0.435f),
                ButtonPressed = Color(0.082f, 0.122f, 0.188f),
                ButtonFocused = Color(0.220f, 0.345f, 0.435f),
                ButtonDisabled = Color(0.070f, 0.082f, 0.110f),
            },
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_CreateUIContext(
            in description, out context, out document), "create the settings UI document");

        List<nint> allocatedNames = [];
        try
        {
            EngineUiElementDescription[] elements = BuildElements(allocatedNames);
            elementKeys = [.. elements.Select(value => value.Key)];
            EngineUiMutation[] mutations = elements.Select(static element => new EngineUiMutation
            {
                Type = EngineUiMutationType.Create,
                Element = element,
            }).ToArray();
            ThrowIfFailed(SpriteForgeNative.SpriteForge_UICommit(
                context, document, 1, mutations, (uint)mutations.Length, out EngineUiCommitReport report),
                "commit the settings UI document");
            if (report.Created != (uint)mutations.Length)
            {
                throw new InvalidOperationException("SpriteForge did not create the complete settings UI document.");
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
        List<EngineUiElementDescription> elements =
        [
            Element(ModalKey, RootKey, 15, 15, 870, 620, EngineUiBehavior.None,
                strings.Get("settings.accessibility.dialog"), names, modal: true, dismissAction: CancelAction),
            TextElement(TitleKey, 45, 32, 810, 42, strings.Get("settings.title"), names),
            TextElement(IntroductionKey, 45, 74, 810, 26, strings.Get("settings.accessibility.description"), names),
            Panel(SidebarKey, 40, 112, 220, 382, strings.Get("settings.accessibility.dialog"), names),
            Panel(ContentKey, 275, 112, 580, 382, strings.Get("settings.accessibility.dialog"), names),
            Button(GeneralCategoryKey, 50, 126, 200, 56, 0, strings.Get("settings.heading.display"), names),
            Button(AudioCategoryKey, 50, 196, 200, 56, 1, strings.Get("settings.heading.audio"), names),
            Button(InterfaceCategoryKey, 50, 266, 200, 56, 2, strings.Get("settings.heading.accessibility"), names),
            TextElement(PageHeadingKey, 305, 132, 520, 34, PageHeading(), names),
            TextElement(StatusKey, 45, 512, 810, 28, strings.Get("settings.accessibility.status"), names),
            Button(ResetButtonKey, 360, 562, 140, 48, 90, strings.Get("settings.button.reset"), names),
            Button(CancelButtonKey, 515, 562, 140, 48, 91, strings.Get("settings.button.cancel"), names),
            Button(ApplyButtonKey, 670, 562, 155, 48, 92, strings.Get("settings.button.apply"), names),
        ];

        switch (category)
        {
            case SettingsCategory.General:
                AddGeneralElements(elements, names);
                break;
            case SettingsCategory.Audio:
                AddAudioElements(elements, names);
                break;
            case SettingsCategory.Interface:
                AddInterfaceElements(elements, names);
                break;
            default:
                throw new InvalidOperationException("The active settings category is invalid.");
        }

        if (popup != ChoicePopup.None)
        {
            AddPopupElements(elements, names);
        }

        return [.. elements];
    }

    private void AddGeneralElements(List<EngineUiElementDescription> elements, List<nint> names)
    {
        elements.Add(TextElement(LanguageLabelKey, 310, 194, 260, 40,
            strings.Get("settings.label.language"), names));
        elements.Add(Button(LanguageButtonKey, 602, 190, 220, 44, 10,
            AccessibleOption("settings.label.language", strings.LanguageName(draft.Language)), names));
        elements.Add(TextElement(ResolutionLabelKey, 310, 258, 260, 40,
            strings.Get("settings.label.resolution"), names));
        elements.Add(Button(ResolutionButtonKey, 602, 254, 220, 44, 11,
            AccessibleOption("settings.label.resolution", strings.ResolutionName(CurrentResolution())), names));
    }

    private void AddAudioElements(List<EngineUiElementDescription> elements, List<nint> names)
    {
        AddSliderRow(elements, names, MasterLabelKey, MasterSliderKey, MasterValueKey, 190,
            draft.MasterVolume, "settings.label.master-volume", 10);
        AddSliderRow(elements, names, MusicLabelKey, MusicSliderKey, MusicValueKey, 262,
            draft.MusicVolume, "settings.label.music-volume", 11);
        AddSliderRow(elements, names, EffectsLabelKey, EffectsSliderKey, EffectsValueKey, 334,
            draft.EffectsVolume, "settings.label.effects-volume", 12);
    }

    private void AddSliderRow(
        List<EngineUiElementDescription> elements,
        List<nint> names,
        ulong labelKey,
        ulong sliderKey,
        ulong valueKey,
        float y,
        int value,
        string textKey,
        int tabOrder)
    {
        elements.Add(TextElement(labelKey, 310, y, 210, 34, strings.Get(textKey), names));
        elements.Add(Slider(sliderKey, 520, y + 4, 225, value, 0, 100, 5, tabOrder,
            strings.Get(textKey), names));
        elements.Add(TextElement(valueKey, 758, y, 64, 34, AccessibleValue(textKey), names));
    }

    private void AddInterfaceElements(List<EngineUiElementDescription> elements, List<nint> names)
    {
        AddToggleRow(elements, names, SubtitlesLabelKey, SubtitlesToggleKey, 184,
            draft.Subtitles, "settings.label.subtitles", 10);
        AddToggleRow(elements, names, MotionLabelKey, MotionToggleKey, 244,
            draft.ReducedMotion, "settings.label.reduced-motion", 11);
        AddToggleRow(elements, names, ShakeLabelKey, ShakeToggleKey, 304,
            draft.ScreenShake, "settings.label.screen-shake", 12);
        elements.Add(TextElement(ScaleLabelKey, 310, 368, 210, 34,
            strings.Get("settings.label.interface-scale"), names));
        elements.Add(Slider(ScaleSliderKey, 520, 372, 225, draft.UiScalePercent, 75, 150, 5, 13,
            strings.Get("settings.label.interface-scale"), names));
        elements.Add(TextElement(ScaleValueKey, 758, 368, 64, 34,
            AccessibleValue("settings.label.interface-scale"), names));
    }

    private void AddToggleRow(
        List<EngineUiElementDescription> elements,
        List<nint> names,
        ulong labelKey,
        ulong toggleKey,
        float y,
        bool value,
        string textKey,
        int tabOrder)
    {
        elements.Add(TextElement(labelKey, 310, y, 280, 40, strings.Get(textKey), names));
        elements.Add(Toggle(toggleKey, 682, y - 2, value, tabOrder, strings.Get(textKey), names));
    }

    private void AddPopupElements(List<EngineUiElementDescription> elements, List<nint> names)
    {
        if (popup == ChoicePopup.Language)
        {
            elements.Add(Element(PopupPanelKey, RootKey, 0, 0, 282, 190, EngineUiBehavior.None,
                strings.Get("settings.label.language"), names, modal: true, dismissAction: PopupCancelAction,
                customColor: true, color: Color(0.082f, 0.110f, 0.165f),
                popupAnchor: LanguageButtonKey, popupEnabled: true));
            elements.Add(TextElement(PopupTitleKey, PopupPanelKey, 20, 14, 242, 30,
                strings.Get("settings.label.language"), names));
            for (int index = 0; index < GameSettingsChoices.Languages.Count; ++index)
            {
                string language = GameSettingsChoices.Languages[index];
                elements.Add(Button(LanguageChoiceKeys[index], PopupPanelKey, 20, 50 + index * 42, 242, 36,
                    index, strings.LanguageName(language), names,
                    selected: string.Equals(language, draft.Language, StringComparison.Ordinal)));
            }
        }
        else
        {
            elements.Add(Element(PopupPanelKey, RootKey, 0, 0, 310, 292, EngineUiBehavior.None,
                strings.Get("settings.label.resolution"), names, modal: true, dismissAction: PopupCancelAction,
                customColor: true, color: Color(0.082f, 0.110f, 0.165f),
                popupAnchor: ResolutionButtonKey, popupEnabled: true));
            elements.Add(TextElement(PopupTitleKey, PopupPanelKey, 20, 14, 270, 30,
                strings.Get("settings.label.resolution"), names));
            for (int index = 0; index < GameSettingsChoices.Resolutions.Count; ++index)
            {
                GameResolutionChoice resolution = GameSettingsChoices.Resolutions[index];
                elements.Add(Button(ResolutionChoiceKeys[index], PopupPanelKey, 20, 50 + index * 44, 270, 38,
                    index, strings.ResolutionName(resolution), names,
                    selected: string.Equals(resolution.Id, draft.Resolution, StringComparison.Ordinal)));
            }
        }
    }

    private string AccessibleValue(string settingKey) => strings.Format(
        "settings.accessibility.value",
        Spelljammer.Localization.LocalizationArgument.Text("setting", strings.Get(settingKey)));

    private string AccessibleOption(string settingKey, string value) =>
        strings.AccessibleOption(strings.Get(settingKey), value);

    private static EngineUiElementDescription TextElement(
        ulong key, float x, float y, float width, float height, string name, List<nint> names) =>
        TextElement(key, ModalKey, x, y, width, height, name, names);

    private static EngineUiElementDescription TextElement(
        ulong key, ulong parent, float x, float y, float width, float height, string name, List<nint> names) =>
        Element(key, parent, x, y, width, height, EngineUiBehavior.None, name, names,
            kind: EngineUiElementKind.Text, customColor: true, color: Color(0, 0, 0, 0));

    private static EngineUiElementDescription Panel(
        ulong key, float x, float y, float width, float height, string name, List<nint> names,
        EngineUiColor? color = null) =>
        Element(key, ModalKey, x, y, width, height, EngineUiBehavior.None, name, names,
            customColor: true, color: color ?? Color(0.040f, 0.059f, 0.098f));

    private static EngineUiElementDescription Button(
        ulong key, float x, float y, float width, float height, int tabOrder, string name, List<nint> names) =>
        Button(key, ModalKey, x, y, width, height, tabOrder, name, names);

    private static EngineUiElementDescription Button(
        ulong key, ulong parent, float x, float y, float width, float height, int tabOrder,
        string name, List<nint> names, bool selected = false) =>
        Element(key, parent, x, y, width, height, EngineUiBehavior.Button, name, names,
            tabOrder: tabOrder, selected: selected);

    private static EngineUiElementDescription Toggle(
        ulong key, float x, float y, bool value, int tabOrder, string name, List<nint> names) =>
        Element(key, ModalKey, x, y, 140, 44, EngineUiBehavior.Toggle, name, names,
            tabOrder: tabOrder, toggle: value);

    private static EngineUiElementDescription Slider(
        ulong key, float x, float y, float width, float value, float minimum, float maximum, float step,
        int tabOrder, string name, List<nint> names) =>
        Element(key, ModalKey, x, y, width, 28, EngineUiBehavior.Slider, name, names,
            tabOrder: tabOrder, sliderMinimum: minimum, sliderMaximum: maximum,
            sliderValue: value, sliderStep: step);

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
        bool toggle = false,
        float sliderMinimum = 0,
        float sliderMaximum = 1,
        float sliderValue = 0,
        float sliderStep = 0.1f,
        bool modal = false,
        ulong dismissAction = 0,
        EngineUiElementKind kind = EngineUiElementKind.Container,
        bool customColor = false,
        EngineUiColor color = default,
        bool selected = false,
        ulong popupAnchor = 0,
        bool popupEnabled = false)
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
            SliderMinimum = sliderMinimum,
            SliderMaximum = sliderMaximum,
            SliderValue = sliderValue,
            SliderStep = sliderStep,
            TabOrder = tabOrder,
            Kind = kind,
            Behavior = behavior,
            AccessibilityRole = AccessibilityRole(kind, behavior),
            ChildLayout = EngineUiLayoutMode.Absolute,
            WidthKind = EngineUiSizeKind.Fixed,
            HeightKind = EngineUiSizeKind.Fixed,
            ToggleValue = toggle ? 1u : 0u,
            Visible = 1,
            Enabled = 1,
            HitTestable = interactive ? 1u : 0u,
            Modal = modal ? 1u : 0u,
            Focusable = interactive ? 1u : 0u,
            Selected = selected ? 1u : 0u,
            CustomColor = customColor ? 1u : 0u,
            PopupAnchor = popupAnchor,
            PopupGap = 6,
            PopupSafeLeft = 24,
            PopupSafeTop = 24,
            PopupSafeRight = 24,
            PopupSafeBottom = 24,
            PopupEdge = EngineUiPopupEdge.Below,
            PopupAlignment = EngineUiPopupAlignment.End,
            PopupEnabled = popupEnabled ? 1u : 0u,
            PopupAllowFlip = popupEnabled ? 1u : 0u,
            PopupAllowClamp = popupEnabled ? 1u : 0u,
            PopupScrollFallback = popupEnabled ? 1u : 0u,
            PopupDismissOnOutsidePress = popupEnabled ? 1u : 0u,
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
            context, document, input, (uint)input.Length), "process settings input");
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIConsumeActions(
            context, document, actions, (uint)actions.Length, null, 0,
            out uint requiredActions, out uint actionCount,
            out uint requiredUtf8Bytes, out uint writtenUtf8Bytes), "consume settings actions");
        RequireCompleteCopy(requiredActions, actionCount, "settings actions");
        RequireCompleteCopy(requiredUtf8Bytes, writtenUtf8Bytes, "settings action text");
        for (int index = 0; index < actionCount; ++index)
        {
            HandleAction(actions[index]);
        }

        InvalidateVisual();
    }

    private void HandleAction(EngineUiAction action)
    {
        if (popup != ChoicePopup.None)
        {
            HandlePopupAction(action);
            return;
        }

        if (action.Source == GeneralCategoryKey)
        {
            SelectCategory(SettingsCategory.General);
        }
        else if (action.Source == AudioCategoryKey)
        {
            SelectCategory(SettingsCategory.Audio);
        }
        else if (action.Source == InterfaceCategoryKey)
        {
            SelectCategory(SettingsCategory.Interface);
        }
        else if (action.Source == LanguageButtonKey)
        {
            OpenPopup(ChoicePopup.Language);
        }
        else if (action.Source == ResolutionButtonKey)
        {
            OpenPopup(ChoicePopup.Resolution);
        }
        else if (action.Source == MasterSliderKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Scalar);
            draft = draft with { MasterVolume = (int)MathF.Round(action.ScalarValue) };
        }
        else if (action.Source == MusicSliderKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Scalar);
            draft = draft with { MusicVolume = (int)MathF.Round(action.ScalarValue) };
        }
        else if (action.Source == EffectsSliderKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Scalar);
            draft = draft with { EffectsVolume = (int)MathF.Round(action.ScalarValue) };
        }
        else if (action.Source == ScaleSliderKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Scalar);
            draft = draft with { UiScalePercent = (int)MathF.Round(action.ScalarValue) };
        }
        else if (action.Source == SubtitlesToggleKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Boolean);
            draft = draft with { Subtitles = action.BooleanValue != 0 };
        }
        else if (action.Source == MotionToggleKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Boolean);
            draft = draft with { ReducedMotion = action.BooleanValue != 0 };
        }
        else if (action.Source == ShakeToggleKey)
        {
            RequireActionValue(action, EngineUiActionValueType.Boolean);
            draft = draft with { ScreenShake = action.BooleanValue != 0 };
        }
        else if (action.Source == ResetButtonKey)
        {
            draft = GameSettingsProfile.Default;
            status = strings.Get("settings.status.reset");
            statusIsError = false;
            Recreate(ResetButtonKey);
        }
        else if (action.Source == ApplyButtonKey)
        {
            ApplyRequested?.Invoke(this, new GameSettingsApplyRequestedEventArgs(draft));
        }
        else if (action.Source == CancelButtonKey || action.Type == CancelAction)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandlePopupAction(EngineUiAction action)
    {
        ulong[] keys = popup == ChoicePopup.Language ? LanguageChoiceKeys : ResolutionChoiceKeys;
        int selected = Array.IndexOf(keys, action.Source);
        if (selected >= 0)
        {
            if (popup == ChoicePopup.Language)
            {
                draft = draft with { Language = GameSettingsChoices.Languages[selected] };
            }
            else
            {
                draft = draft with { Resolution = GameSettingsChoices.Resolutions[selected].Id };
            }

            ulong selectorKey = popup == ChoicePopup.Language ? LanguageButtonKey : ResolutionButtonKey;
            popup = ChoicePopup.None;
            Recreate(selectorKey);
        }
        else if (action.Type == PopupCancelAction || action.Type == CancelAction)
        {
            ulong selectorKey = popup == ChoicePopup.Language ? LanguageButtonKey : ResolutionButtonKey;
            popup = ChoicePopup.None;
            Recreate(selectorKey);
        }
    }

    private void SelectCategory(SettingsCategory value)
    {
        if (category == value)
        {
            return;
        }

        category = value;
        popup = ChoicePopup.None;
        Recreate(CategoryKey(value));
    }

    private void OpenPopup(ChoicePopup value)
    {
        popup = value;
        Recreate();
    }

    private void Recreate(ulong focusKey = 0)
    {
        DestroyNativeDocument();
        CreateNativeDocument();
        if (focusKey != 0)
        {
            FocusElement(focusKey);
        }

        Focus();
        InvalidateVisual();
    }

    private void FocusElement(ulong target)
    {
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UISetFocus(
            context, document, revision, target, out EngineUiFocusResult focus), "restore settings focus");
        if (focus.FocusedKey != target)
        {
            throw new InvalidOperationException("SpriteForge did not restore settings focus after rebuilding the page.");
        }
    }

    private GameResolutionChoice CurrentResolution()
    {
        if (!GameSettingsChoices.TryGetResolution(draft.Resolution, out GameResolutionChoice resolution))
        {
            throw new InvalidOperationException($"Unsupported draft display resolution '{draft.Resolution}'.");
        }

        return resolution;
    }

    private string PageHeading() => category switch
    {
        SettingsCategory.General => strings.Get("settings.heading.display"),
        SettingsCategory.Audio => strings.Get("settings.heading.audio"),
        SettingsCategory.Interface => strings.Get("settings.heading.accessibility"),
        _ => throw new InvalidOperationException("The active settings category is invalid."),
    };

    private void RefreshSnapshots()
    {
        EngineUiElementSnapshot[] values = new EngineUiElementSnapshot[elementKeys.Length];
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIGetElementSnapshots(
            context, document, elementKeys, (uint)elementKeys.Length, values, (uint)values.Length,
            out uint required, out uint count),
            "copy settings element snapshots");
        RequireCompleteCopy(required, count, "settings element snapshots");
        snapshots.Clear();
        for (int index = 0; index < count; ++index)
        {
            snapshots.Add(values[index].Key, values[index]);
        }
    }

    private void DrawCategorySelection(DrawingContext drawingContext)
    {
        ulong key = CategoryKey(category);
        if (!snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        drawingContext.DrawRectangle(Brush("#334A62"), null, bounds);
        drawingContext.DrawRectangle(Brush("#D7AF70"), null, new Rect(bounds.X, bounds.Y, 5, bounds.Height));
    }

    private void DrawPageControls(DrawingContext drawingContext)
    {
        switch (category)
        {
            case SettingsCategory.Audio:
                DrawSlider(drawingContext, MasterSliderKey, draft.MasterVolume, 0, 100);
                DrawSlider(drawingContext, MusicSliderKey, draft.MusicVolume, 0, 100);
                DrawSlider(drawingContext, EffectsSliderKey, draft.EffectsVolume, 0, 100);
                break;
            case SettingsCategory.Interface:
                DrawToggle(drawingContext, SubtitlesToggleKey, draft.Subtitles);
                DrawToggle(drawingContext, MotionToggleKey, draft.ReducedMotion);
                DrawToggle(drawingContext, ShakeToggleKey, draft.ScreenShake);
                DrawSlider(drawingContext, ScaleSliderKey, draft.UiScalePercent, 75, 150);
                break;
        }
    }

    private void DrawPageText(DrawingContext drawingContext)
    {
        switch (category)
        {
            case SettingsCategory.General:
                DrawText(drawingContext, LanguageLabelKey, strings.Get("settings.label.language"), 15, "#F2E9D8", false);
                DrawText(drawingContext, LanguageButtonKey, strings.LanguageName(draft.Language) + "  ▾", 14, "#F2E9D8", true);
                DrawText(drawingContext, ResolutionLabelKey, strings.Get("settings.label.resolution"), 15, "#F2E9D8", false);
                DrawText(drawingContext, ResolutionButtonKey, strings.ResolutionName(CurrentResolution()) + "  ▾", 14, "#F2E9D8", true);
                break;
            case SettingsCategory.Audio:
                DrawText(drawingContext, MasterLabelKey, strings.Get("settings.label.master-volume"), 15, "#F2E9D8", false);
                DrawText(drawingContext, MasterValueKey, strings.Percent(draft.MasterVolume), 14, "#80DED9", true);
                DrawText(drawingContext, MusicLabelKey, strings.Get("settings.label.music-volume"), 15, "#F2E9D8", false);
                DrawText(drawingContext, MusicValueKey, strings.Percent(draft.MusicVolume), 14, "#80DED9", true);
                DrawText(drawingContext, EffectsLabelKey, strings.Get("settings.label.effects-volume"), 15, "#F2E9D8", false);
                DrawText(drawingContext, EffectsValueKey, strings.Percent(draft.EffectsVolume), 14, "#80DED9", true);
                break;
            case SettingsCategory.Interface:
                DrawText(drawingContext, SubtitlesLabelKey, strings.Get("settings.label.subtitles"), 15, "#F2E9D8", false);
                DrawText(drawingContext, SubtitlesToggleKey, ToggleText(draft.Subtitles), 13, "#F2E9D8", true);
                DrawText(drawingContext, MotionLabelKey, strings.Get("settings.label.reduced-motion"), 15, "#F2E9D8", false);
                DrawText(drawingContext, MotionToggleKey, ToggleText(draft.ReducedMotion), 13, "#F2E9D8", true);
                DrawText(drawingContext, ShakeLabelKey, strings.Get("settings.label.screen-shake"), 15, "#F2E9D8", false);
                DrawText(drawingContext, ShakeToggleKey, ToggleText(draft.ScreenShake), 13, "#F2E9D8", true);
                DrawText(drawingContext, ScaleLabelKey, strings.Get("settings.label.interface-scale"), 15, "#F2E9D8", false);
                DrawText(drawingContext, ScaleValueKey, strings.Percent(draft.UiScalePercent), 14, "#80DED9", true);
                break;
        }
    }

    private void DrawPopup(DrawingContext drawingContext)
    {
        string title = popup == ChoicePopup.Language
            ? strings.Get("settings.label.language")
            : strings.Get("settings.label.resolution");
        DrawText(drawingContext, PopupTitleKey, title, 16, "#D7AF70", false);
        if (popup == ChoicePopup.Language)
        {
            for (int index = 0; index < GameSettingsChoices.Languages.Count; ++index)
            {
                string language = GameSettingsChoices.Languages[index];
                string selected = string.Equals(language, draft.Language, StringComparison.Ordinal) ? "✓  " : string.Empty;
                DrawText(drawingContext, LanguageChoiceKeys[index], selected + strings.LanguageName(language),
                    14, "#F2E9D8", false, 12);
            }
        }
        else
        {
            for (int index = 0; index < GameSettingsChoices.Resolutions.Count; ++index)
            {
                GameResolutionChoice resolution = GameSettingsChoices.Resolutions[index];
                string selected = string.Equals(resolution.Id, draft.Resolution, StringComparison.Ordinal) ? "✓  " : string.Empty;
                DrawText(drawingContext, ResolutionChoiceKeys[index], selected + strings.ResolutionName(resolution),
                    14, "#F2E9D8", false, 12);
            }
        }
    }

    private void DrawSlider(DrawingContext drawingContext, ulong key, int value, int minimum, int maximum)
    {
        if (!snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        double normalized = (double)(value - minimum) / (maximum - minimum);
        Rect track = new(bounds.X + 8, bounds.Y + bounds.Height / 2 - 2, bounds.Width - 16, 4);
        drawingContext.DrawRectangle(Brush("#101827"), null, track);
        drawingContext.DrawRectangle(Brush("#80DED9"), null,
            new Rect(track.X, track.Y, track.Width * normalized, track.Height));
        double knobX = track.X + track.Width * normalized;
        drawingContext.DrawEllipse(Brush("#F2E9D8"), null,
            new Point(knobX, bounds.Y + bounds.Height / 2), 6, 6);
    }

    private void DrawToggle(DrawingContext drawingContext, ulong key, bool enabled)
    {
        if (!snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        Rect indicator = new(bounds.X + 12, bounds.Y + bounds.Height / 2 - 7, 14, 14);
        drawingContext.DrawRectangle(enabled ? Brush("#80DED9") : Brush("#101827"), null, indicator);
    }

    private void DrawText(
        DrawingContext drawingContext,
        ulong key,
        string text,
        double fontSize,
        string color,
        bool centered,
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
            new Typeface(centered ? "Segoe UI Semibold" : "Segoe UI"),
            fontSize * ActualHeight / LogicalHeight,
            Brush(color),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, bounds.Width - inset * 2),
            MaxTextHeight = Math.Max(1, bounds.Height),
            TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
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
    private string ToggleText(bool enabled) => strings.Get(enabled ? "settings.state.on" : "settings.state.off");

    private static bool IsPopupElement(ulong key) =>
        key == PopupPanelKey || key == PopupTitleKey ||
        LanguageChoiceKeys.Contains(key) || ResolutionChoiceKeys.Contains(key);

    private static ulong CategoryKey(SettingsCategory value) => value switch
    {
        SettingsCategory.General => GeneralCategoryKey,
        SettingsCategory.Audio => AudioCategoryKey,
        SettingsCategory.Interface => InterfaceCategoryKey,
        _ => throw new InvalidOperationException("The active settings category is invalid."),
    };

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

    private static void RequireActionValue(EngineUiAction action, EngineUiActionValueType expected)
    {
        if (action.ValueType != expected)
        {
            throw new InvalidOperationException(
                $"SpriteForge returned {action.ValueType} for settings element {action.Source:x16}; expected {expected}.");
        }
    }

    private static ulong Key(string value)
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
        elementKeys = [];
    }

    private static void ThrowIfFailed(EngineStatus status, string operation)
    {
        if (status != EngineStatus.Success)
        {
            throw new InvalidOperationException(
                $"SpriteForge.dll could not {operation} ({status}, {(int)status}).");
        }
    }

    private enum SettingsCategory : byte
    {
        General,
        Audio,
        Interface,
    }

    private enum ChoicePopup : byte
    {
        None,
        Language,
        Resolution,
    }
}
