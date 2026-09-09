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
    int GeneratorVersion = 2)
{
    public const int MinimumSystemCount = 8;
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
    private const double FullCircle = Math.PI * 2;
    private const double GoldenAngle = 2.39996322972865332;

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
        if (settings.GeneratorVersion <= 0 || !Enum.IsDefined(settings.Shape) ||
            settings.SystemCount is < GalaxyGenerationSettings.MinimumSystemCount or > GalaxyLimits.MaximumSystems)
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
                ConnectSpiral(systems.Length, Connect);
                break;
            case GalaxyShape.Elliptical:
                ConnectElliptical(systems, degrees, Connect);
                break;
            case GalaxyShape.Ring:
                ConnectRing(systems.Length, Connect);
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
            GalaxyShape.Spiral => PlaceSpiral(settings.SystemCount, ref random),
            GalaxyShape.Elliptical => PlaceElliptical(settings.SystemCount, ref random),
            GalaxyShape.Ring => PlaceRing(settings.SystemCount, ref random),
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };

    private static GeneratedPosition[] PlaceSpiral(int count, ref DeterministicStream random)
    {
        int arms = ArmCount(count);
        int layers = (count + arms - 1) / arms;
        GeneratedPosition[] positions = new GeneratedPosition[count];
        for (int index = 0; index < count; index++)
        {
            int arm = index % arms;
            int layer = index / arms;
            double progress = layers <= 1 ? 0 : (double)layer / (layers - 1);
            double radius = 90 + progress * 710;
            double angle = arm * FullCircle / arms + progress * Math.PI * 1.55;
            positions[index] = new(
                Round(Math.Cos(angle) * radius) + random.Next(-14, 15),
                Round(Math.Sin(angle) * radius * 0.58) + random.Next(-14, 15),
                arm);
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
        GeneratedPosition[] positions = new GeneratedPosition[count];
        for (int index = 0; index < count; index++)
        {
            double angle = Math.PI + index * FullCircle / count;
            int radialJitter = random.Next(-24, 25);
            int x = Round(Math.Cos(angle) * (790 + radialJitter));
            int y = Round(Math.Sin(angle) * (360 + radialJitter / 2.0));
            positions[index] = new(x, y, index * 4 / count);
        }

        return positions;
    }

    private static void ConnectSpiral(int count, Action<int, int> connect)
    {
        int arms = ArmCount(count);
        for (int arm = 0; arm < arms; arm++)
        {
            for (int index = arm; index + arms < count; index += arms)
            {
                connect(index, index + arms);
            }

            connect(arm, (arm + 1) % arms);
        }

        int layers = (count + arms - 1) / arms;
        for (int layer = 2; layer < layers; layer += 3)
        {
            int first = layer * arms;
            for (int arm = 0; arm < arms; arm++)
            {
                int left = first + arm;
                int right = first + ((arm + 1) % arms);
                if (left < count && right < count)
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

    private static void ConnectRing(int count, Action<int, int> connect)
    {
        for (int index = 0; index < count; index++)
        {
            connect(index, (index + 1) % count);
        }

        int quarter = Math.Max(2, count / 4);
        for (int index = 0; index < count; index += 4)
        {
            connect(index, (index + quarter) % count);
        }
    }

    private static long DistanceSquared(StarSystemState first, StarSystemState second)
    {
        long deltaX = first.DisplayX - second.DisplayX;
        long deltaY = first.DisplayY - second.DisplayY;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static int ArmCount(int systemCount) => systemCount switch {
        >= 256 => 4,
        >= 64 => 3,
        _ => 2,
    };

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
