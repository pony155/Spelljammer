using System.Diagnostics.CodeAnalysis;

namespace Spelljammer.Simulation.Content;

public readonly record struct ContentId : IComparable<ContentId>
{
    public const int MaximumLength = 127;

    public ContentId(string value)
    {
        if (!IsCanonical(value))
        {
            throw new ArgumentException("Content ID does not use the canonical lowercase ASCII grammar.", nameof(value));
        }

        Value = value;
    }

    public string? Value { get; }

    public bool IsValid => Value is not null;

    public int CompareTo(ContentId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value ?? string.Empty;

    public static bool TryParse(string? value, out ContentId id)
    {
        if (IsCanonical(value))
        {
            id = new ContentId(value);
            return true;
        }

        id = default;
        return false;
    }

    public static bool IsCanonical([NotNullWhen(true)] string? value)
    {
        if (value is null || value.Length is < 3 or > MaximumLength)
        {
            return false;
        }

        int segments = 1;
        bool segmentStart = true;
        bool afterHyphen = false;
        foreach (char character in value)
        {
            if (character == '.')
            {
                if (segmentStart || afterHyphen || ++segments > 8)
                {
                    return false;
                }

                segmentStart = true;
                afterHyphen = false;
                continue;
            }

            if (segmentStart)
            {
                if (character is < 'a' or > 'z')
                {
                    return false;
                }

                segmentStart = false;
                continue;
            }

            if (character == '-')
            {
                if (afterHyphen)
                {
                    return false;
                }

                afterHyphen = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z' or >= '0' and <= '9'))
            {
                return false;
            }

            afterHyphen = false;
        }

        return segments >= 2 && !segmentStart && !afterHyphen;
    }
}

public readonly record struct AttributeId : IComparable<AttributeId>
{
    public AttributeId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "attribute.");
    public AttributeId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(AttributeId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out AttributeId id) => TypedContentId.TryParse(value, "attribute.", out id);
}

public readonly record struct SkillId : IComparable<SkillId>
{
    public SkillId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "skill.");
    public SkillId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(SkillId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out SkillId id) => TypedContentId.TryParse(value, "skill.", out id);
}

public readonly record struct AccessId : IComparable<AccessId>
{
    public AccessId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "access.");
    public AccessId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(AccessId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out AccessId id) => TypedContentId.TryParse(value, "access.", out id);
}

public readonly record struct FeatId : IComparable<FeatId>
{
    public FeatId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "feat.");
    public FeatId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(FeatId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out FeatId id) => TypedContentId.TryParse(value, "feat.", out id);
}

public readonly record struct PerkId : IComparable<PerkId>
{
    public PerkId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "perk.");
    public PerkId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(PerkId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out PerkId id) => TypedContentId.TryParse(value, "perk.", out id);
}

public readonly record struct RaceId : IComparable<RaceId>
{
    public RaceId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "race.");
    public RaceId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(RaceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out RaceId id) => TypedContentId.TryParse(value, "race.", out id);
}

public readonly record struct CharacterId : IComparable<CharacterId>
{
    public CharacterId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "character.");
    public CharacterId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(CharacterId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out CharacterId id) => TypedContentId.TryParse(value, "character.", out id);
}

public readonly record struct HeritageId : IComparable<HeritageId>
{
    public HeritageId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "heritage.");
    public HeritageId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(HeritageId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out HeritageId id) => TypedContentId.TryParse(value, "heritage.", out id);
}

public readonly record struct BackgroundId : IComparable<BackgroundId>
{
    public BackgroundId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "background.");
    public BackgroundId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(BackgroundId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out BackgroundId id) => TypedContentId.TryParse(value, "background.", out id);
}

public readonly record struct TrainingProjectId : IComparable<TrainingProjectId>
{
    public TrainingProjectId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "training.");
    public TrainingProjectId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(TrainingProjectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out TrainingProjectId id) => TypedContentId.TryParse(value, "training.", out id);
}

public readonly record struct TechniqueId : IComparable<TechniqueId>
{
    public TechniqueId(ContentId value) => Value = HasTechniquePrefix(value)
        ? value
        : throw new ArgumentException("Technique ID must use the technique, spell, or psychic domain.", nameof(value));
    public TechniqueId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(TechniqueId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out TechniqueId id)
    {
        if (ContentId.TryParse(value, out ContentId parsed) && HasTechniquePrefix(parsed))
        {
            id = new TechniqueId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    private static bool HasTechniquePrefix(ContentId value) => value.IsValid &&
        (value.ToString().StartsWith("technique.", StringComparison.Ordinal) ||
         value.ToString().StartsWith("spell.", StringComparison.Ordinal) ||
         value.ToString().StartsWith("psychic.", StringComparison.Ordinal));
}

public readonly record struct SpellId : IComparable<SpellId>
{
    public SpellId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "spell.");
    public SpellId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(SpellId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out SpellId id) => TypedContentId.TryParse(value, "spell.", out id);
}

public readonly record struct PsychicTechniqueId : IComparable<PsychicTechniqueId>
{
    public PsychicTechniqueId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "psychic.");
    public PsychicTechniqueId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(PsychicTechniqueId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out PsychicTechniqueId id) => TypedContentId.TryParse(value, "psychic.", out id);
}

public readonly record struct ScenarioId : IComparable<ScenarioId>
{
    public ScenarioId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "scenario.");
    public ScenarioId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ScenarioId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ActionId : IComparable<ActionId>
{
    public ActionId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "action.");
    public ActionId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ActionId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ResourceId : IComparable<ResourceId>
{
    public ResourceId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "resource.");
    public ResourceId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ResourceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ActorId : IComparable<ActorId>
{
    public ActorId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "actor.");
    public ActorId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ActorId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct TeamId : IComparable<TeamId>
{
    public TeamId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "team.");
    public TeamId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(TeamId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SpaceObjectId : IComparable<SpaceObjectId>
{
    public SpaceObjectId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "space-object.");
    public SpaceObjectId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(SpaceObjectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct PersonalBoardId : IComparable<PersonalBoardId>
{
    public PersonalBoardId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "board.");
    public PersonalBoardId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(PersonalBoardId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct CellId : IComparable<CellId>
{
    public CellId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "cell.");
    public CellId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(CellId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ZoneId : IComparable<ZoneId>
{
    public ZoneId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "zone.");
    public ZoneId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ZoneId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct LinkId : IComparable<LinkId>
{
    public LinkId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "link.");
    public LinkId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(LinkId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct EncounterId : IComparable<EncounterId>
{
    public EncounterId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "encounter.");
    public EncounterId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EncounterId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ObjectiveId : IComparable<ObjectiveId>
{
    public ObjectiveId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "objective.");
    public ObjectiveId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ObjectiveId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct EquipmentId : IComparable<EquipmentId>
{
    public EquipmentId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "equipment.");
    public EquipmentId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EquipmentId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ShipId : IComparable<ShipId>
{
    public ShipId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "ship.");
    public ShipId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ShipFrameId : IComparable<ShipFrameId>
{
    public ShipFrameId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "frame.");
    public ShipFrameId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipFrameId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ModuleId : IComparable<ModuleId>
{
    public ModuleId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "module.");
    public ModuleId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ModuleId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ShipWeaponConfigurationId : IComparable<ShipWeaponConfigurationId>
{
    public ShipWeaponConfigurationId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "ship.weapon.");
    public ShipWeaponConfigurationId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipWeaponConfigurationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct NetworkId : IComparable<NetworkId>
{
    public NetworkId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "network.");
    public NetworkId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(NetworkId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct StationId : IComparable<StationId>
{
    public StationId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "station.");
    public StationId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(StationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct CompartmentId : IComparable<CompartmentId>
{
    public CompartmentId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "compartment.");
    public CompartmentId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(CompartmentId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ModuleInstanceId : IComparable<ModuleInstanceId>
{
    public ModuleInstanceId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "module-instance.");
    public ModuleInstanceId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ModuleInstanceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct EventId : IComparable<EventId>
{
    public EventId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "event.");
    public EventId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EventId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct EffectId : IComparable<EffectId>
{
    public EffectId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "effect.");
    public EffectId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EffectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ContentFingerprint
{
    public ContentFingerprint(string hexadecimal)
    {
        ArgumentNullException.ThrowIfNull(hexadecimal);
        if (hexadecimal.Length != 64 || hexadecimal.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("A content fingerprint must be a lowercase SHA-256 value.", nameof(hexadecimal));
        }

        Hexadecimal = hexadecimal;
    }

    public string? Hexadecimal { get; }
    public bool IsValid => Hexadecimal is not null;
    public override string ToString() => Hexadecimal ?? string.Empty;
}

file static class TypedContentId
{
    public static ContentId RequirePrefix(ContentId value, string prefix) =>
        value.IsValid && value.ToString().StartsWith(prefix, StringComparison.Ordinal)
            ? value
            : throw new ArgumentException($"Content ID must begin with '{prefix}'.", nameof(value));

    public static bool TryParse<T>(string? value, string prefix, out T id)
        where T : struct
    {
        if (ContentId.TryParse(value, out ContentId parsed) && parsed.ToString().StartsWith(prefix, StringComparison.Ordinal))
        {
            id = (T)Activator.CreateInstance(typeof(T), parsed)!;
            return true;
        }

        id = default;
        return false;
    }
}
