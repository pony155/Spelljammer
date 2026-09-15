using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Spelljammer.Simulation.Galaxy;

namespace Spelljammer.Presentation;

internal sealed class GalaxyMapGeneratorCompletedEventArgs(GalaxyMapSelection selection) : EventArgs
{
    internal GalaxyMapSelection Selection { get; } = selection;
}

/// <summary>
/// SpriteForge-rendered galaxy generator and topology preview.
/// </summary>
internal sealed class GalaxyMapGeneratorView : SpriteForgeRenderSurface
{
    internal const uint SurfaceWidth = 1600;
    internal const uint SurfaceHeight = 900;
    private const string BackgroundUri = "pack://application:,,,/Assets/UI/MainMenu/Background.png";
    private const float PopupX = 402;
    private const float PopupWidth = 200;
    private const float PopupRowHeight = 34;
    private static readonly int[] SupportedSystemCounts = [16, 64, 128, 256, 512, 1_024];
    private static readonly int[] SupportedBarStrengths = [0, 25, 50, 75, 100];
    private static readonly int[] SupportedArmCounts = [2, 3, 4, 5, 6];
    private static readonly GalaxyShape[] SupportedShapes = [GalaxyShape.Spiral, GalaxyShape.Elliptical, GalaxyShape.Ring];
    private static readonly HitBox Shape = new(252, 266, 142, 38, Control.Shape);
    private static readonly HitBox Size = new(252, 310, 142, 38, Control.Size);
    private static readonly HitBox BarStrength = new(252, 354, 142, 38, Control.BarStrength);
    private static readonly HitBox ArmCount = new(252, 398, 142, 38, Control.ArmCount);
    private static readonly HitBox Seed = new(70, 512, 205, 40, Control.Seed);
    private static readonly HitBox Randomize = new(285, 512, 109, 40, Control.Randomize);
    private static readonly HitBox Generate = new(70, 584, 324, 42, Control.Generate);
    private static readonly HitBox Back = new(42, 828, 180, 52, Control.Back);
    private static readonly HitBox Continue = new(1320, 828, 238, 52, Control.Continue);
    private static readonly HitBox[] HitBoxes =
        [Shape, Size, BarStrength, ArmCount, Seed, Randomize, Generate, Back, Continue];

    private readonly GameText strings;
    private GalaxyMapSelection selection;
    private GalaxyState? galaxy;
    private GalaxyVisualField? visualField;
    private string seedText;
    private string status = string.Empty;
    private bool statusIsError;
    private bool seedFocused;
    private Control hovered;
    private Control pressed;
    private ChoicePopup popup;
    private int hoveredOption = -1;
    private int pressedOption = -1;

    internal GalaxyMapGeneratorView(GameText strings, GalaxyMapSelection? initial)
        : base(SurfaceWidth, SurfaceHeight, strings.Culture)
    {
        this.strings = strings;
        selection = initial ?? new GalaxyMapSelection(NewSeed(), new GalaxyGenerationSettings());
        seedText = selection.Seed.ToString(CultureInfo.InvariantCulture);
        GeneratePreview();
    }

    internal event EventHandler<GalaxyMapGeneratorCompletedEventArgs>? Completed;
    internal event EventHandler? CancelRequested;

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.ImageCover(BackgroundUri, 0, 0, SurfaceWidth, SurfaceHeight);
        canvas.Fill(0, 0, SurfaceWidth, SurfaceHeight, "#DE050A14", -9);
        canvas.Fill(0, 0, SurfaceWidth, 104, "#F20A1221", -8);
        canvas.Fill(0, 808, SurfaceWidth, 92, "#F20A1221", -8);
        canvas.Fill(0, 103, SurfaceWidth, 1, "#FFD7AF70", -7);
        canvas.Fill(0, 808, SurfaceWidth, 1, "#FFD7AF70", -7);
        canvas.TextLine(strings.Get("galaxy.eyebrow"), 54, 14, 800, 26, 12, "#FFD7AF70");
        canvas.TextLine(strings.Get("galaxy.title"), 54, 38, 900, 52, 29, "#FFF2E9D8");

        DrawParameterPanel(canvas);
        DrawPreviewPanel(canvas);
        DrawButton(canvas, Back, strings.Get("galaxy.button.back"));
        DrawButton(canvas, Continue, strings.Get("galaxy.button.continue"), primary: true);
        canvas.TextLine(status, 260, 828, 1020, 52, 13, statusIsError ? "#FFFF8C82" : "#FFAAB8D0",
            SpriteForgeTextAlignment.Center);
    }

    protected override void PointerMoved(float x, float y)
    {
        if (popup != ChoicePopup.None)
        {
            int nextOption = FindPopupOption(x, y);
            if (nextOption != hoveredOption)
            {
                hoveredOption = nextOption;
                RefreshSurface();
            }

            return;
        }

        Control next = Find(x, y);
        if (next != hovered)
        {
            hovered = next;
            RefreshSurface();
        }
    }

    protected override void PointerPressed(float x, float y)
    {
        if (popup != ChoicePopup.None)
        {
            pressedOption = FindPopupOption(x, y);
            RefreshSurface();
            return;
        }

        pressed = Find(x, y);
        seedFocused = pressed == Control.Seed;
        RefreshSurface();
    }

    protected override void PointerReleased(float x, float y)
    {
        if (popup != ChoicePopup.None)
        {
            int releasedOption = FindPopupOption(x, y);
            if (releasedOption >= 0 && releasedOption == pressedOption)
            {
                ApplyPopupOption(releasedOption);
                GeneratePreview();
            }

            popup = ChoicePopup.None;
            hoveredOption = -1;
            pressedOption = -1;
            RefreshSurface();
            return;
        }

        Control released = Find(x, y);
        Control action = released == pressed ? released : Control.None;
        pressed = Control.None;
        switch (action)
        {
            case Control.Shape:
                popup = ChoicePopup.Shape;
                seedFocused = false;
                break;
            case Control.Size:
                popup = ChoicePopup.Size;
                seedFocused = false;
                break;
            case Control.BarStrength:
                popup = ChoicePopup.BarStrength;
                seedFocused = false;
                break;
            case Control.ArmCount:
                popup = ChoicePopup.ArmCount;
                seedFocused = false;
                break;
            case Control.Randomize:
                seedText = NewSeed().ToString(CultureInfo.InvariantCulture);
                GeneratePreview();
                break;
            case Control.Generate:
                GeneratePreview();
                break;
            case Control.Back:
                Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
                break;
            case Control.Continue:
                GeneratePreview();
                if (galaxy is not null)
                {
                    GalaxyMapSelection completed = selection;
                    Dispatch(() => Completed?.Invoke(this, new GalaxyMapGeneratorCompletedEventArgs(completed)));
                }

                break;
        }

        RefreshSurface();
    }

    protected override void KeyPressed(int virtualKey)
    {
        if (virtualKey == 0x1B)
        {
            if (popup != ChoicePopup.None)
            {
                popup = ChoicePopup.None;
                hoveredOption = -1;
                pressedOption = -1;
                RefreshSurface();
            }
            else
            {
                Dispatch(() => CancelRequested?.Invoke(this, EventArgs.Empty));
            }
        }
        else if (virtualKey == 0x0D && seedFocused)
        {
            GeneratePreview();
            RefreshSurface();
        }
    }

    protected override void TextEntered(char character)
    {
        if (!seedFocused)
        {
            return;
        }

        if (character == '\b' && seedText.Length > 0)
        {
            seedText = seedText[..^1];
        }
        else if (character is >= '0' and <= '9' && seedText.Length < 20)
        {
            seedText += character;
        }
        else
        {
            return;
        }

        status = strings.Get("galaxy.status.preview-stale");
        statusIsError = false;
        RefreshSurface();
    }

    private void DrawParameterPanel(SpriteForgeCanvas canvas)
    {
        canvas.Fill(42, 120, 412, 672, "#F00A1221", -5);
        canvas.Border(42, 120, 412, 672, 1, "#664D668A", -4);
        canvas.TextLine(strings.Get("galaxy.section.parameters"), 70, 140, 324, 34, 18, "#FFF2E9D8");
        canvas.TextLine(strings.Get("galaxy.introduction"), 70, 174, 324, 48, 13, "#FFAAB8D0");
        DrawField(canvas, strings.Get("galaxy.label.scenario"), strings.Get("galaxy.value.scenario.first-voyage"), 254);
        DrawField(canvas, strings.Get("galaxy.label.shape"), ShapeName(), 298, Shape);
        DrawField(canvas, strings.Get("galaxy.label.size"), SizeName(), 342, Size);
        bool spiral = selection.Settings.Shape == GalaxyShape.Spiral;
        DrawField(
            canvas,
            strings.Get("galaxy.label.spiral-bar"),
            spiral ? strings.Percent(selection.Settings.SpiralBarStrength) : strings.Get("galaxy.value.not-applicable"),
            386,
            spiral ? BarStrength : null);
        DrawField(
            canvas,
            strings.Get("galaxy.label.spiral-arms"),
            spiral ? selection.Settings.SpiralArmCount.ToString(strings.Culture) : strings.Get("galaxy.value.not-applicable"),
            430,
            spiral ? ArmCount : null);
        DrawField(canvas, strings.Get("galaxy.label.generator"), strings.Get("galaxy.value.generator.v4"), 474);
        DrawSeedControls(canvas);
        DrawLowerHelp(canvas);
        if (popup != ChoicePopup.None)
        {
            DrawChoicePopup(canvas);
        }
    }

    private void DrawSeedControls(SpriteForgeCanvas canvas)
    {
        canvas.TextLine(strings.Get("galaxy.label.seed"), 70, 488, 324, 24, 12, "#FFD7AF70");
        canvas.Fill(Seed.X, Seed.Y, Seed.Width, Seed.Height, "#FF111D31", 1);
        canvas.Border(Seed.X, Seed.Y, Seed.Width, Seed.Height, seedFocused ? 2 : 1,
            seedFocused ? "#FF80DED9" : "#664D668A", 2);
        canvas.TextLine(seedText, Seed.X + 10, Seed.Y, Seed.Width - 20, Seed.Height, 16, "#FFF2E9D8");
        DrawButton(canvas, Randomize, strings.Get("galaxy.button.randomize"));
        canvas.TextLine(strings.Get("galaxy.hint.seed"), 70, 554, 324, 28, 10, "#FFAAB8D0");
        DrawButton(canvas, Generate, strings.Get("galaxy.button.generate"));
    }

    private void DrawLowerHelp(SpriteForgeCanvas canvas)
    {
        canvas.TextLine(strings.Get("galaxy.hint.locked"), 70, 636, 324, 34, 10, "#FFAAB8D0");
        canvas.TextLine(strings.Get("galaxy.legend.title"), 70, 677, 324, 25, 12, "#FFD7AF70");
        canvas.Circle(82, 716, 11, "#FF80DED9", 2);
        canvas.TextLine(strings.Get("galaxy.legend.anchor-description"), 108, 704, 280, 24, 10, "#FFAAB8D0");
        canvas.Line(70, 746, 99, 746, 2, "#FF7388A8", 2);
        canvas.TextLine(strings.Get("galaxy.legend.local-starway"), 108, 734, 280, 24, 10, "#FFAAB8D0");
        DrawDashedLine(canvas, 70, 776, 99, 776, 2, "#FF5AAFC6", 2);
        canvas.TextLine(strings.Get("galaxy.legend.crossing-starway"), 108, 764, 280, 24, 10, "#FFAAB8D0");
    }

    private void DrawPreviewPanel(SpriteForgeCanvas canvas)
    {
        const float panelX = 478;
        const float panelY = 120;
        const float panelWidth = 1080;
        const float panelHeight = 672;
        canvas.Fill(panelX, panelY, panelWidth, panelHeight, "#F00A1221", -5);
        canvas.Border(panelX, panelY, panelWidth, panelHeight, 1, "#664D668A", -4);
        canvas.TextLine(strings.Get("galaxy.preview.title"), 504, 140, 900, 30, 18, "#FFF2E9D8");
        canvas.TextLine(strings.Get("galaxy.preview.subtitle"), 504, 169, 900, 24, 12, "#FFAAB8D0");
        canvas.Fill(504, 202, 1028, 486, "#FF01040A", -3);
        DrawGalaxy(canvas, 504, 202, 1028, 486);

        int systems = galaxy?.Topology.Systems.Count ?? 0;
        int starways = galaxy?.Topology.Starways.Count ?? 0;
        int regions = galaxy?.Topology.Systems.Values.Select(value => value.Region).Distinct().Count() ?? 0;
        DrawMetric(canvas, systems, strings.Get("galaxy.metric.systems"), 504);
        DrawMetric(canvas, starways, strings.Get("galaxy.metric.starways"), 852);
        DrawMetric(canvas, regions, strings.Get("galaxy.metric.regions"), 1200);
    }

    private void DrawGalaxy(SpriteForgeCanvas canvas, float x, float y, float width, float height)
    {
        if (galaxy is null || visualField is null || galaxy.Topology.Systems.Count == 0)
        {
            return;
        }

        IReadOnlyList<StarSystemState> systems = galaxy.Topology.Systems.Values.OrderBy(value => value.Ordinal).ToArray();
        float centerX = x + width / 2;
        float centerY = y + height / 2;
        float radiusX = width * 0.43f;
        float radiusY = height * 0.41f;
        Dictionary<StarSystemId, (float X, float Y)> points = systems.ToDictionary(
            value => value.Id,
            value => {
                (float projectedX, float projectedY) = GalaxyVisualField.ProjectSystem(
                    value.DisplayX, value.DisplayY, selection.Settings.Shape);
                return (centerX + projectedX * radiusX, centerY + projectedY * radiusY);
            });

        DrawSpaceBackdrop(canvas, x, y, width, height);
        DrawGalaxyLight(canvas, centerX, centerY, radiusX, radiusY);
        foreach (GalaxyVisualParticle particle in visualField.Particles)
        {
            float particleX = centerX + particle.X * radiusX;
            float particleY = centerY + particle.Y * radiusY;
            float glowSize = particle.Size * (particle.Bright ? 3.2f : 2.15f);
            canvas.Glow(particleX, particleY, glowSize, glowSize, particle.Color, -1);
            if (particle.Bright)
            {
                canvas.Circle(particleX, particleY, Math.Max(1.2f, particle.Size * 0.58f), particle.Color, 0);
            }
        }

        foreach (StarwayState starway in galaxy.Topology.Starways.Values.OrderBy(value => value.Id))
        {
            (float X, float Y) first = points[starway.FirstSystemId];
            (float X, float Y) second = points[starway.SecondSystemId];
            bool crossing = galaxy.Topology.Systems[starway.FirstSystemId].Region !=
                galaxy.Topology.Systems[starway.SecondSystemId].Region;
            if (crossing)
            {
                DrawDashedLine(canvas, first.X, first.Y, second.X, second.Y, 1.1f, "#8C5EC4D6", 1);
            }
            else
            {
                canvas.Line(first.X, first.Y, second.X, second.Y, 0.9f, "#668CA8C8", 1);
            }
        }

        float node = systems.Count switch { <= 64 => 4.8f, <= 256 => 3.6f, <= 512 => 2.8f, _ => 2.2f };
        foreach (StarSystemState system in systems)
        {
            (float X, float Y) point = points[system.Id];
            bool anchor = system.Ordinal == 0;
            if (anchor)
            {
                canvas.Glow(point.X, point.Y, 34, 34, SpriteForgeColor.FromSrgb(128, 222, 217, 145), 2);
            }
            else
            {
                float halo = Math.Max(7, node * 3.2f);
                canvas.Glow(point.X, point.Y, halo, halo, SpriteForgeColor.FromSrgb(224, 238, 255, 150), 2);
            }

            canvas.Circle(point.X, point.Y, anchor ? 12 : node,
                anchor
                    ? SpriteForgeColor.FromSrgb(213, 255, 255)
                    : SpriteForgeColor.FromSrgb(242, 233, 216, 235),
                3);
        }
    }

    private void DrawSpaceBackdrop(SpriteForgeCanvas canvas, float x, float y, float width, float height)
    {
        foreach (GalaxyNebula nebula in visualField!.Nebulae)
        {
            canvas.Glow(
                x + nebula.X * width,
                y + nebula.Y * height,
                nebula.Width * width,
                nebula.Height * height,
                nebula.Color,
                -2);
        }

        foreach (GalaxyBackgroundStar star in visualField.BackgroundStars)
        {
            float starX = x + star.X * width;
            float starY = y + star.Y * height;
            float glowSize = star.Size * (star.Bright ? 3.4f : 1.8f);
            canvas.Glow(starX, starY, glowSize, glowSize, star.Color, -1);
            if (star.Bright)
            {
                canvas.Circle(starX, starY, Math.Max(0.9f, star.Size * 0.48f), star.Color, 0);
            }
        }
    }

    private void DrawGalaxyLight(
        SpriteForgeCanvas canvas,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY)
    {
        canvas.Glow(centerX, centerY, radiusX * 1.94f, radiusY * 1.82f,
            SpriteForgeColor.FromSrgb(45, 82, 150, 6), -2);
        if (selection.Settings.Shape == GalaxyShape.Ring)
        {
            canvas.Glow(centerX, centerY, radiusX * 0.23f, radiusY * 0.32f,
                SpriteForgeColor.FromSrgb(255, 226, 174, 65), -2);
            canvas.Glow(centerX, centerY, radiusX * 0.08f, radiusY * 0.13f,
                SpriteForgeColor.FromSrgb(255, 244, 218, 140), -1);
            return;
        }

        if (selection.Settings.Shape == GalaxyShape.Spiral && selection.Settings.SpiralBarStrength > 0)
        {
            float strength = selection.Settings.SpiralBarStrength / 100.0f;
            for (int offset = -4; offset <= 4; ++offset)
            {
                float distance = Math.Abs(offset) / 4.0f;
                canvas.Glow(
                    centerX + offset * radiusX * (0.035f + strength * 0.05f),
                    centerY - offset * radiusY * 0.025f * strength,
                    radiusX * (0.12f + strength * 0.12f - distance * 0.045f * strength),
                    radiusY * (0.12f + strength * 0.08f - distance * 0.03f * strength),
                    SpriteForgeColor.FromSrgb(
                        255,
                        211,
                        146,
                        checked((byte)(12 + strength * (26 + (1 - distance) * 42)))),
                    -2);
            }
        }

        float outerWidth = selection.Settings.Shape == GalaxyShape.Elliptical ? 0.95f : 0.62f;
        canvas.Glow(centerX, centerY, radiusX * outerWidth, radiusY * 0.68f,
            SpriteForgeColor.FromSrgb(255, 207, 136, 45), -2);
        canvas.Glow(centerX, centerY, radiusX * 0.31f, radiusY * 0.35f,
            SpriteForgeColor.FromSrgb(255, 230, 187, 90), -2);
        canvas.Glow(centerX, centerY, radiusX * 0.095f, radiusY * 0.15f,
            SpriteForgeColor.FromSrgb(255, 250, 232, 170), -1);
    }

    private void DrawField(SpriteForgeCanvas canvas, string label, string value, float y, HitBox? button = null)
    {
        canvas.TextLine(label, 70, y - 32, 160, 38, 12, "#FFD7AF70");
        if (button is HitBox box)
        {
            DrawButton(canvas, box, value, combo: true);
        }
        else
        {
            canvas.TextLine(value, 230, y - 32, 164, 38, 13, "#FFF2E9D8", SpriteForgeTextAlignment.Right);
        }

        canvas.Fill(70, y + 8, 324, 1, "#334D668A", 1);
    }

    private void DrawMetric(SpriteForgeCanvas canvas, int value, string label, float x)
    {
        canvas.Border(x, 704, 332, 64, 1, "#334D668A", 1);
        canvas.TextLine(value.ToString(strings.Culture), x, 708, 332, 31, 20, "#FF80DED9",
            SpriteForgeTextAlignment.Center);
        canvas.TextLine(label, x, 738, 332, 24, 11, "#FFAAB8D0", SpriteForgeTextAlignment.Center);
    }

    private void DrawButton(
        SpriteForgeCanvas canvas,
        HitBox box,
        string text,
        bool primary = false,
        bool combo = false)
    {
        string fill = primary ? "#FF80DED9" : pressed == box.Control ? "#FF142238" : "#FF17243A";
        if (hovered == box.Control)
        {
            fill = primary ? "#FFA0F3EE" : "#FF293C58";
        }

        canvas.Fill(box.X, box.Y, box.Width, box.Height, fill, 5);
        canvas.Border(box.X, box.Y, box.Width, box.Height, 1, primary ? "#FFB9FFFF" : "#664D668A", 6);
        canvas.TextLine(text, box.X + 5, box.Y, box.Width - (combo ? 32 : 10), box.Height, 13,
            primary ? "#FF07111E" : "#FFF2E9D8", SpriteForgeTextAlignment.Center);
        if (combo)
        {
            float centerX = box.X + box.Width - 16;
            float centerY = box.Y + box.Height / 2;
            canvas.Line(centerX - 4, centerY - 2, centerX, centerY + 2, 1.5f, "#FFD5FFFF", 7);
            canvas.Line(centerX, centerY + 2, centerX + 4, centerY - 2, 1.5f, "#FFD5FFFF", 7);
        }
    }

    private void DrawChoicePopup(SpriteForgeCanvas canvas)
    {
        IReadOnlyList<string> labels = GetPopupLabels();
        HitBox popupButton = PopupButton();
        float top = popupButton.Y + popupButton.Height;
        float height = labels.Count * PopupRowHeight;
        canvas.Fill(PopupX - 2, top - 2, PopupWidth + 4, height + 4, "#FF80A8C8", 198);
        canvas.Fill(PopupX, top, PopupWidth, height, "#FF081322", 199);

        int selectedIndex = popup switch {
            ChoicePopup.Shape => Array.IndexOf(SupportedShapes, selection.Settings.Shape),
            ChoicePopup.Size => Array.IndexOf(SupportedSystemCounts, selection.Settings.SystemCount),
            ChoicePopup.BarStrength => Array.IndexOf(SupportedBarStrengths, selection.Settings.SpiralBarStrength),
            ChoicePopup.ArmCount => Array.IndexOf(SupportedArmCounts, selection.Settings.SpiralArmCount),
            _ => -1,
        };
        for (int index = 0; index < labels.Count; index++)
        {
            float rowY = top + index * PopupRowHeight;
            bool selected = index == selectedIndex;
            string fill = index == pressedOption
                ? "#FF183149"
                : index == hoveredOption
                    ? "#FF315675"
                    : selected ? "#FF24465C" : "#FF0D1A2B";
            canvas.Fill(PopupX, rowY, PopupWidth, PopupRowHeight, fill, 200 + index);
            if (selected)
            {
                canvas.Fill(PopupX, rowY, 4, PopupRowHeight, "#FF80DED9", 220 + index);
            }

            canvas.TextLine(labels[index], PopupX + 14, rowY, PopupWidth - 28, PopupRowHeight, 12,
                selected ? "#FFD5FFFF" : "#FFF2E9D8");
        }
    }

    private IReadOnlyList<string> GetPopupLabels()
    {
        return popup switch {
            ChoicePopup.Shape => SupportedShapes
                .Select(value => strings.Get($"galaxy.value.shape.{value.ToString().ToLowerInvariant()}"))
                .ToArray(),
            ChoicePopup.Size => SupportedSystemCounts
                .Select(value => strings.Get($"galaxy.value.size.s{value}"))
                .ToArray(),
            ChoicePopup.BarStrength => SupportedBarStrengths
                .Select(strings.Percent)
                .ToArray(),
            ChoicePopup.ArmCount => SupportedArmCounts
                .Select(value => value.ToString(strings.Culture))
                .ToArray(),
            _ => [],
        };
    }

    private int FindPopupOption(float x, float y)
    {
        if (popup == ChoicePopup.None || x < PopupX || x > PopupX + PopupWidth)
        {
            return -1;
        }

        HitBox popupButton = PopupButton();
        float top = popupButton.Y + popupButton.Height;
        int count = GetPopupLabels().Count;
        int index = (int)((y - top) / PopupRowHeight);
        return y >= top && index >= 0 && index < count ? index : -1;
    }

    private void ApplyPopupOption(int index)
    {
        switch (popup)
        {
            case ChoicePopup.Shape:
                selection = selection with {
                    Settings = selection.Settings with { Shape = SupportedShapes[index] },
                };
                break;
            case ChoicePopup.Size:
                selection = selection with {
                    Settings = selection.Settings with { SystemCount = SupportedSystemCounts[index] },
                };
                break;
            case ChoicePopup.BarStrength:
                selection = selection with {
                    Settings = selection.Settings with { SpiralBarStrength = SupportedBarStrengths[index] },
                };
                break;
            case ChoicePopup.ArmCount:
                selection = selection with {
                    Settings = selection.Settings with { SpiralArmCount = SupportedArmCounts[index] },
                };
                break;
        }
    }

    private HitBox PopupButton() => popup switch {
        ChoicePopup.Shape => Shape,
        ChoicePopup.Size => Size,
        ChoicePopup.BarStrength => BarStrength,
        ChoicePopup.ArmCount => ArmCount,
        _ => throw new InvalidOperationException("No choice popup is open."),
    };

    private static void DrawDashedLine(
        SpriteForgeCanvas canvas, float x1, float y1, float x2, float y2, float thickness, string color, int layer)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length <= 0)
        {
            return;
        }

        const float dash = 8;
        const float gap = 6;
        for (float at = 0; at < length; at += dash + gap)
        {
            float end = Math.Min(length, at + dash);
            canvas.Line(x1 + dx * at / length, y1 + dy * at / length,
                x1 + dx * end / length, y1 + dy * end / length, thickness, color, layer);
        }
    }

    private void GeneratePreview()
    {
        if (!ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) || seed == 0)
        {
            galaxy = null;
            visualField = null;
            status = strings.Get("galaxy.status.invalid-seed");
            statusIsError = true;
            return;
        }

        GalaxyGenerationSettings settings = selection.Settings;
        GalaxyGenerationResult result = GalaxyGenerator.Generate(seed, settings);
        if (!result.Succeeded)
        {
            galaxy = null;
            visualField = null;
            status = strings.Get("galaxy.status.generation-failed");
            statusIsError = true;
            return;
        }

        selection = new GalaxyMapSelection(seed, settings);
        galaxy = result.Galaxy;
        visualField = GalaxyVisualField.Generate(seed, settings);
        status = strings.Get("galaxy.status.ready");
        statusIsError = false;
    }

    private string ShapeName() =>
        strings.Get($"galaxy.value.shape.{selection.Settings.Shape.ToString().ToLowerInvariant()}");

    private string SizeName() => strings.Get($"galaxy.value.size.s{selection.Settings.SystemCount}");

    private Control Find(float x, float y)
    {
        Control control = HitBoxes.FirstOrDefault(value => value.Contains(x, y)).Control;
        return selection.Settings.Shape != GalaxyShape.Spiral && control is Control.BarStrength or Control.ArmCount
            ? Control.None
            : control;
    }

    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong value;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }
        while (value == 0);

        return value;
    }

    private enum Control : byte
    {
        None,
        Shape,
        Size,
        BarStrength,
        ArmCount,
        Seed,
        Randomize,
        Generate,
        Back,
        Continue,
    }

    private enum ChoicePopup : byte
    {
        None,
        Shape,
        Size,
        BarStrength,
        ArmCount,
    }

    private readonly record struct HitBox(float X, float Y, float Width, float Height, Control Control)
    {
        internal bool Contains(float x, float y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    }
}
