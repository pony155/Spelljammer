using System.Collections.ObjectModel;
using System.Text;
using System.Threading;

namespace Spelljammer.Localization;

public enum LocalizationStatus
{
    Success,
    NotInitialized,
    AlreadyInitialized,
    WrongThread,
    InvalidArgument,
    ItemNotFound,
    DataCorrupt,
    NotSupported,
    OutOfResource,
    Conflict
}

public enum MissingKeyPolicy
{
    ReturnNotFound,
    DevelopmentMarker
}

public sealed record LocalizationConfig(
    string SourceLocale,
    MissingKeyPolicy MissingKeyPolicy = MissingKeyPolicy.DevelopmentMarker,
    IReadOnlyCollection<string>? RequiredNamespaces = null,
    int DiagnosticCapacity = LocalizationLimits.MaximumDiagnostics,
    int MaximumFormatsPerFrame = LocalizationLimits.MaximumFormatsPerFrame);

public sealed record LocalizedMessage(
    string Text,
    LocalizationKey SourceKey,
    LocaleId RequestedLocale,
    LocaleId ResolvedLocale,
    TextDirection Direction,
    ulong Generation,
    LocalizationLanguageProfile LanguageProfile);

public sealed record LocaleSnapshot(
    bool IsPublished,
    LocaleId RequestedLocale,
    TextDirection Direction,
    ulong Generation);

public sealed record LocalizationDiagnostic(
    ulong Sequence,
    LocalizationStatus Status,
    string Operation,
    string RequestedLocale,
    string? ResolvedLocale,
    string? KeyName,
    ulong KeyId,
    int FallbackDepth,
    ulong Generation,
    int OutputUtf8Bytes,
    string? PluralCategory,
    string LocaleDataVersion,
    string LocaleDataHash,
    string? ArgumentSchema,
    string Detail);

public sealed class LocalizationDiagnostics
{
    internal LocalizationDiagnostics(
        IReadOnlyList<LocalizationDiagnostic> records,
        ulong droppedRecords)
    {
        Records = records;
        DroppedRecords = droppedRecords;
    }

    public IReadOnlyList<LocalizationDiagnostic> Records { get; }

    public ulong DroppedRecords { get; }
}

public sealed class LocaleGeneration
{
    private readonly IReadOnlyDictionary<LocalizationKey, ResolvedEntry> entries;

    internal LocaleGeneration(
        Guid owner,
        ulong sequence,
        LocaleId requestedLocale,
        TextDirection direction,
        LocalizationLanguageProfile languageProfile,
        Dictionary<LocalizationKey, ResolvedEntry> entries)
    {
        Owner = owner;
        Sequence = sequence;
        RequestedLocale = requestedLocale;
        Direction = direction;
        LanguageProfile = languageProfile;
        this.entries = new ReadOnlyDictionary<LocalizationKey, ResolvedEntry>(entries);
    }

    public ulong Sequence { get; }

    public LocaleId RequestedLocale { get; }

    public TextDirection Direction { get; }

    public LocalizationLanguageProfile LanguageProfile { get; }

    internal Guid Owner { get; }

    internal bool TryGet(LocalizationKey key, out ResolvedEntry? entry) => entries.TryGetValue(key, out entry);
}

internal sealed record ResolvedEntry(
    LocalizationCatalogEntry CatalogEntry,
    LocaleId Locale,
    TextDirection Direction,
    NumberProfile NumberProfile,
    LocalizationLanguageProfile LanguageProfile,
    int FallbackDepth);

public sealed class LocalizationService
{
    private readonly Queue<LocalizationDiagnostic> diagnostics = [];
    private LocaleGeneration? published;
    private LocalizationConfig? config;
    private LocaleId sourceLocale;
    private Guid ownerToken;
    private int ownerThreadId;
    private ulong nextGeneration = 1;
    private ulong diagnosticSequence;
    private ulong droppedDiagnostics;
    private int formatsThisFrame;

    public LocalizationStatus Initialize(LocalizationConfig initializationConfig)
    {
        ArgumentNullException.ThrowIfNull(initializationConfig);
        if (config is not null)
        {
            return LocalizationStatus.AlreadyInitialized;
        }

        if (!LocaleId.TryCreate(initializationConfig.SourceLocale, out LocaleId parsedSource, out _) ||
            initializationConfig.DiagnosticCapacity is < 0 or > LocalizationLimits.MaximumDiagnostics ||
            initializationConfig.MaximumFormatsPerFrame is < 1 or > LocalizationLimits.MaximumFormatsPerFrame ||
            !PinnedLocaleData.TryGetNumberProfile(parsedSource, out _) ||
            !Enum.IsDefined(initializationConfig.MissingKeyPolicy))
        {
            return LocalizationStatus.InvalidArgument;
        }

        HashSet<string> requiredNamespaces = new(StringComparer.Ordinal);
        foreach (string requiredNamespace in initializationConfig.RequiredNamespaces ?? [])
        {
            if (!LocalizationNames.IsCanonicalNamespace(requiredNamespace, out _) ||
                !requiredNamespaces.Add(requiredNamespace))
            {
                return LocalizationStatus.InvalidArgument;
            }
        }

        config = initializationConfig with
        {
            RequiredNamespaces = new ReadOnlyCollection<string>(requiredNamespaces.Order(StringComparer.Ordinal).ToArray())
        };
        sourceLocale = parsedSource;
        ownerThreadId = Environment.CurrentManagedThreadId;
        ownerToken = Guid.NewGuid();
        return LocalizationStatus.Success;
    }

    public LocalizationStatus StageLocale(
        LocaleId requestedLocale,
        IReadOnlyCollection<LocalizationCatalog> catalogs,
        out LocaleGeneration? generation)
    {
        generation = null;
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus != LocalizationStatus.Success)
        {
            return ownerStatus;
        }

        ArgumentNullException.ThrowIfNull(catalogs);
        if (requestedLocale.Tag is null || catalogs.Count == 0)
        {
            return LocalizationStatus.InvalidArgument;
        }

        if (catalogs.Count > LocalizationLimits.MaximumCatalogsPerGeneration)
        {
            return RecordStage(LocalizationStatus.OutOfResource, requestedLocale, "Generation contains too many catalogs.");
        }

        Dictionary<(LocaleId Locale, string Namespace), LocalizationCatalog> byCatalog = [];
        Dictionary<LocaleId, LocaleManifest> manifests = [];
        Dictionary<LocaleId, int> localeEntryCounts = [];
        StableNameRegistry keyRegistry = new();
        StableNameRegistry localeRegistry = new();
        StableNameRegistry namespaceRegistry = new();
        HashSet<LocaleId> registeredLocales = [];
        HashSet<string> registeredNamespaces = new(StringComparer.Ordinal);
        foreach (LocalizationCatalog catalog in catalogs)
        {
            if (catalog is null || !byCatalog.TryAdd((catalog.Locale, catalog.Namespace), catalog))
            {
                return RecordStage(LocalizationStatus.Conflict, requestedLocale, "Duplicate locale/namespace catalog.");
            }

            if (registeredLocales.Add(catalog.Locale) &&
                !localeRegistry.TryAdd(catalog.Locale.Value, catalog.Locale.Tag, out string localeError))
            {
                return RecordStage(LocalizationStatus.Conflict, requestedLocale, localeError);
            }

            if (registeredNamespaces.Add(catalog.Namespace) &&
                !namespaceRegistry.TryAdd(catalog.NamespaceId, catalog.Namespace, out string namespaceError))
            {
                return RecordStage(LocalizationStatus.Conflict, requestedLocale, namespaceError);
            }

            if (byCatalog.Keys.Count(key => key.Locale == catalog.Locale) > LocalizationLimits.MaximumCatalogsPerLocale)
            {
                return RecordStage(LocalizationStatus.OutOfResource, requestedLocale, "Locale has too many catalog namespaces.");
            }

            LocaleManifest candidate = new(catalog.Fallbacks, catalog.Direction);
            if (manifests.TryGetValue(catalog.Locale, out LocaleManifest? existing) && !existing.Matches(candidate))
            {
                return RecordStage(LocalizationStatus.Conflict, requestedLocale, "Catalogs disagree on locale fallback/profile metadata.");
            }

            manifests[catalog.Locale] = candidate;
            localeEntryCounts.TryGetValue(catalog.Locale, out int localeEntryCount);
            if (catalog.Entries.Count > LocalizationLimits.MaximumKeysPerLocale - localeEntryCount)
            {
                return RecordStage(LocalizationStatus.OutOfResource, requestedLocale, "Locale has too many keys across its catalogs.");
            }

            localeEntryCounts[catalog.Locale] = localeEntryCount + catalog.Entries.Count;
            foreach (LocalizationCatalogEntry entry in catalog.Entries)
            {
                if (!keyRegistry.TryAdd(entry.Key.Value, entry.Key.Name, out string registryError) &&
                    !registryError.StartsWith("Duplicate", StringComparison.Ordinal))
                {
                    return RecordStage(LocalizationStatus.Conflict, requestedLocale, registryError);
                }
            }
        }

        if (!manifests.TryGetValue(requestedLocale, out LocaleManifest? requestedManifest))
        {
            return RecordStage(LocalizationStatus.ItemNotFound, requestedLocale, "Requested locale is not installed.");
        }

        if (!ValidateFallbackChain(requestedLocale, requestedManifest.Fallbacks, manifests, out string fallbackError))
        {
            return RecordStage(LocalizationStatus.InvalidArgument, requestedLocale, fallbackError);
        }

        LocaleId[] chain = [requestedLocale, .. requestedManifest.Fallbacks];
        HashSet<string> namespaces = byCatalog.Keys
            .Where(key => chain.Contains(key.Locale))
            .Select(key => key.Namespace)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string requiredNamespace in config!.RequiredNamespaces ?? [])
        {
            if (!byCatalog.ContainsKey((sourceLocale, requiredNamespace)))
            {
                return RecordStage(LocalizationStatus.ItemNotFound, requestedLocale,
                    $"Required source namespace '{requiredNamespace}' is unavailable.");
            }

            namespaces.Add(requiredNamespace);
        }

        Dictionary<LocalizationKey, ResolvedEntry> resolvedEntries = [];
        int staticCacheBytes = 0;
        foreach (string catalogNamespace in namespaces.Order(StringComparer.Ordinal))
        {
            Dictionary<LocalizationKey, LocalizationCatalogEntry> sourceSchemas = byCatalog
                .TryGetValue((sourceLocale, catalogNamespace), out LocalizationCatalog? sourceSchemaCatalog)
                ? sourceSchemaCatalog.Entries.ToDictionary(entry => entry.Key)
                : [];
            for (int depth = chain.Length - 1; depth >= 0; --depth)
            {
                if (!byCatalog.TryGetValue((chain[depth], catalogNamespace), out LocalizationCatalog? catalog))
                {
                    continue;
                }

                foreach (LocalizationCatalogEntry entry in catalog.Entries)
                {
                    if (sourceSchemas.TryGetValue(entry.Key, out LocalizationCatalogEntry? sourceEntry) &&
                        (!SchemasMatch(sourceEntry.Arguments, entry.Arguments) ||
                         catalog.Locale.Tag != "qps-keyecho" &&
                         MessageProgramCodec.GetStructureSignature(sourceEntry.Program) !=
                         MessageProgramCodec.GetStructureSignature(entry.Program)))
                    {
                        return RecordStage(LocalizationStatus.Conflict, requestedLocale,
                            $"Argument schema or selection structure for '{entry.Key.Name}' differs from the source locale.");
                    }

                    if (!PinnedLocaleData.TryGetNumberProfile(catalog.Locale, out NumberProfile? numberProfile) ||
                        numberProfile is null)
                    {
                        return RecordStage(LocalizationStatus.NotSupported, requestedLocale,
                            $"Locale '{catalog.Locale.Tag}' has no pinned Phase 2 profile.");
                    }

                    LocalizationLanguageProfile languageProfile = CreateLanguageProfile(
                        catalog.Locale, catalog.Direction, numberProfile);
                    resolvedEntries[entry.Key] = new ResolvedEntry(
                        entry,
                        catalog.Locale,
                        catalog.Direction,
                        numberProfile,
                        languageProfile,
                        depth);
                }
            }
        }

        foreach (ResolvedEntry resolvedEntry in resolvedEntries.Values)
        {
            if (resolvedEntry.CatalogEntry.StaticMessage is { } staticMessage)
            {
                int bytes = Encoding.UTF8.GetByteCount(staticMessage);
                if (bytes > LocalizationLimits.MaximumStaticCacheBytes - staticCacheBytes)
                {
                    return RecordStage(LocalizationStatus.OutOfResource, requestedLocale,
                        "Static-message cache exceeds its generation byte limit.");
                }

                staticCacheBytes += bytes;
            }
        }

        foreach (string requiredNamespace in config.RequiredNamespaces ?? [])
        {
            LocalizationCatalog sourceCatalog = byCatalog[(sourceLocale, requiredNamespace)];
            foreach (LocalizationCatalogEntry sourceEntry in sourceCatalog.Entries)
            {
                if (!resolvedEntries.ContainsKey(sourceEntry.Key))
                {
                    return RecordStage(LocalizationStatus.ItemNotFound, requestedLocale,
                        $"Required key '{sourceEntry.Key.Name}' cannot resolve through the fallback chain.");
                }
            }
        }

        if (!PinnedLocaleData.TryGetNumberProfile(requestedLocale, out NumberProfile? requestedNumberProfile) ||
            requestedNumberProfile is null)
        {
            return RecordStage(LocalizationStatus.NotSupported, requestedLocale,
                $"Requested locale '{requestedLocale.Tag}' has no pinned Phase 2 profile.");
        }

        generation = new LocaleGeneration(
            ownerToken,
            nextGeneration++,
            requestedLocale,
            requestedManifest.Direction,
            CreateLanguageProfile(requestedLocale, requestedManifest.Direction, requestedNumberProfile),
            resolvedEntries);
        return RecordStage(LocalizationStatus.Success, requestedLocale,
            $"Staged {resolvedEntries.Count} validated message programs.", generation.Sequence);
    }

    public LocalizationStatus PublishLocale(LocaleGeneration generation)
    {
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus != LocalizationStatus.Success)
        {
            return ownerStatus;
        }

        if (generation is null || generation.Owner != ownerToken)
        {
            return LocalizationStatus.InvalidArgument;
        }

        Volatile.Write(ref published, generation);
        formatsThisFrame = 0;
        AddDiagnostic(LocalizationStatus.Success, "publish", generation.RequestedLocale, null, null, 0,
            generation.Sequence, 0, null, null, "Published complete locale generation atomically.");
        return LocalizationStatus.Success;
    }

    public LocalizationStatus GetStatic(LocalizationKey key, out LocalizedMessage? message)
    {
        message = null;
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus != LocalizationStatus.Success)
        {
            return ownerStatus;
        }

        LocaleGeneration? current = Volatile.Read(ref published);
        if (current is null)
        {
            return LocalizationStatus.NotInitialized;
        }

        if (key.Name is null)
        {
            return LocalizationStatus.InvalidArgument;
        }

        if (current.TryGet(key, out ResolvedEntry? entry) && entry is not null)
        {
            if (entry.CatalogEntry.StaticMessage is not { } staticMessage)
            {
                AddDiagnostic(LocalizationStatus.InvalidArgument, "lookup", current.RequestedLocale,
                    entry.Locale, key, entry.FallbackDepth, current.Sequence, 0, null,
                    DescribeArguments(entry.CatalogEntry.Arguments),
                    "GetStatic cannot resolve a message that declares arguments.");
                return LocalizationStatus.InvalidArgument;
            }

            message = new LocalizedMessage(
                staticMessage,
                key,
                current.RequestedLocale,
                entry.Locale,
                entry.Direction,
                current.Sequence,
                entry.LanguageProfile);
            AddDiagnostic(LocalizationStatus.Success, "lookup", current.RequestedLocale, entry.Locale, key,
                entry.FallbackDepth, current.Sequence, Encoding.UTF8.GetByteCount(staticMessage), null, null,
                entry.FallbackDepth == 0 ? "Resolved in selected locale." : "Resolved through explicit fallback.");
            return LocalizationStatus.Success;
        }

        if (config!.MissingKeyPolicy == MissingKeyPolicy.DevelopmentMarker)
        {
            string marker = $"[missing:{key.Name}]";
            message = new LocalizedMessage(marker, key, current.RequestedLocale, current.RequestedLocale,
                current.Direction, current.Sequence, current.LanguageProfile);
            AddDiagnostic(LocalizationStatus.ItemNotFound, "lookup", current.RequestedLocale, null, key, -1,
                current.Sequence, Encoding.UTF8.GetByteCount(marker), null, null, "Returned development missing-key marker.");
            return LocalizationStatus.ItemNotFound;
        }

        AddDiagnostic(LocalizationStatus.ItemNotFound, "lookup", current.RequestedLocale, null, key, -1,
            current.Sequence, 0, null, null, "Key is absent from the complete fallback chain.");
        return LocalizationStatus.ItemNotFound;
    }

    public LocalizationStatus Format(
        LocalizationKey key,
        IReadOnlyList<LocalizationArgument> arguments,
        out LocalizedMessage? message)
    {
        message = null;
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus != LocalizationStatus.Success)
        {
            return ownerStatus;
        }

        ArgumentNullException.ThrowIfNull(arguments);
        LocaleGeneration? current = Volatile.Read(ref published);
        if (current is null)
        {
            return LocalizationStatus.NotInitialized;
        }

        if (key.Name is null || arguments.Count > LocalizationLimits.MaximumArgumentsPerMessage)
        {
            return LocalizationStatus.InvalidArgument;
        }

        if (formatsThisFrame >= config!.MaximumFormatsPerFrame)
        {
            AddDiagnostic(LocalizationStatus.OutOfResource, "format", current.RequestedLocale, null,
                key, -1, current.Sequence, 0, null, null, "Per-frame formatting budget is exhausted.");
            return LocalizationStatus.OutOfResource;
        }

        ++formatsThisFrame;
        if (!current.TryGet(key, out ResolvedEntry? entry) || entry is null)
        {
            return CreateMissingMessage(current, key, out message, "format");
        }

        LocalizationStatus status = MessageFormatter.Format(
            entry,
            arguments,
            nestedKey => current.TryGet(nestedKey, out ResolvedEntry? nested) ? nested : null,
            out string text,
            out string? pluralCategory,
            out string error);
        if (status != LocalizationStatus.Success)
        {
            if (config.MissingKeyPolicy == MissingKeyPolicy.DevelopmentMarker)
            {
                string marker = $"[format:{key.Name}]";
                message = new LocalizedMessage(marker, key, current.RequestedLocale, entry.Locale,
                    entry.Direction, current.Sequence, entry.LanguageProfile);
            }

            AddDiagnostic(status, "format", current.RequestedLocale, entry.Locale, key,
                entry.FallbackDepth, current.Sequence, 0, pluralCategory,
                DescribeArguments(entry.CatalogEntry.Arguments), error);
            return status;
        }

        message = new LocalizedMessage(text, key, current.RequestedLocale, entry.Locale,
            entry.Direction, current.Sequence, entry.LanguageProfile);
        AddDiagnostic(LocalizationStatus.Success, "format", current.RequestedLocale, entry.Locale, key,
            entry.FallbackDepth, current.Sequence, Encoding.UTF8.GetByteCount(text), pluralCategory,
            DescribeArguments(entry.CatalogEntry.Arguments),
            entry.FallbackDepth == 0 ? "Formatted in selected locale." : "Formatted through explicit fallback.");
        return LocalizationStatus.Success;
    }

    public LocalizationStatus BeginFormattingFrame()
    {
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus == LocalizationStatus.Success)
        {
            formatsThisFrame = 0;
        }

        return ownerStatus;
    }

    public LocaleSnapshot GetLocaleSnapshot()
    {
        if (CheckOwner() != LocalizationStatus.Success)
        {
            return new LocaleSnapshot(false, default, default, 0);
        }

        LocaleGeneration? current = Volatile.Read(ref published);
        return current is null
            ? new LocaleSnapshot(false, default, default, 0)
            : new LocaleSnapshot(true, current.RequestedLocale, current.Direction, current.Sequence);
    }

    public LocalizationDiagnostics GetDiagnostics()
    {
        if (CheckOwner() != LocalizationStatus.Success)
        {
            return new LocalizationDiagnostics([], 0);
        }

        return new LocalizationDiagnostics(
            new ReadOnlyCollection<LocalizationDiagnostic>(diagnostics.ToArray()),
            droppedDiagnostics);
    }

    public LocalizationStatus Shutdown()
    {
        LocalizationStatus ownerStatus = CheckOwner();
        if (ownerStatus != LocalizationStatus.Success)
        {
            return ownerStatus;
        }

        Volatile.Write(ref published, null);
        diagnostics.Clear();
        config = null;
        sourceLocale = default;
        ownerToken = default;
        ownerThreadId = 0;
        nextGeneration = 1;
        diagnosticSequence = 0;
        droppedDiagnostics = 0;
        formatsThisFrame = 0;
        return LocalizationStatus.Success;
    }

    private LocalizationStatus CheckOwner()
    {
        if (config is null)
        {
            return LocalizationStatus.NotInitialized;
        }

        return Environment.CurrentManagedThreadId == ownerThreadId
            ? LocalizationStatus.Success
            : LocalizationStatus.WrongThread;
    }

    private bool ValidateFallbackChain(
        LocaleId requestedLocale,
        IReadOnlyList<LocaleId> fallbacks,
        IReadOnlyDictionary<LocaleId, LocaleManifest> manifests,
        out string error)
    {
        if (requestedLocale == sourceLocale)
        {
            if (fallbacks.Count != 0)
            {
                error = "Source locale must not have fallbacks.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (fallbacks.Count == 0 || fallbacks.Count > LocalizationLimits.MaximumFallbackDepth ||
            fallbacks[^1] != sourceLocale)
        {
            error = "Fallback chain must be bounded and end in the configured source locale.";
            return false;
        }

        HashSet<LocaleId> seen = [requestedLocale];
        for (int index = 0; index < fallbacks.Count; ++index)
        {
            LocaleId fallback = fallbacks[index];
            if (!seen.Add(fallback) || !manifests.ContainsKey(fallback))
            {
                error = "Fallback chain contains a cycle/duplicate or references an unavailable locale.";
                return false;
            }

            IReadOnlyList<LocaleId> declaredRemainder = manifests[fallback].Fallbacks;
            IReadOnlyList<LocaleId> expectedRemainder = fallbacks.Skip(index + 1).ToArray();
            if (!declaredRemainder.SequenceEqual(expectedRemainder))
            {
                error = $"Fallback metadata for '{fallback.Tag}' is inconsistent with the selected chain.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private LocalizationStatus RecordStage(
        LocalizationStatus status,
        LocaleId requestedLocale,
        string detail,
        ulong generation = 0)
    {
        AddDiagnostic(status, "stage", requestedLocale, null, null, 0, generation, 0, null, null, detail);
        return status;
    }

    private LocalizationStatus CreateMissingMessage(
        LocaleGeneration generation,
        LocalizationKey key,
        out LocalizedMessage? message,
        string operation)
    {
        message = null;
        if (config!.MissingKeyPolicy == MissingKeyPolicy.DevelopmentMarker)
        {
            string marker = $"[missing:{key.Name}]";
            message = new LocalizedMessage(marker, key, generation.RequestedLocale,
                generation.RequestedLocale, generation.Direction, generation.Sequence, generation.LanguageProfile);
        }

        AddDiagnostic(LocalizationStatus.ItemNotFound, operation, generation.RequestedLocale, null, key,
            -1, generation.Sequence, 0, null, null, "Key is absent from the complete fallback chain.");
        return LocalizationStatus.ItemNotFound;
    }

    private static bool SchemasMatch(
        IReadOnlyList<LocalizationArgumentSchema> left,
        IReadOnlyList<LocalizationArgumentSchema> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; ++index)
        {
            LocalizationArgumentSchema first = left[index];
            LocalizationArgumentSchema second = right[index];
            if (first.Name != second.Name || first.NameId != second.NameId || first.Kind != second.Kind ||
                first.DecimalScale != second.DecimalScale || first.Sensitive != second.Sensitive ||
                !first.SelectValues.SequenceEqual(second.SelectValues, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static LocalizationLanguageProfile CreateLanguageProfile(
        LocaleId locale,
        TextDirection direction,
        NumberProfile numberProfile) =>
        new(locale.Tag, direction, numberProfile.DecimalSeparator, numberProfile.GroupSeparator,
            numberProfile.PercentSign, PinnedLocaleData.Version, PinnedLocaleData.TableHash);

    private static string DescribeArguments(IReadOnlyList<LocalizationArgumentSchema> arguments) =>
        string.Join(',', arguments.Select(argument => $"{argument.Name}:{argument.Kind}"));

    private void AddDiagnostic(
        LocalizationStatus status,
        string operation,
        LocaleId requestedLocale,
        LocaleId? resolvedLocale,
        LocalizationKey? key,
        int fallbackDepth,
        ulong generation,
        int outputBytes,
        string? pluralCategory,
        string? argumentSchema,
        string detail)
    {
        int capacity = config?.DiagnosticCapacity ?? 0;
        if (capacity == 0)
        {
            ++droppedDiagnostics;
            return;
        }

        while (diagnostics.Count >= capacity)
        {
            diagnostics.Dequeue();
            ++droppedDiagnostics;
        }

        diagnostics.Enqueue(new LocalizationDiagnostic(
            ++diagnosticSequence,
            status,
            operation,
            requestedLocale.Tag,
            resolvedLocale?.Tag,
            key?.Name,
            key?.Value ?? 0,
            fallbackDepth,
            generation,
            outputBytes,
            pluralCategory,
            PinnedLocaleData.Version,
            PinnedLocaleData.TableHash,
            argumentSchema,
            detail));
    }

    private sealed class LocaleManifest
    {
        public LocaleManifest(IReadOnlyList<LocaleId> fallbacks, TextDirection direction)
        {
            Fallbacks = fallbacks;
            Direction = direction;
        }

        public IReadOnlyList<LocaleId> Fallbacks { get; }

        public TextDirection Direction { get; }

        public bool Matches(LocaleManifest? other) =>
            other is not null && Direction == other.Direction && Fallbacks.SequenceEqual(other.Fallbacks);
    }
}
