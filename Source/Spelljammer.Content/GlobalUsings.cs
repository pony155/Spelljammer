/// <summary>
/// Declares simulation namespaces imported throughout the content compiler project.
/// </summary>
/// <remarks>
/// Code flow: The C# compiler applies these global imports to every content source file before normal type binding, keeping compiler stages focused on content-specific dependencies.
/// </remarks>
global using Spelljammer.Simulation.Characters;
global using Spelljammer.Simulation.Combat;
global using Spelljammer.Simulation.Encounters;
global using Spelljammer.Simulation.Ships;
global using Spelljammer.Simulation.World;
