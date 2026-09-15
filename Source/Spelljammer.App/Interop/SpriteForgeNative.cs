using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Spelljammer.Interop;

/// <summary>
/// Declares the managed representation of the versioned SpriteForge native ABI.
/// </summary>
/// <remarks>
/// Code flow: Managed hosts marshal bounded blittable requests into native functions, inspect returned status values, and copy snapshots or commands back into managed presentation code.
/// </remarks>
internal enum EngineStatus
{
    Success = 0,
    Failure = -1,
    InvalidArgument = -2,
    OutOfResource = -3,
    OutOfMemory = -4,
    InvalidResource = -5,
    ItemNotFound = -6,
    PermissionDenied = -7,
    Timeout = -8,
    NotImplemented = -9,
    NotSupported = -10,
    InvalidState = -11,
    AlreadyExists = -12,
    Busy = -13,
    DeviceLost = -14,
    BackendUnavailable = -15,
    InitializationFailed = -16,
    IoError = -17,
    DataCorrupt = -18,
    Cancelled = -19,
    SkipFrame = -20,
}

internal enum EngineAudioBus : uint
{
    Master,
    Music,
    SoundEffects,
    Ambience,
    UserInterface,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineAudioConfig
{
    internal uint PreferredSampleRate;
    internal uint PreferredBufferFrames;
    internal uint MaximumClips;
    internal uint MaximumVoices;
    internal uint MaximumMixedVoices;
    internal uint CommandCapacity;
    internal uint StreamBufferFrames;
    internal uint StreamLowWaterFrames;
    internal uint StreamHighWaterFrames;
    internal ulong MaximumResidentBytes;
    internal ulong MaximumStreamBufferBytes;
    internal uint EnableVoiceStealing;
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

[Flags]
internal enum EngineRendererFeature : ulong
{
    None = 0,
    SmoothScenes = 1UL << 0,
    SpriteBatches = 1UL << 1,
    ResolvedView = 1UL << 3,
    AbortFrame = 1UL << 4,
    Path2D = 1UL << 6,
    HdrScene = 1UL << 10,
    Bloom = 1UL << 11,
    MapLabels = 1UL << 17,
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererApiInfo
{
    internal uint StructSize;
    internal ushort AbiMajor;
    internal ushort AbiMinor;
    internal EngineRendererFeature FeatureBits;
    internal uint RequiredSessionConfigSize;
    internal uint RequiredTextureDescriptionSize;
    internal uint RequiredSceneDescriptionSize;
    internal uint RequiredSpriteDrawSize;
    internal uint MaximumBatchStride;
    internal uint Reserved32;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererSessionConfig
{
    internal uint StructSize;
    internal ushort AbiMajor;
    internal ushort AbiMinor;
    internal uint Flags;
    internal uint VerticalSync;
    internal ulong NativeWindow;
    internal uint LogicalWidth;
    internal uint LogicalHeight;
    internal uint MaximumSpritesPerFrame;
    internal uint MaximumTextureResources;
    internal uint FramesInFlight;
    internal uint ScaleMode;
    internal uint SmallWindowPolicy;
    internal uint Reserved32;
    internal float LetterboxRed;
    internal float LetterboxGreen;
    internal float LetterboxBlue;
    internal float LetterboxAlpha;
    internal ulong Reserved0;
    internal ulong Reserved1;
    internal ulong Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererTextureDescription
{
    internal uint StructSize;
    internal uint Flags;
    internal uint Width;
    internal uint Height;
    internal uint MipCount;
    internal uint Format;
    internal uint Filter;
    internal uint Srgb;
    internal uint GenerateMipmaps;
    internal uint Reserved0;
    internal uint Reserved1;
    internal uint Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererFrameDescription
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong SnapshotRevision;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererCamera
{
    internal uint StructSize;
    internal uint Flags;
    internal float PositionX;
    internal float PositionY;
    internal float RotationRadians;
    internal float PixelsPerWorldUnit;
    internal float Zoom;
    internal uint IntegerZoom;
    internal uint PixelPerfect;
    internal uint Reserved0;
    internal uint Reserved1;
    internal uint Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererSceneDescription
{
    internal uint StructSize;
    internal uint Flags;
    internal EngineRendererCamera Camera;
    internal uint RasterProfile;
    internal uint TargetSizeMode;
    internal uint ExplicitTargetWidth;
    internal uint ExplicitTargetHeight;
    internal float ClearRed;
    internal float ClearGreen;
    internal float ClearBlue;
    internal float ClearAlpha;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererSpriteDraw
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong Texture;
    internal ulong StablePresentationId;
    internal ulong LocalSequence;
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
    internal float ColorRed;
    internal float ColorGreen;
    internal float ColorBlue;
    internal float ColorAlpha;
    internal int Layer;
    internal int Order;
    internal uint Blend;
    internal uint FlipX;
    internal uint FlipY;
    internal uint PixelSnap;
    internal uint Reserved32;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererSpriteDrawHdr
{
    internal EngineRendererSpriteDraw Base;
    internal float EmissionRed;
    internal float EmissionGreen;
    internal float EmissionBlue;
    internal float EmissionIntensity;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererSpriteBatch
{
    internal uint StructSize;
    internal uint Flags;
    internal nint Records;
    internal uint RecordCount;
    internal uint RecordStride;
    internal uint Reserved0;
    internal uint Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererBatchResult
{
    internal uint StructSize;
    internal uint Flags;
    internal uint InputCount;
    internal uint AcceptedCount;
    internal uint RejectedCount;
    internal uint CapacityExhausted;
    internal uint Reserved0;
    internal uint Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererResolvedView
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong FrameRevision;
    internal ulong ViewRevision;
    internal ulong SnapshotRevision;
    internal uint TargetWidth;
    internal uint TargetHeight;
    internal uint DrawableWidth;
    internal uint DrawableHeight;
    internal float DpiScaleX;
    internal float DpiScaleY;
    internal float ViewportX;
    internal float ViewportY;
    internal float ViewportWidth;
    internal float ViewportHeight;
    internal EngineRendererCamera Camera;
    internal float WorldToTargetM11;
    internal float WorldToTargetM12;
    internal float WorldToTargetTx;
    internal float WorldToTargetM21;
    internal float WorldToTargetM22;
    internal float WorldToTargetTy;
    internal float TargetToWorldM11;
    internal float TargetToWorldM12;
    internal float TargetToWorldTx;
    internal float TargetToWorldM21;
    internal float TargetToWorldM22;
    internal float TargetToWorldTy;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererFrameReport
{
    internal uint StructSize;
    internal uint State;
    internal ulong FrameRevision;
    internal ulong PresentedFrameRevision;
    internal ulong ViewRevision;
    internal ulong SnapshotRevision;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPoint
{
    internal float X;
    internal float Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathGeometry
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong StableId;
    internal ulong GeometryRevision;
    internal nint Points;
    internal uint PointCount;
    internal uint Primitive;
    internal uint Reserved0;
    internal uint Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathStyle
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong StyleRevision;
    internal float Width;
    internal uint WidthUnits;
    internal uint Join;
    internal uint Cap;
    internal float DashOnLength;
    internal float DashOffLength;
    internal float DashPhase;
    internal uint DashUnits;
    internal float GradientStartRed;
    internal float GradientStartGreen;
    internal float GradientStartBlue;
    internal float GradientStartAlpha;
    internal float GradientEndRed;
    internal float GradientEndGreen;
    internal float GradientEndBlue;
    internal float GradientEndAlpha;
    internal uint Blend;
    internal uint Reserved0;
    internal uint Reserved1;
    internal uint Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathDraw
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong Path;
    internal ulong StablePresentationId;
    internal ulong LocalSequence;
    internal float TranslationX;
    internal float TranslationY;
    internal float ClipLeft;
    internal float ClipTop;
    internal float ClipRight;
    internal float ClipBottom;
    internal EngineRendererPathStyle MainStyle;
    internal EngineRendererPathStyle BaseStyle;
    internal EngineRendererPathStyle HaloStyle;
    internal int Layer;
    internal int Order;
    internal float FlowOffset;
    internal float PickPadding;
    internal uint BaseEnabled;
    internal uint HaloEnabled;
    internal uint ClipEnabled;
    internal uint Reserved32;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathEmission
{
    internal float Red;
    internal float Green;
    internal float Blue;
    internal float Intensity;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathDrawHdr
{
    internal EngineRendererPathDraw Base;
    internal EngineRendererPathEmission MainEmission;
    internal EngineRendererPathEmission BaseEmission;
    internal EngineRendererPathEmission HaloEmission;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPathBatch
{
    internal uint StructSize;
    internal uint Flags;
    internal nint Records;
    internal uint RecordCount;
    internal uint RecordStride;
    internal uint Reserved0;
    internal uint Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererPostProcessProfile
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong ColorGradeLut;
    internal float Exposure;
    internal float BloomThreshold;
    internal float BloomSoftKnee;
    internal float BloomRadius;
    internal float BloomIntensity;
    internal uint MaximumBloomLevels;
    internal uint ToneMapOperator;
    internal uint BloomEnabled;
    internal uint Reserved32;
    internal ulong Reserved0;
    internal ulong Reserved1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererFontDescription
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong Revision;
    internal uint FaceIndex;
    internal uint PixelSize;
    internal uint RasterMode;
    internal uint Hinting;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererTextStyle
{
    internal uint StructSize;
    internal uint Flags;
    internal EngineRendererFontHandles Fonts;
    internal nint LocaleUtf8;
    internal uint LocaleBytes;
    internal uint Direction;
    internal uint ScriptTag;
    internal uint Reserved;
}

[InlineArray(8)]
internal struct EngineRendererFontHandles
{
    private ulong element;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererTextLayoutDescription
{
    internal uint StructSize;
    internal uint Flags;
    internal nint Utf8;
    internal uint ByteCount;
    internal uint SpanCount;
    internal nint Spans;
    internal EngineRendererTextStyle Style;
    internal ulong Revision;
    internal float MaximumWidth;
    internal uint MaximumLines;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererTextMetrics
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong Revision;
    internal float Width;
    internal float Height;
    internal float InkX;
    internal float InkY;
    internal float InkWidth;
    internal float InkHeight;
    internal uint GlyphCount;
    internal uint PrepareState;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererLabelDraw
{
    internal uint StructSize;
    internal uint Flags;
    internal ulong StableId;
    internal ulong Layout;
    internal ulong IconTexture;
    internal float WorldX;
    internal float WorldY;
    internal EngineRendererFloat16 Offsets;
    internal uint OffsetCount;
    internal int Priority;
    internal uint CollisionGroup;
    internal uint Reserved;
    internal float MinimumZoom;
    internal float MaximumZoom;
    internal EngineRendererFloat4 Color;
    internal EngineRendererFloat4 PlateColor;
    internal EngineRendererFloat4 EffectColor;
    internal float PlatePadding;
    internal float OutlinePixels;
    internal float ShadowX;
    internal float ShadowY;
    internal EngineRendererFloat4 IconRect;
    internal EngineRendererUInt4 IconSource;
}

[InlineArray(16)]
internal struct EngineRendererFloat16
{
    private float element;
}

[InlineArray(4)]
internal struct EngineRendererFloat4
{
    private float element;
}

[InlineArray(4)]
internal struct EngineRendererUInt4
{
    private uint element;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererLabelBatch
{
    internal uint StructSize;
    internal uint Flags;
    internal nint Records;
    internal uint RecordCount;
    internal uint RecordStride;
    internal nint Obstacles;
    internal uint ObstacleCount;
    internal uint ObstacleStride;
    internal ulong Tick;
    internal ulong CandidatesRevision;
    internal ulong ObstaclesRevision;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineRendererLabelDiagnostics
{
    internal uint StructSize;
    internal uint Flags;
    internal uint Candidates;
    internal uint Displayed;
    internal uint Culled;
    internal uint Collided;
    internal uint PendingGlyphs;
    internal uint CapacityRejected;
    internal uint Quads;
    internal uint CellReferences;
    internal uint HistoryEntries;
    internal uint CandidateHighWater;
    internal uint ReferenceHighWater;
    internal uint PlacementReused;
    internal ulong PlacementMicroseconds;
    internal ulong LayoutCacheHits;
    internal ulong LayoutCacheMisses;
    internal ulong LayoutCacheBytes;
    internal ulong GlyphMisses;
    internal uint QueuedGlyphs;
    internal uint LiveLayouts;
    internal ulong LayoutCacheHighWater;
    internal ulong AtlasMisses;
}

internal static class SpriteForgeNative
{
    private const string LibraryName = "SpriteForge.dll";

    static SpriteForgeNative()
    {
        VerifyLayout<EngineAudioConfig>(64);
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
        VerifyLayout<EngineRendererApiInfo>(56);
        VerifyLayout<EngineRendererSessionConfig>(96);
        VerifyLayout<EngineRendererTextureDescription>(48);
        VerifyLayout<EngineRendererFrameDescription>(32);
        VerifyLayout<EngineRendererCamera>(48);
        VerifyLayout<EngineRendererSceneDescription>(104);
        VerifyLayout<EngineRendererSpriteDraw>(128);
        VerifyLayout<EngineRendererSpriteDrawHdr>(144);
        VerifyLayout<EngineRendererSpriteBatch>(32);
        VerifyLayout<EngineRendererBatchResult>(32);
        VerifyLayout<EngineRendererResolvedView>(184);
        VerifyLayout<EngineRendererFrameReport>(56);
        VerifyLayout<EngineRendererPoint>(8);
        VerifyLayout<EngineRendererPathGeometry>(48);
        VerifyLayout<EngineRendererPathStyle>(96);
        VerifyLayout<EngineRendererPathDraw>(376);
        VerifyLayout<EngineRendererPathEmission>(16);
        VerifyLayout<EngineRendererPathDrawHdr>(424);
        VerifyLayout<EngineRendererPathBatch>(32);
        VerifyLayout<EngineRendererPostProcessProfile>(72);
        VerifyLayout<EngineRendererFontDescription>(32);
        VerifyLayout<EngineRendererTextStyle>(96);
        VerifyLayout<EngineRendererTextLayoutDescription>(144);
        VerifyLayout<EngineRendererTextMetrics>(48);
        VerifyLayout<EngineRendererLabelDraw>(224);
        VerifyLayout<EngineRendererLabelBatch>(64);
        VerifyLayout<EngineRendererLabelDiagnostics>(120);
    }

    private static void VerifyLayout<T>(int expected) where T : struct
    {
        int actual = Marshal.SizeOf<T>();
        if (actual != expected)
        {
            throw new TypeLoadException(
                $"SpriteForge interop layout '{typeof(T).Name}' is {actual} bytes; expected {expected}.");
        }
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_GetDefaultAudioConfig(out EngineAudioConfig config);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_CreateAudio(
        in EngineAudioConfig config,
        out nint audio);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SpriteForge_DestroyAudio(nint audio);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_AudioUpdate(nint audio);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_AudioSetBusGain(
        nint audio,
        EngineAudioBus bus,
        float gain,
        float rampSeconds);

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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_GetApiInfo(
        ref EngineRendererApiInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_CreateSession(
        in EngineRendererSessionConfig config,
        out nint session);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_DestroySession(nint session);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_CreateTexture(
        nint session,
        in EngineRendererTextureDescription description,
        nint pixels,
        ulong sizeBytes,
        out ulong texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_DestroyTexture(
        nint session,
        ulong texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_CreateFont(
        nint session,
        in EngineRendererFontDescription description,
        nint bytes,
        ulong byteCount,
        out ulong font);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_DestroyFont(
        nint session,
        ulong font);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_CreateTextLayout(
        nint session,
        in EngineRendererTextLayoutDescription description,
        out ulong layout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_DestroyTextLayout(
        nint session,
        ulong layout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_GetTextMetrics(
        nint session,
        ulong layout,
        ref EngineRendererTextMetrics metrics);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_UpdateText(nint session);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_SubmitLabels(
        nint session,
        in EngineRendererLabelBatch batch,
        ref EngineRendererLabelDiagnostics diagnostics);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_ResetLabels(nint session);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_CreatePath(
        nint session,
        in EngineRendererPathGeometry geometry,
        out ulong path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_DestroyPath(
        nint session,
        ulong path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_SetPostProcessProfile(
        nint session,
        in EngineRendererPostProcessProfile profile);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_BeginFrame(
        nint session,
        in EngineRendererFrameDescription description,
        ref EngineRendererFrameReport report);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_BeginScene(
        nint session,
        in EngineRendererSceneDescription description,
        ref EngineRendererResolvedView view);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_SubmitPaths(
        nint session,
        in EngineRendererPathBatch batch,
        ref EngineRendererBatchResult result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_SubmitSprites(
        nint session,
        in EngineRendererSpriteBatch batch,
        ref EngineRendererBatchResult result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_EndScene(nint session);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_EndFrame(
        nint session,
        ref EngineRendererFrameReport report);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_Present(
        nint session,
        ref EngineRendererFrameReport report);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern EngineStatus SpriteForge_RendererV2_AbortFrame(
        nint session,
        ref EngineRendererFrameReport report);
}
