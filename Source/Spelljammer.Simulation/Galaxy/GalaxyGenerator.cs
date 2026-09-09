using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Galaxy;

public sealed record GalaxyGenerationSettings(int SystemCount = 16, int GeneratorVersion = 1)
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
        if (settings.GeneratorVersion <= 0 ||
            settings.SystemCount is < GalaxyGenerationSettings.MinimumSystemCount or > GalaxyLimits.MaximumSystems)
        {
            return new(null, null, GalaxyValidationCode.InvalidHeader);
        }

        DeterministicStream random = new(seed ^ 0x67616c617879UL);
        StarSystemState[] systems = new StarSystemState[settings.SystemCount];
        string seedPart = $"g{seed:x16}";
        for (int index = 0; index < systems.Length; index++)
        {
            int region = index < (systems.Length / 2) ? 0 : 1;
            int column = index % ((systems.Length + 3) / 4);
            int row = (index / Math.Max(1, (systems.Length + 3) / 4)) % 2;
            int centerX = region == 0 ? -700 : 700;
            systems[index] = new(
                new StarSystemId($"system.{seedPart}.n{index:0000}"),
                index,
                region,
                centerX + (column * 170) + random.Next(-45, 46),
                ((row * 300) - 150) + random.Next(-60, 61),
                Archetypes[index % Archetypes.Length]);
        }

        List<StarwayState> starways = [];
        HashSet<(int, int)> connections = [];
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
        }

        int split = systems.Length / 2;
        for (int region = 0; region < 2; region++)
        {
            int regionStart = region == 0 ? 0 : split;
            int regionEnd = region == 0 ? split : systems.Length;
            for (int index = regionStart; index < regionEnd - 1; index++)
            {
                Connect(index, index + 1);
            }

            Connect(regionStart, regionEnd - 1);
            if (regionEnd - regionStart > 3)
            {
                Connect(regionStart, regionStart + 2);
            }
        }

        Connect(split - 1, split);
        Connect(Math.Max(1, split - 3), Math.Min(systems.Length - 1, split + 2));

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
