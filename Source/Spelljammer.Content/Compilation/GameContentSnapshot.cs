using System.Collections.Frozen;
using System.Collections.Immutable;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

public sealed record ContentPackIdentity(ContentId Id, SemanticVersion Version, int ContentRevision);

public sealed class GameContentSnapshot : ICharacterContentCatalog
{
    private readonly FrozenDictionary<ContentId, ContentDefinition> definitionsById;
    private readonly FrozenDictionary<ContentId, ContentId> provenanceById;

    internal GameContentSnapshot(
        ContentFingerprint fingerprint,
        ImmutableArray<ContentPackIdentity> packs,
        ImmutableArray<AttributeDefinition> attributes,
        ImmutableArray<SkillDefinition> skills,
        ImmutableArray<AccessDefinition> access,
        ImmutableArray<BackgroundDefinition> backgrounds,
        ImmutableArray<CharacterDefinition> characters,
        ImmutableArray<FeatDefinition> feats,
        ImmutableArray<HeritageDefinition> heritages,
        ImmutableArray<PerkDefinition> perks,
        ImmutableArray<RaceDefinition> races,
        ImmutableArray<SpellDefinition> spells,
        ImmutableArray<PsychicTechniqueDefinition> psychicTechniques,
        ImmutableArray<TechniqueDefinition> techniques,
        ImmutableArray<TrainingProjectDefinition> trainingProjects,
        ImmutableArray<EquipmentDefinition> equipment,
        ImmutableArray<BoardCellDefinition> boardCells,
        ImmutableArray<ZoneLinkDefinition> zoneLinks,
        ImmutableArray<PersonalBoardDefinition> personalBoards,
        ImmutableArray<EncounterDefinition> encounters,
        ImmutableArray<ShipFrameDefinition> shipFrames,
        ImmutableArray<ShipModuleDefinition> shipModules,
        ImmutableArray<ShipWeaponConfigurationDefinition> shipWeaponConfigurations,
        ImmutableArray<byte> canonicalSemanticContent,
        IReadOnlyDictionary<ContentId, ContentId> provenance)
    {
        Fingerprint = fingerprint;
        Packs = packs;
        Attributes = attributes;
        Skills = skills;
        Access = access;
        Backgrounds = backgrounds;
        Characters = characters;
        Feats = feats;
        Heritages = heritages;
        Perks = perks;
        Races = races;
        Spells = spells;
        PsychicTechniques = psychicTechniques;
        Techniques = techniques;
        TrainingProjects = trainingProjects;
        Equipment = equipment;
        BoardCells = boardCells;
        ZoneLinks = zoneLinks;
        PersonalBoards = personalBoards;
        Encounters = encounters;
        ShipFrames = shipFrames;
        ShipModules = shipModules;
        ShipWeaponConfigurations = shipWeaponConfigurations;
        CanonicalSemanticContent = canonicalSemanticContent;
        AttributeRegistry = new TypedDefinitionRegistry<AttributeId, AttributeDefinition>(
            fingerprint, attributes, definition => definition.AttributeId);
        SkillRegistry = new TypedDefinitionRegistry<SkillId, SkillDefinition>(
            fingerprint, skills, definition => definition.SkillId);
        AccessRegistry = new TypedDefinitionRegistry<AccessId, AccessDefinition>(fingerprint, access, definition => definition.AccessId);
        BackgroundRegistry = new TypedDefinitionRegistry<BackgroundId, BackgroundDefinition>(fingerprint, backgrounds, definition => definition.BackgroundId);
        CharacterRegistry = new TypedDefinitionRegistry<CharacterId, CharacterDefinition>(fingerprint, characters, definition => definition.CharacterId);
        FeatRegistry = new TypedDefinitionRegistry<FeatId, FeatDefinition>(fingerprint, feats, definition => definition.FeatId);
        HeritageRegistry = new TypedDefinitionRegistry<HeritageId, HeritageDefinition>(fingerprint, heritages, definition => definition.HeritageId);
        PerkRegistry = new TypedDefinitionRegistry<PerkId, PerkDefinition>(fingerprint, perks, definition => definition.PerkId);
        RaceRegistry = new TypedDefinitionRegistry<RaceId, RaceDefinition>(fingerprint, races, definition => definition.RaceId);
        SpellRegistry = new TypedDefinitionRegistry<SpellId, SpellDefinition>(fingerprint, spells, definition => definition.SpellId);
        PsychicTechniqueRegistry = new TypedDefinitionRegistry<PsychicTechniqueId, PsychicTechniqueDefinition>(fingerprint, psychicTechniques, definition => definition.PsychicTechniqueId);
        TechniqueRegistry = new TypedDefinitionRegistry<TechniqueId, TechniqueDefinition>(fingerprint, techniques, definition => definition.TechniqueId);
        TrainingProjectRegistry = new TypedDefinitionRegistry<TrainingProjectId, TrainingProjectDefinition>(fingerprint, trainingProjects, definition => definition.TrainingProjectId);
        EquipmentRegistry = new TypedDefinitionRegistry<EquipmentId, EquipmentDefinition>(fingerprint, equipment, definition => definition.EquipmentId);
        BoardCellRegistry = new TypedDefinitionRegistry<CellId, BoardCellDefinition>(fingerprint, boardCells, definition => definition.CellId);
        ZoneLinkRegistry = new TypedDefinitionRegistry<LinkId, ZoneLinkDefinition>(fingerprint, zoneLinks, definition => definition.LinkId);
        PersonalBoardRegistry = new TypedDefinitionRegistry<PersonalBoardId, PersonalBoardDefinition>(fingerprint, personalBoards, definition => definition.PersonalBoardId);
        EncounterRegistry = new TypedDefinitionRegistry<EncounterId, EncounterDefinition>(fingerprint, encounters, definition => definition.EncounterId);
        ShipFrameRegistry = new TypedDefinitionRegistry<ShipFrameId, ShipFrameDefinition>(fingerprint, shipFrames, definition => definition.ShipFrameId);
        ShipModuleRegistry = new TypedDefinitionRegistry<ModuleId, ShipModuleDefinition>(fingerprint, shipModules, definition => definition.ModuleId);
        ShipWeaponConfigurationRegistry = new TypedDefinitionRegistry<ShipWeaponConfigurationId, ShipWeaponConfigurationDefinition>(fingerprint, shipWeaponConfigurations, definition => definition.ShipWeaponConfigurationId);

        definitionsById = attributes.Cast<ContentDefinition>()
            .Concat(skills)
            .Concat(access)
            .Concat(backgrounds)
            .Concat(characters)
            .Concat(feats)
            .Concat(heritages)
            .Concat(perks)
            .Concat(races)
            .Concat(spells)
            .Concat(psychicTechniques)
            .Concat(techniques)
            .Concat(trainingProjects)
            .Concat(equipment)
            .Concat(boardCells)
            .Concat(zoneLinks)
            .Concat(personalBoards)
            .Concat(encounters)
            .Concat(shipFrames)
            .Concat(shipModules)
            .Concat(shipWeaponConfigurations)
            .ToFrozenDictionary(definition => definition.Id);
        provenanceById = provenance.ToFrozenDictionary();
    }

    public ContentFingerprint Fingerprint { get; }
    public ImmutableArray<ContentPackIdentity> Packs { get; }
    public ImmutableArray<AttributeDefinition> Attributes { get; }
    public ImmutableArray<SkillDefinition> Skills { get; }
    public ImmutableArray<AccessDefinition> Access { get; }
    public ImmutableArray<BackgroundDefinition> Backgrounds { get; }
    public ImmutableArray<CharacterDefinition> Characters { get; }
    public ImmutableArray<FeatDefinition> Feats { get; }
    public ImmutableArray<HeritageDefinition> Heritages { get; }
    public ImmutableArray<PerkDefinition> Perks { get; }
    public ImmutableArray<RaceDefinition> Races { get; }
    public ImmutableArray<SpellDefinition> Spells { get; }
    public ImmutableArray<PsychicTechniqueDefinition> PsychicTechniques { get; }
    public ImmutableArray<TechniqueDefinition> Techniques { get; }
    public ImmutableArray<TrainingProjectDefinition> TrainingProjects { get; }
    public ImmutableArray<EquipmentDefinition> Equipment { get; }
    public ImmutableArray<BoardCellDefinition> BoardCells { get; }
    public ImmutableArray<ZoneLinkDefinition> ZoneLinks { get; }
    public ImmutableArray<PersonalBoardDefinition> PersonalBoards { get; }
    public ImmutableArray<EncounterDefinition> Encounters { get; }
    public ImmutableArray<ShipFrameDefinition> ShipFrames { get; }
    public ImmutableArray<ShipModuleDefinition> ShipModules { get; }
    public ImmutableArray<ShipWeaponConfigurationDefinition> ShipWeaponConfigurations { get; }
    public ImmutableArray<byte> CanonicalSemanticContent { get; }
    public TypedDefinitionRegistry<AttributeId, AttributeDefinition> AttributeRegistry { get; }
    public TypedDefinitionRegistry<SkillId, SkillDefinition> SkillRegistry { get; }
    public TypedDefinitionRegistry<AccessId, AccessDefinition> AccessRegistry { get; }
    public TypedDefinitionRegistry<BackgroundId, BackgroundDefinition> BackgroundRegistry { get; }
    public TypedDefinitionRegistry<CharacterId, CharacterDefinition> CharacterRegistry { get; }
    public TypedDefinitionRegistry<FeatId, FeatDefinition> FeatRegistry { get; }
    public TypedDefinitionRegistry<HeritageId, HeritageDefinition> HeritageRegistry { get; }
    public TypedDefinitionRegistry<PerkId, PerkDefinition> PerkRegistry { get; }
    public TypedDefinitionRegistry<RaceId, RaceDefinition> RaceRegistry { get; }
    public TypedDefinitionRegistry<SpellId, SpellDefinition> SpellRegistry { get; }
    public TypedDefinitionRegistry<PsychicTechniqueId, PsychicTechniqueDefinition> PsychicTechniqueRegistry { get; }
    public TypedDefinitionRegistry<TechniqueId, TechniqueDefinition> TechniqueRegistry { get; }
    public TypedDefinitionRegistry<TrainingProjectId, TrainingProjectDefinition> TrainingProjectRegistry { get; }
    public TypedDefinitionRegistry<EquipmentId, EquipmentDefinition> EquipmentRegistry { get; }
    public TypedDefinitionRegistry<CellId, BoardCellDefinition> BoardCellRegistry { get; }
    public TypedDefinitionRegistry<LinkId, ZoneLinkDefinition> ZoneLinkRegistry { get; }
    public TypedDefinitionRegistry<PersonalBoardId, PersonalBoardDefinition> PersonalBoardRegistry { get; }
    public TypedDefinitionRegistry<EncounterId, EncounterDefinition> EncounterRegistry { get; }
    public TypedDefinitionRegistry<ShipFrameId, ShipFrameDefinition> ShipFrameRegistry { get; }
    public TypedDefinitionRegistry<ModuleId, ShipModuleDefinition> ShipModuleRegistry { get; }
    public TypedDefinitionRegistry<ShipWeaponConfigurationId, ShipWeaponConfigurationDefinition> ShipWeaponConfigurationRegistry { get; }

    public bool TryGetAttribute(AttributeId id, out AttributeDefinition? definition, out int index) =>
        TryGetIndexed(AttributeRegistry, id, out definition, out index);

    public bool TryGetSkill(SkillId id, out SkillDefinition? definition, out int index) =>
        TryGetIndexed(SkillRegistry, id, out definition, out index);

    public bool TryGetAccess(AccessId id, out AccessDefinition? definition) => AccessRegistry.TryGet(id, out definition);
    public bool TryGetBackground(BackgroundId id, out BackgroundDefinition? definition) => BackgroundRegistry.TryGet(id, out definition);
    public bool TryGetCharacter(CharacterId id, out CharacterDefinition? definition) => CharacterRegistry.TryGet(id, out definition);
    public bool TryGetFeat(FeatId id, out FeatDefinition? definition) => FeatRegistry.TryGet(id, out definition);
    public bool TryGetHeritage(HeritageId id, out HeritageDefinition? definition) => HeritageRegistry.TryGet(id, out definition);
    public bool TryGetPerk(PerkId id, out PerkDefinition? definition) => PerkRegistry.TryGet(id, out definition);
    public bool TryGetRace(RaceId id, out RaceDefinition? definition) => RaceRegistry.TryGet(id, out definition);
    public bool TryGetSpell(SpellId id, out SpellDefinition? definition) => SpellRegistry.TryGet(id, out definition);
    public bool TryGetPsychicTechnique(PsychicTechniqueId id, out PsychicTechniqueDefinition? definition) =>
        PsychicTechniqueRegistry.TryGet(id, out definition);
    public bool TryGetTechnique(TechniqueId id, out TechniqueDefinition? definition) => TechniqueRegistry.TryGet(id, out definition);
    public bool TryGetTrainingProject(TrainingProjectId id, out TrainingProjectDefinition? definition) => TrainingProjectRegistry.TryGet(id, out definition);
    public bool TryGetEquipment(EquipmentId id, out EquipmentDefinition? definition) => EquipmentRegistry.TryGet(id, out definition);
    public bool TryGetBoardCell(CellId id, out BoardCellDefinition? definition) => BoardCellRegistry.TryGet(id, out definition);
    public bool TryGetZoneLink(LinkId id, out ZoneLinkDefinition? definition) => ZoneLinkRegistry.TryGet(id, out definition);
    public bool TryGetPersonalBoard(PersonalBoardId id, out PersonalBoardDefinition? definition) => PersonalBoardRegistry.TryGet(id, out definition);
    public bool TryGetEncounter(EncounterId id, out EncounterDefinition? definition) => EncounterRegistry.TryGet(id, out definition);
    public bool TryGetShipFrame(ShipFrameId id, out ShipFrameDefinition? definition) => ShipFrameRegistry.TryGet(id, out definition);
    public bool TryGetShipModule(ModuleId id, out ShipModuleDefinition? definition) => ShipModuleRegistry.TryGet(id, out definition);
    public bool TryGetShipWeaponConfiguration(ShipWeaponConfigurationId id, out ShipWeaponConfigurationDefinition? definition) =>
        ShipWeaponConfigurationRegistry.TryGet(id, out definition);

    public bool TryGetDefinition(ContentId id, out ContentDefinition? definition) =>
        definitionsById.TryGetValue(id, out definition);

    public RegistryInspectionSnapshot Inspect()
    {
        List<RegistryInspectionEntry> entries = [];
        AddEntries(entries, "Attribute", Attributes, definition => definition.Id);
        AddEntries(entries, "Skill", Skills, definition => definition.Id);
        AddEntries(entries, "Access", Access, definition => definition.Id);
        AddEntries(entries, "Background", Backgrounds, definition => definition.Id);
        AddEntries(entries, "Character", Characters, definition => definition.Id);
        AddEntries(entries, "Feat", Feats, definition => definition.Id);
        AddEntries(entries, "Heritage", Heritages, definition => definition.Id);
        AddEntries(entries, "Perk", Perks, definition => definition.Id);
        AddEntries(entries, "Race", Races, definition => definition.Id);
        AddEntries(entries, "Spell", Spells, definition => definition.Id);
        AddEntries(entries, "PsychicTechnique", PsychicTechniques, definition => definition.Id);
        AddEntries(entries, "Technique", Techniques, definition => definition.Id);
        AddEntries(entries, "TrainingProject", TrainingProjects, definition => definition.Id);
        AddEntries(entries, "Equipment", Equipment, definition => definition.Id);
        AddEntries(entries, "BoardCell", BoardCells, definition => definition.Id);
        AddEntries(entries, "ZoneLink", ZoneLinks, definition => definition.Id);
        AddEntries(entries, "PersonalBoard", PersonalBoards, definition => definition.Id);
        AddEntries(entries, "Encounter", Encounters, definition => definition.Id);
        AddEntries(entries, "ShipFrame", ShipFrames, definition => definition.Id);
        AddEntries(entries, "ShipModule", ShipModules, definition => definition.Id);
        AddEntries(entries, "ShipWeaponConfiguration", ShipWeaponConfigurations, definition => definition.Id);
        return new RegistryInspectionSnapshot(
            Fingerprint,
            Packs.Length,
            definitionsById.Count,
            Attributes.Length,
            Skills.Length,
            [.. entries.OrderBy(entry => entry.Kind, StringComparer.Ordinal).ThenBy(entry => entry.Id, StringComparer.Ordinal)]);
    }

    private static bool TryGetIndexed<TId, TDefinition>(
        TypedDefinitionRegistry<TId, TDefinition> registry,
        TId id,
        out TDefinition? definition,
        out int index)
        where TId : struct
        where TDefinition : class
    {
        if (registry.TryGet(id, out definition) && registry.TryGetIndex(id, out ScopedContentIndex<TId> scoped))
        {
            index = scoped.Value;
            return true;
        }

        index = -1;
        return false;
    }

    private void AddEntries<TDefinition>(
        List<RegistryInspectionEntry> entries,
        string kind,
        ImmutableArray<TDefinition> definitions,
        Func<TDefinition, ContentId> selectId)
        where TDefinition : ContentDefinition
    {
        for (int index = 0; index < definitions.Length; index++)
        {
            TDefinition definition = definitions[index];
            ContentId id = selectId(definition);
            entries.Add(new RegistryInspectionEntry(
                kind,
                id.ToString(),
                provenanceById[id].ToString(),
                definition.Revision,
                index));
        }
    }
}
