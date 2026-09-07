using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spelljammer.Interop;

namespace Spelljammer.Presentation;

internal sealed class SpriteForgeMainMenuView : FrameworkElement, IDisposable
{
    internal const double LogicalWidth = 1280;
    internal const double LogicalHeight = 720;
    private const uint ElementCapacity = 12;
    private const uint ActionCapacity = 8;
    private const uint NonEditableTextCapacity = 1;

    private static readonly ulong RootKey = StableKey("spelljammer.menu.root");
    private static readonly ulong PanelKey = StableKey("spelljammer.menu.panel");
    private static readonly ulong TitleKey = StableKey("spelljammer.menu.title");
    private static readonly ulong SubtitleKey = StableKey("spelljammer.menu.subtitle");
    private static readonly ulong NewGameButtonKey = StableKey("spelljammer.menu.new-game");
    private static readonly ulong SettingsButtonKey = StableKey("spelljammer.menu.settings");
    private static readonly ulong QuitButtonKey = StableKey("spelljammer.menu.quit");
    private static readonly ulong StatusKey = StableKey("spelljammer.menu.status");
    private static readonly ulong VersionKey = StableKey("spelljammer.menu.version");

    private readonly GameText strings;
    private readonly string version;
    private readonly BitmapSource background;
    private readonly ulong[] elementKeys =
    [
        PanelKey,
        TitleKey,
        SubtitleKey,
        NewGameButtonKey,
        SettingsButtonKey,
        QuitButtonKey,
        StatusKey,
        VersionKey,
    ];
    private readonly ulong[] menuButtonKeys = [NewGameButtonKey, SettingsButtonKey, QuitButtonKey];
    private readonly Dictionary<ulong, EngineUiElementSnapshot> snapshots = [];
    private readonly EngineUiPresentationCommand[] presentation = new EngineUiPresentationCommand[ElementCapacity];
    private readonly EngineUiAction[] actions = new EngineUiAction[ActionCapacity];
    private nint context;
    private ulong document;
    private ulong inputSequence;
    private ulong hoveredButtonKey;
    private string status = string.Empty;
    private bool statusIsError;
    private bool disposed;

    internal SpriteForgeMainMenuView(GameText strings, string version)
    {
        this.strings = strings;
        this.version = version;
        background = LoadBackground();
        Focusable = false;
        SnapsToDevicePixels = true;
        CreateNativeDocument();
        Unloaded += View_Unloaded;
    }

    internal event EventHandler? NewGameRequested;
    internal event EventHandler? SettingsRequested;
    internal event EventHandler? QuitRequested;

    internal void SetStatus(string text, bool isError)
    {
        status = text;
        statusIsError = isError;
        InvalidateVisual();
    }

    internal void RefreshLanguage()
    {
        DestroyNativeDocument();
        CreateNativeDocument();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(LogicalWidth, LogicalHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        DrawBackground(drawingContext);
        if (context == nint.Zero)
        {
            return;
        }

        RefreshSnapshots();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIBuildPresentation(
            context,
            document,
            presentation,
            (uint)presentation.Length,
            out uint requiredCommands,
            out uint commandCount,
            out _), "build the main-menu presentation");
        RequireCompleteCopy(requiredCommands, commandCount, "main-menu presentation");
        for (int index = 0; index < commandCount; ++index)
        {
            EngineUiPresentationCommand command = presentation[index];
            if (command.Type != EngineUiPresentationType.SolidQuad)
            {
                continue;
            }

            drawingContext.DrawRectangle(
                ToBrush(command.Color),
                null,
                ScaleAndClip(command));
        }

        strings.BeginFrame();
        DrawText(drawingContext, TitleKey, strings.Get("menu.title"), 48, "#F4E7CE",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, SubtitleKey, strings.Get("menu.subtitle"), 16, "#C4BBD1",
            TextAlignment.Center, FontWeights.Normal);
        DrawText(drawingContext, NewGameButtonKey, strings.Get("menu.button.new-game"), 20, "#F4E7CE",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, SettingsButtonKey, strings.Get("menu.button.settings"), 20, "#F4E7CE",
            TextAlignment.Center, FontWeights.SemiBold);
        DrawText(drawingContext, QuitButtonKey, strings.Get("menu.button.quit"), 20, "#F4E7CE",
            TextAlignment.Center, FontWeights.SemiBold);
        if (!string.IsNullOrEmpty(status))
        {
            DrawText(drawingContext, StatusKey, status, 13,
                statusIsError ? "#F39A8D" : "#B8C7DF", TextAlignment.Center, FontWeights.Normal);
        }
        DrawText(drawingContext, VersionKey, strings.Version(version), 13,
            "#C4BBD1", TextAlignment.Right, FontWeights.Normal);

        if (hoveredButtonKey != 0 && snapshots.TryGetValue(hoveredButtonKey, out EngineUiElementSnapshot hovered))
        {
            DrawOutline(drawingContext, hovered);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point position = e.GetPosition(this);
        SendPointer(EngineUiInputType.PointerMoved, position);
        UpdateHoveredButton(position);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hoveredButtonKey = 0;
        SendPointer(EngineUiInputType.PointerMoved, e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DestroyNativeDocument();

        Unloaded -= View_Unloaded;
        GC.SuppressFinalize(this);
    }

    private static BitmapSource LoadBackground()
    {
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(
            "pack://application:,,,/Assets/UI/MainMenu/Background.png",
            UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
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
                Panel = Color(0, 0, 0, 0),
                Button = Color(0, 0, 0, 0),
                ButtonHovered = Color(0, 0, 0, 0),
                ButtonPressed = Color(0, 0, 0, 0),
                ButtonFocused = Color(0, 0, 0, 0),
                ButtonDisabled = Color(0, 0, 0, 0),
            },
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_CreateUIContext(
            in description,
            out context,
            out document), "create the main-menu UI document");

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
                context,
                document,
                1,
                mutations,
                (uint)mutations.Length,
                out EngineUiCommitReport report), "commit the main-menu UI document");
            if (report.Created != (uint)mutations.Length)
            {
                throw new InvalidOperationException("SpriteForge did not create the complete main-menu UI document.");
            }
        }
        catch
        {
            SpriteForgeNative.SpriteForge_DestroyUIContext(context);
            context = nint.Zero;
            document = 0;
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

    private EngineUiElementDescription[] BuildElements(List<nint> allocatedNames) =>
    [
        Element(PanelKey, RootKey, 800, 70, 420, 580, EngineUiBehavior.None,
            strings.Get("menu.accessibility.main"), allocatedNames, modal: true,
            customColor: true, color: Color(0, 0, 0, 0)),
        TextElement(TitleKey, 30, 55, 360, 72, strings.Get("menu.title"), allocatedNames),
        TextElement(SubtitleKey, 30, 135, 360, 50, strings.Get("menu.subtitle"), allocatedNames),
        Button(NewGameButtonKey, 60, 242, 300, 68, 0,
            strings.Get("menu.button.new-game"), allocatedNames),
        Button(SettingsButtonKey, 60, 326, 300, 68, 1,
            strings.Get("menu.button.settings"), allocatedNames),
        Button(QuitButtonKey, 60, 410, 300, 68, 2,
            strings.Get("menu.button.quit"), allocatedNames),
        TextElement(StatusKey, 35, 485, 350, 58,
            strings.Get("menu.accessibility.status"), allocatedNames),
        Element(VersionKey, RootKey, 1010, 674, 240, 26, EngineUiBehavior.None,
            strings.Version(version), allocatedNames, kind: EngineUiElementKind.Text,
            customColor: true, color: Color(0, 0, 0, 0)),
    ];

    private static EngineUiElementDescription TextElement(
        ulong key,
        float x,
        float y,
        float width,
        float height,
        string name,
        List<nint> names) =>
        Element(key, PanelKey, x, y, width, height, EngineUiBehavior.None, name, names,
            kind: EngineUiElementKind.Text, customColor: true, color: Color(0, 0, 0, 0));

    private static EngineUiElementDescription Button(
        ulong key,
        float x,
        float y,
        float width,
        float height,
        int tabOrder,
        string name,
        List<nint> names) =>
        Element(key, PanelKey, x, y, width, height, EngineUiBehavior.Button, name, names, tabOrder: tabOrder);

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
        EngineUiElementKind kind = EngineUiElementKind.Container,
        bool customColor = false,
        EngineUiColor color = default)
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
            CustomColor = customColor ? 1u : 0u,
            TextMaximumBytes = NonEditableTextCapacity,
            Color = color,
            AccessibleNameUtf8 = name,
            AccessibleNameBytes = (uint)encoded.Length,
        };
    }

    private void DrawBackground(DrawingContext drawingContext)
    {
        double scale = Math.Max(ActualWidth / background.PixelWidth, ActualHeight / background.PixelHeight);
        double width = background.PixelWidth * scale;
        double height = background.PixelHeight * scale;
        drawingContext.DrawImage(background, new Rect(
            (ActualWidth - width) / 2,
            (ActualHeight - height) / 2,
            width,
            height));
    }

    private void DrawText(
        DrawingContext drawingContext,
        ulong key,
        string text,
        double fontSize,
        string color,
        TextAlignment alignment,
        FontWeight weight)
    {
        if (!snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot))
        {
            return;
        }

        double scale = CalculatePresentationLayout().PhysicalPixelsPerLogicalX;
        FormattedText formatted = new(
            text,
            strings.Culture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize * scale,
            Brush(color),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, snapshot.Width * scale),
            TextAlignment = alignment,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        Rect bounds = Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
        drawingContext.DrawText(formatted, new Point(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - formatted.Height) / 2)));
    }

    private void SendPointer(EngineUiInputType type, Point physical)
    {
        if (context == nint.Zero || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        EngineUiPresentationLayout layout = CalculatePresentationLayout();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIMapPhysicalPoint(
            in layout, (float)physical.X, (float)physical.Y,
            out float x, out float y, out uint insideViewport), "map main-menu pointer input");
        Process([new EngineUiInput
        {
            Type = type,
            X = x,
            Y = y,
            Sequence = ++inputSequence,
            PointerId = 1,
            Source = EngineInputDeviceKind.Mouse,
            Button = EngineMouseButton.Left,
            InsideViewport = insideViewport,
        }]);
    }

    private void Process(EngineUiInput[] input)
    {
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIProcessInput(
            context,
            document,
            input,
            (uint)input.Length), "process main-menu input");
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIConsumeActions(
            context,
            document,
            actions,
            (uint)actions.Length,
            null,
            0,
            out uint requiredActions,
            out uint actionCount,
            out uint requiredUtf8Bytes,
            out uint writtenUtf8Bytes), "consume main-menu actions");
        RequireCompleteCopy(requiredActions, actionCount, "main-menu actions");
        RequireCompleteCopy(requiredUtf8Bytes, writtenUtf8Bytes, "main-menu action text");
        for (int index = 0; index < actionCount; ++index)
        {
            EngineUiAction action = actions[index];
            if (action.Source == NewGameButtonKey)
            {
                NewGameRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (action.Source == SettingsButtonKey)
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (action.Source == QuitButtonKey)
            {
                QuitRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        InvalidateVisual();
    }

    private void RefreshSnapshots()
    {
        EngineUiElementSnapshot[] values = new EngineUiElementSnapshot[elementKeys.Length];
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIGetElementSnapshots(
            context,
            document,
            elementKeys,
            (uint)elementKeys.Length,
            values,
            (uint)values.Length,
            out uint required,
            out uint count), "copy main-menu element snapshots");
        RequireCompleteCopy(required, count, "main-menu element snapshots");
        snapshots.Clear();
        for (int index = 0; index < count; ++index)
        {
            snapshots.Add(values[index].Key, values[index]);
        }
    }

    private void UpdateHoveredButton(Point physical)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            hoveredButtonKey = 0;
            return;
        }

        EngineUiPresentationLayout layout = CalculatePresentationLayout();
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIMapPhysicalPoint(
            in layout, (float)physical.X, (float)physical.Y,
            out float logicalX, out float logicalY, out uint insideViewport), "map main-menu hover input");
        Point logical = new(logicalX, logicalY);
        ulong next = 0;
        if (insideViewport != 0)
        {
            foreach (ulong key in menuButtonKeys)
            {
                if (snapshots.TryGetValue(key, out EngineUiElementSnapshot snapshot) &&
                    snapshot.IsVisible && snapshot.IsEnabled &&
                    new Rect(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height).Contains(logical))
                {
                    next = key;
                    break;
                }
            }
        }

        if (next != hoveredButtonKey)
        {
            hoveredButtonKey = next;
            InvalidateVisual();
        }
    }

    private void DrawOutline(DrawingContext drawingContext, EngineUiElementSnapshot snapshot)
    {
        drawingContext.DrawRectangle(null, new Pen(Brush("#D7AF70"), 3),
            Scale(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height));
    }

    private Rect Scale(float x, float y, float width, float height)
    {
        EngineUiPresentationLayout layout = CalculatePresentationLayout();
        return new Rect(
            layout.ViewportX + x * layout.PhysicalPixelsPerLogicalX,
            layout.ViewportY + y * layout.PhysicalPixelsPerLogicalY,
            width * layout.PhysicalPixelsPerLogicalX,
            height * layout.PhysicalPixelsPerLogicalY);
    }

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

    private EngineUiPresentationLayout CalculatePresentationLayout()
    {
        EngineUiPresentationLayout layout = new()
        {
            LogicalWidth = (uint)LogicalWidth,
            LogicalHeight = (uint)LogicalHeight,
            PhysicalWidth = (uint)Math.Max(1, Math.Round(ActualWidth)),
            PhysicalHeight = (uint)Math.Max(1, Math.Round(ActualHeight)),
            ScalingMode = EngineUiScalingMode.FractionalFitNearest,
            SmallWindowPolicy = EngineUiSmallWindowPolicy.FractionalFitNearest,
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UICalculatePresentationLayout(ref layout),
            "calculate the main-menu presentation layout");
        return layout;
    }

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
