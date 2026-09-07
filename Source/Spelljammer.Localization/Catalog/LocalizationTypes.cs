namespace Spelljammer.Localization;

public enum LocalizationValueKind : byte
{
    Integer,
    Unsigned,
    Fixed,
    Percent,
    Text,
    Select,
    Boolean,
    Localizable
}

public sealed record LocalizableValue(
    LocalizationKey Key,
    IReadOnlyList<LocalizationArgument> Arguments);

public readonly struct LocalizationArgument
{
    private LocalizationArgument(
        string name,
        LocalizationValueKind kind,
        long signedValue,
        ulong unsignedValue,
        int decimalScale,
        string? textValue,
        LocalizableValue? localizableValue)
    {
        if (!LocalizationArgumentName.IsCanonical(name))
        {
            throw new ArgumentException("Argument name must be lowercase ASCII and begin with a letter.", nameof(name));
        }

        Name = name;
        NameId = LocalizationArgumentName.ComputeId(name);
        Kind = kind;
        SignedValue = signedValue;
        UnsignedValue = unsignedValue;
        DecimalScale = decimalScale;
        TextValue = textValue;
        LocalizableValue = localizableValue;
    }

    public string Name { get; }

    public uint NameId { get; }

    public LocalizationValueKind Kind { get; }

    public long SignedValue { get; }

    public ulong UnsignedValue { get; }

    public int DecimalScale { get; }

    public string? TextValue { get; }

    public LocalizableValue? LocalizableValue { get; }

    public static LocalizationArgument Integer(string name, long value) =>
        new(name, LocalizationValueKind.Integer, value, 0, 0, null, null);

    public static LocalizationArgument Unsigned(string name, ulong value) =>
        new(name, LocalizationValueKind.Unsigned, 0, value, 0, null, null);

    public static LocalizationArgument Fixed(string name, long scaledValue, int decimalScale)
    {
        ValidateScale(decimalScale);
        return new(name, LocalizationValueKind.Fixed, scaledValue, 0, decimalScale, null, null);
    }

    public static LocalizationArgument Percent(string name, long scaledRatio, int decimalScale)
    {
        ValidateScale(decimalScale);
        return new(name, LocalizationValueKind.Percent, scaledRatio, 0, decimalScale, null, null);
    }

    public static LocalizationArgument Text(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(name, LocalizationValueKind.Text, 0, 0, 0, value, null);
    }

    public static LocalizationArgument Select(string name, string value)
    {
        if (!LocalizationArgumentName.IsCanonicalToken(value))
        {
            throw new ArgumentException("Select value must be a canonical lowercase ASCII token.", nameof(value));
        }

        return new(name, LocalizationValueKind.Select, 0, 0, 0, value, null);
    }

    public static LocalizationArgument Boolean(string name, bool value) =>
        new(name, LocalizationValueKind.Boolean, value ? 1 : 0, 0, 0, null, null);

    public static LocalizationArgument Localizable(
        string name,
        LocalizationKey key,
        IReadOnlyList<LocalizationArgument>? arguments = null) =>
        new(name, LocalizationValueKind.Localizable, 0, 0, 0, null,
            new LocalizableValue(key, arguments ?? Array.Empty<LocalizationArgument>()));

    private static void ValidateScale(int decimalScale)
    {
        if (decimalScale is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalScale), "Decimal scale must be from 0 through 9.");
        }
    }
}

public sealed record LocalizationArgumentSchema(
    string Name,
    uint NameId,
    LocalizationValueKind Kind,
    int DecimalScale,
    bool Sensitive,
    IReadOnlyList<string> SelectValues);

public sealed record LocalizationLanguageProfile(
    string LanguageTag,
    TextDirection Direction,
    string DecimalSeparator,
    string GroupSeparator,
    string PercentSign,
    string LocaleDataVersion,
    string LocaleDataHash);

public static class LocalizationArgumentName
{
    private const uint OffsetBasis = 2166136261U;
    private const uint Prime = 16777619U;

    public static uint ComputeId(string name)
    {
        if (!IsCanonical(name))
        {
            throw new ArgumentException("Argument name is not canonical.", nameof(name));
        }

        uint hash = OffsetBasis;
        foreach (char character in name)
        {
            hash ^= (byte)character;
            hash *= Prime;
        }

        return hash;
    }

    public static bool IsCanonical(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 63 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' || character == '-');
    }

    public static bool IsCanonicalToken(string? value) => IsCanonical(value);
}
