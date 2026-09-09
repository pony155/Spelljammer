using Spelljammer.Simulation.Galaxy;

namespace Spelljammer.Presentation;

/// <summary>
/// Carries the validated, explicit inputs selected for a new campaign galaxy.
/// </summary>
internal sealed record GalaxyMapSelection(ulong Seed, GalaxyGenerationSettings Settings);
