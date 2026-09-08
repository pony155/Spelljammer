using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// Defines data-driven tactical cells, zone links, boards, and encounter layouts.
/// </summary>
/// <remarks>
/// Code flow: Content compilation validates and links stable geometry IDs, the encounter catalog publishes definitions, and board creation materializes bounded cells and connections.
/// </remarks>
public sealed record BoardCellDefinition(
    CellId CellId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ZoneId ZoneId,
    int Q,
    int R,
    int Capacity,
    int Cover,
    int Visibility,
    ContentId AtmosphereId,
    ContentId GravityId,
    ImmutableArray<string> HazardTags)
    : ContentDefinition(CellId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ZoneLinkDefinition(
    LinkId LinkId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    CellId FromCellId,
    CellId ToCellId,
    ContentId AccessId,
    int OneWay,
    int AllowsRetreat)
    : ContentDefinition(LinkId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record PersonalBoardDefinition(
    PersonalBoardId PersonalBoardId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int MaximumOccupants,
    ImmutableArray<CellId> CellIds,
    ImmutableArray<LinkId> LinkIds,
    ImmutableArray<ObjectiveId> RequiredObjectiveIds,
    ImmutableArray<CellId> RetreatCellIds)
    : ContentDefinition(PersonalBoardId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record EncounterDefinition(
    EncounterId EncounterId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    PersonalBoardId PersonalBoardId,
    ContentId ContextId,
    TeamId HostileTeamId,
    ContentId AncientDefenseId,
    ObjectiveId NonCombatObjectiveId,
    ObjectiveId ExtractionObjectiveId)
    : ContentDefinition(EncounterId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
