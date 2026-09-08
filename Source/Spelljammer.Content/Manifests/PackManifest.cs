using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Manifests;

/// <summary>
/// Defines immutable content-pack identity, compatibility, dependency, and source-root metadata.
/// </summary>
/// <remarks>
/// Code flow: Manifest parsing creates these records, dependency ordering consumes their edges and load hints, and the compiled snapshot retains ordered pack identities for saves and fingerprints.
/// </remarks>
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
