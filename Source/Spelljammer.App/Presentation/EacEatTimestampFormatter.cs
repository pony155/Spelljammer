using System.Globalization;
using Spelljammer.Localization;
using Spelljammer.Simulation.World;

namespace Spelljammer.Presentation;

/// <summary>Formats an authoritative world date as localized EAC and EAT presentation text.</summary>
internal sealed class EacEatTimestampFormatter
{
    private static readonly LocalizationKey EraLabelKey = LocalizationKey.Create("calendar.era.eac.label");
    private static readonly LocalizationKey TimeStandardLabelKey =
        LocalizationKey.Create("calendar.time-standard.eat.label");

    private readonly GameText strings;

    internal EacEatTimestampFormatter(GameText strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        this.strings = strings;
    }

    internal string FormatDate(WorldDateTime value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return strings.Format("calendar.date.full", DateArguments(value));
    }

    internal string FormatTime(WorldDateTime value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return strings.Format("calendar.time.full", TimeArguments(value));
    }

    internal string FormatTimestamp(WorldDateTime value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return strings.Format(
            "calendar.timestamp.full",
            LocalizationArgument.Localizable(
                "date",
                LocalizationKey.Create("calendar.date.full"),
                DateArguments(value)),
            LocalizationArgument.Localizable(
                "time",
                LocalizationKey.Create("calendar.time.full"),
                TimeArguments(value)));
    }

    private static LocalizationArgument[] DateArguments(WorldDateTime value) =>
    [
        LocalizationArgument.Text("year", value.Year.ToString(CultureInfo.InvariantCulture)),
        LocalizationArgument.Localizable("era", EraLabelKey),
        LocalizationArgument.Localizable("month", LocalizationKey.Create(value.MonthNameKey)),
        LocalizationArgument.Text("day", TwoDigits(value.Day)),
    ];

    private static LocalizationArgument[] TimeArguments(WorldDateTime value) =>
    [
        LocalizationArgument.Text("hour", TwoDigits(value.Hour)),
        LocalizationArgument.Text("minute", TwoDigits(value.Minute)),
        LocalizationArgument.Text("second", TwoDigits(value.Second)),
        LocalizationArgument.Localizable("standard", TimeStandardLabelKey),
    ];

    private static string TwoDigits(int value) => value.ToString("D2", CultureInfo.InvariantCulture);
}
