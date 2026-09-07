using System.Collections.Immutable;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Parsing;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

public sealed class GameContentCompiler
{
    private static readonly HashSet<string> RuntimePrimitives = new(StringComparer.Ordinal)
    {
        "progression.skill.standard",
        "action.spell.cast",
        "action.spell.identify",
        "effect.recovery.soul-anchor",
        "effect.tracking.observed-trail",
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
        "effect.spirit.magic-missile-impact",
        "effect.psionics.shared-channel",
        "equipment-slot.main-hand",
        "equipment-slot.off-hand",
        "equipment-slot.body",
        "equipment-slot.utility",
        "equipment-slot.relic",
        "equipment-state.ready",
        "resource.equipment-charge",
        "resource.ballistic-ammunition",
        "resource.aether-charge",
        "resource.diesel-shell",
        "resource.spare-parts",
        "action.personal.melee",
        "action.personal.ranged",
        "action.personal.defend",
        "action.personal.engineering",
        "action.personal.medicine",
        "action.personal.spell",
        "effect.equipment.attack",
        "effect.equipment.armor",
        "effect.equipment.repair",
        "effect.equipment.treatment",
        "effect.equipment.focus",
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

    private bool ValidateDefaultLocalization(
        ImmutableArray<CandidatePack> packs,
        IReadOnlyList<SourceDefinition> definitions,
        DiagnosticSink diagnostics,
        out ContentIoFailure? ioFailure)
    {
        ioFailure = null;
        foreach (CandidatePack pack in packs)
        {
            string packId = pack.Manifest.Id.ToString();
            HashSet<string> keys = new(StringComparer.Ordinal);
            SortedSet<string> localeFiles = new(StringComparer.Ordinal);
            foreach (string localizationRoot in pack.Manifest.LocalizationRoots)
            {
                string prefix = localizationRoot + "/" + DefaultLocaleCatalogParser.DefaultLocale + "/";
                foreach (string entry in pack.Entries)
                {
                    if (entry.StartsWith(prefix, StringComparison.Ordinal) &&
                        entry.EndsWith(".sfloc.json", StringComparison.Ordinal))
                    {
                        localeFiles.Add(entry);
                    }
                }
            }

            foreach (string path in localeFiles)
            {
                byte[] bytes;
                try
                {
                    bytes = pack.Source.ReadFile(path, limits.DefinitionFileBytes);
                }
                catch (ContentSourceLimitException)
                {
                    diagnostics.Limit("localization-file-bytes", packId, path);
                    continue;
                }
                catch (ContentSourceException exception)
                {
                    ioFailure = CreateIoFailure(exception);
                    return false;
                }

                if (!DefaultLocaleCatalogParser.TryReadKeys(bytes, packId, path, limits, diagnostics, out IReadOnlyList<string> sourceKeys))
                {
                    continue;
                }

                foreach (string key in sourceKeys)
                {
                    if (!keys.Add(key))
                    {
                        diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, packId, path, propertyPath: "/messages/" + key);
                    }
                }
            }

            if (!keys.Contains(pack.Manifest.DisplayNameKey))
            {
                diagnostics.Add(
                    ContentDiagnosticCodes.LocalizationKeyMissing,
                    packId,
                    "manifest.json",
                    propertyPath: "/displayNameKey",
                    arguments: ContentDiagnosticArgument.SafeId(pack.Manifest.DisplayNameKey));
            }

            foreach (SourceDefinition definition in definitions.Where(value => value.PackId == packId).OrderBy(value => value.Id))
            {
                CheckLocalizationKey(definition, definition.NameKey, "/nameKey", keys, diagnostics);
                CheckLocalizationKey(definition, definition.DescriptionKey, "/descriptionKey", keys, diagnostics);
            }
        }

        return !diagnostics.HasErrors;
    }

    private static void CheckLocalizationKey(
        SourceDefinition definition,
        string key,
        string propertyPath,
        IReadOnlySet<string> availableKeys,
        DiagnosticSink diagnostics)
    {
        if (!availableKeys.Contains(key))
        {
            diagnostics.Add(
                ContentDiagnosticCodes.LocalizationKeyMissing,
                definition.PackId,
                definition.RelativePath,
                definition.Id.ToString(),
                propertyPath,
                ContentDiagnosticArgument.SafeId(key));
        }
    }

    private ImmutableArray<CandidatePack> OrderPacks(
        IReadOnlyList<CandidatePack> packs,
        SemanticVersion gameVersion,
        DiagnosticSink diagnostics)
    {
        Dictionary<ContentId, CandidatePack> byId = [];
        if (packs.Count > limits.GraphNodes)
        {
            diagnostics.Limit("graph-nodes");
            return [];
        }

        foreach (CandidatePack pack in packs)
        {
            if (!byId.TryAdd(pack.Manifest.Id, pack))
            {
                diagnostics.Add(ContentDiagnosticCodes.PackIdDuplicate, pack.Manifest.Id.ToString(), "manifest.json");
            }
            else if (!pack.Manifest.GameVersionRange.Contains(gameVersion))
            {
                diagnostics.Add(
                    ContentDiagnosticCodes.GameVersionIncompatible,
                    pack.Manifest.Id.ToString(),
                    "manifest.json",
                    arguments: ContentDiagnosticArgument.Version(gameVersion.ToString()));
            }
        }

        if (diagnostics.HasErrors)
        {
            return [];
        }

        Dictionary<ContentId, HashSet<ContentId>> successors = byId.Keys.ToDictionary(id => id, _ => new HashSet<ContentId>());
        Dictionary<ContentId, int> indegrees = byId.Keys.ToDictionary(id => id, _ => 0);
        int edgeCount = 0;
        foreach (CandidatePack pack in packs)
        {
            foreach (PackDependency dependency in pack.Manifest.Dependencies)
            {
                if (!byId.TryGetValue(dependency.Id, out CandidatePack? predecessor))
                {
                    diagnostics.Add(ContentDiagnosticCodes.DependencyMissing, pack.Manifest.Id.ToString(), "manifest.json",
                        arguments: ContentDiagnosticArgument.SafeId(dependency.Id.ToString()));
                    continue;
                }

                if (!dependency.VersionRange.Contains(predecessor.Manifest.Version))
                {
                    diagnostics.Add(ContentDiagnosticCodes.DependencyVersionMismatch, pack.Manifest.Id.ToString(), "manifest.json",
                        arguments: ContentDiagnosticArgument.SafeId(dependency.Id.ToString()));
                    continue;
                }

                AddEdge(dependency.Id, pack.Manifest.Id, successors, indegrees, ref edgeCount);
            }

            foreach (ContentId predecessor in pack.Manifest.LoadAfter)
            {
                if (byId.ContainsKey(predecessor))
                {
                    AddEdge(predecessor, pack.Manifest.Id, successors, indegrees, ref edgeCount);
                }
            }
        }

        if (edgeCount > limits.PackEdges)
        {
            diagnostics.Limit("dependency-and-load-after-edges");
        }

        if (diagnostics.HasErrors)
        {
            return [];
        }

        SortedSet<ContentId> ready = new(new PackOrderComparer());
        foreach ((ContentId id, int degree) in indegrees)
        {
            if (degree == 0)
            {
                ready.Add(id);
            }
        }

        List<CandidatePack> result = [];
        Dictionary<ContentId, int> depth = byId.Keys.ToDictionary(id => id, _ => 1);
        while (ready.Count != 0)
        {
            ContentId id = ready.Min;
            ready.Remove(id);
            result.Add(byId[id]);
            foreach (ContentId successor in successors[id].Order())
            {
                depth[successor] = Math.Max(depth[successor], depth[id] + 1);
                if (--indegrees[successor] == 0)
                {
                    ready.Add(successor);
                }
            }
        }

        if (result.Count != packs.Count)
        {
            diagnostics.Add(ContentDiagnosticCodes.DependencyCycle);
            return [];
        }

        if (depth.Values.Any(value => value > limits.DependencyDepth))
        {
            diagnostics.Limit("dependency-depth");
            return [];
        }

        return [.. result];
    }

    private static void AddEdge(
        ContentId predecessor,
        ContentId successor,
        Dictionary<ContentId, HashSet<ContentId>> successors,
        Dictionary<ContentId, int> indegrees,
        ref int edgeCount)
    {
        if (successors[predecessor].Add(successor))
        {
            indegrees[successor]++;
            edgeCount++;
        }
    }

    private List<SourceDefinition> ParseDefinitions(
        ImmutableArray<CandidatePack> packs,
        DiagnosticSink diagnostics,
        out ContentIoFailure? ioFailure)
    {
        ioFailure = null;
        List<SourceDefinition> definitions = [];
        int totalFiles = 0;
        long totalBytes = 0;
        foreach (CandidatePack pack in packs)
        {
            List<(string Path, DefinitionKind Kind)> files = [];
            foreach (string definitionRoot in pack.Manifest.DefinitionRoots)
            {
                string prefix = definitionRoot + "/";
                foreach (string entry in pack.Entries)
                {
                    if (!entry.StartsWith(prefix, StringComparison.Ordinal) || !entry.EndsWith(".json", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string underRoot = entry[prefix.Length..];
                    if (!DefinitionParser.TryGetKind(underRoot, out DefinitionKind kind))
                    {
                        diagnostics.Add(ContentDiagnosticCodes.KindMismatch, pack.Manifest.Id.ToString(), entry);
                        continue;
                    }

                    files.Add((entry, kind));
                }
            }

            files.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

            if (files.Count > limits.DefinitionFilesPerPack)
            {
                diagnostics.Limit("definition-files-per-pack", pack.Manifest.Id.ToString());
                continue;
            }

            totalFiles = checked(totalFiles + files.Count);
            if (totalFiles > limits.DefinitionFilesPerSet)
            {
                diagnostics.Limit("definition-files-per-content-set");
                return definitions;
            }

            long packBytes = 0;
            foreach ((string path, DefinitionKind kind) in files)
            {
                byte[] bytes;
                try
                {
                    bytes = pack.Source.ReadFile(path, limits.DefinitionFileBytes);
                }
                catch (ContentSourceLimitException)
                {
                    diagnostics.Limit("definition-file-bytes", pack.Manifest.Id.ToString(), path);
                    continue;
                }
                catch (ContentSourceException exception)
                {
                    ioFailure = CreateIoFailure(exception);
                    return definitions;
                }

                packBytes = checked(packBytes + bytes.Length);
                totalBytes = checked(totalBytes + bytes.Length);
                if (packBytes > limits.DefinitionBytesPerPack)
                {
                    diagnostics.Limit("definition-bytes-per-pack", pack.Manifest.Id.ToString());
                    break;
                }

                if (totalBytes > limits.DefinitionBytesPerSet)
                {
                    diagnostics.Limit("definition-bytes-per-content-set");
                    return definitions;
                }

                SourceDefinition? definition = DefinitionParser.Parse(bytes, kind, pack.Manifest.Id.ToString(), path, limits, diagnostics);
                if (definition is not null)
                {
                    definitions.Add(definition);
                }
            }

        }

        return definitions;
    }

    private bool Claim(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        Dictionary<ContentId, SourceDefinition> byId = [];
        Dictionary<DefinitionKind, int> kindCounts = [];
        foreach (SourceDefinition definition in definitions.OrderBy(value => value.Id))
        {
            string expectedPrefix = Prefix(definition.Kind);
            if (!definition.Id.ToString().StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                diagnostics.Add(ContentDiagnosticCodes.KindMismatch, definition.PackId, definition.RelativePath, definition.Id.ToString());
                continue;
            }

            if (!Owns(definition.PackId, definition.Id.ToString()))
            {
                diagnostics.Add(ContentDiagnosticCodes.NamespaceViolation, definition.PackId, definition.RelativePath, definition.Id.ToString());
                continue;
            }

            if (!byId.TryAdd(definition.Id, definition))
            {
                diagnostics.Add(ContentDiagnosticCodes.DefinitionIdDuplicate, definition.PackId, definition.RelativePath, definition.Id.ToString());
            }

            kindCounts.TryGetValue(definition.Kind, out int count);
            if (++count > limits.DefinitionsPerKind)
            {
                diagnostics.Limit("definitions-per-kind", definition.PackId, definition.RelativePath);
            }

            kindCounts[definition.Kind] = count;
        }

        if (definitions.Count > limits.DefinitionsPerSet)
        {
            diagnostics.Limit("definitions-per-content-set");
        }

        if (definitions.Count > limits.GraphNodes)
        {
            diagnostics.Limit("graph-nodes");
        }

        return !diagnostics.HasErrors;
    }

    private bool Link(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        Dictionary<string, SourceDefinition> byId = definitions.ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        foreach (SourceDefinition definition in definitions.OrderBy(value => value.Id))
        {
            switch (definition.Kind)
            {
                case DefinitionKind.Skill:
                    CheckPrimitive(definition, definition.Strings["progressionCurveId"], "/progressionCurveId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["actionTags"], "/actionTags", diagnostics);
                    break;
                case DefinitionKind.Scenario:
                    if (definition.Strings.TryGetValue("levelProgressionTableId", out string? levelProgressionTableId))
                    {
                        CheckReference(definition, levelProgressionTableId, DefinitionKind.LevelProgressionTable, byId,
                            "/levelProgressionTableId", diagnostics);
                    }

                    if (definition.Strings.TryGetValue("characterResourceProfileId", out string? characterResourceProfileId))
                    {
                        CheckReference(definition, characterResourceProfileId, DefinitionKind.CharacterResourceProfile, byId,
                            "/characterResourceProfileId", diagnostics);
                    }

                    break;
                case DefinitionKind.Feat:
                    if (definition.Strings.TryGetValue("trainingProjectId", out string? trainingProjectId))
                    {
                        CheckReference(definition, trainingProjectId, DefinitionKind.TrainingProject, byId, "/trainingProjectId", diagnostics);
                    }

                    CheckReferences(definition, definition.Arrays["compatibleRaceIds"], DefinitionKind.Race, byId, "/compatibleRaceIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["requiredAccessIds"], DefinitionKind.Access, byId, "/requiredAccessIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedAccessIds"], DefinitionKind.Access, byId, "/grantedAccessIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["effectIds"], "/effectIds", diagnostics);
                    if (definition.Strings.TryGetValue("skillId", out string? featSkillId))
                    {
                        CheckReference(definition, featSkillId, DefinitionKind.Skill, byId, "/skillId", diagnostics);
                    }

                    if (definition.Strings.TryGetValue("resistanceSkillId", out string? resistanceSkillId))
                    {
                        CheckReference(definition, resistanceSkillId, DefinitionKind.Skill, byId, "/resistanceSkillId", diagnostics);
                    }

                    foreach (string field in new[] { "manaResourceId", "strainResourceId", "contactModeId", "rangeId", "informationScopeId" })
                    {
                        if (definition.Strings.TryGetValue(field, out string? primitive))
                        {
                            CheckPrimitive(definition, primitive, "/" + field, diagnostics);
                        }
                    }

                    CheckPrimitives(definition, definition.Arrays["disciplineIds"], "/disciplineIds", diagnostics);

                    break;
                case DefinitionKind.Background:
                    CheckReferences(definition, definition.Arrays["compatibleRaceIds"], DefinitionKind.Race, byId, "/compatibleRaceIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["abilityBonusIds"], DefinitionKind.Ability, byId, "/abilityBonusIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["focusSkillIds"], DefinitionKind.Skill, byId, "/focusSkillIds", diagnostics);
                    break;
                case DefinitionKind.Character:
                    CheckReference(definition, definition.Strings["raceId"], DefinitionKind.Race, byId, "/raceId", diagnostics);
                    CheckReference(definition, definition.Strings["heritageId"], DefinitionKind.Heritage, byId, "/heritageId", diagnostics);
                    CheckReference(definition, definition.Strings["backgroundId"], DefinitionKind.Background, byId, "/backgroundId", diagnostics);
                    CheckReferences(definition, definition.Arrays["scenarioIds"], DefinitionKind.Scenario, byId, "/scenarioIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["focusSkillIds"], DefinitionKind.Skill, byId, "/focusSkillIds", diagnostics);
                    break;
                case DefinitionKind.Heritage:
                    CheckReference(definition, definition.Strings["raceId"], DefinitionKind.Race, byId, "/raceId", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.Race:
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.TrainingProject:
                    CheckReferences(definition, definition.Arrays["requiredSkillIds"], DefinitionKind.Skill, byId, "/requiredSkillIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    CheckPrimitive(definition, definition.Strings["facilityId"], "/facilityId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["resourceId"], "/resourceId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["safetyId"], "/safetyId", diagnostics);
                    break;
                case DefinitionKind.Equipment:
                    CheckPrimitive(definition, definition.Strings["slotId"], "/slotId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["initialStateId"], "/initialStateId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["resourceId"], "/resourceId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["actionIds"], "/actionIds", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["effectIds"], "/effectIds", diagnostics);
                    break;
                case DefinitionKind.BoardCell:
                    CheckPrimitive(definition, definition.Strings["zoneId"], "/zoneId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["atmosphereId"], "/atmosphereId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["gravityId"], "/gravityId", diagnostics);
                    break;
                case DefinitionKind.ZoneLink:
                    CheckReference(definition, definition.Strings["fromCellId"], DefinitionKind.BoardCell, byId, "/fromCellId", diagnostics);
                    CheckReference(definition, definition.Strings["toCellId"], DefinitionKind.BoardCell, byId, "/toCellId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["accessId"], "/accessId", diagnostics);
                    break;
                case DefinitionKind.PersonalBoard:
                    CheckReferences(definition, definition.Arrays["cellIds"], DefinitionKind.BoardCell, byId, "/cellIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["linkIds"], DefinitionKind.ZoneLink, byId, "/linkIds", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["requiredObjectiveIds"], "/requiredObjectiveIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["retreatCellIds"], DefinitionKind.BoardCell, byId, "/retreatCellIds", diagnostics);
                    break;
                case DefinitionKind.Encounter:
                    CheckReference(definition, definition.Strings["personalBoardId"], DefinitionKind.PersonalBoard, byId, "/personalBoardId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["contextId"], "/contextId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["hostileTeamId"], "/hostileTeamId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["ancientDefenseId"], "/ancientDefenseId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["nonCombatObjectiveId"], "/nonCombatObjectiveId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["extractionObjectiveId"], "/extractionObjectiveId", diagnostics);
                    break;
                case DefinitionKind.ShipFrame:
                    CheckPrimitives(definition, definition.Arrays["mountIds"], "/mountIds", diagnostics);
                    break;
                case DefinitionKind.ShipModule:
                    CheckPrimitive(definition, definition.Strings["networkId"], "/networkId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["mountId"], "/mountId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["primaryEffectId"], "/primaryEffectId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["compatiblePathIds"], "/compatiblePathIds", diagnostics);
                    break;
                case DefinitionKind.ShipWeaponConfiguration:
                    CheckPrimitive(definition, definition.Strings["networkId"], "/networkId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["resourceId"], "/resourceId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["damageTypeId"], "/damageTypeId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["areaId"], "/areaId", diagnostics);
                    break;
            }
        }


        ValidateGrantCycles(definitions, diagnostics);

        return !diagnostics.HasErrors;
    }

    private bool Validate(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        IOrderedEnumerable<SourceDefinition> ordered = definitions.OrderBy(value => value.Id);
        Dictionary<string, SourceDefinition> byId = definitions.ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        foreach (SourceDefinition definition in ordered)
        {
            if (definition.Revision < 1)
            {
                OutOfRange(definition, "/revision", diagnostics);
            }

            switch (definition.Kind)
            {
                case DefinitionKind.Ability:
                    ValidateAbility(definition, diagnostics);
                    break;
                case DefinitionKind.Skill:
                    ValidateSkill(definition, diagnostics);
                    break;
                case DefinitionKind.LevelProgressionTable:
                    ValidateLevelProgressionTable(definition, diagnostics);
                    break;
                case DefinitionKind.CharacterResourceProfile:
                    ValidateCharacterResourceProfile(definition, diagnostics);
                    break;
                case DefinitionKind.Feat:
                    ValidateFeat(definition, diagnostics);
                    break;
                case DefinitionKind.Scenario when definition.Integers["maximumRosterSize"] is < 1 or > CharacterCapabilities.MaximumSetEntries:
                    OutOfRange(definition, "/maximumRosterSize", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["workUnits"] is < 1 or > 1_000_000:
                    OutOfRange(definition, "/workUnits", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["progressCap"] < definition.Integers["workUnits"] ||
                    definition.Integers["progressCap"] > 1_000_000:
                    OutOfRange(definition, "/progressCap", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["resourceCost"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/resourceCost", diagnostics);
                    break;
                case DefinitionKind.Equipment when definition.Integers["resourceCapacity"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/resourceCapacity", diagnostics);
                    break;
                case DefinitionKind.BoardCell when definition.Integers["q"] is < -1_024 or > 1_024 ||
                    definition.Integers["r"] is < -1_024 or > 1_024 || definition.Integers["capacity"] is < 1 or > 8 ||
                    definition.Integers["cover"] is < 0 or > 100 || definition.Integers["visibility"] is < 0 or > 100:
                    OutOfRange(definition, "/capacity", diagnostics);
                    break;
                case DefinitionKind.ZoneLink when definition.Integers["oneWay"] is < 0 or > 1 ||
                    definition.Integers["allowsRetreat"] is < 0 or > 1:
                    OutOfRange(definition, "/oneWay", diagnostics);
                    break;
                case DefinitionKind.PersonalBoard when definition.Integers["maximumOccupants"] is < 1 or > 256:
                    OutOfRange(definition, "/maximumOccupants", diagnostics);
                    break;
                case DefinitionKind.ShipFrame when definition.Integers["maximumHull"] is < 1 or > 1_000_000 ||
                    definition.Integers["baseArmor"] is < 0 or > 100_000 || definition.Integers["maximumSlots"] is < 1 or > 256 ||
                    definition.Integers["cargoCapacity"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/maximumHull", diagnostics);
                    break;
                case DefinitionKind.ShipModule when definition.Integers["slotCost"] is < 1 or > 256 ||
                    definition.Integers["cargoDisplacement"] is < 0 or > 1_000_000 ||
                    definition.Integers["maximumIntegrity"] is < 1 or > 1_000_000 ||
                    definition.Integers["energyGeneration"] is < 0 or > 1_000_000 ||
                    definition.Integers["energyConsumption"] is < 0 or > 1_000_000 ||
                    definition.Integers["armorValue"] is < 0 or > 100_000 ||
                    definition.Integers["shieldValue"] is < 0 or > 1_000_000 ||
                    definition.Integers["shieldRechargeRate"] is < 0 or > 1_000_000 ||
                    definition.Integers["shieldEnergyConsumptionRate"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/slotCost", diagnostics);
                    break;
                case DefinitionKind.ShipWeaponConfiguration when definition.Integers["resourceCost"] is < 1 or > 1_000_000 ||
                    definition.Integers["damage"] is < 1 or > 1_000_000 ||
                    definition.Integers["rateOfFireTicks"] is < 1 or > 1_000_000 ||
                    definition.Integers["effectiveRange"] is < 1 or > 1_000_000_000 ||
                    definition.Integers["maximumRange"] < definition.Integers["effectiveRange"] ||
                    definition.Integers["maximumRange"] > 1_000_000_000 ||
                    definition.Integers["reloadTicks"] is < 1 or > 1_000_000 ||
                    definition.Integers["armorPenetration"] is < 0 or > 100_000:
                    OutOfRange(definition, "/damage", diagnostics);
                    break;
            }
        }

        int totalReferences = 0;
        foreach (SourceDefinition definition in ordered)
        {
            int definitionReferences = 0;
            foreach ((string field, ImmutableArray<string> values) in definition.Arrays)
            {
                if (values.Length != values.Distinct(StringComparer.Ordinal).Count())
                {
                    diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, definition.PackId, definition.RelativePath,
                        definition.Id.ToString(), "/" + field);
                }

                if (field is not ("tags" or "targetTags" or "hazardTags"))
                {
                    totalReferences = checked(totalReferences + values.Length);
                    definitionReferences = checked(definitionReferences + values.Length);
                }
            }

            totalReferences = checked(totalReferences + definition.Strings.Count);
            definitionReferences = checked(definitionReferences + definition.Strings.Count);

            switch (definition.Kind)
            {
                case DefinitionKind.Background:
                    RequireNonempty(definition, "compatibleRaceIds", diagnostics);
                    RequireNonempty(definition, "focusSkillIds", diagnostics);
                    break;
                case DefinitionKind.Character:
                    ValidateCharacter(definition, byId, diagnostics);
                    RequireNonempty(definition, "scenarioIds", diagnostics);
                    RequireNonempty(definition, "languageIds", diagnostics);
                    RequireNonempty(definition, "scriptIds", diagnostics);
                    RequireNonempty(definition, "equipmentIds", diagnostics);
                    RequireNonempty(definition, "focusSkillIds", diagnostics);
                    RequireNonempty(definition, "resourceIds", diagnostics);
                    break;
                case DefinitionKind.Heritage:
                    ValidateHeritage(definition, byId, diagnostics);
                    RequireNonempty(definition, "grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.Race:
                    ValidateRace(definition, byId, diagnostics);
                    RequireNonempty(definition, "grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.TrainingProject:
                    RequireNonempty(definition, "requiredSkillIds", diagnostics);
                    if (definition.Arrays["grantedFeatIds"].IsEmpty)
                    {
                        diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                            definition.Id.ToString(), "/grantedFeatIds");
                    }
                    break;
                case DefinitionKind.Equipment:
                    RequireNonempty(definition, "actionIds", diagnostics);
                    RequireNonempty(definition, "effectIds", diagnostics);
                    break;
                case DefinitionKind.ZoneLink:
                    if (definition.Strings["fromCellId"] == definition.Strings["toCellId"])
                    {
                        diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                            definition.Id.ToString(), "/toCellId");
                    }
                    break;
                case DefinitionKind.PersonalBoard:
                    RequireNonempty(definition, "cellIds", diagnostics);
                    RequireNonempty(definition, "linkIds", diagnostics);
                    RequireNonempty(definition, "requiredObjectiveIds", diagnostics);
                    RequireNonempty(definition, "retreatCellIds", diagnostics);
                    ValidateBoard(definition, byId, diagnostics);
                    break;
                case DefinitionKind.ShipFrame:
                    RequireNonempty(definition, "mountIds", diagnostics);
                    break;
                case DefinitionKind.ShipModule:
                    RequireNonempty(definition, "compatiblePathIds", diagnostics);
                    break;
            }
        }

        foreach (SourceDefinition definition in ordered)
        {
            foreach ((string field, ImmutableArray<string> values) in definition.Arrays)
            {
                bool isTagField = field is "tags" or "targetTags" or "hazardTags";
                int maximum = isTagField ? limits.TagsPerDefinition : limits.ReferencesPerDefinition;
                if (values.Length > maximum)
                {
                    diagnostics.Limit(isTagField ? "tags-per-definition" : "references-per-definition",
                        definition.PackId, definition.RelativePath);
                }
            }

            int definitionReferences = definition.Arrays
                .Where(pair => pair.Key is not ("tags" or "targetTags" or "hazardTags"))
                .Sum(pair => pair.Value.Length);
            definitionReferences += definition.Strings.Count;

            if (definitionReferences > limits.ReferencesPerDefinition)
            {
                diagnostics.Limit("references-per-definition", definition.PackId, definition.RelativePath);
            }
        }

        if (totalReferences > limits.ReferencesPerSet)
        {
            diagnostics.Limit("references-per-content-set");
        }

        if (totalReferences > limits.GraphEdges)
        {
            diagnostics.Limit("graph-edges");
        }

        return !diagnostics.HasErrors;
    }

    private ContentCompilationResult CompileSnapshot(
        ImmutableArray<CandidatePack> packs,
        IReadOnlyList<SourceDefinition> sources,
        DiagnosticSink diagnostics)
    {
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
        ImmutableArray<BoardCellDefinition> boardCells = [.. sources.Where(value => value.Kind == DefinitionKind.BoardCell).OrderBy(value => value.Id).Select(CompileBoardCell)];
        ImmutableArray<ZoneLinkDefinition> zoneLinks = [.. sources.Where(value => value.Kind == DefinitionKind.ZoneLink).OrderBy(value => value.Id).Select(CompileZoneLink)];
        ImmutableArray<PersonalBoardDefinition> personalBoards = [.. sources.Where(value => value.Kind == DefinitionKind.PersonalBoard).OrderBy(value => value.Id).Select(CompilePersonalBoard)];
        ImmutableArray<EncounterDefinition> encounters = [.. sources.Where(value => value.Kind == DefinitionKind.Encounter).OrderBy(value => value.Id).Select(CompileEncounter)];
        ImmutableArray<ShipFrameDefinition> shipFrames = [.. sources.Where(value => value.Kind == DefinitionKind.ShipFrame).OrderBy(value => value.Id).Select(CompileShipFrame)];
        ImmutableArray<ShipModuleDefinition> shipModules = [.. sources.Where(value => value.Kind == DefinitionKind.ShipModule).OrderBy(value => value.Id).Select(CompileShipModule)];
        ImmutableArray<ShipWeaponConfigurationDefinition> shipWeapons = [.. sources.Where(value => value.Kind == DefinitionKind.ShipWeaponConfiguration).OrderBy(value => value.Id).Select(CompileShipWeapon)];
        ImmutableArray<ContentPackIdentity> identities = [.. packs.Select(pack => new ContentPackIdentity(
            pack.Manifest.Id, pack.Manifest.Version, pack.Manifest.ContentRevision))];
        ContentDefinition[] all = [.. abilities, .. skills, .. levelProgressionTables, .. characterResourceProfiles, .. access, .. backgrounds, .. characters, .. scenarios, .. feats, .. heritages, .. races, .. training, .. equipment, .. boardCells, .. zoneLinks, .. personalBoards, .. encounters, .. shipFrames, .. shipModules, .. shipWeapons];
        (byte[] canonicalBytes, ContentFingerprint fingerprint) = CanonicalSemanticWriter.Write(identities, all);
        Dictionary<ContentId, ContentId> provenance = sources.ToDictionary(
            source => source.Id,
            source => new ContentId(source.PackId));
        GameContentSnapshot snapshot = new(fingerprint, identities, abilities, skills, levelProgressionTables, characterResourceProfiles, access, backgrounds, characters, scenarios, feats, heritages, races, training,
            equipment, boardCells, zoneLinks, personalBoards, encounters, shipFrames, shipModules, shipWeapons,
            [.. canonicalBytes], provenance);
        return new ContentCompilationResult(snapshot, diagnostics.ToImmutable(), null);
    }

    private static AbilityDefinition CompileAbility(SourceDefinition value) => new(
        new AbilityId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        (short)value.Ability!.Minimum, (short)value.Ability.Maximum, (short)value.Ability.DefaultValue,
        Sort(value.Ability.Tags));

    private static SkillDefinition CompileSkill(SourceDefinition value) => new(
        new SkillId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        (byte)value.Skill!.Minimum, (byte)value.Skill.Maximum, value.Skill.ProgressionCurveId,
        [.. value.Skill.ActionTags.Order()]);

    private static LevelProgressionTableDefinition CompileLevelProgressionTable(SourceDefinition value) => new(
        new LevelProgressionTableId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.LevelProgressionEntries);

    private static CharacterResourceProfileDefinition CompileCharacterResourceProfile(SourceDefinition value)
    {
        CharacterResourceRule Rule(string id, string fieldPrefix, bool accumulates, string? thresholdsField = null) => new(
            new ResourceId(id),
            value.Integers[fieldPrefix + "Maximum"],
            value.Integers[fieldPrefix + "RecoveryRate"],
            accumulates,
            thresholdsField is null ? [] : value.IntegerArrays[thresholdsField]);

        return new CharacterResourceProfileDefinition(
            new CharacterResourceProfileId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
            [
                Rule("resource.health", "health", false),
                Rule("resource.stamina", "stamina", false),
                Rule("resource.mana", "mana", false),
                Rule("resource.resolve", "resolve", false, "resolveThresholdPercentages"),
                Rule("resource.strain", "strain", true, "strainThresholdPercentages"),
            ]);
    }

    private static AccessDefinition CompileAccess(SourceDefinition value) => new(
        new AccessId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey, Sort(value.Arrays["tags"]));

    private static BackgroundDefinition CompileBackground(SourceDefinition value) => new(
        new BackgroundId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["compatibleRaceIds"]).Select(item => new RaceId(item)).ToImmutableArray(),
        Sort(value.Arrays["abilityBonusIds"]).Select(item => new AbilityId(item)).ToImmutableArray(),
        Sort(value.Arrays["focusSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray());

    private static CharacterDefinition CompileCharacter(SourceDefinition value) => new(
        new CharacterId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new RaceId(value.Strings["raceId"]), new HeritageId(value.Strings["heritageId"]),
        new BackgroundId(value.Strings["backgroundId"]),
        Sort(value.Arrays["scenarioIds"]).Select(item => new ScenarioId(item)).ToImmutableArray(),
        new ContentId(value.Strings["positionId"]),
        Sort(value.Arrays["languageIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["scriptIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["equipmentIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["focusSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray(),
        Sort(value.Arrays["resourceIds"]).Select(item => new ResourceId(item)).ToImmutableArray());

    private static ScenarioDefinition CompileScenario(SourceDefinition value) => new(
        new ScenarioId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumRosterSize"],
        value.Strings.TryGetValue("levelProgressionTableId", out string? progressionTableId)
            ? new LevelProgressionTableId(progressionTableId)
            : null,
        value.Strings.TryGetValue("characterResourceProfileId", out string? resourceProfileId)
            ? new CharacterResourceProfileId(resourceProfileId)
            : null);

    private static FeatDefinition CompileFeat(SourceDefinition value) => new(
        new FeatId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Strings["activation"] == "active" ? FeatActivation.Active : FeatActivation.Passive,
        value.Strings.TryGetValue("trainingProjectId", out string? trainingProjectId)
            ? new TrainingProjectId(trainingProjectId)
            : null,
        Sort(value.Arrays["compatibleRaceIds"]).Select(item => new RaceId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredAccessIds"]).Select(item => new AccessId(item)).ToImmutableArray(),
        Sort(value.Arrays["grantedAccessIds"]).Select(item => new AccessId(item)).ToImmutableArray(),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray(),
        Sort(value.Arrays["effectIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        CompileSpellRules(value),
        CompilePsionicRules(value));

    private static SpellFeatRules? CompileSpellRules(SourceDefinition value) =>
        value.Strings.GetValueOrDefault("activeKind") == "spell"
            ? new SpellFeatRules(
                new SkillId(value.Strings["skillId"]), new ResourceId(value.Strings["manaResourceId"]),
                value.Integers["manaCost"], new ContentId(value.Strings["rangeId"]),
                value.Integers["castTimeTicks"], value.Integers["cooldownTicks"], Sort(value.Arrays["targetTags"]))
            : null;

    private static PsionicFeatRules? CompilePsionicRules(SourceDefinition value) =>
        value.Strings.GetValueOrDefault("activeKind") == "psionic"
            ? new PsionicFeatRules(
                new SkillId(value.Strings["skillId"]), new SkillId(value.Strings["resistanceSkillId"]),
                new ResourceId(value.Strings["strainResourceId"]), value.Integers["strainCost"],
                value.Integers["sustainCostPerTick"], new ContentId(value.Strings["contactModeId"]),
                new ContentId(value.Strings["rangeId"]), new ContentId(value.Strings["informationScopeId"]),
                Sort(value.Arrays["disciplineIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
                Sort(value.Arrays["targetTags"]))
            : null;

    private static RaceDefinition CompileRace(SourceDefinition value) => new(
        new RaceId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredSupportIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static HeritageDefinition CompileHeritage(SourceDefinition value) => new(
        new HeritageId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new RaceId(value.Strings["raceId"]),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray());

    private static TrainingProjectDefinition CompileTraining(SourceDefinition value) => new(
        new TrainingProjectId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["requiredSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray(),
        value.Integers["workUnits"],
        value.Integers["progressCap"], new ContentId(value.Strings["facilityId"]),
        new ResourceId(value.Strings["resourceId"]), value.Integers["resourceCost"],
        new ContentId(value.Strings["safetyId"]),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray());

    private static EquipmentDefinition CompileEquipment(SourceDefinition value) => new(
        new EquipmentId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new ContentId(value.Strings["slotId"]), new ContentId(value.Strings["initialStateId"]),
        new ResourceId(value.Strings["resourceId"]), value.Integers["resourceCapacity"],
        Sort(value.Arrays["actionIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["effectIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static BoardCellDefinition CompileBoardCell(SourceDefinition value) => new(
        new CellId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new ZoneId(value.Strings["zoneId"]), value.Integers["q"], value.Integers["r"],
        value.Integers["capacity"], value.Integers["cover"], value.Integers["visibility"],
        new ContentId(value.Strings["atmosphereId"]), new ContentId(value.Strings["gravityId"]),
        Sort(value.Arrays["hazardTags"]));

    private static ZoneLinkDefinition CompileZoneLink(SourceDefinition value) => new(
        new LinkId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new CellId(value.Strings["fromCellId"]), new CellId(value.Strings["toCellId"]),
        new ContentId(value.Strings["accessId"]),
        value.Integers["oneWay"], value.Integers["allowsRetreat"]);

    private static PersonalBoardDefinition CompilePersonalBoard(SourceDefinition value) => new(
        new PersonalBoardId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumOccupants"],
        Sort(value.Arrays["cellIds"]).Select(item => new CellId(item)).ToImmutableArray(),
        Sort(value.Arrays["linkIds"]).Select(item => new LinkId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredObjectiveIds"]).Select(item => new ObjectiveId(item)).ToImmutableArray(),
        Sort(value.Arrays["retreatCellIds"]).Select(item => new CellId(item)).ToImmutableArray());

    private static EncounterDefinition CompileEncounter(SourceDefinition value) => new(
        new EncounterId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new PersonalBoardId(value.Strings["personalBoardId"]), new ContentId(value.Strings["contextId"]),
        new TeamId(value.Strings["hostileTeamId"]), new ContentId(value.Strings["ancientDefenseId"]),
        new ObjectiveId(value.Strings["nonCombatObjectiveId"]), new ObjectiveId(value.Strings["extractionObjectiveId"]));

    private static ShipFrameDefinition CompileShipFrame(SourceDefinition value) => new(
        new ShipFrameId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumHull"], value.Integers["baseArmor"], value.Integers["maximumSlots"],
        value.Integers["cargoCapacity"], Sort(value.Arrays["mountIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static ShipModuleDefinition CompileShipModule(SourceDefinition value) => new(
        new ModuleId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["slotCost"], value.Integers["cargoDisplacement"], value.Integers["maximumIntegrity"],
        new NetworkId(value.Strings["networkId"]), value.Integers["energyGeneration"], value.Integers["energyConsumption"],
        new ContentId(value.Strings["mountId"]), new ContentId(value.Strings["primaryEffectId"]),
        value.Integers["armorValue"], value.Integers["shieldValue"], value.Integers["shieldRechargeRate"],
        value.Integers["shieldEnergyConsumptionRate"],
        Sort(value.Arrays["compatiblePathIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static ShipWeaponConfigurationDefinition CompileShipWeapon(SourceDefinition value) => new(
        new ShipWeaponConfigurationId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new NetworkId(value.Strings["networkId"]), new ResourceId(value.Strings["resourceId"]),
        value.Integers["resourceCost"], value.Integers["damage"], value.Integers["rateOfFireTicks"],
        value.Integers["effectiveRange"], value.Integers["maximumRange"], value.Integers["reloadTicks"],
        new ContentId(value.Strings["damageTypeId"]), new ContentId(value.Strings["areaId"]),
        value.Integers["armorPenetration"]);

    private static ImmutableArray<string> Sort(ImmutableArray<string> values) => [.. values.Order(StringComparer.Ordinal)];

    private static void ValidateAbility(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        AbilitySourceDto source = definition.Ability!;
        int minimum = source.Minimum;
        int maximum = source.Maximum;
        int defaultValue = source.DefaultValue;
        bool storageValid = minimum >= short.MinValue && maximum <= short.MaxValue && minimum <= maximum &&
            defaultValue >= minimum && defaultValue <= maximum;
        bool baseRangeValid = definition.PackId != "spelljammer.base" || minimum == 1 && maximum == 10;
        if (!storageValid || !baseRangeValid)
        {
            OutOfRange(definition, "/defaultValue", diagnostics);
        }
    }

    private static void ValidateSkill(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        SkillSourceDto source = definition.Skill!;
        int minimum = source.Minimum;
        int maximum = source.Maximum;
        bool storageValid = minimum >= byte.MinValue && maximum <= byte.MaxValue && minimum <= maximum;
        bool baseRangeValid = definition.PackId != "spelljammer.base" || minimum == 0 && maximum == 100;
        if (!storageValid || !baseRangeValid)
        {
            OutOfRange(definition, "/minimum", diagnostics);
        }
    }

    private static void ValidateLevelProgressionTable(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        ImmutableArray<LevelProgressionEntry> entries = definition.LevelProgressionEntries;
        if (entries.IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/levels");
            return;
        }

        int previousExperience = -1;
        for (int index = 0; index < entries.Length; index++)
        {
            LevelProgressionEntry entry = entries[index];
            string path = $"/levels/{index}";
            bool invalidLevel = entry.Level != index + 1;
            bool invalidExperience = index == 0
                ? entry.RequiredExperience != 0
                : entry.RequiredExperience <= previousExperience;
            bool invalidReward = entry.MaximumHealthIncrease < 0 || entry.MaximumManaIncrease < 0 || entry.MaximumStaminaIncrease < 0 ||
                entry.MaximumResolveIncrease < 0 || entry.MaximumStrainIncrease < 0 ||
                entry.AbilityPoints < 0 || entry.SkillPoints < 0 || entry.FeatPoints < 0;
            if (invalidLevel || invalidExperience || invalidReward)
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                    definition.Id.ToString(), path);
            }

            previousExperience = entry.RequiredExperience;
        }
    }

    private static void ValidateCharacterResourceProfile(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string[] prefixes = ["health", "stamina", "mana", "resolve", "strain"];
        if (prefixes.Any(prefix => definition.Integers[prefix + "Maximum"] <= 0 ||
                definition.Integers[prefix + "RecoveryRate"] < 0))
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/resources");
        }

        ImmutableArray<int> resolve = definition.IntegerArrays["resolveThresholdPercentages"];
        ImmutableArray<int> strain = definition.IntegerArrays["strainThresholdPercentages"];
        bool invalidResolve = resolve.IsEmpty || resolve.Any(value => value is < 0 or > 100) ||
            resolve.Zip(resolve.Skip(1)).Any(pair => pair.First <= pair.Second);
        bool invalidStrain = strain.IsEmpty || strain.Any(value => value is < 0 or > 100) ||
            strain.Zip(strain.Skip(1)).Any(pair => pair.First >= pair.Second);
        if (invalidResolve || invalidStrain)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), invalidResolve ? "/resolveThresholdPercentages" : "/strainThresholdPercentages");
        }
    }

    private static void ValidateFeat(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string activation = definition.Strings["activation"];
        definition.Strings.TryGetValue("activeKind", out string? activeKind);
        if (activation == "passive")
        {
            if (activeKind is not null)
            {
                Invalid("/activeKind");
            }

            return;
        }

        if (activation != "active" || activeKind is not ("general" or "spell" or "psionic"))
        {
            Invalid("/activation");
            return;
        }

        if (activeKind == "spell")
        {
            if (!HasStrings("skillId", "manaResourceId", "rangeId") ||
                !HasIntegers("manaCost", "castTimeTicks", "cooldownTicks") ||
                definition.Integers.GetValueOrDefault("manaCost") is < 1 or > 1_000_000 ||
                definition.Integers.GetValueOrDefault("castTimeTicks") is < 0 or > 10_000 ||
                definition.Integers.GetValueOrDefault("cooldownTicks") is < 0 or > 1_000_000 ||
                definition.Arrays["targetTags"].IsEmpty || definition.Arrays["effectIds"].IsEmpty)
            {
                Invalid("/activeKind");
            }
        }
        else if (activeKind == "psionic")
        {
            if (!HasStrings("skillId", "resistanceSkillId", "strainResourceId", "contactModeId", "rangeId", "informationScopeId") ||
                !HasIntegers("strainCost", "sustainCostPerTick") ||
                definition.Integers.GetValueOrDefault("strainCost") is < 1 or > 100 ||
                definition.Integers.GetValueOrDefault("sustainCostPerTick") is < 0 or > 100 ||
                definition.Arrays["disciplineIds"].IsEmpty || definition.Arrays["targetTags"].IsEmpty ||
                definition.Arrays["effectIds"].IsEmpty)
            {
                Invalid("/activeKind");
            }
        }

        return;

        bool HasStrings(params string[] fields) => fields.All(definition.Strings.ContainsKey);
        bool HasIntegers(params string[] fields) => fields.All(definition.Integers.ContainsKey);
        void Invalid(string property) => diagnostics.Add(
            ContentDiagnosticCodes.SemanticInvalid,
            definition.PackId,
            definition.RelativePath,
            definition.Id.ToString(),
            property);
    }

    private static void ValidateRace(SourceDefinition race, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        foreach (string featId in race.Arrays["grantedFeatIds"])
        {
            SourceDefinition feat = byId[featId];
            if (!feat.Arrays["compatibleRaceIds"].Contains(race.Id.ToString(), StringComparer.Ordinal))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, race.PackId, race.RelativePath, race.Id.ToString(), "/grantedFeatIds");
            }
        }
    }

    private static void ValidateHeritage(SourceDefinition heritage, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        string raceId = heritage.Strings["raceId"];
        foreach (string featId in heritage.Arrays["grantedFeatIds"])
        {
            SourceDefinition feat = byId[featId];
            if (!feat.Arrays["compatibleRaceIds"].Contains(raceId, StringComparer.Ordinal))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, heritage.PackId, heritage.RelativePath,
                    heritage.Id.ToString(), "/grantedFeatIds");
            }
        }
    }

    private static void ValidateCharacter(SourceDefinition character, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        string raceId = character.Strings["raceId"];
        SourceDefinition race = byId[raceId];
        SourceDefinition heritage = byId[character.Strings["heritageId"]];
        SourceDefinition background = byId[character.Strings["backgroundId"]];
        if (heritage.Strings["raceId"] != raceId)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/heritageId");
        }

        if (!background.Arrays["compatibleRaceIds"].Contains(raceId, StringComparer.Ordinal))
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/backgroundId");
        }

        if (race.Arrays["requiredSupportIds"].IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/raceId");
        }
    }

    private void ValidateBoard(
        SourceDefinition board,
        IReadOnlyDictionary<string, SourceDefinition> byId,
        DiagnosticSink diagnostics)
    {
        HashSet<string> cells = board.Arrays["cellIds"].ToHashSet(StringComparer.Ordinal);
        if (board.Arrays["retreatCellIds"].Any(value => !cells.Contains(value)))
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                board.Id.ToString(), "/retreatCellIds");
            return;
        }

        Dictionary<string, List<string>> adjacent = cells.ToDictionary(value => value, _ => new List<string>(), StringComparer.Ordinal);
        foreach (string linkId in board.Arrays["linkIds"])
        {
            if (!byId.TryGetValue(linkId, out SourceDefinition? link))
            {
                continue;
            }

            string from = link.Strings["fromCellId"];
            string to = link.Strings["toCellId"];
            if (!cells.Contains(from) || !cells.Contains(to))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                    board.Id.ToString(), "/linkIds");
                return;
            }

            adjacent[from].Add(to);
            if (link.Integers["oneWay"] == 0)
            {
                adjacent[to].Add(from);
            }
        }

        foreach (string retreatCell in board.Arrays["retreatCellIds"])
        {
            bool hasRetreatLink = board.Arrays["linkIds"].Any(linkId =>
                byId.TryGetValue(linkId, out SourceDefinition? link) &&
                link.Integers["allowsRetreat"] == 1 &&
                (link.Strings["fromCellId"] == retreatCell || link.Strings["toCellId"] == retreatCell));
            if (!hasRetreatLink)
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                    board.Id.ToString(), "/retreatCellIds");
                return;
            }
        }

        if (cells.Count == 0)
        {
            return;
        }

        HashSet<string> reached = [];
        Queue<string> pending = new();
        pending.Enqueue(cells.Order(StringComparer.Ordinal).First());
        while (pending.Count > 0 && reached.Count <= limits.GraphNodes)
        {
            string current = pending.Dequeue();
            if (!reached.Add(current))
            {
                continue;
            }

            foreach (string next in adjacent[current].Order(StringComparer.Ordinal))
            {
                pending.Enqueue(next);
            }
        }

        if (reached.Count != cells.Count)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                board.Id.ToString(), "/linkIds");
        }
    }

    private void ValidateGrantCycles(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        Dictionary<string, SourceDefinition> grantNodes = definitions
            .Where(value => value.Kind == DefinitionKind.Feat)
            .ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        Dictionary<string, byte> marks = new(StringComparer.Ordinal);
        foreach (SourceDefinition node in grantNodes.Values.OrderBy(value => value.Id))
        {
            Visit(node, 0);
        }

        void Visit(SourceDefinition node, int depth)
        {
            if (depth >= limits.ValidationTraversalDepth)
            {
                diagnostics.Limit("grant-depth", node.PackId, node.RelativePath);
                return;
            }

            string id = node.Id.ToString();
            if (marks.TryGetValue(id, out byte mark))
            {
                if (mark == 1)
                {
                    diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, node.PackId, node.RelativePath, id, "/grants");
                }

                return;
            }

            marks[id] = 1;
            IEnumerable<string> successors = node.Arrays["grantedFeatIds"];
            foreach (string successor in successors.Order(StringComparer.Ordinal))
            {
                if (grantNodes.TryGetValue(successor, out SourceDefinition? target))
                {
                    Visit(target, depth + 1);
                }
            }

            marks[id] = 2;
        }
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
