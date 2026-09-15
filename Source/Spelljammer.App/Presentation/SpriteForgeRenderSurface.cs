using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;
using Spelljammer.Interop;

namespace Spelljammer.Presentation;

internal enum SpriteForgeTextAlignment : byte
{
    Left,
    Center,
    Right,
}

internal enum SpriteForgeBlendMode : uint
{
    Opaque = 0,
    PremultipliedAlpha = 1,
    Additive = 2,
}

internal readonly record struct SpriteForgeColor(float Red, float Green, float Blue, float Alpha)
{
    internal static SpriteForgeColor FromSrgb(byte red, byte green, byte blue, byte alpha = byte.MaxValue)
    {
        float a = alpha / 255.0f;
        return new SpriteForgeColor(red / 255.0f * a, green / 255.0f * a, blue / 255.0f * a, a);
    }

    internal static SpriteForgeColor Parse(string value)
    {
        string hex = value.TrimStart('#');
        uint packed = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte alpha = hex.Length == 8 ? (byte)(packed >> 24) : byte.MaxValue;
        byte red = hex.Length == 8 ? (byte)(packed >> 16) : (byte)(packed >> 16);
        byte green = (byte)(packed >> 8);
        byte blue = (byte)packed;
        return FromSrgb(red, green, blue, alpha);
    }
}

internal sealed class SpriteForgeCanvas
{
    private readonly SpriteForgeRenderSurface owner;
    private readonly List<EngineRendererSpriteDraw> sprites = [];
    private readonly List<PendingText> text = [];
    private ulong sequence;

    internal SpriteForgeCanvas(SpriteForgeRenderSurface owner)
    {
        this.owner = owner;
    }

    internal IReadOnlyList<EngineRendererSpriteDraw> Sprites => sprites;

    internal IReadOnlyList<PendingText> Text => text;

    internal void Fill(float x, float y, float width, float height, string color, int layer = 0) =>
        Sprite(owner.WhiteTexture, 1, 1, x, y, width, height, SpriteForgeColor.Parse(color), layer);

    internal void Border(float x, float y, float width, float height, float thickness, string color, int layer = 1)
    {
        Fill(x, y, width, thickness, color, layer);
        Fill(x, y + height - thickness, width, thickness, color, layer);
        Fill(x, y + thickness, thickness, Math.Max(0, height - thickness * 2), color, layer);
        Fill(x + width - thickness, y + thickness, thickness, Math.Max(0, height - thickness * 2), color, layer);
    }

    internal void Line(float x1, float y1, float x2, float y2, float thickness, string color, int layer = 1)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length <= 0)
        {
            return;
        }

        Sprite(owner.WhiteTexture, 1, 1, x1, y1 - thickness / 2, length, thickness,
            SpriteForgeColor.Parse(color), layer, -MathF.Atan2(dy, dx));
    }

    internal void Circle(float centerX, float centerY, float diameter, string color, int layer = 2) =>
        Sprite(owner.CircleTexture, 64, 64, centerX - diameter / 2, centerY - diameter / 2,
            diameter / 64, diameter / 64, SpriteForgeColor.Parse(color), layer);

    internal void Circle(float centerX, float centerY, float diameter, SpriteForgeColor color, int layer = 2) =>
        Sprite(owner.CircleTexture, 64, 64, centerX - diameter / 2, centerY - diameter / 2,
            diameter / 64, diameter / 64, color, layer, pixelSnap: false);

    internal void Glow(
        float centerX,
        float centerY,
        float width,
        float height,
        SpriteForgeColor color,
        int layer = -1) =>
        Sprite(owner.GlowTexture, 64, 64, centerX - width / 2, centerY - height / 2,
            width / 64, height / 64, color, layer, blend: SpriteForgeBlendMode.Additive, pixelSnap: false);

    internal void Ellipse(
        float centerX,
        float centerY,
        float width,
        float height,
        string color,
        int layer = 2) =>
        Sprite(owner.CircleTexture, 64, 64, centerX - width / 2, centerY - height / 2,
            width / 64, height / 64, SpriteForgeColor.Parse(color), layer);

    internal void ImageCover(string resourceUri, float x, float y, float width, float height, int layer = -10)
    {
        SpriteForgeTexture texture = owner.GetTexture(resourceUri);
        float scale = Math.Max(width / texture.Width, height / texture.Height);
        float drawWidth = texture.Width * scale;
        float drawHeight = texture.Height * scale;
        Sprite(texture.Handle, texture.Width, texture.Height,
            x + (width - drawWidth) / 2, y + (height - drawHeight) / 2,
            scale, scale, new SpriteForgeColor(1, 1, 1, 1), layer);
    }

    internal void TextLine(
        string value,
        float x,
        float y,
        float width,
        float height,
        uint size,
        string color,
        SpriteForgeTextAlignment alignment = SpriteForgeTextAlignment.Left,
        int layer = 100) =>
        text.Add(new PendingText(value, x, y, width, height, size, SpriteForgeColor.Parse(color), alignment, layer));

    private void Sprite(
        ulong texture,
        uint sourceWidth,
        uint sourceHeight,
        float x,
        float y,
        float scaleX,
        float scaleY,
        SpriteForgeColor color,
        int layer,
        float rotation = 0,
        SpriteForgeBlendMode blend = SpriteForgeBlendMode.PremultipliedAlpha,
        bool pixelSnap = true)
    {
        ulong id = ++sequence;
        sprites.Add(new EngineRendererSpriteDraw {
            StructSize = checked((uint)Marshal.SizeOf<EngineRendererSpriteDraw>()),
            Texture = texture,
            StablePresentationId = id,
            LocalSequence = id,
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight,
            UntrimmedWidth = sourceWidth,
            UntrimmedHeight = sourceHeight,
            PositionX = x - owner.LogicalWidth / 2,
            PositionY = owner.LogicalHeight / 2 - y,
            ScaleX = scaleX,
            ScaleY = scaleY,
            RotationRadians = rotation,
            ColorRed = color.Red,
            ColorGreen = color.Green,
            ColorBlue = color.Blue,
            ColorAlpha = color.Alpha,
            Layer = layer,
            Order = checked((int)id),
            Blend = (uint)blend,
            PixelSnap = pixelSnap ? 1u : 0u,
        });
    }

    internal readonly record struct PendingText(
        string Value,
        float X,
        float Y,
        float Width,
        float Height,
        uint Size,
        SpriteForgeColor Color,
        SpriteForgeTextAlignment Alignment,
        int Layer);
}

internal readonly record struct SpriteForgeTexture(ulong Handle, uint Width, uint Height);

/// <summary>
/// Hosts one SpriteForge renderer session. WPF owns only the parent/child HWND lifecycle;
/// every visible pixel is submitted through the SpriteForge renderer ABI.
/// </summary>
internal abstract class SpriteForgeRenderSurface : HwndHost
{
    private const ushort RendererAbiMajor = 2;
    private const ushort RendererAbiMinor = 6;
    private const uint WindowStyleChild = 0x40000000;
    private const uint WindowStyleVisible = 0x10000000;
    private const uint WindowStyleClipSiblings = 0x04000000;
    private const uint StaticStyleNotify = 0x00000100;
    private const int WmMouseMove = 0x0200;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmKeyDown = 0x0100;
    private const int WmChar = 0x0102;
    private const int VkEscape = 0x1B;
    private const int VkReturn = 0x0D;
    private const ulong MapLabelsFeature = 1UL << 17;

    private readonly Dictionary<string, SpriteForgeTexture> textures = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, ulong> fonts = [];
    private readonly Dictionary<TextLayoutKey, TextLayoutResource> layouts = [];
    private nint childWindow;
    private nint session;
    private ulong whiteTexture;
    private ulong circleTexture;
    private ulong glowTexture;
    private ulong frameRevision = 1;
    private bool renderQueued;
    private bool disposing;

    protected SpriteForgeRenderSurface(uint logicalWidth, uint logicalHeight, CultureInfo culture)
    {
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        Culture = culture;
        Focusable = true;
        SizeChanged += Surface_SizeChanged;
    }

    internal uint LogicalWidth { get; }

    internal uint LogicalHeight { get; }

    protected CultureInfo Culture { get; private set; }

    internal ulong WhiteTexture => whiteTexture;

    internal ulong CircleTexture => circleTexture;

    internal ulong GlowTexture => glowTexture;

    protected abstract void Compose(SpriteForgeCanvas canvas);

    protected virtual void PointerMoved(float x, float y)
    {
    }

    protected virtual void PointerPressed(float x, float y)
    {
    }

    protected virtual void PointerReleased(float x, float y)
    {
    }

    protected virtual void KeyPressed(int virtualKey)
    {
    }

    protected virtual void TextEntered(char character)
    {
    }

    protected void RefreshSurface() => QueueRender();

    protected void Dispatch(Action action) => _ = Dispatcher.BeginInvoke(action, DispatcherPriority.Input);

    protected void ResetText(CultureInfo culture)
    {
        Culture = culture;
        DestroyTextResources();
        frameRevision++;
        QueueRender();
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        childWindow = CreateWindowEx(
            0,
            "static",
            null,
            WindowStyleChild | WindowStyleVisible | WindowStyleClipSiblings | StaticStyleNotify,
            0,
            0,
            1,
            1,
            hwndParent.Handle,
            0,
            0,
            0);
        if (childWindow == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the SpriteForge display surface.");
        }

        InitializeRenderer();
        QueueRender();
        return new HandleRef(this, childWindow);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        DisposeRenderer();
        if (hwnd.Handle != 0)
        {
            _ = DestroyWindow(hwnd.Handle);
        }

        childWindow = 0;
    }

    protected override nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmMouseMove:
                if (TryMapPointer(lParam, out float moveX, out float moveY))
                {
                    PointerMoved(moveX, moveY);
                }

                break;
            case WmLeftButtonDown:
                _ = SetFocus(hwnd);
                _ = SetCapture(hwnd);
                if (TryMapPointer(lParam, out float downX, out float downY))
                {
                    PointerPressed(downX, downY);
                }

                handled = true;
                break;
            case WmLeftButtonUp:
                _ = ReleaseCapture();
                if (TryMapPointer(lParam, out float upX, out float upY))
                {
                    PointerReleased(upX, upY);
                }

                handled = true;
                break;
            case WmKeyDown:
                int key = unchecked((int)wParam);
                if (key is VkEscape or VkReturn)
                {
                    KeyPressed(key);
                    handled = true;
                }

                break;
            case WmChar:
                TextEntered(unchecked((char)(int)wParam));
                handled = true;
                break;
        }

        return 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.disposing)
        {
            this.disposing = true;
            SizeChanged -= Surface_SizeChanged;
        }

        base.Dispose(disposing);
    }

    internal SpriteForgeTexture GetTexture(string resourceUri)
    {
        if (textures.TryGetValue(resourceUri, out SpriteForgeTexture cached))
        {
            return cached;
        }

        StreamResourceInfo resource = Application.GetResourceStream(new Uri(resourceUri, UriKind.Absolute))
            ?? throw new InvalidOperationException($"Application texture '{resourceUri}' was not found.");
        using Stream stream = resource.Stream;
        BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        BitmapSource source = frame.Format == PixelFormats.Pbgra32
            ? frame
            : new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);
        int stride = checked(source.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        SpriteForgeTexture texture = CreateTexture(
            pixels,
            checked((uint)source.PixelWidth),
            checked((uint)source.PixelHeight),
            format: 3,
            filter: 1,
            srgb: 1);
        textures.Add(resourceUri, texture);
        return texture;
    }

    private void InitializeRenderer()
    {
        EngineRendererApiInfo info = new() { StructSize = SizeOf<EngineRendererApiInfo>() };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_GetApiInfo(ref info), "query renderer ABI");
        if (info.AbiMajor != RendererAbiMajor || info.AbiMinor < RendererAbiMinor ||
            ((ulong)info.FeatureBits & MapLabelsFeature) == 0)
        {
            throw new NotSupportedException(
                $"SpriteForge renderer ABI {RendererAbiMajor}.{RendererAbiMinor} with text labels is required; " +
                $"the loaded engine provides {info.AbiMajor}.{info.AbiMinor}.");
        }

        EngineRendererSessionConfig config = new() {
            StructSize = SizeOf<EngineRendererSessionConfig>(),
            AbiMajor = RendererAbiMajor,
            AbiMinor = RendererAbiMinor,
            VerticalSync = 1,
            NativeWindow = checked((ulong)childWindow),
            LogicalWidth = LogicalWidth,
            LogicalHeight = LogicalHeight,
            MaximumSpritesPerFrame = 32768,
            MaximumTextureResources = 32,
            FramesInFlight = 2,
            ScaleMode = 1,
            SmallWindowPolicy = 0,
            LetterboxRed = 3.0f / 255.0f,
            LetterboxGreen = 6.0f / 255.0f,
            LetterboxBlue = 14.0f / 255.0f,
            LetterboxAlpha = 1,
        };
        ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_CreateSession(in config, out session),
            "create display renderer session");

        whiteTexture = CreateTexture([255, 255, 255, 255], 1, 1, 2, 0, 0).Handle;
        circleTexture = CreateTexture(BuildCirclePixels(), 64, 64, 2, 1, 0).Handle;
        glowTexture = CreateTexture(BuildGlowPixels(), 64, 64, 2, 1, 0).Handle;
    }

    private SpriteForgeTexture CreateTexture(byte[] pixels, uint width, uint height, uint format, uint filter, uint srgb)
    {
        EngineRendererTextureDescription description = new() {
            StructSize = SizeOf<EngineRendererTextureDescription>(),
            Width = width,
            Height = height,
            MipCount = 1,
            Format = format,
            Filter = filter,
            Srgb = srgb,
        };
        GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_CreateTexture(
                session, in description, pinned.AddrOfPinnedObject(), checked((ulong)pixels.Length), out ulong texture),
                "create display texture");
            return new SpriteForgeTexture(texture, width, height);
        }
        finally
        {
            pinned.Free();
        }
    }

    private void Render()
    {
        renderQueued = false;
        if (session == 0 || childWindow == 0 || ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        SpriteForgeCanvas canvas = new(this);
        Compose(canvas);
        if (layouts.Count + canvas.Text.Count > 1024)
        {
            DestroyTextResources();
        }

        List<PreparedText> preparedText = PrepareText(canvas.Text);
        for (int index = 0; index < 8 && preparedText.Any(static item => item.Metrics.PrepareState != 2); ++index)
        {
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_UpdateText(session), "prepare display text");
            RefreshTextMetrics(preparedText);
        }

        EngineRendererFrameDescription frame = new() {
            StructSize = SizeOf<EngineRendererFrameDescription>(),
            SnapshotRevision = frameRevision++,
        };
        EngineRendererFrameReport report = new() { StructSize = SizeOf<EngineRendererFrameReport>() };
        EngineStatus begin = SpriteForgeNative.SpriteForge_RendererV2_BeginFrame(session, in frame, ref report);
        if (begin == EngineStatus.SkipFrame)
        {
            QueueRender();
            return;
        }

        ThrowIfFailed(begin, "begin display frame");
        bool frameOpen = true;
        try
        {
            EngineRendererSceneDescription scene = new() {
                StructSize = SizeOf<EngineRendererSceneDescription>(),
                Camera = new EngineRendererCamera {
                    StructSize = SizeOf<EngineRendererCamera>(),
                    PixelsPerWorldUnit = 1,
                    Zoom = 1,
                    IntegerZoom = 1,
                },
                RasterProfile = 1,
                TargetSizeMode = 0,
                ClearRed = 3.0f / 255.0f,
                ClearGreen = 6.0f / 255.0f,
                ClearBlue = 14.0f / 255.0f,
                ClearAlpha = 1,
            };
            EngineRendererResolvedView view = new() { StructSize = SizeOf<EngineRendererResolvedView>() };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_BeginScene(session, in scene, ref view),
                "begin display scene");
            SubmitSprites(canvas.Sprites);
            SubmitText(preparedText);
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_EndScene(session), "end display scene");
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_EndFrame(session, ref report), "end display frame");
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_Present(session, ref report), "present display frame");
            frameOpen = false;
        }
        finally
        {
            if (frameOpen)
            {
                report.StructSize = SizeOf<EngineRendererFrameReport>();
                _ = SpriteForgeNative.SpriteForge_RendererV2_AbortFrame(session, ref report);
            }
        }

        if (preparedText.Any(static item => item.Metrics.PrepareState != 2))
        {
            QueueRender();
        }
    }

    private List<PreparedText> PrepareText(IReadOnlyList<SpriteForgeCanvas.PendingText> pending)
    {
        List<PreparedText> prepared = new(pending.Count);
        foreach (SpriteForgeCanvas.PendingText item in pending)
        {
            TextLayoutResource resource = GetTextLayout(item.Value, item.Size, item.Width);
            prepared.Add(new PreparedText(item, resource.Handle, resource.Metrics));
        }

        return prepared;
    }

    private TextLayoutResource GetTextLayout(string text, uint size, float maximumWidth)
    {
        TextLayoutKey key = new(text, size, MathF.Round(maximumWidth));
        if (layouts.TryGetValue(key, out TextLayoutResource cached))
        {
            return cached;
        }

        ulong font = GetFont(size);
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        byte[] locale = Encoding.UTF8.GetBytes(Culture.Name);
        GCHandle textPin = GCHandle.Alloc(utf8, GCHandleType.Pinned);
        GCHandle localePin = GCHandle.Alloc(locale, GCHandleType.Pinned);
        try
        {
            EngineRendererFontHandles fontHandles = default;
            fontHandles[0] = font;
            EngineRendererTextStyle style = new() {
                StructSize = SizeOf<EngineRendererTextStyle>(),
                Fonts = fontHandles,
                LocaleUtf8 = localePin.AddrOfPinnedObject(),
                LocaleBytes = checked((uint)locale.Length),
            };
            EngineRendererTextLayoutDescription description = new() {
                StructSize = SizeOf<EngineRendererTextLayoutDescription>(),
                Utf8 = textPin.AddrOfPinnedObject(),
                ByteCount = checked((uint)utf8.Length),
                Style = style,
                Revision = StableId($"{Culture.Name}:{size}:{maximumWidth}:{text}"),
                MaximumWidth = maximumWidth,
                MaximumLines = 1,
            };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_CreateTextLayout(
                session, in description, out ulong layout), "create display text layout");
            EngineRendererTextMetrics metrics = new() { StructSize = SizeOf<EngineRendererTextMetrics>() };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_GetTextMetrics(session, layout, ref metrics),
                "query display text metrics");
            TextLayoutResource resource = new(layout, metrics);
            layouts.Add(key, resource);
            return resource;
        }
        finally
        {
            localePin.Free();
            textPin.Free();
        }
    }

    private ulong GetFont(uint size)
    {
        if (fonts.TryGetValue(size, out ulong cached))
        {
            return cached;
        }

        string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates = Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? ["msjh.ttc", "msjhbd.ttc", "segoeui.ttf"]
            : ["segoeui.ttf", "arial.ttf"];
        string? path = candidates.Select(name => Path.Combine(fontsDirectory, name)).FirstOrDefault(File.Exists);
        if (path is null)
        {
            throw new FileNotFoundException("No supported system font is available for SpriteForge text rendering.");
        }

        byte[] bytes = File.ReadAllBytes(path);
        GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            EngineRendererFontDescription description = new() {
                StructSize = SizeOf<EngineRendererFontDescription>(),
                Revision = StableId(path),
                PixelSize = size,
                RasterMode = 2,
            };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_CreateFont(
                session, in description, pinned.AddrOfPinnedObject(), checked((ulong)bytes.Length), out ulong font),
                "create display font");
            fonts.Add(size, font);
            return font;
        }
        finally
        {
            pinned.Free();
        }
    }

    private void RefreshTextMetrics(List<PreparedText> prepared)
    {
        for (int index = 0; index < prepared.Count; ++index)
        {
            PreparedText value = prepared[index];
            EngineRendererTextMetrics metrics = new() { StructSize = SizeOf<EngineRendererTextMetrics>() };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_GetTextMetrics(session, value.Layout, ref metrics),
                "refresh display text metrics");
            prepared[index] = value with { Metrics = metrics };
        }
    }

    private void SubmitSprites(IReadOnlyList<EngineRendererSpriteDraw> sprites)
    {
        if (sprites.Count == 0)
        {
            return;
        }

        EngineRendererSpriteDraw[] records = [.. sprites.OrderBy(static value => value.Layer).ThenBy(static value => value.Order)];
        GCHandle pinned = GCHandle.Alloc(records, GCHandleType.Pinned);
        try
        {
            EngineRendererSpriteBatch batch = new() {
                StructSize = SizeOf<EngineRendererSpriteBatch>(),
                Records = pinned.AddrOfPinnedObject(),
                RecordCount = checked((uint)records.Length),
                RecordStride = SizeOf<EngineRendererSpriteDraw>(),
            };
            EngineRendererBatchResult result = new() { StructSize = SizeOf<EngineRendererBatchResult>() };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_SubmitSprites(session, in batch, ref result),
                "submit display sprites");
            if (result.AcceptedCount != result.InputCount)
            {
                throw new InvalidOperationException("SpriteForge rejected part of the display sprite batch.");
            }
        }
        finally
        {
            pinned.Free();
        }
    }

    private void SubmitText(IReadOnlyList<PreparedText> prepared)
    {
        if (prepared.Count == 0)
        {
            return;
        }

        EngineRendererLabelDraw[] labels = new EngineRendererLabelDraw[prepared.Count];
        for (int index = 0; index < prepared.Count; ++index)
        {
            PreparedText value = prepared[index];
            float x = value.Pending.Alignment switch {
                SpriteForgeTextAlignment.Center => value.Pending.X + (value.Pending.Width - value.Metrics.Width) / 2,
                SpriteForgeTextAlignment.Right => value.Pending.X + value.Pending.Width - value.Metrics.Width,
                _ => value.Pending.X,
            };
            float y = value.Pending.Y + Math.Max(0, (value.Pending.Height - value.Metrics.Height) / 2);
            ulong stableId = StableId($"label:{index}:{value.Pending.Value}:{value.Pending.X}:{value.Pending.Y}");
            EngineRendererFloat4 color = default;
            color[0] = value.Pending.Color.Red;
            color[1] = value.Pending.Color.Green;
            color[2] = value.Pending.Color.Blue;
            color[3] = value.Pending.Color.Alpha;
            labels[index] = new EngineRendererLabelDraw {
                StructSize = SizeOf<EngineRendererLabelDraw>(),
                StableId = stableId,
                Layout = value.Layout,
                WorldX = x - LogicalWidth / 2,
                WorldY = LogicalHeight / 2 - y,
                OffsetCount = 1,
                Priority = value.Pending.Layer,
                CollisionGroup = unchecked((uint)stableId) | 1,
                MinimumZoom = 0,
                MaximumZoom = 64,
                Color = color,
            };
        }

        GCHandle pinned = GCHandle.Alloc(labels, GCHandleType.Pinned);
        try
        {
            EngineRendererLabelBatch batch = new() {
                StructSize = SizeOf<EngineRendererLabelBatch>(),
                Records = pinned.AddrOfPinnedObject(),
                RecordCount = checked((uint)labels.Length),
                RecordStride = SizeOf<EngineRendererLabelDraw>(),
                Tick = frameRevision,
                CandidatesRevision = frameRevision,
                ObstaclesRevision = 1,
            };
            EngineRendererLabelDiagnostics diagnostics = new() { StructSize = SizeOf<EngineRendererLabelDiagnostics>() };
            ThrowIfFailed(SpriteForgeNative.SpriteForge_RendererV2_SubmitLabels(session, in batch, ref diagnostics),
                "submit display text");
        }
        finally
        {
            pinned.Free();
        }
    }

    private bool TryMapPointer(nint packed, out float x, out float y)
    {
        int raw = unchecked((int)packed);
        int physicalX = unchecked((short)(raw & 0xFFFF));
        int physicalY = unchecked((short)((raw >> 16) & 0xFFFF));
        if (!GetClientRect(childWindow, out WindowRectangle rectangle))
        {
            x = 0;
            y = 0;
            return false;
        }

        float width = rectangle.Right - rectangle.Left;
        float height = rectangle.Bottom - rectangle.Top;
        float scale = Math.Min(width / LogicalWidth, height / LogicalHeight);
        float viewportWidth = LogicalWidth * scale;
        float viewportHeight = LogicalHeight * scale;
        float left = (width - viewportWidth) / 2;
        float top = (height - viewportHeight) / 2;
        x = (physicalX - left) / scale;
        y = (physicalY - top) / scale;
        return scale > 0 && x >= 0 && x <= LogicalWidth && y >= 0 && y <= LogicalHeight;
    }

    private void QueueRender()
    {
        if (renderQueued || disposing)
        {
            return;
        }

        renderQueued = true;
        _ = Dispatcher.BeginInvoke(Render, DispatcherPriority.Render);
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => QueueRender();

    private void DisposeRenderer()
    {
        if (session == 0)
        {
            return;
        }

        DestroyTextResources();
        foreach (SpriteForgeTexture texture in textures.Values)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyTexture(session, texture.Handle);
        }

        textures.Clear();
        if (glowTexture != 0)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyTexture(session, glowTexture);
            glowTexture = 0;
        }

        if (circleTexture != 0)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyTexture(session, circleTexture);
            circleTexture = 0;
        }

        if (whiteTexture != 0)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyTexture(session, whiteTexture);
            whiteTexture = 0;
        }

        _ = SpriteForgeNative.SpriteForge_RendererV2_DestroySession(session);
        session = 0;
    }

    private void DestroyTextResources()
    {
        if (session == 0)
        {
            layouts.Clear();
            fonts.Clear();
            return;
        }

        _ = SpriteForgeNative.SpriteForge_RendererV2_ResetLabels(session);
        foreach (TextLayoutResource layout in layouts.Values)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyTextLayout(session, layout.Handle);
        }

        layouts.Clear();
        foreach (ulong font in fonts.Values)
        {
            _ = SpriteForgeNative.SpriteForge_RendererV2_DestroyFont(session, font);
        }

        fonts.Clear();
    }

    private static byte[] BuildCirclePixels()
    {
        byte[] pixels = new byte[64 * 64 * 4];
        for (int y = 0; y < 64; ++y)
        {
            for (int x = 0; x < 64; ++x)
            {
                float dx = x + 0.5f - 32;
                float dy = y + 0.5f - 32;
                float alpha = Math.Clamp(32 - MathF.Sqrt(dx * dx + dy * dy), 0, 1);
                int offset = (y * 64 + x) * 4;
                byte coverage = checked((byte)MathF.Round(alpha * 255));
                pixels[offset] = coverage;
                pixels[offset + 1] = coverage;
                pixels[offset + 2] = coverage;
                pixels[offset + 3] = coverage;
            }
        }

        return pixels;
    }

    private static byte[] BuildGlowPixels()
    {
        byte[] pixels = new byte[64 * 64 * 4];
        for (int y = 0; y < 64; ++y)
        {
            for (int x = 0; x < 64; ++x)
            {
                float dx = (x + 0.5f - 32) / 32;
                float dy = (y + 0.5f - 32) / 32;
                float distanceSquared = dx * dx + dy * dy;
                float alpha = MathF.Exp(-distanceSquared * 4.5f) * Math.Clamp(1 - distanceSquared, 0, 1);
                int offset = (y * 64 + x) * 4;
                byte coverage = checked((byte)MathF.Round(alpha * 255));
                pixels[offset] = coverage;
                pixels[offset + 1] = coverage;
                pixels[offset + 2] = coverage;
                pixels[offset + 3] = coverage;
            }
        }

        return pixels;
    }

    private static uint SizeOf<T>() where T : struct => checked((uint)Marshal.SizeOf<T>());

    private static ulong StableId(string value)
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

    private readonly record struct TextLayoutKey(string Text, uint Size, float MaximumWidth);
    private readonly record struct TextLayoutResource(ulong Handle, EngineRendererTextMetrics Metrics);
    private readonly record struct PreparedText(
        SpriteForgeCanvas.PendingText Pending,
        ulong Layout,
        EngineRendererTextMetrics Metrics);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out WindowRectangle rectangle);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetCapture(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();
}
