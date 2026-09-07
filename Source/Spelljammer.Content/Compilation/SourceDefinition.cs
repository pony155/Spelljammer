using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

internal enum DefinitionKind : byte
{
    Ability,
    Skill,
    Access,
    Background,
    Character,
    Feat,
    Heritage,
    Feat,
    Race,
    Spell,
    PsychicTechnique,
    Technique,
    TrainingProject,
    Equipment,
    BoardCell,
    ZoneLink,
    PersonalBoard,
    Encounter,
    ShipFrame,
    ShipModule,
    ShipWeaponConfiguration
}

internal sealed record SourceDefinition(
    DefinitionKind Kind,
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    string PackId,
    string RelativePath,
    IReadOnlyDictionary<string, int> Integers,
    IReadOnlyDictionary<string, string> Strings,
    IReadOnlyDictionary<string, ImmutableArray<string>> Arrays,
    AbilitySourceDto? Ability,
    SkillSourceDto? Skill);

internal sealed record AbilitySourceDto(
    int Minimum,
    int Maximum,
    int DefaultValue,
    ImmutableArray<string> Tags);

internal sealed record SkillSourceDto(
    int Minimum,
    int Maximum,
    ContentId ProgressionCurveId,
    ImmutableArray<ContentId> ActionTags);

internal sealed record CandidatePack(
    Sources.IContentPackSource Source,
    Manifests.PackManifest Manifest,
    ImmutableArray<string> Entries);
