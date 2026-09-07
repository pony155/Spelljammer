using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

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
    int AttributeCount,
    int SkillCount,
    ImmutableArray<RegistryInspectionEntry> Entries);
