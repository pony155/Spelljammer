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
/// Coordinates the complete deterministic gameplay-content compilation pipeline.
/// </summary>
/// <remarks>
/// Code flow: Pack sources are loaded and ordered, definitions and default locale keys are parsed, linking and validation run, and success produces one fingerprinted immutable snapshot.
/// </remarks>
public sealed partial class GameContentCompiler
{
    private static readonly HashSet<string> RuntimePrimitives = new(StringComparer.Ordinal)
    {
        "progression.skill.standard",
        "action.spell.cast",
        "action.spell.identify",
        "resource.health",
        "resource.stamina",
        "resource.mana",
        "resource.resolve",
        "resource.strain",
        "resource.training-supplies",
        "facility.arcane-study",
        "facility.quiet-sanctum",
        "safety.supervised",
        "range.far",
        "range.near",
        "contact.invited",
        "psionics.discipline.contact",
        "psionics.scope.deliberate-message",
        "equipment-slot.main-hand",
        "equipment-slot.off-hand",
        "equipment-slot.body",
        "equipment-slot.utility",
        "equipment-slot.relic",
        "equipment-state.ready",
        "resource.equipment-charge",
        "resource.ballistic-ammunition",
        "resource.aether-charge",
        "resource.propellant",
        "resource.diesel-shell",
        "resource.spare-parts",
        "action.personal.melee",
        "action.personal.ranged",
        "action.personal.defend",
        "action.personal.engineering",
        "action.personal.medicine",
        "action.personal.spell",
        "zone.ruin.entry",
        "zone.ruin.gantry",
        "zone.ruin.archive",
        "zone.ruin.reactor",
        "zone.ruin.sanctum",
        "zone.ruin.extraction",
        "atmosphere.breathable",
        "gravity.standard",
        "traversal.open",
        "objective.ruin.extract-relic",
        "objective.ruin.disable-defense",
        "combat.context.ruin",
        "team.ruin.sentinels",
        "object.ruin.ancient-defense",
        "ship.path.arcane",
        "ship.path.industrial",
        "mount.power",
        "mount.propulsion",
        "mount.armor",
        "mount.shield",
        "mount.prow",
        "mount.weapon",
        "mount.cargo",
        "network.aether",
        "network.power",
        "network.supplies",
        "effect.ship.energy-generation",
        "effect.ship.propulsion",
        "effect.ship.armor",
        "effect.ship.shield",
        "effect.ship.ram",
        "effect.ship.weapon-mount",
        "effect.ship.cargo",
        "damage.arcane",
        "damage.kinetic",
        "area.single-target",
    };

    private readonly ContentLimits limits;

    public GameContentCompiler(ContentLimits? limits = null)
    {
        this.limits = limits ?? ContentLimits.Version1;
        this.limits.Validate();
    }

    public ContentCompilationResult Compile(
        IReadOnlyList<IContentPackSource> packSources,
        SemanticVersion gameVersion)
    {
        ArgumentNullException.ThrowIfNull(packSources);
        DiagnosticSink diagnostics = new(limits);
        if (packSources.Count > limits.EnabledPacks)
        {
            diagnostics.Limit("enabled-packs");
            return Failed(diagnostics);
        }

        List<CandidatePack> discovered = [];
        foreach (IContentPackSource source in packSources)
        {
            ImmutableArray<string> entries;
            try
            {
                IReadOnlyList<string> sourceEntries = source.EnumerateFiles();
                entries = [.. sourceEntries.Order(StringComparer.Ordinal)];
            }
            catch (ContentSourceException exception)
            {
                return IoFailed(diagnostics, exception);
            }

            if (entries.Any(entry => !SourceValidation.IsRelativePath(entry)))
            {
                diagnostics.Add(ContentDiagnosticCodes.PathInvalid);
                continue;
            }

            int manifestCount = entries.Count(entry => entry == "manifest.json");
            if (manifestCount == 0)
            {
                diagnostics.Add(ContentDiagnosticCodes.ManifestMissing, relativePath: "manifest.json");
                continue;
            }

            if (manifestCount != 1)
            {
                diagnostics.Add(ContentDiagnosticCodes.ManifestMultiple, relativePath: "manifest.json");
                continue;
            }

            byte[] manifestBytes;
            try
            {
                manifestBytes = source.ReadFile("manifest.json", limits.ManifestBytes);
            }
            catch (ContentSourceLimitException)
            {
                diagnostics.Limit("manifest-bytes", relativePath: "manifest.json");
                continue;
            }
            catch (ContentSourceException exception)
            {
                return IoFailed(diagnostics, exception);
            }

            if (manifestBytes.Length > limits.ManifestBytes)
            {
                diagnostics.Limit("manifest-bytes", relativePath: "manifest.json");
                continue;
            }

            PackManifest? manifest = ManifestParser.Parse(manifestBytes, limits, diagnostics);
            if (manifest is not null)
            {
                discovered.Add(new CandidatePack(source, manifest, entries));
            }
        }

        if (diagnostics.HasErrors)
        {
            return Failed(diagnostics);
        }

        ImmutableArray<CandidatePack> orderedPacks = OrderPacks(discovered, gameVersion, diagnostics);
        if (diagnostics.HasErrors)
        {
            return Failed(diagnostics);
        }

        List<SourceDefinition> definitions = ParseDefinitions(orderedPacks, diagnostics, out ContentIoFailure? ioFailure);
        if (ioFailure is not null)
        {
            return new ContentCompilationResult(null, diagnostics.ToImmutable(), ioFailure);
        }

        if (diagnostics.HasErrors || !Claim(definitions, diagnostics) || !Link(definitions, diagnostics) || !Validate(definitions, diagnostics))
        {
            return Failed(diagnostics);
        }

        if (!ValidateDefaultLocalization(orderedPacks, definitions, diagnostics, out ioFailure))
        {
            return ioFailure is null
                ? Failed(diagnostics)
                : new ContentCompilationResult(null, diagnostics.ToImmutable(), ioFailure);
        }

        return CompileSnapshot(orderedPacks, definitions, diagnostics);
    }

    private static void RequireNonempty(SourceDefinition definition, string field, DiagnosticSink diagnostics)
    {
        if (definition.Arrays[field].IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/" + field);
        }
    }

    private static void OutOfRange(SourceDefinition definition, string property, DiagnosticSink diagnostics) =>
        diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, definition.PackId, definition.RelativePath, definition.Id.ToString(), property);

    private static void CheckPrimitive(SourceDefinition definition, string id, string property, DiagnosticSink diagnostics)
    {
        if (!RuntimePrimitives.Contains(id))
        {
            Unknown(definition, id, property, diagnostics);
        }
    }

    private static void CheckPrimitives(SourceDefinition definition, IEnumerable<string> ids, string property, DiagnosticSink diagnostics)
    {
        foreach (string id in ids)
        {
            CheckPrimitive(definition, id, property, diagnostics);
        }
    }

    private static void CheckReferences(
        SourceDefinition definition,
        IEnumerable<string> ids,
        DefinitionKind kind,
        IReadOnlyDictionary<string, SourceDefinition> byId,
        string property,
        DiagnosticSink diagnostics)
    {
        foreach (string id in ids)
        {
            CheckReference(definition, id, kind, byId, property, diagnostics);
        }
    }

    private static void CheckReference(
        SourceDefinition definition,
        string id,
        DefinitionKind kind,
        IReadOnlyDictionary<string, SourceDefinition> byId,
        string property,
        DiagnosticSink diagnostics)
    {
        if (!byId.TryGetValue(id, out SourceDefinition? target) || target.Kind != kind)
        {
            Unknown(definition, id, property, diagnostics);
        }
    }

    private static void Unknown(SourceDefinition definition, string id, string property, DiagnosticSink diagnostics) =>
        diagnostics.Add(ContentDiagnosticCodes.ReferenceUnknown, definition.PackId, definition.RelativePath,
            definition.Id.ToString(), property, ContentDiagnosticArgument.SafeId(id));

    private static string Prefix(DefinitionKind kind) => kind switch
    {
        DefinitionKind.WorldTime => "world-time.",
        DefinitionKind.Calendar => "calendar.",
        DefinitionKind.TimeScale => "time-scale.",
        DefinitionKind.Ability => "ability.",
        DefinitionKind.Skill => "skill.",
        DefinitionKind.LevelProgressionTable => "level-progression.",
        DefinitionKind.CharacterResourceProfile => "character-resources.",
        DefinitionKind.Access => "access.",
        DefinitionKind.Background => "background.",
        DefinitionKind.Character => "character.",
        DefinitionKind.Scenario => "scenario.",
        DefinitionKind.Feat => "feat.",
        DefinitionKind.Heritage => "heritage.",
        DefinitionKind.Race => "race.",
        DefinitionKind.TrainingProject => "training.",
        DefinitionKind.Equipment => "equipment.",
        DefinitionKind.MeleeWeapon => "melee-weapon.",
        DefinitionKind.MeleeWeaponAction => "melee-action.",
        DefinitionKind.RangedWeapon => "ranged-weapon.",
        DefinitionKind.Ammunition => "ammunition.",
        DefinitionKind.RangedWeaponAction => "ranged-action.",
        DefinitionKind.Effect => "effect.",
        DefinitionKind.Status => "status.",
        DefinitionKind.BoardCell => "cell.",
        DefinitionKind.ZoneLink => "link.",
        DefinitionKind.PersonalBoard => "board.",
        DefinitionKind.Encounter => "encounter.",
        DefinitionKind.ShipFrame => "frame.",
        DefinitionKind.ShipModule => "module.",
        DefinitionKind.ShipWeaponConfiguration => "ship.weapon.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool Owns(string packId, string definitionId)
    {
        string[] segments = definitionId.Split('.');
        if (packId == "spelljammer.base")
        {
            return !segments.Contains("mod", StringComparer.Ordinal);
        }

        string namespaceName = packId["mod.".Length..];
        return segments.Length >= 4 && segments[1] == "mod" && segments[2] == namespaceName;
    }

    private static ContentCompilationResult Failed(DiagnosticSink diagnostics) =>
        new(null, diagnostics.ToImmutable(), null);

    private ContentCompilationResult IoFailed(DiagnosticSink diagnostics, ContentSourceException exception) =>
        new(null, diagnostics.ToImmutable(), CreateIoFailure(exception));

    private ContentIoFailure CreateIoFailure(ContentSourceException exception)
    {
        string? relativePath = exception.RelativePath;
        if (relativePath is null || !SourceValidation.IsRelativePath(relativePath) ||
            System.Text.Encoding.UTF8.GetByteCount(relativePath) > limits.DiagnosticArgumentBytes)
        {
            relativePath = null;
        }

        return new ContentIoFailure(exception.Kind, relativePath);
    }

    private sealed class PackOrderComparer : IComparer<ContentId>
    {
        public int Compare(ContentId left, ContentId right)
        {
            bool leftBase = left.ToString() == "spelljammer.base";
            bool rightBase = right.ToString() == "spelljammer.base";
            if (leftBase != rightBase)
            {
                return leftBase ? -1 : 1;
            }

            return left.CompareTo(right);
        }
    }
}
