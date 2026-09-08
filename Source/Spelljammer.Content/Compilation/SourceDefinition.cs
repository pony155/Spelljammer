using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

internal enum DefinitionKind : byte
{
    Ability,
    Skill,
    LevelProgressionTable,
    CharacterResourceProfile,
    Access,
    Background,
    Character,
    Scenario,
    Feat,
    Heritage,
    Race,
    TrainingProject,
    Equipment,
    MeleeWeapon,
    MeleeWeaponAction,
    RangedWeapon,
    Ammunition,
    RangedWeaponAction,
    Effect,
    Status,
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
    IReadOnlyDictionary<string, ImmutableArray<int>> IntegerArrays,
    AbilitySourceDto? Ability,
    SkillSourceDto? Skill,
    StatusSourceDto? Status,
    ImmutableArray<LevelProgressionEntry> LevelProgressionEntries);

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

internal sealed record StatusModifierSourceDto(string Type, int Amount);

internal sealed record StatusRestrictionSourceDto(string Type, string? TargetRule, string? ActionTag);

internal sealed record StatusAiRuleSourceDto(string Type, string? TargetRule, string? ActionTag, int Amount);

internal sealed record StatusSourceDto(
    ImmutableArray<StatusModifierSourceDto> Modifiers,
    ImmutableArray<StatusRestrictionSourceDto> Restrictions,
    ImmutableArray<StatusAiRuleSourceDto> AiRules);

internal sealed record CandidatePack(
    Sources.IContentPackSource Source,
    Manifests.PackManifest Manifest,
    ImmutableArray<string> Entries);
