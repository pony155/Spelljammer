using Spelljammer.Simulation.Galaxy;

namespace Spelljammer.Presentation;

/// <summary>
/// Seeded presentation-only star dust. These particles never become simulation entities or save data.
/// </summary>
internal sealed class GalaxyVisualField
{
    private const double FullCircle = Math.PI * 2;
    private const double ProjectionCosine = 0.9928;
    private const double ProjectionSine = -0.1197;

    private GalaxyVisualField(
        GalaxyVisualParticle[] particles,
        GalaxyBackgroundStar[] backgroundStars,
        GalaxyNebula[] nebulae)
    {
        Particles = particles;
        BackgroundStars = backgroundStars;
        Nebulae = nebulae;
    }

    internal IReadOnlyList<GalaxyVisualParticle> Particles { get; }

    internal IReadOnlyList<GalaxyBackgroundStar> BackgroundStars { get; }

    internal IReadOnlyList<GalaxyNebula> Nebulae { get; }

    internal static GalaxyVisualField Generate(ulong seed, GalaxyGenerationSettings settings)
    {
        int particleCount = Math.Clamp(5_600 + settings.SystemCount * 7, 6_000, 14_000);
        GalaxyVisualParticle[] particles = new GalaxyVisualParticle[particleCount];
        VisualRandom random = new(seed ^ 0xA0761D6478BD642FUL ^ ((ulong)settings.Shape << 56));

        for (int index = 0; index < particles.Length; ++index)
        {
            ParticlePosition position = settings.Shape switch {
                GalaxyShape.Spiral => SpiralPosition(settings, ref random),
                GalaxyShape.Elliptical => EllipticalPosition(ref random),
                GalaxyShape.Ring => RingPosition(ref random),
                _ => throw new ArgumentOutOfRangeException(nameof(settings)),
            };

            (float projectedX, float projectedY) = Project(position.X, position.Y, position.Depth);
            bool bright = random.NextUnit() > 0.996;
            float size = bright
                ? 1.8f + random.NextFloat() * 1.6f
                : 0.75f + random.NextFloat() * 1.65f;
            byte alpha = bright
                ? checked((byte)random.Next(125, 196))
                : checked((byte)random.Next(35, 112));
            SpriteForgeColor color = StellarColor(position.Radius, alpha, ref random);
            particles[index] = new GalaxyVisualParticle(
                projectedX,
                projectedY,
                size,
                color,
                bright);
        }

        GalaxyBackgroundStar[] backgroundStars = GenerateBackgroundStars(ref random);
        GalaxyNebula[] nebulae = GenerateNebulae(ref random);
        return new GalaxyVisualField(particles, backgroundStars, nebulae);
    }

    private static GalaxyBackgroundStar[] GenerateBackgroundStars(ref VisualRandom random)
    {
        GalaxyBackgroundStar[] stars = new GalaxyBackgroundStar[520];
        for (int index = 0; index < stars.Length; ++index)
        {
            bool bright = random.NextUnit() > 0.975;
            float size = bright
                ? 1.5f + random.NextFloat() * 1.5f
                : 0.45f + random.NextFloat() * 0.85f;
            byte alpha = bright
                ? checked((byte)random.Next(90, 151))
                : checked((byte)random.Next(28, 76));
            SpriteForgeColor color = random.NextUnit() switch {
                < 0.22 => SpriteForgeColor.FromSrgb(141, 185, 255, alpha),
                < 0.78 => SpriteForgeColor.FromSrgb(218, 232, 255, alpha),
                _ => SpriteForgeColor.FromSrgb(255, 226, 184, alpha),
            };
            stars[index] = new GalaxyBackgroundStar(
                0.035f + random.NextFloat() * 0.93f,
                0.055f + random.NextFloat() * 0.89f,
                size,
                color,
                bright);
        }

        return stars;
    }

    private static GalaxyNebula[] GenerateNebulae(ref VisualRandom random)
    {
        GalaxyNebula[] nebulae = new GalaxyNebula[14];
        for (int index = 0; index < nebulae.Length; ++index)
        {
            byte alpha = checked((byte)random.Next(6, 17));
            SpriteForgeColor color = (index % 3) switch {
                0 => SpriteForgeColor.FromSrgb(45, 94, 170, alpha),
                1 => SpriteForgeColor.FromSrgb(92, 52, 145, alpha),
                _ => SpriteForgeColor.FromSrgb(39, 126, 151, alpha),
            };
            nebulae[index] = new GalaxyNebula(
                0.13f + random.NextFloat() * 0.74f,
                0.16f + random.NextFloat() * 0.68f,
                0.16f + random.NextFloat() * 0.24f,
                0.10f + random.NextFloat() * 0.17f,
                color);
        }

        return nebulae;
    }

    internal static (float X, float Y) ProjectSystem(int displayX, int displayY, GalaxyShape shape)
    {
        (double horizontalRadius, double verticalRadius) = shape switch {
            GalaxyShape.Spiral => (800, 464),
            GalaxyShape.Elliptical => (790, 390),
            GalaxyShape.Ring => (440, 390),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        return Project(displayX / horizontalRadius, displayY / verticalRadius, 0);
    }

    private static (float X, float Y) Project(double x, double y, double depth)
    {
        double rotatedX = x * ProjectionCosine - y * ProjectionSine;
        double rotatedY = x * ProjectionSine + y * ProjectionCosine + depth * 0.18;
        return ((float)Math.Clamp(rotatedX, -1.04, 1.04), (float)Math.Clamp(rotatedY, -1.04, 1.04));
    }

    private static ParticlePosition SpiralPosition(
        GalaxyGenerationSettings settings,
        ref VisualRandom random)
    {
        double barStrength = settings.SpiralBarStrength / 100.0;
        double population = random.NextUnit();
        double barPopulation = barStrength * 0.18;
        if (population < barPopulation)
        {
            double barHalfLength = (130 + barStrength * 210) / 800;
            double barX = (random.NextUnit() + random.NextUnit() - 1) * barHalfLength;
            double barY = random.NextBell() * (0.014 + Math.Abs(barX) * 0.035);
            return new ParticlePosition(barX, barY, random.NextBell() * 0.035, Math.Abs(barX));
        }

        if (population < barPopulation + 0.12)
        {
            double radius = Math.Pow(random.NextUnit(), 2.15) * 0.34;
            double angle = random.NextUnit() * FullCircle;
            return new ParticlePosition(
                Math.Cos(angle) * radius,
                Math.Sin(angle) * radius,
                random.NextBell() * 0.09,
                radius);
        }

        if (population < barPopulation + 0.28)
        {
            double radius = Math.Sqrt(random.NextUnit());
            double angle = random.NextUnit() * FullCircle;
            return new ParticlePosition(
                Math.Cos(angle) * radius,
                Math.Sin(angle) * radius,
                random.NextBell() * 0.065 * (1.15 - radius),
                radius);
        }

        double armRadius = Math.Pow(random.NextUnit(), 0.72);
        int arm = random.Next(0, settings.SpiralArmCount);
        double spread = (0.045 + armRadius * 0.16) * random.NextBell();
        double baseAngle = arm * FullCircle / settings.SpiralArmCount;
        double armAngle = baseAngle + armRadius * Math.PI * 1.55 + spread;
        double noisyRadius = Math.Clamp(armRadius + random.NextBell() * 0.018, 0, 1);
        double barPull = barStrength * Math.Pow(1 - armRadius, 2.25);
        double targetX = Math.Cos(baseAngle) >= 0 ? 0.425 : -0.425;
        double spiralX = Math.Cos(armAngle) * noisyRadius;
        double spiralY = Math.Sin(armAngle) * noisyRadius;
        return new ParticlePosition(
            spiralX + (targetX - spiralX) * barPull,
            spiralY * (1 - barPull),
            random.NextBell() * 0.045 * (1.1 - noisyRadius),
            noisyRadius);
    }

    private static ParticlePosition EllipticalPosition(ref VisualRandom random)
    {
        double radius = Math.Pow(random.NextUnit(), 0.8);
        double angle = random.NextUnit() * FullCircle;
        double spread = 0.82 + random.NextUnit() * 0.18;
        return new ParticlePosition(
            Math.Cos(angle) * radius * spread,
            Math.Sin(angle) * radius * spread,
            random.NextBell() * 0.13 * (1.1 - radius),
            radius);
    }

    private static ParticlePosition RingPosition(ref VisualRandom random)
    {
        if (random.NextUnit() > 0.965)
        {
            double coreRadius = Math.Pow(random.NextUnit(), 2.4) * 0.09;
            double coreAngle = random.NextUnit() * FullCircle;
            return new ParticlePosition(
                Math.Cos(coreAngle) * coreRadius,
                Math.Sin(coreAngle) * coreRadius,
                random.NextBell() * 0.025,
                coreRadius);
        }

        const double innerRadius = 0.48;
        double radius = Math.Sqrt(
            innerRadius * innerRadius + random.NextUnit() * (1 - innerRadius * innerRadius));
        double angle = random.NextUnit() * FullCircle;
        radius = Math.Clamp(radius + Math.Sin(angle * 5) * 0.018 + random.NextBell() * 0.018, innerRadius, 1);
        return new ParticlePosition(
            Math.Cos(angle) * radius,
            Math.Sin(angle) * radius,
            random.NextBell() * 0.055,
            radius);
    }

    private static SpriteForgeColor StellarColor(double radius, byte alpha, ref VisualRandom random)
    {
        double value = random.NextUnit();
        if (radius < 0.22 && value < 0.72)
        {
            return value < 0.28
                ? SpriteForgeColor.FromSrgb(255, 197, 112, alpha)
                : SpriteForgeColor.FromSrgb(255, 232, 190, alpha);
        }

        return value switch {
            < 0.14 => SpriteForgeColor.FromSrgb(132, 183, 255, alpha),
            < 0.42 => SpriteForgeColor.FromSrgb(181, 213, 255, alpha),
            < 0.82 => SpriteForgeColor.FromSrgb(224, 238, 255, alpha),
            _ => SpriteForgeColor.FromSrgb(255, 240, 207, alpha),
        };
    }

    private readonly record struct ParticlePosition(double X, double Y, double Depth, double Radius);

    private struct VisualRandom
    {
        private ulong state;

        internal VisualRandom(ulong seed)
        {
            state = seed;
        }

        internal double NextUnit() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        internal float NextFloat() => (float)NextUnit();

        internal int Next(int minimumInclusive, int maximumExclusive) =>
            minimumInclusive + (int)(NextUnit() * (maximumExclusive - minimumInclusive));

        internal double NextBell() =>
            (NextUnit() + NextUnit() + NextUnit() + NextUnit() - 2) * 0.72;

        private ulong NextUInt64()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

internal readonly record struct GalaxyVisualParticle(
    float X,
    float Y,
    float Size,
    SpriteForgeColor Color,
    bool Bright);

internal readonly record struct GalaxyBackgroundStar(
    float X,
    float Y,
    float Size,
    SpriteForgeColor Color,
    bool Bright);

internal readonly record struct GalaxyNebula(
    float X,
    float Y,
    float Width,
    float Height,
    SpriteForgeColor Color);
