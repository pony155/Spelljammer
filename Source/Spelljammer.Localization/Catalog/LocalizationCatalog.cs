using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Spelljammer.Localization;

public enum TextDirection : byte
{
    LeftToRight = 0,
    RightToLeft = 1
}

public sealed class LocalizationCatalogEntry
{
    internal LocalizationCatalogEntry(
        LocalizationKey key,
        IReadOnlyList<LocalizationArgumentSchema> arguments,
        CompiledMessageProgram program,
        bool emptyAllowed)
    {
        Key = key;
        Arguments = new ReadOnlyCollection<LocalizationArgumentSchema>(arguments.ToArray());
        Program = program;
        EmptyAllowed = emptyAllowed;
    }

    public LocalizationKey Key { get; }

    public IReadOnlyList<LocalizationArgumentSchema> Arguments { get; }

    public bool EmptyAllowed { get; }

    public bool IsStatic => Arguments.Count == 0 && Program.IsStatic;

    public string? StaticMessage => IsStatic ? Program.StaticText : null;

    internal CompiledMessageProgram Program { get; }
}

public sealed class LocalizationCatalog
{
    private readonly ReadOnlyDictionary<LocalizationKey, LocalizationCatalogEntry> entries;

    internal LocalizationCatalog(
        LocaleId locale,
        string catalogNamespace,
        TextDirection direction,
        IReadOnlyList<LocaleId> fallbacks,
        IEnumerable<LocalizationCatalogEntry> entries,
        byte[] fingerprint)
    {
        Locale = locale;
        Namespace = catalogNamespace;
        Direction = direction;
        Fallbacks = new ReadOnlyCollection<LocaleId>(fallbacks.ToArray());
        this.entries = new ReadOnlyDictionary<LocalizationKey, LocalizationCatalogEntry>(
            entries.ToDictionary(entry => entry.Key));
        Fingerprint = Convert.ToHexStringLower(fingerprint);
    }

    public LocaleId Locale { get; }

    public string Namespace { get; }

    public ulong NamespaceId => StableNameHash.Compute(Namespace);

    public TextDirection Direction { get; }

    public IReadOnlyList<LocaleId> Fallbacks { get; }

    public IReadOnlyCollection<LocalizationCatalogEntry> Entries => entries.Values;

    public string Fingerprint { get; }

    internal bool TryGet(LocalizationKey key, out LocalizationCatalogEntry? entry) =>
        entries.TryGetValue(key, out entry);
}

public sealed record LocalizationCatalogDefinition(
    string Locale,
    string Namespace,
    TextDirection Direction,
    IReadOnlyList<string> Fallbacks,
    IReadOnlyList<LocalizationCatalogEntryDefinition> Entries);

public sealed record LocalizationCatalogEntryDefinition(
    string Key,
    string Message,
    bool EmptyAllowed = false,
    IReadOnlyList<LocalizationArgumentSchema>? Arguments = null,
    CompiledMessageProgram? Program = null);

public static class LocalizationArtifact
{
    public const ushort SchemaVersion = 2;
    public const string CompilerIdentity = "SpriteForge.Localization.Compiler/2";

    private static ReadOnlySpan<byte> Magic => "SFLOC\r\n\x1a"u8;

    private const int HeaderBytes = 8 + sizeof(ushort) + sizeof(ushort) + sizeof(uint) + 32;

    public static LocalizationStatus Encode(
        LocalizationCatalogDefinition definition,
        out byte[] artifact,
        out string error)
    {
        artifact = [];
        if (!TryValidateDefinition(definition, out LocaleId locale, out LocaleId[] fallbacks,
            out LocalizationCatalogEntry[] entries, out error))
        {
            return LocalizationStatus.InvalidArgument;
        }

        using MemoryStream payloadStream = new();
        using (BinaryWriter writer = new(payloadStream, new UTF8Encoding(false, true), leaveOpen: true))
        {
            WriteString(writer, CompilerIdentity);
            WriteString(writer, PinnedLocaleData.Version);
            WriteString(writer, PinnedLocaleData.TableHash);
            writer.Write(locale.Value);
            WriteString(writer, locale.Tag);
            writer.Write(StableNameHash.Compute(definition.Namespace));
            WriteString(writer, definition.Namespace);
            writer.Write((byte)definition.Direction);
            writer.Write((byte)fallbacks.Length);
            writer.Write((ushort)0);
            writer.Write((uint)entries.Length);

            foreach (LocaleId fallback in fallbacks)
            {
                writer.Write(fallback.Value);
                WriteString(writer, fallback.Tag);
            }

            foreach (LocalizationCatalogEntry entry in entries)
            {
                writer.Write(entry.Key.Value);
                WriteString(writer, entry.Key.Name);
                writer.Write(entry.EmptyAllowed);
                writer.Write((byte)entry.Arguments.Count);
                writer.Write((ushort)0);
                foreach (LocalizationArgumentSchema argument in entry.Arguments)
                {
                    writer.Write(argument.NameId);
                    WriteString(writer, argument.Name);
                    writer.Write((byte)argument.Kind);
                    writer.Write((byte)argument.DecimalScale);
                    writer.Write(argument.Sensitive);
                    writer.Write((byte)argument.SelectValues.Count);
                    foreach (string selectValue in argument.SelectValues)
                    {
                        WriteString(writer, selectValue);
                    }
                }

                writer.Write((uint)entry.Program.Code.Length);
                writer.Write(entry.Program.Code.Span);
            }
        }

        byte[] payload = payloadStream.ToArray();
        if (payload.Length + HeaderBytes > LocalizationLimits.MaximumArtifactBytes)
        {
            error = $"Artifact exceeds {LocalizationLimits.MaximumArtifactBytes} bytes.";
            return LocalizationStatus.OutOfResource;
        }

        byte[] checksum = SHA256.HashData(payload);
        artifact = new byte[HeaderBytes + payload.Length];
        Magic.CopyTo(artifact);
        BinaryPrimitives.WriteUInt16LittleEndian(artifact.AsSpan(8), SchemaVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(artifact.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(artifact.AsSpan(12), (uint)payload.Length);
        checksum.CopyTo(artifact, 16);
        payload.CopyTo(artifact, HeaderBytes);
        error = string.Empty;
        return LocalizationStatus.Success;
    }

    public static LocalizationStatus Decode(
        ReadOnlySpan<byte> artifact,
        out LocalizationCatalog? catalog,
        out string error)
    {
        catalog = null;
        if (artifact.Length < HeaderBytes || artifact.Length > LocalizationLimits.MaximumArtifactBytes)
        {
            error = "Artifact size is outside the supported bounds.";
            return LocalizationStatus.DataCorrupt;
        }

        if (!artifact[..8].SequenceEqual(Magic))
        {
            error = "Artifact magic is invalid.";
            return LocalizationStatus.DataCorrupt;
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(artifact[8..]);
        if (version != SchemaVersion)
        {
            error = $"Artifact schema {version} is unsupported.";
            return LocalizationStatus.NotSupported;
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(artifact[10..]) != 0)
        {
            error = "Artifact has unsupported flags.";
            return LocalizationStatus.NotSupported;
        }

        uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(artifact[12..]);
        if (payloadLength != artifact.Length - HeaderBytes)
        {
            error = "Artifact payload length or trailing data is invalid.";
            return LocalizationStatus.DataCorrupt;
        }

        ReadOnlySpan<byte> payload = artifact[HeaderBytes..];
        if (!CryptographicOperations.FixedTimeEquals(artifact.Slice(16, 32), SHA256.HashData(payload)))
        {
            error = "Artifact checksum does not match its payload.";
            return LocalizationStatus.DataCorrupt;
        }

        try
        {
            CatalogReader reader = new(payload);
            RequireIdentity(reader.ReadString(127), CompilerIdentity, "compiler identity");
            RequireIdentity(reader.ReadString(31), PinnedLocaleData.Version, "locale-data version");
            RequireIdentity(reader.ReadString(64), PinnedLocaleData.TableHash, "locale-data hash");

            ulong localeValue = reader.ReadUInt64();
            string localeName = reader.ReadString(LocalizationLimits.MaximumLocaleTagBytes);
            if (!LocaleId.TryCreate(localeName, out LocaleId locale, out error) || locale.Value != localeValue)
            {
                error = string.IsNullOrEmpty(error) ? $"Locale '{localeName}' has a mismatched stable ID." : error;
                return LocalizationStatus.DataCorrupt;
            }

            if (!PinnedLocaleData.TryGetNumberProfile(locale, out _))
            {
                error = $"Locale '{locale.Tag}' has no pinned Phase 2 number/plural profile.";
                return LocalizationStatus.NotSupported;
            }

            ulong namespaceValue = reader.ReadUInt64();
            string catalogNamespace = reader.ReadString(LocalizationLimits.MaximumNamespaceBytes);
            if (!LocalizationNames.IsCanonicalNamespace(catalogNamespace, out error) ||
                StableNameHash.Compute(catalogNamespace) != namespaceValue)
            {
                error = string.IsNullOrEmpty(error) ? $"Namespace '{catalogNamespace}' has a mismatched stable ID." : error;
                return LocalizationStatus.DataCorrupt;
            }

            byte directionValue = reader.ReadByte();
            if (directionValue > (byte)TextDirection.RightToLeft)
            {
                error = "Artifact text direction is invalid.";
                return LocalizationStatus.DataCorrupt;
            }

            int fallbackCount = reader.ReadByte();
            if (fallbackCount > LocalizationLimits.MaximumFallbackDepth)
            {
                error = "Artifact fallback chain exceeds the supported depth.";
                return LocalizationStatus.OutOfResource;
            }

            if (reader.ReadUInt16() != 0)
            {
                error = "Artifact reserved data is nonzero.";
                return LocalizationStatus.NotSupported;
            }

            int entryCount = reader.ReadCount(LocalizationLimits.MaximumKeysPerLocale);
            List<LocaleId> fallbacks = ReadFallbacks(ref reader, locale, fallbackCount);
            List<LocalizationCatalogEntry> entries = new(entryCount);
            StableNameRegistry registry = new();
            (ulong Id, string Name)? previous = null;
            for (int index = 0; index < entryCount; ++index)
            {
                ulong serializedId = reader.ReadUInt64();
                string keyName = reader.ReadString(LocalizationLimits.MaximumCanonicalKeyBytes);
                if (!LocalizationKey.TryCreate(keyName, out LocalizationKey key, out error) || key.Value != serializedId)
                {
                    error = string.IsNullOrEmpty(error) ? $"Key '{keyName}' has a mismatched stable ID." : error;
                    return LocalizationStatus.DataCorrupt;
                }

                if (!keyName.StartsWith(catalogNamespace + ".", StringComparison.Ordinal))
                {
                    error = $"Key '{keyName}' does not belong to namespace '{catalogNamespace}'.";
                    return LocalizationStatus.DataCorrupt;
                }

                if (previous is { } prior && Compare(prior, (key.Value, key.Name)) >= 0)
                {
                    error = "Artifact key table is not strictly sorted.";
                    return LocalizationStatus.DataCorrupt;
                }

                if (!registry.TryAdd(key.Value, key.Name, out error))
                {
                    return LocalizationStatus.Conflict;
                }

                bool emptyAllowed = reader.ReadBoolean();
                int argumentCount = reader.ReadByte();
                if (argumentCount > LocalizationLimits.MaximumArgumentsPerMessage || reader.ReadUInt16() != 0)
                {
                    error = "Artifact argument count or reserved data is invalid.";
                    return LocalizationStatus.DataCorrupt;
                }

                List<LocalizationArgumentSchema> arguments = ReadArgumentSchema(ref reader, argumentCount);
                int programLength = reader.ReadCount(LocalizationLimits.MaximumMessageBytes);
                ReadOnlySpan<byte> programBytes = reader.ReadBytes(programLength);
                LocalizationStatus programStatus = MessageProgramCodec.Validate(programBytes, arguments,
                    out CompiledMessageProgram? program, out error);
                if (programStatus != LocalizationStatus.Success || program is null)
                {
                    return programStatus;
                }

                programStatus = MessageProgramCodec.ValidateLocaleSelections(program, locale, out error);
                if (programStatus != LocalizationStatus.Success)
                {
                    return programStatus;
                }

                if (!emptyAllowed && program.IsStatic && program.StaticText!.Length == 0)
                {
                    error = $"Static message '{keyName}' is empty without permission.";
                    return LocalizationStatus.DataCorrupt;
                }

                entries.Add(new LocalizationCatalogEntry(key, arguments, program, emptyAllowed));
                previous = (key.Value, key.Name);
            }

            reader.RequireEnd();
            catalog = new LocalizationCatalog(locale, catalogNamespace, (TextDirection)directionValue,
                fallbacks, entries, SHA256.HashData(artifact));
            error = string.Empty;
            return LocalizationStatus.Success;
        }
        catch (UnsupportedArtifactException exception)
        {
            error = exception.Message;
            return LocalizationStatus.NotSupported;
        }
        catch (CatalogReadException exception)
        {
            error = exception.Message;
            return LocalizationStatus.DataCorrupt;
        }
        catch (OverflowException)
        {
            error = "Artifact contains an overflowing size or offset.";
            return LocalizationStatus.DataCorrupt;
        }
    }

    private static bool TryValidateDefinition(
        LocalizationCatalogDefinition definition,
        out LocaleId locale,
        out LocaleId[] fallbacks,
        out LocalizationCatalogEntry[] entries,
        out string error)
    {
        locale = default;
        fallbacks = [];
        entries = [];
        if (definition is null || definition.Fallbacks is null || definition.Entries is null)
        {
            error = "Catalog definition is required.";
            return false;
        }

        if (!LocaleId.TryCreate(definition.Locale, out locale, out error) ||
            !LocalizationNames.IsCanonicalNamespace(definition.Namespace, out error) ||
            !PinnedLocaleData.TryGetNumberProfile(locale, out _))
        {
            error = string.IsNullOrEmpty(error) ? $"Locale '{definition.Locale}' has no pinned Phase 2 profile." : error;
            return false;
        }

        if (!Enum.IsDefined(definition.Direction) || definition.Fallbacks.Count > LocalizationLimits.MaximumFallbackDepth ||
            definition.Entries.Count > LocalizationLimits.MaximumKeysPerLocale)
        {
            error = "Catalog direction, fallback count, or entry count is outside supported bounds.";
            return false;
        }

        HashSet<LocaleId> fallbackSet = [];
        fallbacks = new LocaleId[definition.Fallbacks.Count];
        for (int index = 0; index < fallbacks.Length; ++index)
        {
            if (!LocaleId.TryCreate(definition.Fallbacks[index], out LocaleId fallback, out error) ||
                fallback == locale || !fallbackSet.Add(fallback))
            {
                error = string.IsNullOrEmpty(error) ? "Fallback chain contains a cycle or duplicate." : error;
                return false;
            }

            fallbacks[index] = fallback;
        }

        StableNameRegistry registry = new();
        List<LocalizationCatalogEntry> validatedEntries = new(definition.Entries.Count);
        foreach (LocalizationCatalogEntryDefinition entry in definition.Entries)
        {
            if (entry is null || entry.Message is null ||
                !LocalizationKey.TryCreate(entry.Key, out LocalizationKey key, out error) ||
                !entry.Key.StartsWith(definition.Namespace + ".", StringComparison.Ordinal))
            {
                error = string.IsNullOrEmpty(error) ? "Catalog entry is null or outside its namespace." : error;
                return false;
            }

            if (!registry.TryAdd(key.Value, key.Name, out error))
            {
                return false;
            }

            IReadOnlyList<LocalizationArgumentSchema> arguments = entry.Arguments ?? [];
            if (!TryValidateArgumentSchema(arguments, out error))
            {
                return false;
            }

            CompiledMessageProgram? program = entry.Program;
            LocalizationStatus programStatus = program is null
                ? MessageProgramCodec.CreateLiteral(entry.Message, out program, out error)
                : MessageProgramCodec.Validate(program.Code.Span, arguments, out program, out error);
            if (programStatus != LocalizationStatus.Success || program is null)
            {
                return false;
            }

            if (MessageProgramCodec.ValidateLocaleSelections(program, locale, out error) != LocalizationStatus.Success)
            {
                return false;
            }

            if (!entry.EmptyAllowed && program.IsStatic && program.StaticText!.Length == 0)
            {
                error = $"Message '{entry.Key}' is empty without permission.";
                return false;
            }

            validatedEntries.Add(new LocalizationCatalogEntry(key, arguments, program, entry.EmptyAllowed));
        }

        entries = validatedEntries.OrderBy(entry => entry.Key.Value)
            .ThenBy(entry => entry.Key.Name, StringComparer.Ordinal).ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryValidateArgumentSchema(
        IReadOnlyList<LocalizationArgumentSchema> arguments,
        out string error)
    {
        if (arguments.Count > LocalizationLimits.MaximumArgumentsPerMessage)
        {
            error = "Message has too many arguments.";
            return false;
        }

        StableNameRegistry registry = new();
        (uint Id, string Name)? previous = null;
        foreach (LocalizationArgumentSchema argument in arguments)
        {
            if (argument is null || argument.SelectValues is null || !LocalizationArgumentName.IsCanonical(argument.Name) ||
                argument.NameId != LocalizationArgumentName.ComputeId(argument.Name) || !Enum.IsDefined(argument.Kind))
            {
                error = "Argument schema has an invalid name, ID, or kind.";
                return false;
            }

            if (!registry.TryAdd(argument.NameId, argument.Name, out error))
            {
                return false;
            }

            if (previous is { } prior && (prior.Id > argument.NameId ||
                prior.Id == argument.NameId && StringComparer.Ordinal.Compare(prior.Name, argument.Name) >= 0))
            {
                error = "Argument schema is not strictly sorted by stable ID and name.";
                return false;
            }

            bool scaled = argument.Kind is LocalizationValueKind.Fixed or LocalizationValueKind.Percent;
            if (argument.DecimalScale is < 0 or > 9 || (!scaled && argument.DecimalScale != 0) ||
                (argument.Sensitive && argument.Kind != LocalizationValueKind.Text))
            {
                error = $"Argument '{argument.Name}' has invalid scale or sensitivity metadata.";
                return false;
            }

            string[] selectValues = argument.SelectValues.ToArray();
            if (argument.Kind == LocalizationValueKind.Select)
            {
                if (selectValues.Length == 0 || selectValues.Length > LocalizationLimits.MaximumBranchesPerSelection ||
                    selectValues.Any(value => !LocalizationArgumentName.IsCanonicalToken(value)) ||
                    !selectValues.SequenceEqual(selectValues.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
                    selectValues.Distinct(StringComparer.Ordinal).Count() != selectValues.Length)
                {
                    error = $"Select argument '{argument.Name}' has an invalid or unsorted allow-list.";
                    return false;
                }
            }
            else if (selectValues.Length != 0)
            {
                error = $"Non-select argument '{argument.Name}' has select values.";
                return false;
            }

            previous = (argument.NameId, argument.Name);
        }

        error = string.Empty;
        return true;
    }

    private static List<LocaleId> ReadFallbacks(ref CatalogReader reader, LocaleId locale, int count)
    {
        List<LocaleId> fallbacks = new(count);
        HashSet<LocaleId> unique = [];
        for (int index = 0; index < count; ++index)
        {
            ulong value = reader.ReadUInt64();
            string name = reader.ReadString(LocalizationLimits.MaximumLocaleTagBytes);
            if (!LocaleId.TryCreate(name, out LocaleId fallback, out _) || fallback.Value != value ||
                fallback == locale || !unique.Add(fallback))
            {
                throw new CatalogReadException("Artifact fallback chain contains a mismatched ID, cycle, or duplicate.");
            }

            fallbacks.Add(fallback);
        }

        return fallbacks;
    }

    private static List<LocalizationArgumentSchema> ReadArgumentSchema(ref CatalogReader reader, int count)
    {
        List<LocalizationArgumentSchema> arguments = new(count);
        for (int index = 0; index < count; ++index)
        {
            uint nameId = reader.ReadUInt32();
            string name = reader.ReadString(63);
            LocalizationValueKind kind = (LocalizationValueKind)reader.ReadByte();
            int scale = reader.ReadByte();
            bool sensitive = reader.ReadBoolean();
            int selectCount = reader.ReadByte();
            if (selectCount > LocalizationLimits.MaximumBranchesPerSelection)
            {
                throw new CatalogReadException("Argument select allow-list exceeds its bound.");
            }

            List<string> selectValues = new(selectCount);
            for (int selectIndex = 0; selectIndex < selectCount; ++selectIndex)
            {
                selectValues.Add(reader.ReadString(63));
            }

            arguments.Add(new LocalizationArgumentSchema(name, nameId, kind, scale, sensitive,
                new ReadOnlyCollection<string>(selectValues)));
        }

        if (!TryValidateArgumentSchema(arguments, out string error))
        {
            throw new CatalogReadException(error);
        }

        return arguments;
    }

    private static void RequireIdentity(string actual, string expected, string label)
    {
        if (actual != expected)
        {
            throw new UnsupportedArtifactException($"Artifact {label} '{actual}' is unsupported.");
        }
    }

    private static int Compare((ulong Id, string Name) left, (ulong Id, string Name) right)
    {
        int idResult = left.Id.CompareTo(right.Id);
        return idResult != 0 ? idResult : StringComparer.Ordinal.Compare(left.Name, right.Name);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    private ref struct CatalogReader
    {
        private ReadOnlySpan<byte> data;
        private int offset;

        public CatalogReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            offset = 0;
        }

        public byte ReadByte()
        {
            Require(sizeof(byte));
            return data[offset++];
        }

        public bool ReadBoolean() => ReadByte() switch
        {
            0 => false,
            1 => true,
            _ => throw new CatalogReadException("Artifact Boolean value is invalid.")
        };

        public ushort ReadUInt16()
        {
            Require(sizeof(ushort));
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
            offset += sizeof(ushort);
            return value;
        }

        public uint ReadUInt32()
        {
            Require(sizeof(uint));
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
            offset += sizeof(uint);
            return value;
        }

        public ulong ReadUInt64()
        {
            Require(sizeof(ulong));
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
            offset += sizeof(ulong);
            return value;
        }

        public int ReadCount(int maximum)
        {
            uint value = ReadUInt32();
            if (value > maximum)
            {
                throw new CatalogReadException($"Artifact count exceeds its {maximum}-item bound.");
            }

            return checked((int)value);
        }

        public string ReadString(int maximumBytes)
        {
            int size = ReadCount(maximumBytes);
            Require(size);
            try
            {
                string value = new UTF8Encoding(false, true).GetString(data.Slice(offset, size));
                offset += size;
                return value;
            }
            catch (DecoderFallbackException exception)
            {
                throw new CatalogReadException("Artifact contains malformed UTF-8.", exception);
            }
        }

        public ReadOnlySpan<byte> ReadBytes(int size)
        {
            Require(size);
            ReadOnlySpan<byte> value = data.Slice(offset, size);
            offset += size;
            return value;
        }

        public void RequireEnd()
        {
            if (offset != data.Length)
            {
                throw new CatalogReadException("Artifact contains trailing payload data.");
            }
        }

        private void Require(int size)
        {
            if (size < 0 || offset > data.Length - size)
            {
                throw new CatalogReadException("Artifact is truncated.");
            }
        }
    }

    private sealed class CatalogReadException : Exception
    {
        public CatalogReadException(string message)
            : base(message)
        {
        }

        public CatalogReadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private sealed class UnsupportedArtifactException : Exception
    {
        public UnsupportedArtifactException(string message)
            : base(message)
        {
        }
    }
}
