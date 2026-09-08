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
/// Loads pack files and parses definition sources for the content compiler.
/// </summary>
/// <remarks>
/// Code flow: Ordered pack roots enumerate normalized files, bounded reads feed strict parsers, source definitions accumulate in deterministic order, and I/O failures stop processing explicitly.
/// </remarks>
public sealed partial class GameContentCompiler
{
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
}
