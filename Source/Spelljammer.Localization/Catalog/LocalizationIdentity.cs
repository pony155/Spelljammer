using System.Text;

namespace Spelljammer.Localization;

public readonly struct LocalizationKey : IEquatable<LocalizationKey>
{
    private LocalizationKey(ulong value, string name)
    {
        Value = value;
        Name = name;
    }

    public ulong Value { get; }

    public string Name { get; }

    public static bool TryCreate(string name, out LocalizationKey key, out string error)
    {
        if (!LocalizationNames.IsCanonicalKey(name, out error))
        {
            key = default;
            return false;
        }

        key = new LocalizationKey(StableNameHash.Compute(name), name);
        return true;
    }

    public static LocalizationKey Create(string name)
    {
        if (!TryCreate(name, out LocalizationKey key, out string error))
        {
            throw new ArgumentException(error, nameof(name));
        }

        return key;
    }

    public bool Equals(LocalizationKey other) => Value == other.Value && Name == other.Name;

    public override bool Equals(object? obj) => obj is LocalizationKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Name);

    public override string ToString() => Name ?? string.Empty;

    public static bool operator ==(LocalizationKey left, LocalizationKey right) => left.Equals(right);

    public static bool operator !=(LocalizationKey left, LocalizationKey right) => !left.Equals(right);
}

public readonly struct LocaleId : IEquatable<LocaleId>
{
    private LocaleId(ulong value, string tag)
    {
        Value = value;
        Tag = tag;
    }

    public ulong Value { get; }

    public string Tag { get; }

    public static bool TryCreate(string tag, out LocaleId locale, out string error)
    {
        if (!LocalizationNames.IsCanonicalLocaleTag(tag, out error))
        {
            locale = default;
            return false;
        }

        locale = new LocaleId(StableNameHash.Compute(tag), tag);
        return true;
    }

    public static LocaleId Create(string tag)
    {
        if (!TryCreate(tag, out LocaleId locale, out string error))
        {
            throw new ArgumentException(error, nameof(tag));
        }

        return locale;
    }

    public bool Equals(LocaleId other) => Value == other.Value && Tag == other.Tag;

    public override bool Equals(object? obj) => obj is LocaleId other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Tag);

    public override string ToString() => Tag ?? string.Empty;

    public static bool operator ==(LocaleId left, LocaleId right) => left.Equals(right);

    public static bool operator !=(LocaleId left, LocaleId right) => !left.Equals(right);
}

public static class StableNameHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(string canonicalAsciiName)
    {
        ArgumentNullException.ThrowIfNull(canonicalAsciiName);

        ulong hash = OffsetBasis;
        foreach (char character in canonicalAsciiName)
        {
            if (character > 0x7f)
            {
                throw new ArgumentException("Stable localization names must be ASCII.", nameof(canonicalAsciiName));
            }

            hash ^= (byte)character;
            hash *= Prime;
        }

        return hash;
    }
}

public sealed class StableNameRegistry
{
    private readonly Dictionary<ulong, string> names = [];

    public bool TryAdd(ulong value, string canonicalName, out string error)
    {
        ArgumentNullException.ThrowIfNull(canonicalName);

        if (names.TryGetValue(value, out string? existing))
        {
            if (existing == canonicalName)
            {
                error = $"Duplicate stable name '{canonicalName}'.";
            }
            else
            {
                error = $"Stable ID collision between '{existing}' and '{canonicalName}'.";
            }

            return false;
        }

        names.Add(value, canonicalName);
        error = string.Empty;
        return true;
    }
}

public static class LocalizationNames
{
    public static bool IsCanonicalKey(string? value, out string error)
    {
        if (string.IsNullOrEmpty(value))
        {
            error = "A localization key cannot be empty.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(value) > LocalizationLimits.MaximumCanonicalKeyBytes)
        {
            error = $"Localization key exceeds {LocalizationLimits.MaximumCanonicalKeyBytes} UTF-8 bytes.";
            return false;
        }

        string[] segments = value.Split('.');
        if (segments.Length < 2)
        {
            error = "A localization key must contain at least two dotted segments.";
            return false;
        }

        foreach (string segment in segments)
        {
            if (!IsLowerAsciiSegment(segment))
            {
                error = $"Localization key '{value}' is not canonical lowercase ASCII.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool IsCanonicalNamespace(string? value, out string error)
    {
        if (string.IsNullOrEmpty(value) || !IsLowerAsciiSegment(value))
        {
            error = "A namespace must be one lowercase ASCII key segment.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(value) > LocalizationLimits.MaximumNamespaceBytes)
        {
            error = $"Namespace exceeds {LocalizationLimits.MaximumNamespaceBytes} bytes.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsCanonicalLocaleTag(string? value, out string error)
    {
        if (string.IsNullOrEmpty(value) || value.Length > LocalizationLimits.MaximumLocaleTagBytes)
        {
            error = "Locale tag is empty or too long.";
            return false;
        }

        if (value is "qps-ploc" or "qps-keyecho")
        {
            error = string.Empty;
            return true;
        }

        string[] parts = value.Split('-');
        if (parts.Length == 0 || parts[0].Length is < 2 or > 8 || !IsLowerAsciiLetters(parts[0]))
        {
            error = $"Locale tag '{value}' has an invalid or non-canonical language subtag.";
            return false;
        }

        bool sawScript = false;
        bool sawRegion = false;
        for (int index = 1; index < parts.Length; ++index)
        {
            string part = parts[index];
            if (!sawScript && part.Length == 4 && IsTitleAsciiLetters(part))
            {
                sawScript = true;
                continue;
            }

            if (!sawRegion && ((part.Length == 2 && IsUpperAsciiLetters(part)) ||
                (part.Length == 3 && IsAsciiDigits(part))))
            {
                sawRegion = true;
                continue;
            }

            if ((part.Length is >= 5 and <= 8 ||
                (part.Length == 4 && part[0] is >= '0' and <= '9')) && IsLowerAsciiAlphaNumeric(part))
            {
                continue;
            }

            error = $"Locale tag '{value}' contains an unsupported or non-canonical subtag '{part}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsLowerAsciiSegment(string value)
    {
        if (value.Length == 0 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is not (>= 'a' and <= 'z') &&
                character is not (>= '0' and <= '9') && character != '-')
            {
                return false;
            }
        }

        return value[^1] != '-';
    }

    private static bool IsLowerAsciiLetters(string value) =>
        value.All(character => character is >= 'a' and <= 'z');

    private static bool IsUpperAsciiLetters(string value) =>
        value.All(character => character is >= 'A' and <= 'Z');

    private static bool IsTitleAsciiLetters(string value) =>
        value[0] is >= 'A' and <= 'Z' && value[1..].All(character => character is >= 'a' and <= 'z');

    private static bool IsAsciiDigits(string value) =>
        value.All(character => character is >= '0' and <= '9');

    private static bool IsLowerAsciiAlphaNumeric(string value) =>
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');
}
