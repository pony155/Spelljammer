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
/// Validates default-locale coverage for compiled gameplay definitions.
/// </summary>
/// <remarks>
/// Code flow: Ordered packs contribute bounded default-locale keys, duplicate and missing entries produce diagnostics, and every definition's name and description keys must resolve before publication.
/// </remarks>
public sealed partial class GameContentCompiler
{
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
                if (definition.Calendar is CalendarSourceDto calendar)
                {
                    for (int index = 0; index < calendar.Months.Length; index++)
                    {
                        CheckLocalizationKey(
                            definition,
                            calendar.Months[index].NameKey,
                            $"/months/{index}/nameKey",
                            keys,
                            diagnostics);
                    }
                }
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
}
