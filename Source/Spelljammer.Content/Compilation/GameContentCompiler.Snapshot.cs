using System.Collections.Immutable;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Parsing;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;
using EquipmentDefinition = Spelljammer.Simulation.Items.EquipmentDefinition;
using ArmorDefinition = Spelljammer.Simulation.Items.ArmorDefinition;
using GearDefinition = Spelljammer.Simulation.Items.GearDefinition;
using AmmunitionDefinition = Spelljammer.Simulation.Items.AmmunitionDefinition;

namespace Spelljammer.Content.Compilation;

/// <summary>
/// Assembles validated source definitions into the immutable game-content snapshot.
/// </summary>
/// <remarks>
/// Code flow: Definitions are grouped and compiled by kind, typed registries and aggregate catalogs are created, canonical bytes produce fingerprints, and the completed snapshot enters the result.
/// </remarks>
public sealed partial class GameContentCompiler
{
    private ContentCompilationResult CompileSnapshot(
        ImmutableArray<CandidatePack> packs,
        IReadOnlyList<SourceDefinition> sources,
        DiagnosticSink diagnostics)
    {
        ImmutableArray<WorldTimeDefinition> worldTimes = [.. sources.Where(value => value.Kind == DefinitionKind.WorldTime)
            .OrderBy(value => value.Id).Select(CompileWorldTime)];
        ImmutableArray<CalendarDefinition> calendars = [.. sources.Where(value => value.Kind == DefinitionKind.Calendar)
            .OrderBy(value => value.Id).Select(CompileCalendar)];
        ImmutableArray<TimeScaleDefinition> timeScales = [.. sources.Where(value => value.Kind == DefinitionKind.TimeScale)
            .OrderBy(value => value.Id).Select(CompileTimeScale)];
        ImmutableArray<AbilityDefinition> abilities = [.. sources.Where(value => value.Kind == DefinitionKind.Ability).OrderBy(value => value.Id).Select(CompileAbility)];
        ImmutableArray<SkillDefinition> skills = [.. sources.Where(value => value.Kind == DefinitionKind.Skill).OrderBy(value => value.Id).Select(CompileSkill)];
        ImmutableArray<LevelProgressionTableDefinition> levelProgressionTables = [.. sources
            .Where(value => value.Kind == DefinitionKind.LevelProgressionTable)
            .OrderBy(value => value.Id)
            .Select(CompileLevelProgressionTable)];
        ImmutableArray<CharacterResourceProfileDefinition> characterResourceProfiles = [.. sources
            .Where(value => value.Kind == DefinitionKind.CharacterResourceProfile)
            .OrderBy(value => value.Id)
            .Select(CompileCharacterResourceProfile)];
        ImmutableArray<AccessDefinition> access = [.. sources.Where(value => value.Kind == DefinitionKind.Access).OrderBy(value => value.Id).Select(CompileAccess)];
        ImmutableArray<BackgroundDefinition> backgrounds = [.. sources.Where(value => value.Kind == DefinitionKind.Background).OrderBy(value => value.Id).Select(CompileBackground)];
        ImmutableArray<CharacterDefinition> characters = [.. sources.Where(value => value.Kind == DefinitionKind.Character).OrderBy(value => value.Id).Select(CompileCharacter)];
        ImmutableArray<ScenarioDefinition> scenarios = [.. sources.Where(value => value.Kind == DefinitionKind.Scenario).OrderBy(value => value.Id).Select(CompileScenario)];
        ImmutableArray<FeatDefinition> feats = [.. sources.Where(value => value.Kind == DefinitionKind.Feat).OrderBy(value => value.Id).Select(CompileFeat)];
        ImmutableArray<HeritageDefinition> heritages = [.. sources.Where(value => value.Kind == DefinitionKind.Heritage).OrderBy(value => value.Id).Select(CompileHeritage)];
        ImmutableArray<RaceDefinition> races = [.. sources.Where(value => value.Kind == DefinitionKind.Race).OrderBy(value => value.Id).Select(CompileRace)];
        ImmutableArray<TrainingProjectDefinition> training = [.. sources.Where(value => value.Kind == DefinitionKind.TrainingProject).OrderBy(value => value.Id).Select(CompileTraining)];
        ImmutableArray<EquipmentDefinition> equipment = [.. sources.Where(value => value.Kind == DefinitionKind.Equipment).OrderBy(value => value.Id).Select(CompileEquipment)];
        ImmutableArray<MeleeWeaponDefinition> meleeWeapons = [.. sources.Where(value => value.Kind == DefinitionKind.MeleeWeapon).OrderBy(value => value.Id).Select(CompileMeleeWeapon)];
        ImmutableArray<MeleeWeaponActionDefinition> meleeWeaponActions = [.. sources.Where(value => value.Kind == DefinitionKind.MeleeWeaponAction).OrderBy(value => value.Id).Select(CompileMeleeWeaponAction)];
        ImmutableArray<RangedWeaponDefinition> rangedWeapons = [.. sources.Where(value => value.Kind == DefinitionKind.RangedWeapon).OrderBy(value => value.Id).Select(CompileRangedWeapon)];
        ImmutableArray<AmmunitionDefinition> ammunition = [.. sources.Where(value => value.Kind == DefinitionKind.Ammunition).OrderBy(value => value.Id).Select(CompileAmmunition)];
        ImmutableArray<RangedWeaponActionDefinition> rangedWeaponActions = [.. sources.Where(value => value.Kind == DefinitionKind.RangedWeaponAction).OrderBy(value => value.Id).Select(CompileRangedWeaponAction)];
        ImmutableArray<EffectDefinition> effects = [.. sources.Where(value => value.Kind == DefinitionKind.Effect).OrderBy(value => value.Id).Select(CompileEffect)];
        ImmutableArray<StatusDefinition> statuses = [.. sources.Where(value => value.Kind == DefinitionKind.Status).OrderBy(value => value.Id).Select(CompileStatus)];
        ImmutableArray<BoardCellDefinition> boardCells = [.. sources.Where(value => value.Kind == DefinitionKind.BoardCell).OrderBy(value => value.Id).Select(CompileBoardCell)];
        ImmutableArray<ZoneLinkDefinition> zoneLinks = [.. sources.Where(value => value.Kind == DefinitionKind.ZoneLink).OrderBy(value => value.Id).Select(CompileZoneLink)];
        ImmutableArray<PersonalBoardDefinition> personalBoards = [.. sources.Where(value => value.Kind == DefinitionKind.PersonalBoard).OrderBy(value => value.Id).Select(CompilePersonalBoard)];
        ImmutableArray<EncounterDefinition> encounters = [.. sources.Where(value => value.Kind == DefinitionKind.Encounter).OrderBy(value => value.Id).Select(CompileEncounter)];
        ImmutableArray<ShipFrameDefinition> shipFrames = [.. sources.Where(value => value.Kind == DefinitionKind.ShipFrame).OrderBy(value => value.Id).Select(CompileShipFrame)];
        ImmutableArray<ShipModuleDefinition> shipModules = [.. sources.Where(value => value.Kind == DefinitionKind.ShipModule).OrderBy(value => value.Id).Select(CompileShipModule)];
        ImmutableArray<ShipWeaponConfigurationDefinition> shipWeapons = [.. sources.Where(value => value.Kind == DefinitionKind.ShipWeaponConfiguration).OrderBy(value => value.Id).Select(CompileShipWeapon)];
        ImmutableArray<ContentPackIdentity> identities = [.. packs.Select(pack => new ContentPackIdentity(
            pack.Manifest.Id, pack.Manifest.Version, pack.Manifest.ContentRevision))];
        ContentDefinition[] all = [.. worldTimes, .. calendars, .. timeScales, .. abilities, .. skills, .. levelProgressionTables, .. characterResourceProfiles, .. access, .. backgrounds, .. characters, .. scenarios, .. feats, .. heritages, .. races, .. training, .. equipment, .. meleeWeapons, .. meleeWeaponActions, .. rangedWeapons, .. ammunition, .. rangedWeaponActions, .. effects, .. statuses, .. boardCells, .. zoneLinks, .. personalBoards, .. encounters, .. shipFrames, .. shipModules, .. shipWeapons];
        (byte[] canonicalBytes, ContentFingerprint fingerprint) = CanonicalSemanticWriter.Write(identities, all);
        Dictionary<ContentId, ContentId> provenance = sources.ToDictionary(
            source => source.Id,
            source => new ContentId(source.PackId));
        GameContentSnapshot snapshot = new(fingerprint, identities, worldTimes, calendars, timeScales, abilities, skills, levelProgressionTables, characterResourceProfiles, access, backgrounds, characters, scenarios, feats, heritages, races, training,
            equipment, meleeWeapons, meleeWeaponActions, rangedWeapons, ammunition, rangedWeaponActions, effects, statuses,
            boardCells, zoneLinks, personalBoards, encounters, shipFrames, shipModules, shipWeapons,
            [.. canonicalBytes], provenance);
        return new ContentCompilationResult(snapshot, diagnostics.ToImmutable(), null);
    }
}
