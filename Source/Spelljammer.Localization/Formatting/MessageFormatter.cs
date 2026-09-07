using System.Globalization;
using System.Text;

namespace Spelljammer.Localization;

internal static class MessageFormatter
{
    public static LocalizationStatus Format(
        ResolvedEntry resolvedEntry,
        IReadOnlyList<LocalizationArgument> arguments,
        Func<LocalizationKey, ResolvedEntry?> lookup,
        out string text,
        out string? pluralCategory,
        out string error)
    {
        text = string.Empty;
        pluralCategory = null;
        error = string.Empty;
        BoundedMessageWriter writer = new(LocalizationLimits.MaximumFormattedMessageBytes);
        HashSet<LocalizationKey> callStack = [resolvedEntry.CatalogEntry.Key];
        LocalizationStatus status = FormatEntry(
            resolvedEntry.CatalogEntry,
            arguments,
            resolvedEntry.NumberProfile,
            lookup,
            writer,
            callStack,
            0,
            ref pluralCategory,
            out error);
        if (status != LocalizationStatus.Success)
        {
            return status;
        }

        text = writer.ToString();
        return LocalizationStatus.Success;
    }

    private static LocalizationStatus FormatEntry(
        LocalizationCatalogEntry entry,
        IReadOnlyList<LocalizationArgument> arguments,
        NumberProfile profile,
        Func<LocalizationKey, ResolvedEntry?> lookup,
        BoundedMessageWriter writer,
        HashSet<LocalizationKey> callStack,
        int callDepth,
        ref string? pluralCategory,
        out string error)
    {
        if (callDepth > LocalizationLimits.MaximumMessageNesting)
        {
            error = "Nested localizable call depth exceeds its limit.";
            return LocalizationStatus.OutOfResource;
        }

        LocalizationStatus bindStatus = BindArguments(entry.Arguments, arguments,
            out LocalizationArgument[] bound, out error);
        if (bindStatus != LocalizationStatus.Success)
        {
            return bindStatus;
        }

        try
        {
            MessageProgramCodec.ProgramReader reader = new(entry.Program.Code.Span);
            LocalizationStatus status = ExecuteBlock(ref reader, entry.Arguments, bound, profile, lookup,
                writer, callStack, callDepth, null, ref pluralCategory, out error);
            if (status != LocalizationStatus.Success)
            {
                return status;
            }

            reader.RequireEnd();
            return LocalizationStatus.Success;
        }
        catch (MessageProgramCodec.ProgramValidationException exception)
        {
            error = exception.Message;
            return LocalizationStatus.DataCorrupt;
        }
    }

    private static LocalizationStatus ExecuteBlock(
        ref MessageProgramCodec.ProgramReader reader,
        IReadOnlyList<LocalizationArgumentSchema> schema,
        LocalizationArgument[] arguments,
        NumberProfile profile,
        Func<LocalizationKey, ResolvedEntry?> lookup,
        BoundedMessageWriter writer,
        HashSet<LocalizationKey> callStack,
        int callDepth,
        int? pluralArgument,
        ref string? pluralCategory,
        out string error)
    {
        while (true)
        {
            MessageOpcode opcode = (MessageOpcode)reader.ReadByte();
            switch (opcode)
            {
                case MessageOpcode.End:
                    error = string.Empty;
                    return LocalizationStatus.Success;
                case MessageOpcode.Literal:
                    if (!writer.TryAppend(reader.ReadString(LocalizationLimits.MaximumMessageBytes)))
                    {
                        error = "Formatted message exceeds its output byte limit.";
                        return LocalizationStatus.OutOfResource;
                    }

                    break;
                case MessageOpcode.Argument:
                    {
                        int index = reader.ReadByte();
                        MessageArgumentPresentation presentation = (MessageArgumentPresentation)reader.ReadByte();
                        LocalizationStatus status = AppendArgument(schema[index], arguments[index], presentation,
                            profile, lookup, writer, callStack, callDepth, ref pluralCategory, out error);
                        if (status != LocalizationStatus.Success)
                        {
                            return status;
                        }

                        break;
                    }
                case MessageOpcode.Pound:
                    {
                        int index = reader.ReadByte();
                        if (pluralArgument != index || !TryGetOperand(arguments[index], out DecimalOperand operand))
                        {
                            error = "Pound instruction has no valid plural operand.";
                            return LocalizationStatus.DataCorrupt;
                        }

                        if (!writer.TryAppend(FormatNumber(operand, profile, 0)))
                        {
                            error = "Formatted message exceeds its output byte limit.";
                            return LocalizationStatus.OutOfResource;
                        }

                        break;
                    }
                case MessageOpcode.Selection:
                    {
                        int index = reader.ReadByte();
                        MessageSelectionKind kind = (MessageSelectionKind)reader.ReadByte();
                        int branchCount = reader.ReadByte();
                        string selected = SelectBranch(arguments[index], profile, kind, out string? category);
                        pluralCategory = category ?? pluralCategory;
                        ReadOnlySpan<byte> exactBranch = default;
                        ReadOnlySpan<byte> selectedBranch = default;
                        ReadOnlySpan<byte> otherBranch = default;
                        for (int branchIndex = 0; branchIndex < branchCount; ++branchIndex)
                        {
                            string selector = reader.ReadString(63);
                            int branchLength = reader.ReadLength(LocalizationLimits.MaximumMessageBytes);
                            ReadOnlySpan<byte> branch = reader.ReadBytes(branchLength);
                            if (kind != MessageSelectionKind.Select && selector[0] == '=' &&
                                IsExactSelector(arguments[index], selector))
                            {
                                exactBranch = branch;
                            }
                            else if (selector == selected)
                            {
                                selectedBranch = branch;
                            }
                            else if (selector == "other")
                            {
                                otherBranch = branch;
                            }
                        }

                        ReadOnlySpan<byte> chosen = !exactBranch.IsEmpty
                            ? exactBranch
                            : !selectedBranch.IsEmpty ? selectedBranch : otherBranch;
                        if (chosen.IsEmpty)
                        {
                            error = "Validated selection has no applicable branch.";
                            return LocalizationStatus.DataCorrupt;
                        }

                        MessageProgramCodec.ProgramReader branchReader = new(chosen);
                        LocalizationStatus status = ExecuteBlock(ref branchReader, schema, arguments, profile,
                            lookup, writer, callStack, callDepth,
                            kind == MessageSelectionKind.Select ? pluralArgument : index,
                            ref pluralCategory, out error);
                        if (status != LocalizationStatus.Success)
                        {
                            return status;
                        }

                        branchReader.RequireEnd();
                        break;
                    }
                default:
                    error = "Validated message contains an unknown opcode.";
                    return LocalizationStatus.DataCorrupt;
            }
        }
    }

    private static LocalizationStatus AppendArgument(
        LocalizationArgumentSchema schema,
        LocalizationArgument argument,
        MessageArgumentPresentation presentation,
        NumberProfile profile,
        Func<LocalizationKey, ResolvedEntry?> lookup,
        BoundedMessageWriter writer,
        HashSet<LocalizationKey> callStack,
        int callDepth,
        ref string? pluralCategory,
        out string error)
    {
        string? value = null;
        if (presentation == MessageArgumentPresentation.Percent)
        {
            _ = TryGetOperand(argument, out DecimalOperand operand);
            value = profile.PercentPrefix + FormatNumber(operand, profile, 2) + profile.PercentSuffix;
        }
        else if (argument.Kind is LocalizationValueKind.Integer or LocalizationValueKind.Unsigned or LocalizationValueKind.Fixed)
        {
            _ = TryGetOperand(argument, out DecimalOperand operand);
            value = FormatNumber(operand, profile, 0);
        }
        else if (argument.Kind is LocalizationValueKind.Text or LocalizationValueKind.Select)
        {
            value = argument.TextValue;
        }
        else if (argument.Kind == LocalizationValueKind.Boolean)
        {
            value = argument.SignedValue == 0 ? "false" : "true";
        }
        else if (argument.Kind == LocalizationValueKind.Localizable)
        {
            LocalizableValue nested = argument.LocalizableValue!;
            ResolvedEntry? nestedEntry = lookup(nested.Key);
            if (nestedEntry is null)
            {
                error = $"Nested localizable key '{nested.Key.Name}' was not found.";
                return LocalizationStatus.ItemNotFound;
            }

            if (!callStack.Add(nested.Key))
            {
                error = $"Nested localizable cycle includes '{nested.Key.Name}'.";
                return LocalizationStatus.InvalidArgument;
            }

            LocalizationStatus nestedStatus = FormatEntry(nestedEntry.CatalogEntry, nested.Arguments,
                nestedEntry.NumberProfile, lookup,
                writer, callStack, callDepth + 1, ref pluralCategory, out error);
            callStack.Remove(nested.Key);
            return nestedStatus;
        }
        else if (argument.Kind == LocalizationValueKind.Percent)
        {
            _ = TryGetOperand(argument, out DecimalOperand operand);
            value = profile.PercentPrefix + FormatNumber(operand, profile, 2) + profile.PercentSuffix;
        }

        if (value is null || !writer.TryAppend(value))
        {
            error = "Formatted argument is invalid or exceeds the output byte limit.";
            return value is null ? LocalizationStatus.InvalidArgument : LocalizationStatus.OutOfResource;
        }

        error = string.Empty;
        return LocalizationStatus.Success;
    }

    private static LocalizationStatus BindArguments(
        IReadOnlyList<LocalizationArgumentSchema> schema,
        IReadOnlyList<LocalizationArgument> supplied,
        out LocalizationArgument[] bound,
        out string error)
    {
        bound = new LocalizationArgument[schema.Count];
        if (supplied.Count != schema.Count || supplied.Count > LocalizationLimits.MaximumArgumentsPerMessage)
        {
            error = "Supplied argument count does not match the message schema.";
            return LocalizationStatus.InvalidArgument;
        }

        bool[] assigned = new bool[schema.Count];
        foreach (LocalizationArgument argument in supplied)
        {
            int index = -1;
            for (int candidate = 0; candidate < schema.Count; ++candidate)
            {
                if (schema[candidate].NameId == argument.NameId && schema[candidate].Name == argument.Name)
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0 || assigned[index] || argument.Kind != schema[index].Kind ||
                argument.DecimalScale != schema[index].DecimalScale)
            {
                error = $"Argument '{argument.Name}' is unknown, duplicate, or has the wrong type/scale.";
                return LocalizationStatus.InvalidArgument;
            }

            if (argument.Kind == LocalizationValueKind.Select &&
                !schema[index].SelectValues.Contains(argument.TextValue!, StringComparer.Ordinal))
            {
                error = $"Argument '{argument.Name}' has a select value outside its allow-list.";
                return LocalizationStatus.InvalidArgument;
            }

            if (argument.Kind == LocalizationValueKind.Text &&
                Encoding.UTF8.GetByteCount(argument.TextValue!) > LocalizationLimits.MaximumFormattedMessageBytes)
            {
                error = $"Text argument '{argument.Name}' exceeds its byte limit.";
                return LocalizationStatus.OutOfResource;
            }

            if (argument.Kind == LocalizationValueKind.Localizable &&
                (argument.LocalizableValue is null || argument.LocalizableValue.Arguments.Count > LocalizationLimits.MaximumArgumentsPerMessage))
            {
                error = $"Localizable argument '{argument.Name}' is invalid or too large.";
                return LocalizationStatus.InvalidArgument;
            }

            bound[index] = argument;
            assigned[index] = true;
        }

        error = string.Empty;
        return LocalizationStatus.Success;
    }

    private static string SelectBranch(
        LocalizationArgument argument,
        NumberProfile profile,
        MessageSelectionKind kind,
        out string? category)
    {
        category = null;
        if (kind == MessageSelectionKind.Select)
        {
            return argument.Kind == LocalizationValueKind.Boolean
                ? argument.SignedValue == 0 ? "false" : "true"
                : argument.TextValue!;
        }

        _ = TryGetOperand(argument, out DecimalOperand operand);
        PluralCategory selected = kind == MessageSelectionKind.Cardinal
            ? PinnedLocaleData.SelectCardinal(profile, operand)
            : PinnedLocaleData.SelectOrdinal(profile, operand);
        category = selected.ToString().ToLowerInvariant();
        return category;
    }

    private static bool IsExactSelector(LocalizationArgument argument, string selector)
    {
        if (!long.TryParse(selector.AsSpan(1), NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out long exact) || !TryGetOperand(argument, out DecimalOperand operand))
        {
            return false;
        }

        return operand.IsExactInteger(exact);
    }

    private static bool TryGetOperand(LocalizationArgument argument, out DecimalOperand operand)
    {
        switch (argument.Kind)
        {
            case LocalizationValueKind.Unsigned:
                operand = new DecimalOperand(false, argument.UnsignedValue, 0);
                return true;
            case LocalizationValueKind.Integer:
            case LocalizationValueKind.Fixed:
            case LocalizationValueKind.Percent:
                bool negative = argument.SignedValue < 0;
                ulong magnitude = negative
                    ? (ulong)(-(argument.SignedValue + 1)) + 1
                    : (ulong)argument.SignedValue;
                operand = new DecimalOperand(negative, magnitude, argument.DecimalScale);
                return true;
            default:
                operand = default;
                return false;
        }
    }

    private static string FormatNumber(DecimalOperand operand, NumberProfile profile, int decimalShift)
    {
        string digits = operand.Magnitude.ToString(CultureInfo.InvariantCulture);
        int decimalPosition = digits.Length - operand.Scale + decimalShift;
        if (decimalPosition <= 0)
        {
            digits = new string('0', 1 - decimalPosition) + digits;
            decimalPosition = 1;
        }
        else if (decimalPosition > digits.Length)
        {
            digits += new string('0', decimalPosition - digits.Length);
        }

        string integer = digits[..decimalPosition];
        string fraction = digits[decimalPosition..];
        StringBuilder formatted = new(integer.Length + fraction.Length + 8);
        if (operand.Negative)
        {
            formatted.Append('-');
        }

        for (int index = 0; index < integer.Length; ++index)
        {
            if (index > 0 && (integer.Length - index) % 3 == 0)
            {
                formatted.Append(profile.GroupSeparator);
            }

            formatted.Append(MapDigit(integer[index], profile.Digits));
        }

        if (fraction.Length != 0)
        {
            formatted.Append(profile.DecimalSeparator);
            foreach (char digit in fraction)
            {
                formatted.Append(MapDigit(digit, profile.Digits));
            }
        }

        return formatted.ToString();
    }

    private static char MapDigit(char asciiDigit, string digits) => digits[asciiDigit - '0'];

    private sealed class BoundedMessageWriter
    {
        private readonly int maximumBytes;
        private readonly StringBuilder builder = new();
        private int byteCount;

        public BoundedMessageWriter(int maximumBytes)
        {
            this.maximumBytes = maximumBytes;
        }

        public bool TryAppend(string value)
        {
            int bytes = Encoding.UTF8.GetByteCount(value);
            if (bytes > maximumBytes - byteCount)
            {
                return false;
            }

            builder.Append(value);
            byteCount += bytes;
            return true;
        }

        public override string ToString() => builder.ToString();
    }
}
