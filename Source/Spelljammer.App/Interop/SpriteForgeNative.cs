using System.Runtime.InteropServices;

namespace Spelljammer.Interop;

internal enum EngineStatus
{
    Success = 0,
    Failure = -1,
    InvalidArgument = -2,
    OutOfResource = -3,
    OutOfMemory = -4,
    InvalidResource = -5,
    NotSupported = -10,
    InvalidState = -11,
    DeviceLost = -14,
    BackendUnavailable = -15,
    InitializationFailed = -16,
    SkipFrame = -20,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineCamera2D
{
    internal float PositionX;
    internal float PositionY;
    internal float RotationRadians;
    internal float PixelsPerWorldUnit;
    internal uint IntegerZoom;
    internal uint PixelPerfect;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineSpriteDraw
{
    internal uint Texture;
    internal uint SourceX;
    internal uint SourceY;
    internal uint SourceWidth;
    internal uint SourceHeight;
    internal int PivotX;
    internal int PivotY;
    internal uint UntrimmedWidth;
    internal uint UntrimmedHeight;
    internal float PositionX;
    internal float PositionY;
    internal float ScaleX;
    internal float ScaleY;
    internal float RotationRadians;
    internal float ColorR;
    internal float ColorG;
    internal float ColorB;
    internal float ColorA;
    internal int Layer;
    internal int Order;
    internal uint FlipX;
    internal uint FlipY;
    internal uint PixelSnap;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiColor
{
    internal float Red;
    internal float Green;
    internal float Blue;
    internal float Alpha;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiTheme
{
    internal EngineUiColor Panel;
    internal EngineUiColor Button;
    internal EngineUiColor ButtonHovered;
    internal EngineUiColor ButtonPressed;
    internal EngineUiColor ButtonFocused;
    internal EngineUiColor ButtonDisabled;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiDocumentDescription
{
    internal ulong RootKey;
    internal uint LogicalWidth;
    internal uint LogicalHeight;
    internal uint MaximumElements;
    internal uint MaximumActions;
    internal EngineUiTheme Theme;
}

internal enum EngineUiElementKind : uint
{
    Container,
    Text,
    Image,
}

internal enum EngineUiBehavior : uint
{
    None,
    Button,
    Toggle,
    Slider,
    Scroll,
    Selection,
    TextEdit,
}

internal enum EngineUiLayoutMode : uint
{
    Overlay,
    Stack,
    Absolute,
    Scroll,
    VirtualList,
}

internal enum EngineUiSizeKind : uint
{
    Fixed,
    Percent,
    Content,
    Fill,
}

internal enum EngineUiPopupEdge : uint
{
    Below,
    Above,
    Right,
    Left,
}

internal enum EngineUiPopupAlignment : uint
{
    Start,
    Center,
    End,
}

internal enum EngineUiAccessibilityRole : uint
{
    None,
    Panel,
    Image,
    Text,
    Button,
    Toggle,
    Slider,
    ScrollArea,
    List,
    ListItem,
    TextField,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiElementDescription
{
    internal ulong Key;
    internal ulong ParentKey;
    internal ulong Action;
    internal ulong ChangingAction;
    internal ulong DismissAction;
    internal ulong SubmitAction;
    internal EngineUiElementKind Kind;
    internal EngineUiBehavior Behavior;
    internal EngineUiAccessibilityRole AccessibilityRole;
    internal EngineUiLayoutMode ChildLayout;
    internal uint StackOrientation;
    internal uint Overflow;
    internal EngineUiSizeKind WidthKind;
    internal EngineUiSizeKind HeightKind;
    internal float X;
    internal float Y;
    internal float Width;
    internal float Height;
    internal float PaddingLeft;
    internal float PaddingTop;
    internal float PaddingRight;
    internal float PaddingBottom;
    internal float ContentWidth;
    internal float ContentHeight;
    internal float VirtualItemExtent;
    internal uint VirtualFirstItem;
    internal uint MaximumRealizedItems;
    internal float ScrollX;
    internal float ScrollY;
    internal float MaximumScrollX;
    internal float MaximumScrollY;
    internal float SliderMinimum;
    internal float SliderMaximum;
    internal float SliderValue;
    internal float SliderStep;
    internal ulong SelectionItemId;
    internal ulong SpriteSheet;
    internal ulong SpriteFrame;
    internal ulong TextLayout;
    internal ulong PopupAnchor;
    internal float PopupGap;
    internal float PopupSafeLeft;
    internal float PopupSafeTop;
    internal float PopupSafeRight;
    internal float PopupSafeBottom;
    internal ushort NineSliceLeft;
    internal ushort NineSliceTop;
    internal ushort NineSliceRight;
    internal ushort NineSliceBottom;
    internal int TabOrder;
    internal EngineUiPopupEdge PopupEdge;
    internal EngineUiPopupAlignment PopupAlignment;
    internal uint ToggleValue;
    internal uint Visible;
    internal uint Enabled;
    internal uint HitTestable;
    internal uint Modal;
    internal uint Focusable;
    internal uint Selected;
    internal uint CustomColor;
    internal uint PopupEnabled;
    internal uint PopupAllowFlip;
    internal uint PopupAllowClamp;
    internal uint PopupScrollFallback;
    internal uint PopupDismissOnOutsidePress;
    internal uint TextMultiline;
    internal uint TextSensitive;
    internal uint TextReadOnly;
    internal uint TextAllowClipboard;
    internal uint TextMaximumBytes;
    internal EngineUiColor Color;
    internal nint AccessibleNameUtf8;
    internal uint AccessibleNameBytes;
    internal nint AccessibleValueUtf8;
    internal uint AccessibleValueBytes;
    internal nint TextUtf8;
    internal uint TextBytes;
}

internal enum EngineUiMutationType : uint
{
    Create,
    Remove,
    Update,
    Reparent,
    Reorder,
    Viewport,
    Layer,
    Theme,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiMutation
{
    internal EngineUiMutationType Type;
    internal uint SiblingOrder;
    internal ulong Key;
    internal ulong ParentKey;
    internal uint LogicalWidth;
    internal uint LogicalHeight;
    internal ushort ScaleNumerator;
    internal ushort ScaleDenominator;
    internal int Layer;
    internal EngineUiTheme Theme;
    internal EngineUiElementDescription Element;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiCommitReport
{
    internal ulong PreviousRevision;
    internal ulong Revision;
    internal uint Created;
    internal uint Removed;
    internal uint Updated;
    internal uint FocusRestored;
    internal ulong FocusedKey;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiFocusResult
{
    internal ulong Revision;
    internal ulong RequestedKey;
    internal ulong FocusedKey;
    internal uint Restored;
    internal uint Reserved;
}

internal enum EngineUiInputType : uint
{
    PointerMoved,
    PointerDown,
    PointerUp,
    PointerScrolled,
    KeyDown,
    KeyUp,
    Navigation,
    Cancel,
    TextCommit,
    CompositionStarted,
    CompositionUpdated,
    CompositionCommitted,
    CompositionCancelled,
}

internal enum EngineUiNavigation : uint
{
    None,
    Next,
    Previous,
    Left,
    Right,
    Up,
    Down,
    Accept,
    Cancel,
}

internal enum EngineInputDeviceKind : uint
{
    Keyboard,
    Mouse,
    Gamepad,
    Synthetic,
}

internal enum EngineMouseButton : uint
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiInput
{
    internal EngineUiInputType Type;
    internal EngineUiNavigation Navigation;
    internal float X;
    internal float Y;
    internal float DeltaX;
    internal float DeltaY;
    internal ulong Sequence;
    internal uint PointerId;
    internal EngineInputDeviceKind Source;
    internal EngineMouseButton Button;
    internal uint Key;
    internal uint Modifiers;
    internal uint InsideViewport;
    internal uint Repeat;
    internal nint Utf8;
    internal uint Utf8Bytes;
    internal uint TextSelectionStartByte;
    internal uint TextSelectionEndByte;
}

internal enum EngineUiActionValueType : uint
{
    None,
    Boolean,
    Scalar,
    Point,
    UnsignedInteger,
    Utf8,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiAction
{
    internal ulong Type;
    internal ulong Source;
    internal ulong InteractionSequence;
    internal ulong AudioCue;
    internal ulong UnsignedValue;
    internal float PointX;
    internal float PointY;
    internal float ScalarValue;
    internal uint DeviceId;
    internal uint DeviceKind;
    internal uint Kind;
    internal EngineUiActionValueType ValueType;
    internal uint BooleanValue;
    internal uint Preview;
    internal uint Utf8Offset;
    internal uint Utf8Bytes;
}

[Flags]
internal enum EngineUiElementStateFlags : uint
{
    None = 0,
    Visible = 1 << 0,
    Enabled = 1 << 1,
    Focused = 1 << 2,
    Captured = 1 << 3,
    Selected = 1 << 4,
    ClipEnabled = 1 << 5,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiElementSnapshot
{
    internal ulong Key;
    internal ulong ParentKey;
    internal float X;
    internal float Y;
    internal float Width;
    internal float Height;
    internal float ClipX;
    internal float ClipY;
    internal float ClipWidth;
    internal float ClipHeight;
    internal EngineUiElementStateFlags StateFlags;
    internal EngineUiPopupEdge ResolvedPopupEdge;
    internal uint PopupClamped;
    internal uint Reserved;

    internal readonly bool IsVisible => StateFlags.HasFlag(EngineUiElementStateFlags.Visible);
    internal readonly bool IsEnabled => StateFlags.HasFlag(EngineUiElementStateFlags.Enabled);
    internal readonly bool IsFocused => StateFlags.HasFlag(EngineUiElementStateFlags.Focused);
}

internal enum EngineUiPresentationType : uint
{
    SolidQuad,
    Sprite,
    Text,
    NineSlice,
}

[Flags]
internal enum EngineUiPresentationFlags : uint
{
    None = 0,
    Clipped = 1 << 0,
    PixelSnapped = 1 << 1,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiPresentationCommand
{
    internal EngineUiPresentationType Type;
    internal EngineUiPresentationFlags Flags;
    internal ulong Source;
    internal float X;
    internal float Y;
    internal float Width;
    internal float Height;
    internal float ClipX;
    internal float ClipY;
    internal float ClipWidth;
    internal float ClipHeight;
    internal EngineUiColor Color;
    internal ulong SpriteSheet;
    internal ulong SpriteFrame;
    internal ulong TextLayout;
    internal ushort NineSliceLeft;
    internal ushort NineSliceTop;
    internal ushort NineSliceRight;
    internal ushort NineSliceBottom;
    internal int Layer;
    internal int Order;
}

internal enum EngineUiScalingMode : uint
{
    IntegerFit,
    FractionalFitNearest,
    StretchNearest,
}

internal enum EngineUiSmallWindowPolicy : uint
{
    FractionalFitNearest,
    CropAtOneToOne,
    SkipPresentation,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineUiPresentationLayout
{
    internal uint LogicalWidth;
    internal uint LogicalHeight;
    internal uint PhysicalWidth;
    internal uint PhysicalHeight;
    internal EngineUiScalingMode ScalingMode;
    internal EngineUiSmallWindowPolicy SmallWindowPolicy;
    internal float ViewportX;
    internal float ViewportY;
    internal float ViewportWidth;
    internal float ViewportHeight;
    internal float PhysicalPixelsPerLogicalX;
    internal float PhysicalPixelsPerLogicalY;
    internal uint Drawable;
}

internal static class SpriteForgeNative
{
    private const string LibraryName = "SpriteForge.dll";

    static SpriteForgeNative()
    {
        VerifyLayout<EngineUiDocumentDescription>(120);
        VerifyLayout<EngineUiElementDescription>(384);
        VerifyLayout<EngineUiMutation>(520);
        VerifyLayout<EngineUiCommitReport>(40);
        VerifyLayout<EngineUiFocusResult>(32);
        VerifyLayout<EngineUiInput>(88);
        VerifyLayout<EngineUiAction>(88);
        VerifyLayout<EngineUiElementSnapshot>(64);
        VerifyLayout<EngineUiPresentationCommand>(104);
        VerifyLayout<EngineUiPresentationLayout>(52);
    }

    private static void VerifyLayout<T>(int expected) where T : struct
    {
        int actual = Marshal.SizeOf<T>();
        if (actual != expected)
        {
            throw new TypeLoadException(
                $"SpriteForge UI interop layout '{typeof(T).Name}' is {actual} bytes; expected {expected}.");
        }
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint SpriteForge_GetInteropVersion();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_CreateRenderer(
        nint nativeWindow,
        uint logicalWidth,
        uint logicalHeight,
        uint maxSprites,
        uint verticalSync,
        out nint renderer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SpriteForge_DestroyRenderer(nint renderer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_CreateRgba8Texture(
        nint renderer,
        uint width,
        uint height,
        byte[] pixels,
        nuint sizeBytes,
        out uint texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_DestroyTexture(
        nint renderer,
        uint texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RenderSprites(
        nint renderer,
        in EngineCamera2D camera,
        [In] EngineSpriteDraw[] draws,
        uint drawCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_CreateUIContext(
        in EngineUiDocumentDescription description,
        out nint context,
        out ulong document);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SpriteForge_DestroyUIContext(nint context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UICancelInput(
        nint context,
        ulong document);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UICommit(
        nint context,
        ulong document,
        ulong expectedRevision,
        [In] EngineUiMutation[] mutations,
        uint mutationCount,
        out EngineUiCommitReport report);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIGetRevision(
        nint context,
        ulong document,
        out ulong revision);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UISetFocus(
        nint context,
        ulong document,
        ulong expectedRevision,
        ulong elementKey,
        out EngineUiFocusResult result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIProcessInput(
        nint context,
        ulong document,
        [In] EngineUiInput[] inputs,
        uint inputCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIConsumeActions(
        nint context,
        ulong document,
        [Out] EngineUiAction[] actions,
        uint actionCapacity,
        [Out] byte[]? utf8,
        uint utf8Capacity,
        out uint requiredActions,
        out uint writtenActions,
        out uint requiredUtf8Bytes,
        out uint writtenUtf8Bytes);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIGetElementSnapshots(
        nint context,
        ulong document,
        [In] ulong[] keys,
        uint keyCount,
        [Out] EngineUiElementSnapshot[] snapshots,
        uint capacity,
        out uint required,
        out uint written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIBuildPresentation(
        nint context,
        ulong document,
        [Out] EngineUiPresentationCommand[] commands,
        uint capacity,
        out uint required,
        out uint written,
        out ulong revision);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UICalculatePresentationLayout(
        ref EngineUiPresentationLayout layout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_UIMapPhysicalPoint(
        in EngineUiPresentationLayout layout,
        float physicalX,
        float physicalY,
        out float logicalX,
        out float logicalY,
        out uint insideViewport);
}
