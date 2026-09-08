using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

public sealed partial class ContentContracts
{
    private static void ValidFixtureIsCanonicalAndDeterministic()
    {
        string baseRoot = Path.Combine(FixtureRoot, "valid", "base");
        GameContentCompiler compiler = new();
        ContentCompilationResult first = compiler.Compile([new DirectoryContentPackSource(baseRoot)], GameVersion);
        ContentCompilationResult second = compiler.Compile([new DirectoryContentPackSource(baseRoot)], GameVersion);
        True(first.Succeeded, Primary(first));
        True(second.Succeeded, Primary(second));

        using JsonDocument expectedFingerprint = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot, "expected", "fingerprints.json")));
        string expectedHash = expectedFingerprint.RootElement.GetProperty("sha256").GetString()!;
        Equal(expectedHash, first.Snapshot!.Fingerprint.ToString(), "Canonical fingerprint changed.");
        Equal(first.Snapshot.Fingerprint, second.Snapshot!.Fingerprint, "Repeated compilation was not deterministic.");
        byte[] expectedCanonical = File.ReadAllBytes(Path.Combine(FixtureRoot, "expected", "canonical-semantic.json"));
        True(expectedCanonical.AsSpan().SequenceEqual(first.Snapshot.CanonicalSemanticContent.AsSpan()), "Canonical semantic bytes changed.");
    }

    private static void EveryFrozenDiagnosticCaseIsRecognized()
    {
        using JsonDocument casesDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot, "invalid", "cases.json")));
        Dictionary<string, byte[]> baseFiles = ReadFiles(Path.Combine(FixtureRoot, "valid", "base"));
        foreach (JsonElement testCase in casesDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            Dictionary<string, byte[]> files = baseFiles.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            List<string> duplicateEntries = [];
            ApplyMutations(testCase, files, duplicateEntries);
            List<IContentPackSource> sources = [new MemoryPackSource(files, duplicateEntries)];
            if (testCase.TryGetProperty("additionalPacks", out JsonElement additionalPacks))
            {
                foreach (JsonElement pack in additionalPacks.EnumerateArray())
                {
                    Dictionary<string, byte[]> packFiles = [];
                    foreach (JsonElement file in pack.GetProperty("files").EnumerateArray())
                    {
                        packFiles.Add(file.GetProperty("path").GetString()!, Encoding.UTF8.GetBytes(file.GetProperty("text").GetString()!));
                    }

                    sources.Add(new MemoryPackSource(packFiles, []));
                }
            }

            SemanticVersion gameVersion = testCase.TryGetProperty("gameVersion", out JsonElement versionElement)
                ? ParseVersion(versionElement.GetString()!)
                : GameVersion;
            ContentLimits limits = testCase.TryGetProperty("limitOverrides", out JsonElement overrides)
                ? ContentLimits.Version1 with { ManifestBytes = overrides.GetProperty("manifestBytes").GetInt32() }
                : ContentLimits.Version1;
            ContentCompilationResult result = new GameContentCompiler(limits).Compile(sources, gameVersion);
            string expected = testCase.GetProperty("expectedPrimaryDiagnostic").GetString()!;
            Equal(expected, result.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
                $"Wrong primary diagnostic for '{testCase.GetProperty("id").GetString()}'.");
        }
    }

    private static void FailedReplacementPreservesPublishedSnapshot()
    {
        GameContentRegistry registry = new();
        string baseRoot = Path.Combine(FixtureRoot, "valid", "base");
        ContentCompilationResult valid = registry.CompileAndPublish([new DirectoryContentPackSource(baseRoot)], GameVersion);
        True(valid.Succeeded, Primary(valid));
        GameContentSnapshot published = registry.Current!;
        ContentCompilationResult invalid = registry.CompileAndPublish([new MemoryPackSource([], [])], GameVersion);
        False(invalid.Succeeded, "An invalid replacement was published.");
        True(ReferenceEquals(published, registry.Current), "Failed replacement changed the registry owner.");
    }

    private static void BaseAbilitysAndSkillsAreTypedAndIndexed()
    {
        ContentCompilationResult result = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(result.Succeeded, Primary(result));
        GameContentSnapshot snapshot = result.Snapshot!;
        using JsonDocument expectedFingerprints = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(Milestone2Root, "expected", "fingerprints.json")));
        Equal(
            expectedFingerprints.RootElement.GetProperty("baseSha256").GetString()!,
            snapshot.Fingerprint.ToString(),
            "The base Ability and Skill fingerprint changed.");
        Equal(6, snapshot.AbilityRegistry.Count, "The base Ability roster is incomplete.");
        Equal(30, snapshot.SkillRegistry.Count, "The base Skill roster is incomplete.");
        Equal(5, snapshot.StatusRegistry.Count, "The base Status roster is incomplete.");
        Equal(11, snapshot.EffectRegistry.Count, "The base Effect roster is incomplete.");
        True(snapshot.TryGetStatus(new StatusId("status.charmed"), out StatusDefinition? charmed),
            "Charmed was not published through the Status registry.");
        Equal(StatusStackPolicy.Refresh, charmed!.StackPolicy, "Charmed stack policy changed.");
        True(snapshot.TryGetEffect(new EffectId("effect.status.apply-burning"), out EffectDefinition? ignite) &&
            ignite!.Payload is ApplyStatusEffectPayload { StatusId: var statusId } &&
            statusId == new StatusId("status.burning"),
            "Apply Burning did not resolve its Status reference.");
        True(snapshot.TryGetEffect(CombatEffectIds.PhysicalDamage, out EffectDefinition? physicalDamage) &&
            physicalDamage!.Payload is DamageEffectPayload { Type: EffectType.PhysicalDamage },
            "Combat physical damage was not compiled as a typed damage payload.");
        True(snapshot.TryGetEffect(new EffectId("effect.equipment.focus"), out EffectDefinition? focus) &&
            focus!.Payload is EmitEventEffectPayload { EventId: var eventId } &&
            eventId == new ContentId("event.equipment.focus"),
            "Equipment focus was not compiled as a typed event payload.");
        True(snapshot.TryGetStatus(new StatusId("status.mindlinked"), out StatusDefinition? mindlinked) &&
            mindlinked!.DurationType == StatusDurationType.UntilRemoved,
            "Mindlinked was not published as an until-removed Status.");
        string[] expectedAbilitys =
        [
            "ability.agility", "ability.intelligence", "ability.perception",
            "ability.strength", "ability.toughness", "ability.willpower",
        ];
        string[] expectedSkills =
        [
            "skill.acrobatics", "skill.alchemy", "skill.ancient-lore", "skill.archery", "skill.astrogation",
            "skill.athletics", "skill.command", "skill.cooking", "skill.crafting", "skill.deception",
            "skill.defense", "skill.enchantment", "skill.engineering", "skill.eva", "skill.firearms", "skill.gunnery",
            "skill.insight", "skill.language-literacy", "skill.magic", "skill.medicine", "skill.melee",
            "skill.merchant", "skill.negotiation", "skill.piloting", "skill.psionics", "skill.rigging",
            "skill.salvage", "skill.sensors", "skill.stealth", "skill.xenology",
        ];
        Equal(string.Join('|', expectedAbilitys), string.Join('|', snapshot.Abilities.Select(value => value.Id)),
            "Ability iteration is incomplete or nondeterministic.");
        Equal(string.Join('|', expectedSkills), string.Join('|', snapshot.Skills.Select(value => value.Id)),
            "Skill iteration is incomplete or nondeterministic.");

        SkillId engineeringId = new("skill.engineering");
        True(snapshot.SkillRegistry.TryGet(engineeringId, out var engineering), "Typed Skill lookup failed.");
        Equal(engineeringId, engineering!.SkillId, "Typed Skill lookup returned the wrong definition.");
        True(snapshot.SkillRegistry.TryGetIndex(engineeringId, out ScopedContentIndex<SkillId> index),
            "Dense Skill index lookup failed.");
        Equal(engineeringId, snapshot.SkillRegistry.Resolve(index).SkillId, "Dense Skill index resolved incorrectly.");
    }

    private static void Milestone2InvalidCasesAreRecognized()
    {
        using JsonDocument casesDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Milestone2Root, "invalid", "cases.json")));
        Dictionary<string, byte[]> baseFiles = ReadFiles(Path.Combine(Milestone2Root, "base"));
        foreach (JsonElement testCase in casesDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            Dictionary<string, byte[]> files = baseFiles.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            ApplyMutations(testCase, files, []);
            ContentLimits limits = ApplyLimitOverrides(testCase, ContentLimits.Version1);
            ContentCompilationResult result = new GameContentCompiler(limits).Compile([new MemoryPackSource(files, [])], GameVersion);
            string expected = testCase.GetProperty("expectedPrimaryDiagnostic").GetString()!;
            Equal(expected, result.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
                $"Wrong M2 diagnostic for '{testCase.GetProperty("id").GetString()}'.");
        }
    }
}
