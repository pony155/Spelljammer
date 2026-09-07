using System.Buffers.Binary;
using System.Text;

namespace Spelljammer.Localization;

public enum MessageArgumentPresentation : byte
{
    Default,
    Number,
    Percent
}

public enum MessageSelectionKind : byte
{
    Select,
    Cardinal,
    Ordinal
}

public abstract record MessageNode;

public sealed record MessageLiteralNode(string Text) : MessageNode;

public sealed record MessageArgumentNode(int ArgumentIndex, MessageArgumentPresentation Presentation) : MessageNode;

public sealed record MessagePoundNode(int ArgumentIndex) : MessageNode;

public sealed record MessageSelectionBranch(string Selector, IReadOnlyList<MessageNode> Nodes);

public sealed record MessageSelectionNode(
    int ArgumentIndex,
    MessageSelectionKind Kind,
    IReadOnlyList<MessageSelectionBranch> Branches) : MessageNode;

internal enum MessageOpcode : byte
{
    End,
    Literal,
    Argument,
    Pound,
    Selection
}

public sealed class CompiledMessageProgram
{
    internal CompiledMessageProgram(byte[] code, string? staticText)
    {
        Code = code;
        StaticText = staticText;
    }

    public ReadOnlyMemory<byte> Code { get; }

    public bool IsStatic => StaticText is not null;

    public string? StaticText { get; }
}

public static class MessageProgramCodec
{
    public static LocalizationStatus Encode(
        IReadOnlyList<MessageNode> nodes,
        IReadOnlyList<LocalizationArgumentSchema> schema,
        out CompiledMessageProgram? program,
        out string error)
    {
        program = null;
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(schema);

        try
        {
            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, new UTF8Encoding(false, true), leaveOpen: true))
            {
                WriteNodes(writer, nodes, 0);
                writer.Write((byte)MessageOpcode.End);
            }

            if (stream.Length > LocalizationLimits.MaximumMessageBytes)
            {
                error = "Compiled message program exceeds its byte limit.";
                return LocalizationStatus.OutOfResource;
            }

            return Validate(stream.ToArray(), schema, out program, out error);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return LocalizationStatus.InvalidArgument;
        }
        catch (OverflowException)
        {
            error = "Compiled message program contains an overflowing size.";
            return LocalizationStatus.OutOfResource;
        }
    }

    public static LocalizationStatus CreateLiteral(
        string text,
        out CompiledMessageProgram? program,
        out string error) =>
        Encode([new MessageLiteralNode(text)], [], out program, out error);

    public static LocalizationStatus Validate(
        ReadOnlySpan<byte> code,
        IReadOnlyList<LocalizationArgumentSchema> schema,
        out CompiledMessageProgram? program,
        out string error)
    {
        program = null;
        if (code.Length == 0 || code.Length > LocalizationLimits.MaximumMessageBytes ||
            schema.Count > LocalizationLimits.MaximumArgumentsPerMessage)
        {
            error = "Message program or argument schema exceeds supported bounds.";
            return LocalizationStatus.OutOfResource;
        }

        try
        {
            ProgramReader reader = new(code);
            StringBuilder? staticText = new();
            ValidateBlock(ref reader, schema, 0, null, ref staticText);
            reader.RequireEnd();
            program = new CompiledMessageProgram(code.ToArray(), staticText?.ToString());
            error = string.Empty;
            return LocalizationStatus.Success;
        }
        catch (ProgramValidationException exception)
        {
            error = exception.Message;
            return LocalizationStatus.DataCorrupt;
        }
    }

    public static string GetStructureSignature(CompiledMessageProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ProgramReader reader = new(program.Code.Span);
        string signature = AnalyzeBlock(ref reader);
        reader.RequireEnd();
        return signature;
    }

    public static LocalizationStatus ValidateLocaleSelections(
        CompiledMessageProgram program,
        LocaleId locale,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (!PinnedLocaleData.TryGetNumberProfile(locale, out _))
        {
            error = $"Locale '{locale.Tag}' has no pinned number/plural profile.";
            return LocalizationStatus.NotSupported;
        }

        try
        {
            ProgramReader reader = new(program.Code.Span);
            ValidateLocaleBlock(ref reader, locale);
            reader.RequireEnd();
            error = string.Empty;
            return LocalizationStatus.Success;
        }
        catch (ProgramValidationException exception)
        {
            error = exception.Message;
            return LocalizationStatus.DataCorrupt;
        }
    }

    private static void ValidateLocaleBlock(ref ProgramReader reader, LocaleId locale)
    {
        while (true)
        {
            MessageOpcode opcode = (MessageOpcode)reader.ReadByte();
            switch (opcode)
            {
                case MessageOpcode.End:
                    return;
                case MessageOpcode.Literal:
                    reader.Skip(reader.ReadLength(LocalizationLimits.MaximumMessageBytes));
                    break;
                case MessageOpcode.Argument:
                    reader.Skip(2);
                    break;
                case MessageOpcode.Pound:
                    reader.Skip(1);
                    break;
                case MessageOpcode.Selection:
                    {
                        reader.Skip(1);
                        MessageSelectionKind kind = (MessageSelectionKind)reader.ReadByte();
                        int count = reader.ReadByte();
                        HashSet<string> selectors = new(StringComparer.Ordinal);
                        List<ReadOnlyMemory<byte>> branches = new(count);
                        for (int index = 0; index < count; ++index)
                        {
                            selectors.Add(reader.ReadString(63));
                            int length = reader.ReadLength(LocalizationLimits.MaximumMessageBytes);
                            branches.Add(reader.ReadBytes(length).ToArray());
                        }

                        foreach (string required in PinnedLocaleData.GetRequiredCategories(locale, kind))
                        {
                            if (!selectors.Contains(required))
                            {
                                throw new ProgramValidationException(
                                    $"Locale '{locale.Tag}' requires plural category '{required}'.");
                            }
                        }

                        foreach (ReadOnlyMemory<byte> branch in branches)
                        {
                            ProgramReader branchReader = new(branch.Span);
                            ValidateLocaleBlock(ref branchReader, locale);
                            branchReader.RequireEnd();
                        }

                        break;
                    }
                default:
                    throw new ProgramValidationException("Validated program contains an unknown opcode.");
            }
        }
    }

    private static string AnalyzeBlock(ref ProgramReader reader)
    {
        List<string> constructs = [];
        while (true)
        {
            MessageOpcode opcode = (MessageOpcode)reader.ReadByte();
            switch (opcode)
            {
                case MessageOpcode.End:
                    constructs.Sort(StringComparer.Ordinal);
                    return string.Join('|', constructs);
                case MessageOpcode.Literal:
                    reader.Skip(reader.ReadLength(LocalizationLimits.MaximumMessageBytes));
                    break;
                case MessageOpcode.Argument:
                    constructs.Add($"A:{reader.ReadByte()}:{reader.ReadByte()}");
                    break;
                case MessageOpcode.Pound:
                    constructs.Add($"P:{reader.ReadByte()}");
                    break;
                case MessageOpcode.Selection:
                    {
                        int argument = reader.ReadByte();
                        int kind = reader.ReadByte();
                        int branchCount = reader.ReadByte();
                        List<string> selectors = new(branchCount);
                        List<string> nestedStructures = [];
                        for (int index = 0; index < branchCount; ++index)
                        {
                            string selector = reader.ReadString(63);
                            int length = reader.ReadLength(LocalizationLimits.MaximumMessageBytes);
                            ProgramReader branchReader = reader.ReadSubReader(length);
                            string nested = AnalyzeBlock(ref branchReader);
                            branchReader.RequireEnd();
                            if (kind == (int)MessageSelectionKind.Select || selector.StartsWith('='))
                            {
                                selectors.Add(selector);
                            }

                            if (nested.Length != 0)
                            {
                                nestedStructures.Add(nested);
                            }
                        }

                        selectors.Sort(StringComparer.Ordinal);
                        nestedStructures.Sort(StringComparer.Ordinal);
                        constructs.Add($"S:{argument}:{kind}[{string.Join(',', selectors)}]{{{string.Join(';', nestedStructures)}}}");
                        break;
                    }
                default:
                    throw new ProgramValidationException("Validated program contains an unknown opcode.");
            }
        }
    }

    private static void WriteNodes(BinaryWriter writer, IReadOnlyList<MessageNode> nodes, int depth)
    {
        if (depth > LocalizationLimits.MaximumMessageNesting)
        {
            throw new ArgumentException("Message selection nesting exceeds its limit.");
        }

        foreach (MessageNode node in nodes)
        {
            switch (node)
            {
                case MessageLiteralNode literal:
                    byte[] literalBytes = new UTF8Encoding(false, true).GetBytes(literal.Text);
                    writer.Write((byte)MessageOpcode.Literal);
                    writer.Write((uint)literalBytes.Length);
                    writer.Write(literalBytes);
                    break;
                case MessageArgumentNode argument:
                    RequireArgumentIndex(argument.ArgumentIndex);
                    writer.Write((byte)MessageOpcode.Argument);
                    writer.Write((byte)argument.ArgumentIndex);
                    writer.Write((byte)argument.Presentation);
                    break;
                case MessagePoundNode pound:
                    RequireArgumentIndex(pound.ArgumentIndex);
                    writer.Write((byte)MessageOpcode.Pound);
                    writer.Write((byte)pound.ArgumentIndex);
                    break;
                case MessageSelectionNode selection:
                    RequireArgumentIndex(selection.ArgumentIndex);
                    if (!Enum.IsDefined(selection.Kind) || selection.Branches.Count is 0 or > LocalizationLimits.MaximumBranchesPerSelection)
                    {
                        throw new ArgumentException("Message selection kind or branch count is invalid.");
                    }

                    writer.Write((byte)MessageOpcode.Selection);
                    writer.Write((byte)selection.ArgumentIndex);
                    writer.Write((byte)selection.Kind);
                    writer.Write((byte)selection.Branches.Count);
                    foreach (MessageSelectionBranch branch in selection.Branches)
                    {
                        WriteString(writer, branch.Selector);
                        using MemoryStream branchStream = new();
                        using (BinaryWriter branchWriter = new(branchStream, new UTF8Encoding(false, true), leaveOpen: true))
                        {
                            WriteNodes(branchWriter, branch.Nodes, depth + 1);
                            branchWriter.Write((byte)MessageOpcode.End);
                        }

                        byte[] branchBytes = branchStream.ToArray();
                        writer.Write((uint)branchBytes.Length);
                        writer.Write(branchBytes);
                    }

                    break;
                default:
                    throw new ArgumentException("Message contains an unknown instruction node.");
            }
        }
    }

    private static void ValidateBlock(
        ref ProgramReader reader,
        IReadOnlyList<LocalizationArgumentSchema> schema,
        int depth,
        int? pluralArgument,
        ref StringBuilder? staticText)
    {
        if (depth > LocalizationLimits.MaximumMessageNesting)
        {
            throw new ProgramValidationException("Message selection nesting exceeds its limit.");
        }

        while (true)
        {
            MessageOpcode opcode = (MessageOpcode)reader.ReadByte();
            switch (opcode)
            {
                case MessageOpcode.End:
                    return;
                case MessageOpcode.Literal:
                    {
                        string literal = reader.ReadString(LocalizationLimits.MaximumMessageBytes);
                        staticText?.Append(literal);
                        break;
                    }
                case MessageOpcode.Argument:
                    {
                        int argumentIndex = reader.ReadByte();
                        MessageArgumentPresentation presentation = (MessageArgumentPresentation)reader.ReadByte();
                        LocalizationArgumentSchema argument = GetArgument(schema, argumentIndex);
                        ValidatePresentation(argument, presentation);
                        staticText = null;
                        break;
                    }
                case MessageOpcode.Pound:
                    {
                        int argumentIndex = reader.ReadByte();
                        _ = GetArgument(schema, argumentIndex);
                        if (pluralArgument != argumentIndex)
                        {
                            throw new ProgramValidationException("Pound instruction is outside its plural selection.");
                        }

                        staticText = null;
                        break;
                    }
                case MessageOpcode.Selection:
                    {
                        int argumentIndex = reader.ReadByte();
                        MessageSelectionKind kind = (MessageSelectionKind)reader.ReadByte();
                        int branchCount = reader.ReadByte();
                        LocalizationArgumentSchema argument = GetArgument(schema, argumentIndex);
                        ValidateSelectionType(argument, kind, branchCount);
                        HashSet<string> selectors = new(StringComparer.Ordinal);
                        bool hasOther = false;
                        for (int branchIndex = 0; branchIndex < branchCount; ++branchIndex)
                        {
                            string selector = reader.ReadString(63);
                            ValidateSelector(argument, kind, selector);
                            if (!selectors.Add(selector))
                            {
                                throw new ProgramValidationException($"Selection contains duplicate branch '{selector}'.");
                            }

                            hasOther |= selector == "other";
                            int branchLength = reader.ReadLength(LocalizationLimits.MaximumMessageBytes);
                            ProgramReader branchReader = reader.ReadSubReader(branchLength);
                            StringBuilder? branchStatic = staticText is null ? null : new StringBuilder();
                            ValidateBlock(ref branchReader, schema, depth + 1,
                                kind == MessageSelectionKind.Select ? pluralArgument : argumentIndex,
                                ref branchStatic);
                            branchReader.RequireEnd();
                        }

                        if (!hasOther)
                        {
                            throw new ProgramValidationException("Every selection must contain an 'other' branch.");
                        }

                        staticText = null;
                        break;
                    }
                default:
                    throw new ProgramValidationException($"Message program contains unknown opcode {(byte)opcode}.");
            }
        }
    }

    private static void ValidatePresentation(
        LocalizationArgumentSchema argument,
        MessageArgumentPresentation presentation)
    {
        if (!Enum.IsDefined(presentation))
        {
            throw new ProgramValidationException("Argument presentation is invalid.");
        }

        bool valid = presentation switch
        {
            MessageArgumentPresentation.Number => argument.Kind is LocalizationValueKind.Integer or
                LocalizationValueKind.Unsigned or LocalizationValueKind.Fixed,
            MessageArgumentPresentation.Percent => argument.Kind == LocalizationValueKind.Percent,
            _ => true
        };
        if (!valid)
        {
            throw new ProgramValidationException($"Presentation does not match argument '{argument.Name}'.");
        }
    }

    private static void ValidateSelectionType(
        LocalizationArgumentSchema argument,
        MessageSelectionKind kind,
        int branchCount)
    {
        if (!Enum.IsDefined(kind) || branchCount is 0 or > LocalizationLimits.MaximumBranchesPerSelection)
        {
            throw new ProgramValidationException("Selection kind or branch count is invalid.");
        }

        bool valid = kind == MessageSelectionKind.Select
            ? argument.Kind is LocalizationValueKind.Select or LocalizationValueKind.Boolean
            : argument.Kind is LocalizationValueKind.Integer or LocalizationValueKind.Unsigned or LocalizationValueKind.Fixed;
        if (!valid)
        {
            throw new ProgramValidationException($"Selection does not match argument '{argument.Name}'.");
        }
    }

    private static void ValidateSelector(
        LocalizationArgumentSchema argument,
        MessageSelectionKind kind,
        string selector)
    {
        if (selector == "other")
        {
            return;
        }

        if (kind == MessageSelectionKind.Select)
        {
            if (argument.Kind == LocalizationValueKind.Boolean)
            {
                if (selector is not ("true" or "false"))
                {
                    throw new ProgramValidationException("Boolean selection accepts only true, false, and other.");
                }
            }
            else if (!argument.SelectValues.Contains(selector, StringComparer.Ordinal))
            {
                throw new ProgramValidationException($"Select branch '{selector}' is outside the argument allow-list.");
            }

            return;
        }

        if (selector[0] == '=')
        {
            if (!long.TryParse(selector.AsSpan(1), System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                throw new ProgramValidationException($"Exact plural selector '{selector}' is invalid.");
            }

            return;
        }

        if (selector is not ("zero" or "one" or "two" or "few" or "many"))
        {
            throw new ProgramValidationException($"Plural category '{selector}' is invalid.");
        }
    }

    private static LocalizationArgumentSchema GetArgument(
        IReadOnlyList<LocalizationArgumentSchema> schema,
        int index)
    {
        if ((uint)index >= (uint)schema.Count)
        {
            throw new ProgramValidationException("Message program references an invalid argument index.");
        }

        return schema[index];
    }

    private static void RequireArgumentIndex(int index)
    {
        if (index is < 0 or >= LocalizationLimits.MaximumArgumentsPerMessage)
        {
            throw new ArgumentException("Message argument index is outside supported bounds.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    internal ref struct ProgramReader
    {
        private ReadOnlySpan<byte> data;
        private int offset;

        public ProgramReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            offset = 0;
        }

        public int Remaining => data.Length - offset;

        public byte ReadByte()
        {
            Require(sizeof(byte));
            return data[offset++];
        }

        public int ReadLength(int maximum)
        {
            Require(sizeof(uint));
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
            offset += sizeof(uint);
            if (value > maximum)
            {
                throw new ProgramValidationException("Message program length exceeds its bound.");
            }

            return checked((int)value);
        }

        public long ReadInt64()
        {
            Require(sizeof(long));
            long value = BinaryPrimitives.ReadInt64LittleEndian(data[offset..]);
            offset += sizeof(long);
            return value;
        }

        public string ReadString(int maximumBytes)
        {
            int length = ReadLength(maximumBytes);
            Require(length);
            try
            {
                string value = new UTF8Encoding(false, true).GetString(data.Slice(offset, length));
                offset += length;
                return value;
            }
            catch (DecoderFallbackException exception)
            {
                throw new ProgramValidationException("Message program contains malformed UTF-8.", exception);
            }
        }

        public ProgramReader ReadSubReader(int length)
        {
            Require(length);
            ProgramReader reader = new(data.Slice(offset, length));
            offset += length;
            return reader;
        }

        public void Skip(int length)
        {
            Require(length);
            offset += length;
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            Require(length);
            ReadOnlySpan<byte> result = data.Slice(offset, length);
            offset += length;
            return result;
        }

        public void RequireEnd()
        {
            if (offset != data.Length)
            {
                throw new ProgramValidationException("Message block contains trailing data.");
            }
        }

        private void Require(int length)
        {
            if (length < 0 || offset > data.Length - length)
            {
                throw new ProgramValidationException("Message program is truncated.");
            }
        }
    }

    internal sealed class ProgramValidationException : Exception
    {
        public ProgramValidationException(string message)
            : base(message)
        {
        }

        public ProgramValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
