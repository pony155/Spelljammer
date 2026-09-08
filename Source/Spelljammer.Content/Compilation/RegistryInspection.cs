using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

/// <summary>
/// Defines deterministic diagnostic views of a compiled content registry.
/// </summary>
/// <remarks>
/// Code flow: Typed registries are enumerated in canonical kind and ID order, entries are projected with pack and index metadata, and callers receive an immutable inspection snapshot.
/// </remarks>
public sealed record RegistryInspectionEntry(
    string Kind,
    string Id,
    string PackId,
    int Revision,
    int Index);

public sealed record RegistryInspectionSnapshot(
    ContentFingerprint Fingerprint,
    int PackCount,
    int DefinitionCount,
    int AbilityCount,
    int SkillCount,
    int LevelProgressionTableCount,
    ImmutableArray<RegistryInspectionEntry> Entries);
