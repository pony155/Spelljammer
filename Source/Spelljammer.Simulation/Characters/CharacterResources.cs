using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Defines and updates character health, stamina, mana, resolve, strain, and other data-driven resources.
/// </summary>
/// <remarks>
/// Code flow: A resource profile creates bounded state, permanent and temporary modifiers derive limits and recovery, and spend, restore, damage, healing, or rest return replacement resource sets.
/// </remarks>
public sealed record CharacterResourceModifier(ContentId SourceId, int MaximumDelta, int RecoveryRateDelta);

public sealed record CharacterResourceState(
    ResourceId ResourceId,
    int CurrentValue,
    int BaseMaximum,
    int BaseRecoveryRate,
    bool Accumulates,
    ImmutableArray<CharacterResourceModifier> PermanentModifiers,
    ImmutableArray<CharacterResourceModifier> TemporaryModifiers)
{
    public int Maximum => Math.Max(0, BaseMaximum + PermanentModifiers.Sum(value => value.MaximumDelta) +
        TemporaryModifiers.Sum(value => value.MaximumDelta));

    public int RecoveryRate => Math.Max(0, BaseRecoveryRate + PermanentModifiers.Sum(value => value.RecoveryRateDelta) +
        TemporaryModifiers.Sum(value => value.RecoveryRateDelta));

    public CharacterResourceState Clamp() => this with { CurrentValue = Math.Clamp(CurrentValue, 0, Maximum) };
}

public sealed class CharacterResourceSet
{
    public const int MaximumModifiersPerResource = 128;
    private readonly ImmutableDictionary<ResourceId, CharacterResourceState> resources;

    private CharacterResourceSet(ImmutableDictionary<ResourceId, CharacterResourceState> resources) => this.resources = resources;

    public static CharacterResourceSet Empty { get; } = new(ImmutableDictionary<ResourceId, CharacterResourceState>.Empty);

    public IEnumerable<CharacterResourceState> Values => resources.Values;

    public static CharacterResourceSet Create(CharacterResourceProfileDefinition profile) => new(
        profile.Resources.ToImmutableDictionary(
            rule => rule.ResourceId,
            rule => new CharacterResourceState(
                rule.ResourceId,
                rule.Accumulates ? 0 : rule.BaseMaximum,
                rule.BaseMaximum,
                rule.BaseRecoveryRate,
                rule.Accumulates,
                [],
                [])));

    public static CharacterResourceSet Restore(IEnumerable<CharacterResourceState> values)
    {
        ImmutableDictionary<ResourceId, CharacterResourceState> restored = values
            .Select(value => value.Clamp())
            .ToImmutableDictionary(value => value.ResourceId);
        return new CharacterResourceSet(restored);
    }

    public bool TryGet(ResourceId id, out CharacterResourceState? value) => resources.TryGetValue(id, out value);

    public int GetCurrentValue(ResourceId id) => resources.TryGetValue(id, out CharacterResourceState? value)
        ? value.CurrentValue
        : throw new KeyNotFoundException($"Character resource '{id}' is not present.");

    public CharacterResourceSet WithCurrentValue(ResourceId id, int value)
    {
        if (!resources.TryGetValue(id, out CharacterResourceState? resource))
        {
            throw new KeyNotFoundException($"Character resource '{id}' is not present.");
        }

        return With((resource with { CurrentValue = value }).Clamp());
    }

    public CharacterResourceSet ClampResource(ResourceId id)
    {
        CharacterResourceState value = resources.TryGetValue(id, out CharacterResourceState? found)
            ? found
            : throw new KeyNotFoundException($"Character resource '{id}' is not present.");
        return With(value.Clamp());
    }

    public CharacterResourceSet WithModifier(ResourceId id, CharacterResourceModifier modifier, bool temporary)
    {
        CharacterResourceState value = resources.TryGetValue(id, out CharacterResourceState? found)
            ? found
            : throw new KeyNotFoundException($"Character resource '{id}' is not present.");
        ImmutableArray<CharacterResourceModifier> selected = temporary ? value.TemporaryModifiers : value.PermanentModifiers;
        if (!modifier.SourceId.IsValid || selected.Length >= MaximumModifiersPerResource ||
            selected.Any(existing => existing.SourceId == modifier.SourceId))
        {
            throw new InvalidOperationException("Character resource modifier is invalid, duplicated, or exceeds capacity.");
        }

        return With(temporary
            ? value with { TemporaryModifiers = selected.Add(modifier) }
            : value with { PermanentModifiers = selected.Add(modifier) });
    }

    public CharacterResourceSet WithoutModifier(ContentId sourceId)
    {
        ImmutableDictionary<ResourceId, CharacterResourceState>.Builder changed = resources.ToBuilder();
        foreach (CharacterResourceState value in resources.Values)
        {
            changed[value.ResourceId] = (value with
            {
                PermanentModifiers = [.. value.PermanentModifiers.Where(modifier => modifier.SourceId != sourceId)],
                TemporaryModifiers = [.. value.TemporaryModifiers.Where(modifier => modifier.SourceId != sourceId)],
            }).Clamp();
        }

        return new CharacterResourceSet(changed.ToImmutable());
    }

    public bool CanSpendResource(ResourceId id, int amount) =>
        amount >= 0 && resources.TryGetValue(id, out CharacterResourceState? value) && !value.Accumulates &&
        value.CurrentValue >= amount;

    public CharacterResourceSet SpendResource(ResourceId id, int amount)
    {
        if (!CanSpendResource(id, amount))
        {
            throw new InvalidOperationException("The character cannot spend the requested resource amount.");
        }

        CharacterResourceState value = resources[id];
        return With(value with { CurrentValue = value.CurrentValue - amount });
    }

    public CharacterResourceSet RestoreResource(ResourceId id, int amount) =>
        AdjustDepleting(id, RequireNonnegative(amount));

    public CharacterResourceSet ApplyResourceDamage(ResourceId id, int amount) =>
        AdjustDepleting(id, -RequireNonnegative(amount));

    public CharacterResourceSet GenerateStrain(int amount) =>
        AdjustAccumulating(CharacterResourceIds.Strain, RequireNonnegative(amount));

    public CharacterResourceSet ReduceStrain(int amount) =>
        AdjustAccumulating(CharacterResourceIds.Strain, -RequireNonnegative(amount));

    public int GetStrainPercentage() => GetResourcePercentage(CharacterResourceIds.Strain);

    public int GetStrainThresholdState(CharacterResourceProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        CharacterResourceRule rule = profile.Resources.Single(value => value.ResourceId == CharacterResourceIds.Strain);
        return GetThresholdIndex(rule);
    }

    public bool CheckPsionicOverload()
    {
        CharacterResourceState value = Require(CharacterResourceIds.Strain, true);
        return value.CurrentValue >= value.Maximum;
    }

    public int GetResourcePercentage(ResourceId id)
    {
        CharacterResourceState value = resources.TryGetValue(id, out CharacterResourceState? found)
            ? found
            : throw new KeyNotFoundException($"Character resource '{id}' is not present.");
        return value.Maximum == 0 ? 0 : (int)((long)value.CurrentValue * 100 / value.Maximum);
    }

    public int GetThresholdIndex(CharacterResourceRule rule)
    {
        int percentage = GetResourcePercentage(rule.ResourceId);
        int matched = -1;
        for (int index = 0; index < rule.ThresholdPercentages.Length; index++)
        {
            bool crossed = rule.Accumulates
                ? percentage >= rule.ThresholdPercentages[index]
                : percentage <= rule.ThresholdPercentages[index];
            if (crossed)
            {
                matched = index;
            }
        }

        return matched;
    }

    public CharacterResourceSet RecoverOneTick()
    {
        ImmutableDictionary<ResourceId, CharacterResourceState>.Builder changed = resources.ToBuilder();
        foreach (CharacterResourceState value in resources.Values)
        {
            int delta = value.Accumulates ? -value.RecoveryRate : value.RecoveryRate;
            changed[value.ResourceId] = (value with { CurrentValue = value.CurrentValue + delta }).Clamp();
        }

        return new CharacterResourceSet(changed.ToImmutable());
    }

    public void Validate(CharacterResourceProfileDefinition profile)
    {
        if (resources.Count != profile.Resources.Length || resources.Values.Any(value =>
                !value.ResourceId.IsValid || value.BaseMaximum < 0 || value.BaseRecoveryRate < 0 ||
                value.CurrentValue < 0 || value.CurrentValue > value.Maximum ||
                value.PermanentModifiers.Length > MaximumModifiersPerResource ||
                value.TemporaryModifiers.Length > MaximumModifiersPerResource ||
                value.PermanentModifiers.Concat(value.TemporaryModifiers).Any(modifier => !modifier.SourceId.IsValid) ||
                value.PermanentModifiers.Concat(value.TemporaryModifiers).Select(modifier => modifier.SourceId).Distinct().Count() !=
                    value.PermanentModifiers.Length + value.TemporaryModifiers.Length) ||
            profile.Resources.Any(rule => !resources.TryGetValue(rule.ResourceId, out CharacterResourceState? value) ||
                value.BaseMaximum != rule.BaseMaximum || value.BaseRecoveryRate != rule.BaseRecoveryRate ||
                value.Accumulates != rule.Accumulates))
        {
            throw new InvalidOperationException("Character resource state is incompatible with its profile.");
        }
    }

    private CharacterResourceSet AdjustDepleting(ResourceId id, int delta)
    {
        CharacterResourceState value = Require(id, false);
        return With((value with { CurrentValue = value.CurrentValue + delta }).Clamp());
    }

    private CharacterResourceSet AdjustAccumulating(ResourceId id, int delta)
    {
        CharacterResourceState value = Require(id, true);
        return With((value with { CurrentValue = value.CurrentValue + delta }).Clamp());
    }

    private CharacterResourceState Require(ResourceId id, bool accumulates)
    {
        if (!resources.TryGetValue(id, out CharacterResourceState? value) || value.Accumulates != accumulates)
        {
            throw new InvalidOperationException("The requested character resource has incompatible behavior.");
        }

        return value;
    }

    private CharacterResourceSet With(CharacterResourceState value) => new(resources.SetItem(value.ResourceId, value.Clamp()));

    private static int RequireNonnegative(int amount) => amount >= 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount));
}

public static class CharacterResourceIds
{
    public static ResourceId Health { get; } = new("resource.health");
    public static ResourceId Stamina { get; } = new("resource.stamina");
    public static ResourceId Mana { get; } = new("resource.mana");
    public static ResourceId Resolve { get; } = new("resource.resolve");
    public static ResourceId Strain { get; } = new("resource.strain");
}
