using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Galaxy;

public enum GalaxyShape : byte
{
    Spiral = 1,
    Elliptical = 2,
    Ring = 3,
}

public sealed record GalaxyGenerationSettings(
    int SystemCount = 16,
    GalaxyShape Shape = GalaxyShape.Elliptical,
    int GeneratorVersion = GalaxyGenerator.CurrentGeneratorVersion,
    int SpiralBarStrength = 0,
    int SpiralArmCount = 2)
{
    public const int MinimumSystemCount = 8;
    public const int MinimumSpiralBarStrength = 0;
    public const int MaximumSpiralBarStrength = 100;
    public const int MinimumSpiralArmCount = 2;
    public const int MaximumSpiralArmCount = 6;
}

public sealed record GalaxyGenerationResult(
    GalaxyState? Galaxy,
    VoyageNavigationState? Navigation,
    GalaxyValidationCode Code)
{
    public bool Succeeded => Galaxy is not null && Code == GalaxyValidationCode.None;
}

public static class GalaxyGenerator
{
    public const int CurrentGeneratorVersion = 4;

    private const double FullCircle = Math.PI * 2;
    private const double GoldenAngle = 2.39996322972865332;
    private const double RingInnerRadius = 0.46;
    private const double RingHorizontalRadius = 440;
    private const double RingVerticalRadius = 390;

    private static readonly ContentId[] Archetypes =
    [
        new("galaxy-system.stable-star"),
        new("galaxy-system.binary-star"),
        new("galaxy-system.dead-star"),
        new("galaxy-system.aether-star"),
    ];

    public static GalaxyGenerationResult Generate(ulong seed, GalaxyGenerationSettings? settings = null)
    {
        settings ??= new();
        if (settings.GeneratorVersion != CurrentGeneratorVersion || !Enum.IsDefined(settings.Shape) ||
            settings.SystemCount is < GalaxyGenerationSettings.MinimumSystemCount or > GalaxyLimits.MaximumSystems ||
            settings.SpiralBarStrength is < GalaxyGenerationSettings.MinimumSpiralBarStrength or
                > GalaxyGenerationSettings.MaximumSpiralBarStrength ||
            settings.SpiralArmCount is < GalaxyGenerationSettings.MinimumSpiralArmCount or
                > GalaxyGenerationSettings.MaximumSpiralArmCount)
        {
            return new(null, null, GalaxyValidationCode.InvalidHeader);
        }

        DeterministicStream random = new(seed ^ 0x67616c617879UL ^ ((ulong)settings.Shape << 48));
        GeneratedPosition[] positions = PlaceSystems(settings, ref random);
        StarSystemState[] systems = new StarSystemState[settings.SystemCount];
        string seedPart = $"g{seed:x16}";
        for (int index = 0; index < systems.Length; index++)
        {
            GeneratedPosition position = positions[index];
            systems[index] = new(
                new StarSystemId($"system.{seedPart}.n{index:0000}"),
                index,
                position.Region,
                position.X,
                position.Y,
                Archetypes[random.Next(0, Archetypes.Length)]);
        }

        List<StarwayState> starways = [];
        HashSet<(int, int)> connections = [];
        int[] degrees = new int[systems.Length];
        void Connect(int left, int right)
        {
            if (left == right)
            {
                return;
            }

            (int first, int second) = left < right ? (left, right) : (right, left);
            if (!connections.Add((first, second)))
            {
                return;
            }

            int distance = 8 + Math.Abs(systems[first].DisplayX - systems[second].DisplayX) / 70 +
                Math.Abs(systems[first].DisplayY - systems[second].DisplayY) / 90;
            starways.Add(new(
                new StarwayId($"starway.{seedPart}.n{first:0000}-n{second:0000}"),
                systems[first].Id,
                systems[second].Id,
                distance,
                Math.Max(1, (distance + 2) / 3),
                random.Next(0, 6)));
            degrees[first]++;
            degrees[second]++;
        }

        switch (settings.Shape)
        {
            case GalaxyShape.Spiral:
                ConnectSpiral(
                    systems,
                    BarSystemCount(settings.SystemCount, settings.SpiralBarStrength, settings.SpiralArmCount),
                    settings.SpiralArmCount,
                    Connect);
                break;
            case GalaxyShape.Elliptical:
                ConnectElliptical(systems, degrees, Connect);
                break;
            case GalaxyShape.Ring:
                ConnectRing(systems, Connect);
                break;
            default:
                return new(null, null, GalaxyValidationCode.InvalidHeader);
        }

        ImmutableDictionary<StarSystemId, StarSystemState> systemMap = systems.ToImmutableDictionary(value => value.Id);
        GalaxyState galaxy = new(
            new GalaxyTopology(
                settings.GeneratorVersion,
                seed,
                systemMap,
                starways.ToImmutableDictionary(value => value.Id)),
            new GalaxyKnowledgeState(
                ImmutableDictionary<StarSystemId, GalaxyKnowledgeLevel>.Empty
                    .Add(systems[0].Id, GalaxyKnowledgeLevel.Charted)
                    .Add(systems[1].Id, GalaxyKnowledgeLevel.Detected)
                    .Add(systems[2].Id, GalaxyKnowledgeLevel.Detected)),
            GalaxyDynamicState.Empty);
        GalaxyValidationResult validation = GalaxyValidator.Validate(galaxy);
        return validation.Accepted
            ? new(galaxy, VoyageNavigationState.AtAnchor(systems[0].Id), GalaxyValidationCode.None)
            : new(null, null, validation.Code);
    }

    private static GeneratedPosition[] PlaceSystems(
        GalaxyGenerationSettings settings,
        ref DeterministicStream random) => settings.Shape switch {
            GalaxyShape.Spiral => PlaceSpiral(
                settings.SystemCount,
                settings.SpiralBarStrength,
                settings.SpiralArmCount,
                ref random),
            GalaxyShape.Elliptical => PlaceElliptical(settings.SystemCount, ref random),
            GalaxyShape.Ring => PlaceRing(settings.SystemCount, ref random),
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };

    private static GeneratedPosition[] PlaceSpiral(
        int count,
        int barStrength,
        int armCount,
        ref DeterministicStream random)
    {
        int barCount = BarSystemCount(count, barStrength, armCount);
        double normalizedBarStrength = barStrength / 100.0;
        GeneratedPosition[] positions = new GeneratedPosition[count];
        for (int index = 0; index < barCount; ++index)
        {
            double progress = barCount <= 1 ? 0.5 : (double)index / (barCount - 1);
            int x = Round((progress * 2 - 1) * (130 + normalizedBarStrength * 210)) + random.Next(-9, 10);
            int y = random.Next(-10, 11) + Round(Math.Sin(progress * Math.PI) * 8);
            positions[index] = new(x, y, RegionOf(x, y));
        }

        int spiralCount = count - barCount;
        int layers = (spiralCount + armCount - 1) / armCount;
        for (int index = 0; index < spiralCount; ++index)
        {
            int arm = index % armCount;
            int layer = index / armCount;
            double progress = layers <= 1 ? 0 : (double)layer / (layers - 1);
            double radius = 90 + progress * 710;
            double angle = arm * FullCircle / armCount + progress * Math.PI * 1.55;
            double barPull = normalizedBarStrength * Math.Pow(1 - progress, 2.25);
            double targetX = Math.Cos(arm * FullCircle / armCount) >= 0 ? 340 : -340;
            double spiralX = Math.Cos(angle) * radius;
            double spiralY = Math.Sin(angle) * radius * 0.58;
            int x = Round(spiralX + (targetX - spiralX) * barPull) + random.Next(-14, 15);
            int y = Round(spiralY * (1 - barPull)) + random.Next(-14, 15);
            positions[barCount + index] = new(x, y, RegionOf(x, y));
        }

        return positions;
    }

    private static GeneratedPosition[] PlaceElliptical(int count, ref DeterministicStream random)
    {
        GeneratedPosition[] positions = new GeneratedPosition[count];
        for (int index = 0; index < count; index++)
        {
            double progress = Math.Sqrt((index + 0.6) / count);
            double angle = index * GoldenAngle;
            int x = Round(Math.Cos(angle) * progress * 790) + random.Next(-18, 19);
            int y = Round(Math.Sin(angle) * progress * 390) + random.Next(-18, 19);
            int region = y >= 0 ? (x >= 0 ? 0 : 1) : (x < 0 ? 2 : 3);
            positions[index] = new(x, y, region);
        }

        return positions;
    }

    private static GeneratedPosition[] PlaceRing(int count, ref DeterministicStream random)
    {
        int radialBandCount = Math.Clamp((int)Math.Round(Math.Sqrt(count) / 2), 3, 8);
        GeneratedPosition[] positions = new GeneratedPosition[count];
        for (int index = 0; index < count; index++)
        {
            double angularJitter = random.Next(-1_000, 1_001) / 1_000.0 * FullCircle / count * 0.4;
            double angle = Math.PI + index * GoldenAngle + angularJitter;
            int radialBand = index % radialBandCount;
            double withinBand = random.Next(0, 1_000_000) / 1_000_000.0;
            double areaProgress = (radialBand + withinBand) / radialBandCount;
            double radius = Math.Sqrt(
                RingInnerRadius * RingInnerRadius +
                areaProgress * (1 - RingInnerRadius * RingInnerRadius));

            if (index == 0)
            {
                angle = Math.PI;
                radius = 0.82;
            }

            int x = Round(Math.Cos(angle) * radius * RingHorizontalRadius);
            int y = Round(Math.Sin(angle) * radius * RingVerticalRadius);
            double normalizedAngle = angle % FullCircle;
            if (normalizedAngle < 0)
            {
                normalizedAngle += FullCircle;
            }

            int region = Math.Min(3, (int)(normalizedAngle / (FullCircle / 4)));
            positions[index] = new(x, y, region);
        }

        return positions;
    }

    private static void ConnectSpiral(
        IReadOnlyList<StarSystemState> systems,
        int barCount,
        int armCount,
        Action<int, int> connect)
    {
        for (int index = 0; index + 1 < barCount; ++index)
        {
            connect(index, index + 1);
        }

        int spiralCount = systems.Count - barCount;
        for (int arm = 0; arm < armCount; ++arm)
        {
            int first = barCount + arm;
            if (first >= systems.Count)
            {
                break;
            }

            for (int index = first; index + armCount < systems.Count; index += armCount)
            {
                connect(index, index + armCount);
            }

            if (barCount > 0)
            {
                int endpoint = systems[first].DisplayX >= 0 ? barCount - 1 : 0;
                connect(endpoint, first);
            }
            else
            {
                connect(arm, (arm + 1) % armCount);
            }
        }

        if (barCount > 0 && barCount == 2)
        {
            connect(0, Math.Min(systems.Count - 1, barCount + armCount / 2));
        }

        int layers = (spiralCount + armCount - 1) / armCount;
        for (int layer = 2; layer < layers; layer += 3)
        {
            int first = barCount + layer * armCount;
            for (int arm = 0; arm < armCount; ++arm)
            {
                int left = first + arm;
                int right = first + ((arm + 1) % armCount);
                if (left < systems.Count && right < systems.Count)
                {
                    connect(left, right);
                }
            }
        }
    }

    private static void ConnectElliptical(
        IReadOnlyList<StarSystemState> systems,
        IReadOnlyList<int> degrees,
        Action<int, int> connect)
    {
        for (int index = 1; index < systems.Count; index++)
        {
            int nearestPrevious = Enumerable.Range(0, index)
                .OrderBy(candidate => DistanceSquared(systems[index], systems[candidate]))
                .ThenBy(candidate => candidate)
                .First();
            connect(index, nearestPrevious);
        }

        for (int index = 0; index < systems.Count; index++)
        {
            foreach (int candidate in Enumerable.Range(0, systems.Count)
                .Where(candidate => candidate != index && degrees[candidate] < 6)
                .OrderBy(candidate => DistanceSquared(systems[index], systems[candidate]))
                .ThenBy(candidate => candidate))
            {
                if (degrees[index] >= 3)
                {
                    break;
                }

                connect(index, candidate);
            }
        }
    }

    private static void ConnectRing(IReadOnlyList<StarSystemState> systems, Action<int, int> connect)
    {
        int[] angularOrder = Enumerable.Range(0, systems.Count)
            .OrderBy(index => Math.Atan2(
                systems[index].DisplayY / RingVerticalRadius,
                systems[index].DisplayX / RingHorizontalRadius))
            .ThenBy(index => index)
            .ToArray();

        for (int position = 0; position < angularOrder.Length; position++)
        {
            connect(angularOrder[position], angularOrder[(position + 1) % angularOrder.Length]);
        }

        for (int position = 0; position < angularOrder.Length; position += 4)
        {
            connect(angularOrder[position], angularOrder[(position + 2) % angularOrder.Length]);
        }
    }

    private static long DistanceSquared(StarSystemState first, StarSystemState second)
    {
        long deltaX = first.DisplayX - second.DisplayX;
        long deltaY = first.DisplayY - second.DisplayY;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static int BarSystemCount(int systemCount, int barStrength, int armCount)
    {
        if (barStrength == 0)
        {
            return 0;
        }

        int desired = Round(systemCount * (0.06 + barStrength / 100.0 * 0.16));
        return Math.Clamp(desired, 2, systemCount - armCount);
    }

    private static int RegionOf(int x, int y) => y >= 0 ? (x >= 0 ? 0 : 1) : (x < 0 ? 2 : 3);

    private static int Round(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private readonly record struct GeneratedPosition(int X, int Y, int Region);

    private struct DeterministicStream(ulong state)
    {
        private ulong state = state;

        public int Next(int minimum, int maximumExclusive)
        {
            ulong value = NextValue();
            return minimum + (int)(value % (uint)(maximumExclusive - minimum));
        }

        private ulong NextValue()
        {
            state += 0x9e3779b97f4a7c15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }
    }
}
