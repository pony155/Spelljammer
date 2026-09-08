using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Defines a character's deterministic turn meter, action points, costs, and modifiers.
/// </summary>
/// <remarks>
/// Code flow: Tick advancement derives turn-meter gain from resources and modifiers, reaching the threshold opens a turn, and accepted actions atomically spend configured action points.
/// </remarks>
public sealed record CharacterTurnModifier(
    ContentId SourceId,
    int TurnMeterGainPercentageDelta,
    int MaximumActionPointsDelta);

public sealed record CharacterTurnState(
    int CurrentTurnMeter,
    int TurnMeterThreshold,
    int BaseTurnMeterGain,
    int CurrentActionPoints,
    int BaseMaximumActionPoints,
    int NormalTurnMeterGainPercentage,
    ImmutableArray<StaminaTurnMeterRule> StaminaTurnMeterRules,
    ImmutableDictionary<ContentId, int> ActionPointCosts,
    ImmutableArray<CharacterTurnModifier> PermanentModifiers,
    ImmutableArray<CharacterTurnModifier> TemporaryModifiers)
{
    public const int MaximumModifiers = 128;

    public int MaximumActionPoints => Math.Max(0, BaseMaximumActionPoints +
        PermanentModifiers.Sum(value => value.MaximumActionPointsDelta) +
        TemporaryModifiers.Sum(value => value.MaximumActionPointsDelta));

    public int TurnMeterGainModifierPercentage => Math.Max(0, 100 +
        PermanentModifiers.Sum(value => value.TurnMeterGainPercentageDelta) +
        TemporaryModifiers.Sum(value => value.TurnMeterGainPercentageDelta));

    public bool CanActivate => CurrentTurnMeter >= TurnMeterThreshold;

    public static CharacterTurnState Create(CharacterTurnRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new CharacterTurnState(
            0,
            rules.TurnMeterThreshold,
            rules.BaseTurnMeterGain,
            0,
            rules.BaseActionPoints,
            rules.NormalTurnMeterGainPercentage,
            rules.StaminaTurnMeterRules,
            rules.ActionPointCosts,
            [],
            []);
    }

    public CharacterTurnState Clamp() => this with
    {
        CurrentTurnMeter = Math.Clamp(CurrentTurnMeter, 0, TurnMeterThreshold),
        CurrentActionPoints = Math.Clamp(CurrentActionPoints, 0, MaximumActionPoints),
    };

    public int GetTurnMeterGain(int staminaPercentage)
    {
        int boundedStamina = Math.Clamp(staminaPercentage, 0, 100);
        int staminaGainPercentage = NormalTurnMeterGainPercentage;
        foreach (StaminaTurnMeterRule rule in StaminaTurnMeterRules)
        {
            if (boundedStamina <= rule.MaximumStaminaPercentage)
            {
                staminaGainPercentage = rule.TurnMeterGainPercentage;
            }
        }

        long scaled = (long)BaseTurnMeterGain * TurnMeterGainModifierPercentage * staminaGainPercentage;
        return (int)Math.Clamp(scaled / 10_000, 0, int.MaxValue);
    }

    public CharacterTurnState AddTurnMeter(int staminaPercentage)
    {
        int gain = GetTurnMeterGain(staminaPercentage);
        return (this with
        {
            CurrentTurnMeter = (int)Math.Min((long)TurnMeterThreshold, (long)CurrentTurnMeter + gain),
        }).Clamp();
    }

    public CharacterTurnState ResetTurnMeter() => (this with { CurrentTurnMeter = 0 }).Clamp();

    public CharacterTurnState BeginActivation()
    {
        if (!CanActivate)
        {
            throw new InvalidOperationException("The character is not ready to begin an activation.");
        }

        return (this with { CurrentTurnMeter = 0, CurrentActionPoints = MaximumActionPoints }).Clamp();
    }

    public CharacterTurnState EndActivation() => (this with { CurrentActionPoints = 0 }).Clamp();

    public bool CanSpendActionPoints(int amount) => amount >= 0 && CurrentActionPoints >= amount;

    public int GetRemainingActionPoints() => CurrentActionPoints;

    public CharacterTurnState SpendActionPoints(int amount)
    {
        if (!CanSpendActionPoints(amount))
        {
            throw new InvalidOperationException("The character cannot spend the requested Action Points.");
        }

        return this with { CurrentActionPoints = CurrentActionPoints - amount };
    }

    public CharacterTurnState RestoreActionPoints(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return (this with
        {
            CurrentActionPoints = (int)Math.Min((long)MaximumActionPoints, (long)CurrentActionPoints + amount),
        }).Clamp();
    }

    public int GetActionPointCost(ContentId actionId) => ActionPointCosts.TryGetValue(actionId, out int cost)
        ? cost
        : throw new KeyNotFoundException($"Action Point cost for '{actionId}' is not defined.");

    public CharacterTurnState WithModifier(CharacterTurnModifier modifier, bool temporary)
    {
        ImmutableArray<CharacterTurnModifier> selected = temporary ? TemporaryModifiers : PermanentModifiers;
        if (!modifier.SourceId.IsValid || selected.Length >= MaximumModifiers ||
            PermanentModifiers.Concat(TemporaryModifiers).Any(value => value.SourceId == modifier.SourceId))
        {
            throw new InvalidOperationException("Character turn modifier is invalid, duplicated, or exceeds capacity.");
        }

        return (temporary
            ? this with { TemporaryModifiers = selected.Add(modifier) }
            : this with { PermanentModifiers = selected.Add(modifier) }).Clamp();
    }

    public CharacterTurnState WithoutModifier(ContentId sourceId) => (this with
    {
        PermanentModifiers = [.. PermanentModifiers.Where(value => value.SourceId != sourceId)],
        TemporaryModifiers = [.. TemporaryModifiers.Where(value => value.SourceId != sourceId)],
    }).Clamp();

    public void Validate(CharacterTurnRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        bool invalidModifiers = PermanentModifiers.Length > MaximumModifiers || TemporaryModifiers.Length > MaximumModifiers ||
            PermanentModifiers.Concat(TemporaryModifiers).Any(value => !value.SourceId.IsValid) ||
            PermanentModifiers.Concat(TemporaryModifiers).Select(value => value.SourceId).Distinct().Count() !=
                PermanentModifiers.Length + TemporaryModifiers.Length;
        if (TurnMeterThreshold != rules.TurnMeterThreshold || BaseTurnMeterGain != rules.BaseTurnMeterGain ||
            BaseMaximumActionPoints != rules.BaseActionPoints ||
            NormalTurnMeterGainPercentage != rules.NormalTurnMeterGainPercentage ||
            !StaminaTurnMeterRules.SequenceEqual(rules.StaminaTurnMeterRules) ||
            !ActionPointCosts.OrderBy(value => value.Key).SequenceEqual(rules.ActionPointCosts.OrderBy(value => value.Key)) ||
            CurrentTurnMeter < 0 || CurrentTurnMeter > TurnMeterThreshold ||
            CurrentActionPoints < 0 || CurrentActionPoints > MaximumActionPoints || invalidModifiers)
        {
            throw new InvalidOperationException("Character turn state is incompatible with its profile.");
        }
    }
}
