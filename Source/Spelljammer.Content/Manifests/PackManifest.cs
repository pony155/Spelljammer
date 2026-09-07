using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Manifests;

public sealed record PackDependency(ContentId Id, VersionRange VersionRange);

public sealed record PackManifest(
    int SchemaVersion,
    ContentId Id,
    SemanticVersion Version,
    string DisplayNameKey,
    VersionRange GameVersionRange,
    ImmutableArray<PackDependency> Dependencies,
    ImmutableArray<ContentId> LoadAfter,
    ImmutableArray<string> DefinitionRoots,
    ImmutableArray<string> LocalizationRoots,
    int ContentRevision);
