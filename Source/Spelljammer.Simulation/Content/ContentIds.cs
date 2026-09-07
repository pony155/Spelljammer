using System.Diagnostics.CodeAnalysis;

namespace Spelljammer.Simulation.Content;

/// <summary>
/// A strongly-typed identifier for game content entities.
/// </summary>
/// <remarks>
/// Content IDs follow a strict canonical grammar: lowercase letters and digits separated by dots and hyphens,
/// with at least 3 characters and at most 127 characters. The format is: namespace.type-variant or similar.
/// This ensures IDs are human-readable, deterministic, and safe for serialization.
/// </remarks>
public readonly record struct ContentId : IComparable<ContentId>
{
    /// <summary>
    /// The maximum allowed length of a content ID string in characters.
    /// </summary>
    public const int MaximumLength = 127;

    /// <summary>
    /// Initializes a new content ID from a string value.
    /// </summary>
    /// <param name="value">The ID string, which must match the canonical lowercase ASCII grammar.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> does not conform to the canonical grammar.</exception>
    public ContentId(string value)
    {
        if (!IsCanonical(value))
        {
            throw new ArgumentException("Content ID does not use the canonical lowercase ASCII grammar.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the string value of this content ID, or null if the ID is invalid (default struct).
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets a value indicating whether this content ID is valid (not the default value).
    /// </summary>
    public bool IsValid => Value is not null;

    /// <summary>
    /// Compares this content ID with another using ordinal string comparison.
    /// </summary>
    /// <param name="other">The content ID to compare with.</param>
    /// <returns>A negative value if this ID is less than <paramref name="other"/>, zero if equal, positive if greater.</returns>
    public int CompareTo(ContentId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    /// <summary>
    /// Returns the string representation of this content ID.
    /// </summary>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Attempts to parse a string into a content ID without throwing exceptions.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="id">When this method returns true, contains the parsed content ID; otherwise, the default value.</param>
    /// <returns>True if the string was successfully parsed as a valid content ID; otherwise, false.</returns>
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

    /// <summary>
    /// Determines whether a string conforms to the canonical content ID grammar.
    /// </summary>
    /// <remarks>
    /// A canonical content ID must:
    /// - Be between 3 and 127 characters long (inclusive)
    /// - Contain 2 to 8 dot-separated segments
    /// - Start each segment with a lowercase letter (a-z)
    /// - Contain only lowercase letters, digits, and hyphens within segments
    /// - Not have hyphens adjacent to dots or other hyphens
    /// </remarks>
    /// <param name="value">The string to validate.</param>
    /// <returns>True if the string is a canonical content ID; otherwise, false.</returns>
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

/// <summary>
/// A strongly-typed identifier for a character attribute (e.g., Strength, Dexterity).
/// </summary>
/// <remarks>
/// Attribute IDs must have the "attribute." prefix. They are used to reference base character attributes
/// that affect skills, saving throws, and other mechanics.
/// </remarks>
public readonly record struct AttributeId : IComparable<AttributeId>
{
    /// <summary>
    /// Initializes an attribute ID from a content ID that must begin with "attribute.".
    /// </summary>
    /// <param name="value">A content ID with the "attribute." prefix.</param>
    /// <exception cref="ArgumentException">Thrown if the content ID does not have the required prefix.</exception>
    public AttributeId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "attribute.");

    /// <summary>
    /// Initializes an attribute ID from a string (which must be a valid content ID with the "attribute." prefix).
    /// </summary>
    public AttributeId(string value) : this(new ContentId(value)) { }

    /// <summary>
    /// Gets the underlying content ID.
    /// </summary>
    public ContentId Value { get; }

    /// <summary>
    /// Gets a value indicating whether this attribute ID is valid.
    /// </summary>
    public bool IsValid => Value.IsValid;

    /// <summary>
    /// Compares this attribute ID with another using ordinal content ID comparison.
    /// </summary>
    public int CompareTo(AttributeId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Returns the string representation of this attribute ID.
    /// </summary>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Attempts to parse a string into an attribute ID without throwing exceptions.
    /// </summary>
    public static bool TryParse(string? value, out AttributeId id) => TypedContentId.TryParse(value, "attribute.", out id);
}

/// <summary>
/// A strongly-typed identifier for a character skill.
/// </summary>
public readonly record struct SkillId : IComparable<SkillId>
{
    public SkillId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "skill.");
    public SkillId(string value) : this(new ContentId(value)) { }
    /// <summary>Gets the underlying content ID.</summary>
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(SkillId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out SkillId id) => TypedContentId.TryParse(value, "skill.", out id);
}

/// <summary>
/// A strongly-typed identifier for an access privilege or ability category.
/// </summary>
public readonly record struct AccessId : IComparable<AccessId>
{
    public AccessId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "access.");
    public AccessId(string value) : this(new ContentId(value)) { }
    /// <summary>Gets the underlying content ID.</summary>
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(AccessId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
    public static bool TryParse(string? value, out AccessId id) => TypedContentId.TryParse(value, "access.", out id);
}

/// <summary>A strongly-typed identifier for a feat (special ability or achievement).</summary>
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

/// <summary>A strongly-typed identifier for a perk (racial or class ability).</summary>
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

/// <summary>A strongly-typed identifier for a player character race.</summary>
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

/// <summary>A strongly-typed identifier for a character definition or instance.</summary>
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

/// <summary>A strongly-typed identifier for a character heritage (sub-race variant).</summary>
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

/// <summary>A strongly-typed identifier for a character background (origin story).</summary>
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

/// <summary>A strongly-typed identifier for a character training project.</summary>
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

/// <summary>A strongly-typed identifier for any technique (combat ability, spell, or psychic power).</summary>
/// <remarks>This is a union type that accepts technique., spell., or psychic. prefixes.</remarks>
public readonly record struct TechniqueId : IComparable<TechniqueId>
{
    /// <summary>Initializes a technique ID from a content ID with technique, spell, or psychic prefix.</summary>
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

/// <summary>A strongly-typed identifier for a magical spell.</summary>
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

/// <summary>A strongly-typed identifier for a psychic technique.</summary>
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

/// <summary>A strongly-typed identifier for an adventure scenario.</summary>
public readonly record struct ScenarioId : IComparable<ScenarioId>
{
    public ScenarioId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "scenario.");
    public ScenarioId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ScenarioId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a game action or ability.</summary>
public readonly record struct ActionId : IComparable<ActionId>
{
    public ActionId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "action.");
    public ActionId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ActionId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a game resource (fuel, ammo, health, etc.).</summary>
public readonly record struct ResourceId : IComparable<ResourceId>
{
    public ResourceId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "resource.");
    public ResourceId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ResourceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a game actor (NPC or entity).</summary>
public readonly record struct ActorId : IComparable<ActorId>
{
    public ActorId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "actor.");
    public ActorId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ActorId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a team (group of characters or units).</summary>
public readonly record struct TeamId : IComparable<TeamId>
{
    public TeamId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "team.");
    public TeamId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(TeamId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a space object (asteroid, debris, station).</summary>
public readonly record struct SpaceObjectId : IComparable<SpaceObjectId>
{
    public SpaceObjectId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "space-object.");
    public SpaceObjectId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(SpaceObjectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a personal combat board (encounter arena).</summary>
public readonly record struct PersonalBoardId : IComparable<PersonalBoardId>
{
    public PersonalBoardId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "board.");
    public PersonalBoardId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(PersonalBoardId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a combat board cell (hex or square).</summary>
public readonly record struct CellId : IComparable<CellId>
{
    public CellId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "cell.");
    public CellId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(CellId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a board zone (tactical area).</summary>
public readonly record struct ZoneId : IComparable<ZoneId>
{
    public ZoneId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "zone.");
    public ZoneId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ZoneId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a link between board cells.</summary>
public readonly record struct LinkId : IComparable<LinkId>
{
    public LinkId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "link.");
    public LinkId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(LinkId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a combat encounter definition.</summary>
public readonly record struct EncounterId : IComparable<EncounterId>
{
    public EncounterId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "encounter.");
    public EncounterId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EncounterId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a combat or quest objective.</summary>
public readonly record struct ObjectiveId : IComparable<ObjectiveId>
{
    public ObjectiveId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "objective.");
    public ObjectiveId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ObjectiveId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a piece of equipment or gear.</summary>
public readonly record struct EquipmentId : IComparable<EquipmentId>
{
    public EquipmentId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "equipment.");
    public EquipmentId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EquipmentId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship instance or definition.</summary>
public readonly record struct ShipId : IComparable<ShipId>
{
    public ShipId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "ship.");
    public ShipId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship hull frame.</summary>
public readonly record struct ShipFrameId : IComparable<ShipFrameId>
{
    public ShipFrameId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "frame.");
    public ShipFrameId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipFrameId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship module or subsystem.</summary>
public readonly record struct ModuleId : IComparable<ModuleId>
{
    public ModuleId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "module.");
    public ModuleId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ModuleId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship weapon configuration.</summary>
public readonly record struct ShipWeaponConfigurationId : IComparable<ShipWeaponConfigurationId>
{
    public ShipWeaponConfigurationId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "ship.weapon.");
    public ShipWeaponConfigurationId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ShipWeaponConfigurationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a power network or electrical system.</summary>
public readonly record struct NetworkId : IComparable<NetworkId>
{
    public NetworkId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "network.");
    public NetworkId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(NetworkId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship station or facility.</summary>
public readonly record struct StationId : IComparable<StationId>
{
    public StationId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "station.");
    public StationId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(StationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a ship compartment or room.</summary>
public readonly record struct CompartmentId : IComparable<CompartmentId>
{
    public CompartmentId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "compartment.");
    public CompartmentId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(CompartmentId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for an instance of a ship module in the world.</summary>
public readonly record struct ModuleInstanceId : IComparable<ModuleInstanceId>
{
    public ModuleInstanceId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "module-instance.");
    public ModuleInstanceId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(ModuleInstanceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a game event trigger or occurrence.</summary>
public readonly record struct EventId : IComparable<EventId>
{
    public EventId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "event.");
    public EventId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EventId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>A strongly-typed identifier for a game effect or status condition.</summary>
public readonly record struct EffectId : IComparable<EffectId>
{
    public EffectId(ContentId value) => Value = TypedContentId.RequirePrefix(value, "effect.");
    public EffectId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(EffectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// A SHA-256 hash fingerprint used to validate content integrity and detect modifications.
/// </summary>
/// <remarks>
/// Content fingerprints are 64-character lowercase hexadecimal strings representing SHA-256 hashes.
/// They allow the game to detect if content has been modified or corrupted.
/// </remarks>
public readonly record struct ContentFingerprint
{
    /// <summary>
    /// Initializes a content fingerprint from a lowercase hexadecimal SHA-256 hash string.
    /// </summary>
    /// <param name="hexadecimal">A 64-character lowercase hexadecimal string representing a SHA-256 hash.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="hexadecimal"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the string is not exactly 64 characters or contains non-hex characters.</exception>
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

    /// <summary>
    /// Gets the hexadecimal representation of the SHA-256 hash, or null if invalid.
    /// </summary>
    public string? Hexadecimal { get; }

    /// <summary>
    /// Gets a value indicating whether this fingerprint is valid (not the default value).
    /// </summary>
    public bool IsValid => Hexadecimal is not null;

    /// <summary>
    /// Returns the hexadecimal string representation of this fingerprint.
    /// </summary>
    public override string ToString() => Hexadecimal ?? string.Empty;
}

/// <summary>
/// Internal helper for creating and validating typed content IDs with specific prefixes.
/// </summary>
file static class TypedContentId
{
    /// <summary>
    /// Validates that a content ID has the required prefix, throwing if not.
    /// </summary>
    /// <param name="value">The content ID to validate.</param>
    /// <param name="prefix">The required prefix (e.g., "attribute.").</param>
    /// <returns>The validated content ID.</returns>
    /// <exception cref="ArgumentException">Thrown if the ID does not have the required prefix.</exception>
    public static ContentId RequirePrefix(ContentId value, string prefix) =>
        value.IsValid && value.ToString().StartsWith(prefix, StringComparison.Ordinal)
            ? value
            : throw new ArgumentException($"Content ID must begin with '{prefix}'.", nameof(value));

    /// <summary>
    /// Attempts to parse a string into a typed content ID with a specific prefix.
    /// </summary>
    /// <typeparam name="T">The typed ID struct to create (must have a constructor taking a ContentId).</typeparam>
    /// <param name="value">The string to parse.</param>
    /// <param name="prefix">The required prefix for this type.</param>
    /// <param name="id">When true is returned, contains the parsed typed ID; otherwise, the default value.</param>
    /// <returns>True if successfully parsed with the correct prefix; otherwise, false.</returns>
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
