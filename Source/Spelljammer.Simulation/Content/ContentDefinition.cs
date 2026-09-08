namespace Spelljammer.Simulation.Content;

/// <summary>Common identity, versioning, and localization metadata for authored game content.</summary>
public abstract record ContentDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey);
