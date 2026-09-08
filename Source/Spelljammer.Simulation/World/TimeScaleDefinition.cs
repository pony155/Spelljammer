using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>Rational mapping from simulation ticks to elapsed world seconds.</summary>
public sealed record TimeScaleDefinition(
    TimeScaleId TimeScaleId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WorldSecondsNumerator,
    int SimulationTicksDenominator)
    : ContentDefinition(TimeScaleId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
