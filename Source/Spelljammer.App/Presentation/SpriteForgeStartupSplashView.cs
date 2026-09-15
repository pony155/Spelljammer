using System.Globalization;
using System.Windows.Threading;

namespace Spelljammer.Presentation;

/// <summary>
/// SpriteForge-rendered startup presentation shown while application services initialize.
/// </summary>
internal sealed class SpriteForgeStartupSplashView : SpriteForgeRenderSurface
{
    internal const uint SurfaceWidth = 960;
    internal const uint SurfaceHeight = 540;
    private const string BackgroundUri = "pack://application:,,,/Assets/UI/MainMenu/Background.png";
    private readonly DispatcherTimer animationTimer;
    private float displayedProgress = 0.08f;
    private float targetProgress = 0.08f;
    private float phase;

    internal SpriteForgeStartupSplashView()
        : base(SurfaceWidth, SurfaceHeight, CultureInfo.InvariantCulture)
    {
        animationTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        animationTimer.Tick += AnimationTimer_Tick;
        animationTimer.Start();
    }

    internal void SetProgress(float value)
    {
        targetProgress = Math.Clamp(value, displayedProgress, 1);
        RefreshSurface();
    }

    protected override void Compose(SpriteForgeCanvas canvas)
    {
        canvas.ImageCover(BackgroundUri, 0, 0, SurfaceWidth, SurfaceHeight);
        canvas.Fill(0, 0, SurfaceWidth, SurfaceHeight, "#C7050912", -9);
        canvas.Fill(0, 0, SurfaceWidth, 72, "#99030A16", -8);
        canvas.Fill(0, SurfaceHeight - 72, SurfaceWidth, 72, "#99030A16", -8);

        DrawOrbit(canvas);
        canvas.TextLine("SPELLJAMMER", 0, 205, SurfaceWidth, 82, 44, "#FFF2E9D8",
            SpriteForgeTextAlignment.Center);
        canvas.Fill(330, 300, 300, 1, "#99D7AF70", 4);

        const float trackX = 250;
        const float trackY = 394;
        const float trackWidth = 460;
        canvas.Fill(trackX, trackY, trackWidth, 3, "#553C526D", 4);
        canvas.Fill(trackX, trackY, Math.Max(2, trackWidth * displayedProgress), 3, "#FF80DED9", 5);
        canvas.Circle(trackX + trackWidth * displayedProgress, trackY + 1.5f, 12, "#6680DED9", 5);
        canvas.Circle(trackX + trackWidth * displayedProgress, trackY + 1.5f, 5, "#FFD5FFFF", 6);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Stop();
            animationTimer.Tick -= AnimationTimer_Tick;
        }

        base.Dispose(disposing);
    }

    private void DrawOrbit(SpriteForgeCanvas canvas)
    {
        const float centerX = SurfaceWidth / 2f;
        const float centerY = 164;
        const float radiusX = 118;
        const float radiusY = 34;
        for (int index = 0; index < 12; index++)
        {
            float angle = phase + index * MathF.PI * 2 / 12;
            float x = centerX + MathF.Cos(angle) * radiusX;
            float y = centerY + MathF.Sin(angle) * radiusY;
            float depth = (MathF.Sin(angle) + 1) / 2;
            float diameter = 3 + depth * 5;
            byte alpha = checked((byte)MathF.Round(80 + depth * 175));
            canvas.Circle(x, y, diameter, $"#{alpha:X2}D5FFFF", 3);
        }

        canvas.Circle(centerX, centerY, 34, "#2280DED9", 2);
        canvas.Circle(centerX, centerY, 12, "#FFD5FFFF", 4);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        phase = (phase + 0.055f) % (MathF.PI * 2);
        displayedProgress += (targetProgress - displayedProgress) * 0.16f;
        if (targetProgress >= 1 && 1 - displayedProgress < 0.002f)
        {
            displayedProgress = 1;
        }

        RefreshSurface();
    }
}
