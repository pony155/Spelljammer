using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Ships;

/// <summary>
/// Provides deterministic range bands and firing geometry for ship combat.
/// </summary>
/// <remarks>
/// Code flow: Fixed-point positions and headings are compared, distance and firing arcs are classified, and command resolution consumes the resulting range and solution flags.
/// </remarks>
public enum ShipRange : byte
{
    Contact,
    Near,
    Far,
    Beyond,
}

public static class ShipGeometry
{
    public static ShipRange Range(FixedVector2 left, FixedVector2 right)
    {
        long x = Math.Abs(left.X.Raw - right.X.Raw);
        long y = Math.Abs(left.Y.Raw - right.Y.Raw);
        long distance = Math.Max(x, y);
        return distance switch
        {
            <= 2_000 => ShipRange.Contact,
            <= 10_000 => ShipRange.Near,
            <= 30_000 => ShipRange.Far,
            _ => ShipRange.Beyond,
        };
    }
}
