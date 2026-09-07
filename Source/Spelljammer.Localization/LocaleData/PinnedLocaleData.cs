using System.Security.Cryptography;
using System.Text;

namespace Spelljammer.Localization;

internal enum PluralCategory : byte
{
    Zero,
    One,
    Two,
    Few,
    Many,
    Other
}

internal sealed record NumberProfile(
    string Id,
    string Digits,
    string DecimalSeparator,
    string GroupSeparator,
    string PercentSign,
    string PercentPrefix,
    string PercentSuffix);

internal static class PinnedLocaleData
{
    public const string Version = "CLDR-48.2.0";

    private const string CanonicalTable =
        "CLDR-48.2.0|en:0123456789,.;en-cardinal,ordinal|" +
        "fr:0123456789,\\u202f;fr-cardinal,other-ordinal|" +
        "ru:0123456789,\\u00a0;ru-cardinal,other-ordinal|" +
        "ar:٠١٢٣٤٥٦٧٨٩,٫٬٪;ar-cardinal,other-ordinal|" +
        "zh:0123456789,.;other-cardinal,other-ordinal";

    private static readonly NumberProfile English = new(
        "en", "0123456789", ".", ",", "%", string.Empty, "%");

    private static readonly NumberProfile French = new(
        "fr", "0123456789", ",", "\u202f", "%", string.Empty, "\u00a0%");

    private static readonly NumberProfile Russian = new(
        "ru", "0123456789", ",", "\u00a0", "%", string.Empty, "\u00a0%");

    private static readonly NumberProfile Arabic = new(
        "ar", "٠١٢٣٤٥٦٧٨٩", "٫", "٬", "٪", string.Empty, "٪");

    private static readonly NumberProfile Chinese = new(
        "zh", "0123456789", ".", ",", "%", string.Empty, "%");

    public static string TableHash { get; } =
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalTable)));

    public static bool TryGetNumberProfile(LocaleId locale, out NumberProfile? profile)
    {
        string language = locale.Tag switch
        {
            "qps-ploc" or "qps-keyecho" => "en",
            _ => locale.Tag.Split('-')[0]
        };
        profile = language switch
        {
            "en" => English,
            "fr" => French,
            "ru" => Russian,
            "ar" => Arabic,
            "zh" => Chinese,
            _ => null
        };
        return profile is not null;
    }

    public static PluralCategory SelectCardinal(NumberProfile profile, in DecimalOperand operand)
    {
        ulong integer = operand.AbsoluteInteger;
        ulong mod10 = integer % 10;
        ulong mod100 = integer % 100;
        return profile.Id switch
        {
            "en" => operand.VisibleFractionDigits == 0 && integer == 1
                ? PluralCategory.One
                : PluralCategory.Other,
            "fr" => integer is 0 or 1
                ? PluralCategory.One
                : PluralCategory.Other,
            "ru" when operand.VisibleFractionDigits != 0 => PluralCategory.Other,
            "ru" when mod10 == 1 && mod100 != 11 => PluralCategory.One,
            "ru" when mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14) => PluralCategory.Few,
            "ru" when mod10 == 0 || mod10 is >= 5 and <= 9 || mod100 is >= 11 and <= 14 => PluralCategory.Many,
            "ar" when operand.IsExactInteger(0) => PluralCategory.Zero,
            "ar" when operand.IsExactInteger(1) => PluralCategory.One,
            "ar" when operand.IsExactInteger(2) => PluralCategory.Two,
            "ar" when operand.IsInteger && mod100 is >= 3 and <= 10 => PluralCategory.Few,
            "ar" when operand.IsInteger && mod100 is >= 11 and <= 99 => PluralCategory.Many,
            _ => PluralCategory.Other
        };
    }

    public static PluralCategory SelectOrdinal(NumberProfile profile, in DecimalOperand operand)
    {
        if (profile.Id != "en" || !operand.IsInteger)
        {
            return PluralCategory.Other;
        }

        ulong integer = operand.AbsoluteInteger;
        ulong mod10 = integer % 10;
        ulong mod100 = integer % 100;
        if (mod10 == 1 && mod100 != 11)
        {
            return PluralCategory.One;
        }

        if (mod10 == 2 && mod100 != 12)
        {
            return PluralCategory.Two;
        }

        return mod10 == 3 && mod100 != 13 ? PluralCategory.Few : PluralCategory.Other;
    }

    public static IReadOnlyList<string> GetRequiredCategories(LocaleId locale, MessageSelectionKind kind)
    {
        _ = TryGetNumberProfile(locale, out NumberProfile? profile);
        if (kind == MessageSelectionKind.Cardinal)
        {
            return profile!.Id switch
            {
                "en" or "fr" => ["one"],
                "ru" => ["one", "few", "many"],
                "ar" => ["zero", "one", "two", "few", "many"],
                _ => []
            };
        }

        return kind == MessageSelectionKind.Ordinal && profile!.Id == "en"
            ? ["one", "two", "few"]
            : [];
    }
}

internal readonly struct DecimalOperand
{
    public DecimalOperand(bool negative, ulong magnitude, int scale)
    {
        Negative = negative;
        Magnitude = magnitude;
        Scale = scale;
    }

    public bool Negative { get; }

    public ulong Magnitude { get; }

    public int Scale { get; }

    public int VisibleFractionDigits => Scale;

    public bool IsInteger => Scale == 0 || Magnitude % PowerOfTen(Scale) == 0;

    public ulong AbsoluteInteger => Scale == 0 ? Magnitude : Magnitude / PowerOfTen(Scale);

    public bool IsExactInteger(long value)
    {
        bool expectedNegative = value < 0;
        if (expectedNegative != Negative && (value != 0 || Negative))
        {
            return false;
        }

        ulong expectedMagnitude = expectedNegative
            ? (ulong)(-(value + 1)) + 1
            : (ulong)value;
        ulong divisor = PowerOfTen(Scale);
        return Magnitude % divisor == 0 && Magnitude / divisor == expectedMagnitude;
    }

    public static ulong PowerOfTen(int scale)
    {
        ReadOnlySpan<ulong> powers =
        [
            1UL,
            10UL,
            100UL,
            1_000UL,
            10_000UL,
            100_000UL,
            1_000_000UL,
            10_000_000UL,
            100_000_000UL,
            1_000_000_000UL
        ];
        return powers[scale];
    }
}
