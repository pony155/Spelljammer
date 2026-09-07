using System.Globalization;

namespace Spelljammer.Content.Manifests;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public int CompareTo(SemanticVersion other)
    {
        int major = Major.CompareTo(other.Major);
        int minor = Minor.CompareTo(other.Minor);
        return major != 0 ? major : minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (value is null)
        {
            return false;
        }

        string[] components = value.Split('.');
        if (components.Length != 3 || components.Any(component =>
                component.Length == 0 ||
                component.Length > 1 && component[0] == '0' ||
                component.Any(character => character is < '0' or > '9')))
        {
            return false;
        }

        if (!int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor) ||
            !int.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch);
        return true;
    }
}

public readonly record struct VersionRange(SemanticVersion Minimum, SemanticVersion Maximum)
{
    public bool Contains(SemanticVersion version) => version.CompareTo(Minimum) >= 0 && version.CompareTo(Maximum) < 0;

    public override string ToString() => $">={Minimum} <{Maximum}";

    public static bool TryParse(string? value, out VersionRange range)
    {
        range = default;
        if (value is null || !value.StartsWith(">=", StringComparison.Ordinal))
        {
            return false;
        }

        int separator = value.IndexOf(" <", StringComparison.Ordinal);
        if (separator < 3 || value.IndexOf(' ', separator + 1) >= 0 ||
            !SemanticVersion.TryParse(value[2..separator], out SemanticVersion minimum) ||
            !SemanticVersion.TryParse(value[(separator + 2)..], out SemanticVersion maximum) ||
            minimum.CompareTo(maximum) >= 0)
        {
            return false;
        }

        range = new VersionRange(minimum, maximum);
        return true;
    }
}
