using System.Collections.Frozen;
using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

public readonly record struct ScopedContentIndex<TId>
    where TId : struct
{
    internal ScopedContentIndex(ContentFingerprint fingerprint, int value)
    {
        Fingerprint = fingerprint;
        Value = value;
    }

    public ContentFingerprint Fingerprint { get; }

    public int Value { get; }

    public bool IsValid => Fingerprint.IsValid && Value >= 0;
}

public sealed class ContentIndexFingerprintMismatchException : InvalidOperationException
{
    public ContentIndexFingerprintMismatchException()
        : base("The dense content index belongs to a different content fingerprint.")
    {
    }
}

public sealed class TypedDefinitionRegistry<TId, TDefinition>
    where TId : struct
    where TDefinition : class
{
    private readonly ImmutableArray<TDefinition> definitions;
    private readonly FrozenDictionary<TId, int> indices;

    internal TypedDefinitionRegistry(
        ContentFingerprint fingerprint,
        ImmutableArray<TDefinition> definitions,
        Func<TDefinition, TId> selectId)
    {
        Fingerprint = fingerprint;
        this.definitions = definitions;
        indices = definitions
            .Select((definition, index) => KeyValuePair.Create(selectId(definition), index))
            .ToFrozenDictionary();
    }

    public ContentFingerprint Fingerprint { get; }

    public int Count => definitions.Length;

    public ImmutableArray<TDefinition> Definitions => definitions;

    public bool TryGet(TId id, out TDefinition? definition)
    {
        if (indices.TryGetValue(id, out int index))
        {
            definition = definitions[index];
            return true;
        }

        definition = null;
        return false;
    }

    public bool TryGetIndex(TId id, out ScopedContentIndex<TId> index)
    {
        if (indices.TryGetValue(id, out int value))
        {
            index = new ScopedContentIndex<TId>(Fingerprint, value);
            return true;
        }

        index = default;
        return false;
    }

    public TDefinition Resolve(ScopedContentIndex<TId> index)
    {
        if (index.Fingerprint != Fingerprint)
        {
            throw new ContentIndexFingerprintMismatchException();
        }

        if ((uint)index.Value >= (uint)definitions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return definitions[index.Value];
    }
}
