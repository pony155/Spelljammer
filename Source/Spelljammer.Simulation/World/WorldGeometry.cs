namespace Spelljammer.Simulation.World;

/// <summary>A deterministic fixed-point scalar used by authoritative world geometry.</summary>
public readonly record struct FixedScalar : IComparable<FixedScalar>
{
    public const long Scale = 1_000;
    public const long MaximumMagnitude = 1_000_000_000 * Scale;

    public FixedScalar(long raw)
    {
        if (raw is < -MaximumMagnitude or > MaximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(raw));
        }

        Raw = raw;
    }

    public long Raw { get; }
    public int CompareTo(FixedScalar other) => Raw.CompareTo(other.Raw);
    public static FixedScalar FromInt(int value) => new(checked(value * Scale));
    public static FixedScalar operator +(FixedScalar left, FixedScalar right) => new(checked(left.Raw + right.Raw));
    public static FixedScalar operator -(FixedScalar left, FixedScalar right) => new(checked(left.Raw - right.Raw));
    public static FixedScalar operator *(FixedScalar value, int multiplier) => new(checked(value.Raw * multiplier));
}

/// <summary>A deterministic two-dimensional vector used by authoritative world geometry.</summary>
public readonly record struct FixedVector2(FixedScalar X, FixedScalar Y)
{
    public static FixedVector2 Zero => new(new FixedScalar(0), new FixedScalar(0));
    public static FixedVector2 operator +(FixedVector2 left, FixedVector2 right) => new(left.X + right.X, left.Y + right.Y);
    public static FixedVector2 operator -(FixedVector2 left, FixedVector2 right) => new(left.X - right.X, left.Y - right.Y);
}
