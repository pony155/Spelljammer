namespace Spelljammer.Simulation.Content;

/// <summary>Common identity, versioning, and localization metadata for authored game content.</summary>
/// <remarks>
/// Code flow: Parsers and compilers create derived immutable definitions with stable IDs, catalogs publish them, and simulation state retains only those identities and authoritative numeric data.
/// </remarks>
public abstract record ContentDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey);
