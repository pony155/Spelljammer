using System.Runtime.InteropServices;
using System.Text;
using Spelljammer.Interop;

namespace Spelljammer.Presentation;

/// <summary>
/// SpriteForge-rendered main menu with SpriteForge UI hit testing and actions.
/// </summary>
internal sealed class SpriteForgeMainMenuView : SpriteForgeRenderSurface
{
    internal const uint SurfaceWidth = 1280;
    internal const uint SurfaceHeight = 720;
    private const string BackgroundUri = "pack://application:,,,/Assets/UI/MainMenu/Background.png";
    private const uint ElementCapacity = 5;
    private const uint ActionCapacity = 8;

    private static readonly ulong RootKey = StableKey("spelljammer.menu.root");
    private static readonly ulong PanelKey = StableKey("spelljammer.menu.panel");
    private static readonly ulong NewGameButtonKey = StableKey("spelljammer.menu.new-game");
    private static readonly ulong SettingsButtonKey = StableKey("spelljammer.menu.settings");
    private static readonly ulong QuitButtonKey = StableKey("spelljammer.menu.quit");

    private readonly GameText strings;
    private readonly string version;
    private readonly EngineUiPresentationCommand[] presentation = new EngineUiPresentationCommand[ElementCapacity];
    private readonly EngineUiAction[] actions = new EngineUiAction[ActionCapacity];
    private nint context;
    private ulong document;
    private ulong inputSequence;
    private string status = string.Empty;
    private bool statusIsError;

    internal SpriteForgeMainMenuView(GameText strings, string version)
        : base(SurfaceWidth, SurfaceHeight, strings.Culture)
    {
        this.strings = strings;
        this.version = version;
        CreateNativeDocument();
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

    internal void RefreshLanguage()
    {
        DestroyNativeDocument();
        CreateNativeDocument();
        ResetText(strings.Culture);
    }

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.ImageCover(BackgroundUri, 0, 0, SurfaceWidth, SurfaceHeight);
        DrawNativePresentation(canvas);
        canvas.TextLine(strings.Get("menu.title"), 830, 120, 360, 72, 48, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(strings.Get("menu.subtitle"), 830, 190, 360, 44, 16, "#FFC4BBD1",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(strings.Get("menu.button.new-game"), 860, 242, 300, 68, 20, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(strings.Get("menu.button.settings"), 860, 326, 300, 68, 20, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(strings.Get("menu.button.quit"), 860, 410, 300, 68, 20, "#FFF4E7CE",
            SpriteForgeTextAlignment.Center);
        if (!string.IsNullOrEmpty(status))
        {
            canvas.TextLine(status, 835, 485, 350, 58, 13,
                statusIsError ? "#FFF39A8D" : "#FFB8C7DF", SpriteForgeTextAlignment.Center);
        }

        canvas.TextLine(strings.Version(version), 1010, 674, 240, 26, 13, "#FFC4BBD1",
            SpriteForgeTextAlignment.Right);
    }

    protected override void PointerMoved(float x, float y) => SendPointer(EngineUiInputType.PointerMoved, x, y);

    protected override void PointerPressed(float x, float y) => SendPointer(EngineUiInputType.PointerDown, x, y);

    protected override void PointerReleased(float x, float y) => SendPointer(EngineUiInputType.PointerUp, x, y);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DestroyNativeDocument();
        }

        base.Dispose(disposing);
    }

    private void CreateNativeDocument()
    {
        EngineUiDocumentDescription description = new() {
            RootKey = RootKey,
            LogicalWidth = SurfaceWidth,
            LogicalHeight = SurfaceHeight,
            MaximumElements = ElementCapacity,
            MaximumActions = ActionCapacity,
            Theme = new EngineUiTheme {
                Panel = Color(0, 0, 0, 0),
                Button = Color(0, 0, 0, 0),
                ButtonHovered = Color(0.12f, 0.20f, 0.30f, 0.34f),
                ButtonPressed = Color(0.08f, 0.15f, 0.24f, 0.52f),
                ButtonFocused = Color(0.12f, 0.26f, 0.32f, 0.36f),
                ButtonDisabled = Color(0, 0, 0, 0),
            },
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_CreateUIContext(in description, out context, out document),
            "create the main-menu UI document");

        List<nint> allocatedNames = [];
        try
        {
            EngineUiElementDescription[] elements =
            [
                Element(PanelKey, RootKey, 800, 70, 420, 580, EngineUiBehavior.None,
                    strings.Get("menu.accessibility.main"), allocatedNames, modal: true),
                Element(NewGameButtonKey, PanelKey, 60, 172, 300, 68, EngineUiBehavior.Button,
                    strings.Get("menu.button.new-game"), allocatedNames, tabOrder: 0),
                Element(SettingsButtonKey, PanelKey, 60, 256, 300, 68, EngineUiBehavior.Button,
                    strings.Get("menu.button.settings"), allocatedNames, tabOrder: 1),
                Element(QuitButtonKey, PanelKey, 60, 340, 300, 68, EngineUiBehavior.Button,
                    strings.Get("menu.button.quit"), allocatedNames, tabOrder: 2),
            ];
            EngineUiMutation[] mutations = elements.Select(static element => new EngineUiMutation {
                Type = EngineUiMutationType.Create,
                Element = element,
            }).ToArray();
            ThrowIfFailed(SpriteForgeNative.SpriteForge_UICommit(
                context, document, 1, mutations, checked((uint)mutations.Length), out EngineUiCommitReport report),
                "commit the main-menu UI document");
            if (report.Created != mutations.Length)
            {
                throw new InvalidOperationException("SpriteForge did not create the complete main-menu UI document.");
            }
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

    private void DrawNativePresentation(SpriteForgeCanvas canvas)
    {
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIBuildPresentation(
            context, document, presentation, checked((uint)presentation.Length),
            out uint required, out uint written, out _), "build the main-menu UI presentation");
        if (required != written)
        {
            throw new InvalidOperationException("SpriteForge returned an incomplete main-menu presentation.");
        }

        for (int index = 0; index < written; ++index)
        {
            EngineUiPresentationCommand command = presentation[index];
            if (command.Type != EngineUiPresentationType.SolidQuad || command.Color.Alpha <= 0)
            {
                continue;
            }

            canvas.Fill(command.X, command.Y, command.Width, command.Height, Hex(command.Color), command.Layer);
        }
    }

    private void SendPointer(EngineUiInputType type, float x, float y)
    {
        if (context == 0)
        {
            return;
        }

        EngineUiInput[] input =
        [
            new EngineUiInput {
                Type = type,
                X = x,
                Y = y,
                Sequence = ++inputSequence,
                PointerId = 1,
                Source = EngineInputDeviceKind.Mouse,
                Button = EngineMouseButton.Left,
                InsideViewport = 1,
            },
        ];
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIProcessInput(context, document, input, 1),
            "process main-menu UI input");
        ThrowIfFailed(SpriteForgeNative.SpriteForge_UIConsumeActions(
            context, document, actions, checked((uint)actions.Length), null, 0,
            out uint requiredActions, out uint writtenActions, out uint requiredText, out uint writtenText),
            "consume main-menu UI actions");
        if (requiredActions != writtenActions || requiredText != writtenText)
        {
            throw new InvalidOperationException("SpriteForge returned an incomplete main-menu action batch.");
        }

        for (int index = 0; index < writtenActions; ++index)
        {
            ulong source = actions[index].Source;
            if (source == NewGameButtonKey)
            {
                Dispatch(() => NewGameRequested?.Invoke(this, EventArgs.Empty));
            }
            else if (source == SettingsButtonKey)
            {
                Dispatch(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
            }
            else if (source == QuitButtonKey)
            {
                Dispatch(() => QuitRequested?.Invoke(this, EventArgs.Empty));
            }
        }

        RefreshSurface();
    }

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
        bool modal = false)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(accessibleName);
        nint name = Marshal.StringToCoTaskMemUTF8(accessibleName);
        allocatedNames.Add(name);
        bool interactive = behavior != EngineUiBehavior.None;
        return new EngineUiElementDescription {
            Key = key,
            ParentKey = parent,
            Action = interactive ? key : 0,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            SliderMaximum = 1,
            SliderStep = 0.1f,
            TabOrder = tabOrder,
            Kind = EngineUiElementKind.Container,
            Behavior = behavior,
            AccessibilityRole = interactive ? EngineUiAccessibilityRole.Button : EngineUiAccessibilityRole.Panel,
            ChildLayout = EngineUiLayoutMode.Absolute,
            WidthKind = EngineUiSizeKind.Fixed,
            HeightKind = EngineUiSizeKind.Fixed,
            Visible = 1,
            Enabled = 1,
            HitTestable = interactive ? 1u : 0u,
            Modal = modal ? 1u : 0u,
            Focusable = interactive ? 1u : 0u,
            TextMaximumBytes = 1,
            AccessibleNameUtf8 = name,
            AccessibleNameBytes = checked((uint)encoded.Length),
        };
    }

    private void DestroyNativeDocument()
    {
        if (context != 0)
        {
            SpriteForgeNative.SpriteForge_DestroyUIContext(context);
            context = 0;
            document = 0;
        }
    }

    private static EngineUiColor Color(float red, float green, float blue, float alpha = 1) => new() {
        Red = red * alpha,
        Green = green * alpha,
        Blue = blue * alpha,
        Alpha = alpha,
    };

    private static string Hex(EngineUiColor color)
    {
        byte alpha = checked((byte)MathF.Round(Math.Clamp(color.Alpha, 0, 1) * 255));
        float divisor = color.Alpha > 0 ? color.Alpha : 1;
        byte red = checked((byte)MathF.Round(Math.Clamp(color.Red / divisor, 0, 1) * 255));
        byte green = checked((byte)MathF.Round(Math.Clamp(color.Green / divisor, 0, 1) * 255));
        byte blue = checked((byte)MathF.Round(Math.Clamp(color.Blue / divisor, 0, 1) * 255));
        return $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
    }

    private static ulong StableKey(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte character in Encoding.UTF8.GetBytes(value))
        {
            hash ^= character;
            hash *= prime;
        }

        return hash == 0 ? 1 : hash;
    }

    private static void ThrowIfFailed(EngineStatus status, string operation)
    {
        if (status != EngineStatus.Success)
        {
            throw new InvalidOperationException($"SpriteForge could not {operation} ({status}).");
        }
    }
}
