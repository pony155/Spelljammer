using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Persistence;

public static class CampaignSaveCodec
{
    private const int HeaderBytes = 52;
    private const string DocumentDiscriminator = "spelljammer.campaign-save.v1";
    private static readonly byte[] Magic = "SJSAVE01"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = CampaignSaveLimits.MaximumNestingDepth,
        WriteIndented = false,
    };

    public static byte[] Encode(CampaignState campaign, GameContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        if (!CampaignValidator.TryValidate(campaign, content, out ContentId? missingId))
        {
            throw new InvalidOperationException($"Campaign state is invalid{(missingId is null ? "." : $": {missingId}.")}");
        }

        string[] required = [.. CampaignValidator.RequiredDefinitions(campaign, content).Select(value => value.ToString())];
        if (required.Length > CampaignSaveLimits.MaximumRequiredDefinitions)
        {
            throw new InvalidOperationException("Campaign definition references exceed capacity.");
        }

        SavePreflightDto preflight = new()
        {
            Discriminator = DocumentDiscriminator,
            GameBuild = campaign.GameBuild,
            ContentLock = ToDto(campaign.ContentLock),
            RequiredDefinitionIds = required,
        };
        byte[] preflightBytes = JsonSerializer.SerializeToUtf8Bytes(preflight, JsonOptions);
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(ToDto(campaign, content), JsonOptions);
        if (preflightBytes.Length > CampaignSaveLimits.MaximumPreflightBytes ||
            payloadBytes.Length > CampaignSaveLimits.MaximumPayloadBytes ||
            HeaderBytes + preflightBytes.Length + payloadBytes.Length > CampaignSaveLimits.MaximumSaveBytes)
        {
            throw new InvalidOperationException("Campaign save exceeds its bounded envelope.");
        }

        byte[] result = new byte[HeaderBytes + preflightBytes.Length + payloadBytes.Length];
        Magic.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8, 2), CampaignSaveVersions.Envelope);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10, 2), CampaignSaveVersions.SaveSchema);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)preflightBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), (uint)payloadBytes.Length);
        preflightBytes.CopyTo(result, HeaderBytes);
        payloadBytes.CopyTo(result, HeaderBytes + preflightBytes.Length);
        SHA256.HashData(result.AsSpan(HeaderBytes)).CopyTo(result, 20);
        return result;
    }

    public static SaveDiagnosticCode ValidateEnvelope(ReadOnlyMemory<byte> bytes) =>
        TryReadEnvelope(bytes, out _, out _, out SaveDiagnosticCode diagnostic) ? SaveDiagnosticCode.None : diagnostic;

    public static ContentPreflightResult Preflight(
        ReadOnlyMemory<byte> bytes,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!TryReadEnvelope(bytes, out SavePreflightDto? metadata, out _, out SaveDiagnosticCode diagnostic))
        {
            return FailedPreflight(diagnostic);
        }

        CampaignContentLock contentLock;
        ImmutableArray<ContentId> required;
        try
        {
            contentLock = FromDto(metadata!.ContentLock);
            required = ParseIds(metadata.RequiredDefinitionIds, CampaignSaveLimits.MaximumRequiredDefinitions);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedPreflight(SaveDiagnosticCode.Corrupt);
        }

        if (metadata.Discriminator != DocumentDiscriminator ||
            contentLock.SaveSchemaVersion != CampaignSaveVersions.SaveSchema ||
            contentLock.GeneratorVersion != CampaignSaveVersions.WorldGenerator ||
            contentLock.FormulaVersion != CampaignSaveVersions.Formula ||
            contentLock.EffectVersion != CampaignSaveVersions.Effect)
        {
            return new ContentPreflightResult(ContentPreflightKind.Incompatible, SaveDiagnosticCode.Unsupported,
                contentLock, [], [], []);
        }

        ImmutableHashSet<ContentId> availablePacks = content.Packs.Select(value => value.Id).ToImmutableHashSet();
        ImmutableArray<ContentId> missingPacks = [.. contentLock.Packs.Select(value => value.Id)
            .Where(id => !availablePacks.Contains(id)).Distinct().Order()];
        ImmutableArray<ContentId> missingDefinitions = [.. required.Where(id => !content.TryGetDefinition(id, out _)).Distinct().Order()];
        if (!missingPacks.IsEmpty || !missingDefinitions.IsEmpty)
        {
            return new ContentPreflightResult(ContentPreflightKind.Missing, SaveDiagnosticCode.MissingContent,
                contentLock, missingPacks, missingDefinitions, []);
        }

        CampaignContentLock available = CampaignContentLock.Create(content);
        bool packsExact = contentLock.Packs.SequenceEqual(available.Packs);
        if (packsExact && contentLock.ManifestFingerprint == available.ManifestFingerprint &&
            contentLock.SemanticFingerprint == content.Fingerprint && contentLock.EffectiveFingerprint == content.Fingerprint)
        {
            return new ContentPreflightResult(ContentPreflightKind.Exact, SaveDiagnosticCode.None, contentLock, [], [], []);
        }

        if (compatibility?.Any(rule => rule.SourceFingerprint == contentLock.EffectiveFingerprint &&
                rule.DestinationFingerprint == content.Fingerprint) == true)
        {
            return new ContentPreflightResult(ContentPreflightKind.Compatible, SaveDiagnosticCode.None, contentLock, [], [], []);
        }

        ImmutableArray<ContentId> path = migrations?.FindPath(contentLock.EffectiveFingerprint, content.Fingerprint) ?? [];
        if (!path.IsEmpty)
        {
            return new ContentPreflightResult(ContentPreflightKind.Migratable, SaveDiagnosticCode.None, contentLock, [], [], path);
        }

        return new ContentPreflightResult(ContentPreflightKind.Incompatible, SaveDiagnosticCode.IncompatibleContent,
            contentLock, [], [], []);
    }

    public static CampaignReadResult Decode(
        ReadOnlyMemory<byte> bytes,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ContentPreflightResult preflight = Preflight(bytes, content, compatibility, migrations);
        if (!preflight.CanLoad)
        {
            return new CampaignReadResult(null, preflight, preflight.Diagnostic);
        }

        if (!TryReadEnvelope(bytes, out SavePreflightDto? metadata, out ReadOnlyMemory<byte> payloadBytes, out SaveDiagnosticCode diagnostic))
        {
            return new CampaignReadResult(null, preflight, diagnostic);
        }

        try
        {
            CampaignPayloadDto payload = JsonSerializer.Deserialize<CampaignPayloadDto>(payloadBytes.Span, JsonOptions) ??
                throw new InvalidOperationException("Campaign payload is empty.");
            CampaignState campaign = FromDto(
                metadata!, preflight.ContentLock!, payload, content, preflight.Kind == ContentPreflightKind.Compatible);
            if (!CampaignValidator.TryValidate(campaign, content, out _))
            {
                throw new InvalidOperationException("Campaign invariants failed.");
            }

            return new CampaignReadResult(campaign, preflight, SaveDiagnosticCode.None);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return new CampaignReadResult(null, preflight, SaveDiagnosticCode.InvalidState);
        }
    }

    private static bool TryReadEnvelope(
        ReadOnlyMemory<byte> bytes,
        out SavePreflightDto? metadata,
        out ReadOnlyMemory<byte> payload,
        out SaveDiagnosticCode diagnostic)
    {
        metadata = null;
        payload = default;
        diagnostic = SaveDiagnosticCode.None;
        if (bytes.Length > CampaignSaveLimits.MaximumSaveBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Oversized);
        }

        if (bytes.Length < HeaderBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Truncated);
        }

        ReadOnlySpan<byte> span = bytes.Span;
        if (!span[..8].SequenceEqual(Magic))
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
        }

        ushort envelope = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));
        ushort schema = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(10, 2));
        if (envelope != CampaignSaveVersions.Envelope || schema != CampaignSaveVersions.SaveSchema)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Unsupported);
        }

        int preflightLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4)));
        int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4)));
        if (preflightLength < 2 || payloadLength < 2 || preflightLength > CampaignSaveLimits.MaximumPreflightBytes ||
            payloadLength > CampaignSaveLimits.MaximumPayloadBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Oversized);
        }

        long expected = (long)HeaderBytes + preflightLength + payloadLength;
        if (expected != bytes.Length)
        {
            return Fail(out diagnostic, expected > bytes.Length ? SaveDiagnosticCode.Truncated : SaveDiagnosticCode.Corrupt);
        }

        ReadOnlyMemory<byte> preflightBytes = bytes.Slice(HeaderBytes, preflightLength);
        payload = bytes.Slice(HeaderBytes + preflightLength, payloadLength);
        if (!SHA256.HashData(span[HeaderBytes..]).AsSpan().SequenceEqual(span.Slice(20, 32)))
        {
            return Fail(out diagnostic, SaveDiagnosticCode.ChecksumMismatch);
        }

        try
        {
            ValidateJsonShape(preflightBytes.Span);
            ValidateJsonShape(payload.Span);
            metadata = JsonSerializer.Deserialize<SavePreflightDto>(preflightBytes.Span, JsonOptions);
            if (metadata is null || Encoding.UTF8.GetByteCount(metadata.GameBuild) is 0 or > CampaignState.MaximumGameBuildBytes)
            {
                return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
            }
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
        }

        return true;

    }

    private static bool Fail(out SaveDiagnosticCode diagnostic, SaveDiagnosticCode value)
    {
        diagnostic = value;
        return false;
    }

    private static void ValidateJsonShape(ReadOnlySpan<byte> json)
    {
        Utf8JsonReader reader = new(json, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = CampaignSaveLimits.MaximumNestingDepth,
        });
        Stack<HashSet<string>> objectProperties = new();
        int values = 0;
        while (reader.Read())
        {
            if (++values > CampaignSaveLimits.MaximumCollectionEntries * 64)
            {
                throw new InvalidOperationException("JSON token capacity exceeded.");
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                objectProperties.Pop();
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString() ?? throw new JsonException();
                if (objectProperties.Count == 0 || !objectProperties.Peek().Add(name))
                {
                    throw new JsonException("Duplicate property.");
                }
            }
            else if (reader.TokenType == JsonTokenType.String && reader.HasValueSequence && reader.ValueSequence.Length > CampaignSaveLimits.MaximumStringBytes)
            {
                throw new InvalidOperationException("String capacity exceeded.");
            }
            else if (reader.TokenType == JsonTokenType.String && !reader.HasValueSequence && reader.ValueSpan.Length > CampaignSaveLimits.MaximumStringBytes)
            {
                throw new InvalidOperationException("String capacity exceeded.");
            }
        }
    }

    private static ContentPreflightResult FailedPreflight(SaveDiagnosticCode diagnostic) => new(
        ContentPreflightKind.Incompatible, diagnostic, null, [], [], []);

    private static ImmutableArray<ContentId> ParseIds(IEnumerable<string> values, int maximum)
    {
        string[] items = [.. values];
        if (items.Length > maximum || items.Distinct(StringComparer.Ordinal).Count() != items.Length)
        {
            throw new InvalidOperationException("ID collection exceeds capacity or contains duplicates.");
        }

        return [.. items.Select(value => new ContentId(value))];
    }

    private static ContentLockDto ToDto(CampaignContentLock value) => new()
    {
        BaseContentRevision = value.BaseContentRevision,
        Packs = [.. value.Packs.Select(pack => new PackLockDto
        {
            Id = pack.Id.ToString(),
            Version = pack.Version.ToString(),
            ContentRevision = pack.ContentRevision,
        })],
        ManifestFingerprint = value.ManifestFingerprint.ToString(),
        SemanticFingerprint = value.SemanticFingerprint.ToString(),
        EffectiveFingerprint = value.EffectiveFingerprint.ToString(),
        GeneratorVersion = value.GeneratorVersion,
        FormulaVersion = value.FormulaVersion,
        EffectVersion = value.EffectVersion,
        SaveSchemaVersion = value.SaveSchemaVersion,
        AppliedMigrationIds = [.. value.AppliedMigrationIds.Order().Select(id => id.ToString())],
    };

    private static CampaignContentLock FromDto(ContentLockDto value)
    {
        if (value.Packs.Length is 0 or > CampaignSaveLimits.MaximumCollectionEntries ||
            value.Packs.Select(pack => pack.Id).Distinct(StringComparer.Ordinal).Count() != value.Packs.Length)
        {
            throw new InvalidOperationException("Pack lock is invalid.");
        }

        ImmutableArray<CampaignPackLock> packs = [.. value.Packs.Select(pack =>
        {
            if (!SemanticVersion.TryParse(pack.Version, out SemanticVersion version) || pack.ContentRevision <= 0)
            {
                throw new InvalidOperationException("Pack version is invalid.");
            }

            return new CampaignPackLock(new ContentId(pack.Id), version, pack.ContentRevision);
        })];
        return new CampaignContentLock(
            value.BaseContentRevision,
            packs,
            new ContentFingerprint(value.ManifestFingerprint),
            new ContentFingerprint(value.SemanticFingerprint),
            new ContentFingerprint(value.EffectiveFingerprint),
            value.GeneratorVersion,
            value.FormulaVersion,
            value.EffectVersion,
            value.SaveSchemaVersion,
            ParseIds(value.AppliedMigrationIds, CampaignSaveLimits.MaximumCollectionEntries));
    }

    private static CampaignPayloadDto ToDto(CampaignState campaign, GameContentSnapshot content) => new()
    {
        CurrentLocationId = campaign.CurrentLocationId.ToString(),
        World = ToDto(campaign.Voyage),
        Characters = [.. campaign.Characters.OrderBy(value => value.Id).Select(value => ToDto(value, content))],
    };

    private static WorldDto ToDto(VoyageWorld world) => new()
    {
        Seed = world.Seed,
        Tick = world.Tick,
        RandomSequence = world.RandomSequence,
        PlayerTeamId = world.PlayerTeamId.ToString(),
        ShipPaused = world.ShipPaused,
        PersonalPaused = world.PersonalPaused,
        Ships = [.. world.Ships.Values.OrderBy(value => value.Id).Select(ToDto)],
        PersonalEncounter = world.PersonalEncounter is null ? null : ToDto(world.PersonalEncounter),
        Commands = [.. world.Commands.Select(ToDto)],
        CommandHistory = [.. world.CommandHistory.Select(value => new CommandLogDto
        {
            SubmittedTick = value.SubmittedTick,
            Command = ToDto(value.Command),
            CancelledTick = value.CancelledTick,
        })],
        ScheduledActions = [.. world.ScheduledActions.Select(value => new ScheduledActionDto
        {
            Command = ToDto(value.Command),
            Phase = (int)value.Phase,
            CommitTick = value.CommitTick,
            RecoverTick = value.RecoverTick,
            ReservedResourceId = value.ReservedResourceId?.ToString(),
            ReservedAmount = value.ReservedAmount,
            History = [.. value.History.Select(phase => (int)phase)],
        })],
        ReadyActorIds = [.. world.ReadyActors.Select(value => value.ToString())],
        Events = [.. world.Events.Select(value => new VoyageEventDto
        {
            Id = value.Id.ToString(),
            Tick = value.Tick,
            SourceId = value.SourceId.ToString(),
            TargetId = value.TargetId.ToString(),
            Kind = (int)value.Kind,
            Succeeded = value.Succeeded,
            Amount = value.Amount,
            ResultCode = value.ResultCode,
        })],
    };

    private static ShipDto ToDto(ShipState ship) => new()
    {
        Id = ship.Id.ToString(),
        TeamId = ship.TeamId.ToString(),
        FrameId = ship.Frame.ShipFrameId.ToString(),
        PathId = ship.PathId.ToString(),
        Hull = ship.Hull,
        Armor = ship.Armor,
        Cargo = ship.Cargo,
        PositionX = ship.Position.X.Raw,
        PositionY = ship.Position.Y.Raw,
        VelocityX = ship.Velocity.X.Raw,
        VelocityY = ship.Velocity.Y.Raw,
        HeadingMilliDegrees = ship.HeadingMilliDegrees,
        CollisionRadius = ship.CollisionRadius,
        Modules = [.. ship.Modules.OrderBy(value => value.InstanceId).Select(value => new ModuleDto
        {
            InstanceId = value.InstanceId.ToString(),
            DefinitionId = value.Definition.ModuleId.ToString(),
            Condition = (int)value.Condition,
            Integrity = value.Integrity,
            IsOn = value.IsOn,
            IsPowered = value.IsPowered,
            ShieldRaised = value.ShieldRaised,
            CurrentShield = value.CurrentShield,
            WeaponConfigurationId = value.Weapon?.ShipWeaponConfigurationId.ToString(),
            WeaponReadiness = (int)value.WeaponReadiness,
            ReadyTick = value.ReadyTick,
        })],
        Resources = Values(ship.Resources.Select(value => (value.Key.Value, value.Value))),
        Contacts = [.. ship.Contacts.Values.OrderBy(value => value.ShipId).Select(value => new ContactDto
        {
            ShipId = value.ShipId.ToString(),
            KnowledgeId = value.KnowledgeId.ToString(),
            LastObservedTick = value.LastObservedTick,
            HasFiringSolution = value.HasFiringSolution,
            WitnessIds = [.. value.Witnesses.Order().Select(id => id.ToString())],
        })],
        PersistentEvidenceIds = [.. ship.PersistentEvidence.Order().Select(value => value.ToString())],
        Disengaged = ship.Disengaged,
        Defending = ship.Defending,
    };

    private static CharacterDto ToDto(CharacterState character, GameContentSnapshot content)
    {
        CharacterCapabilitySnapshot snapshot = character.Capabilities.Snapshot(content);
        return new CharacterDto
        {
            Id = character.Id.ToString(),
            ScenarioId = character.ScenarioId.ToString(),
            RaceId = character.RaceId.ToString(),
            HeritageId = character.HeritageId.ToString(),
            BackgroundId = character.BackgroundId.ToString(),
            PositionId = character.PositionId.ToString(),
            Capabilities = new CapabilityDto
            {
                Attributes = [.. snapshot.Attributes.Select(value => new AttributeValueDto { Id = value.Id.ToString(), Value = value.Value })],
                Skills = [.. snapshot.Skills.Select(value => new SkillValueDto { Id = value.Id.ToString(), Value = value.Value, Practice = value.Practice })],
                FeatIds = [.. snapshot.Feats.Select(value => value.ToString())],
                PerkIds = [.. snapshot.Perks.Select(value => value.ToString())],
                TechniqueIds = [.. snapshot.Techniques.Select(value => value.ToString())],
                GrantSources = [.. snapshot.GrantSources.Select(value => new GrantDto
                {
                    CapabilityId = value.CapabilityId.ToString(),
                    SourceId = value.SourceId.ToString(),
                    SourceKind = (int)value.SourceKind,
                })],
                PracticeKeys = [.. snapshot.PracticeKeys.Select(value => value.ToString())],
            },
            LanguageIds = [.. character.LanguageIds.Order().Select(value => value.ToString())],
            ScriptIds = [.. character.ScriptIds.Order().Select(value => value.ToString())],
            EquipmentIds = [.. character.EquipmentIds.Order().Select(value => value.ToString())],
            Resources = Values(character.Resources.Select(value => (value.Key.Value, value.Value))),
            TrainingProgress = Values(character.TrainingProgress.Select(value => (value.Key.Value, value.Value))),
            CanAct = character.CanAct,
            ActiveEffects = [.. character.ActiveEffects.Select(value => new CapabilityEffectDto
            {
                EffectId = value.EffectId.ToString(),
                SourceId = value.SourceId.ToString(),
                ActorId = value.ActorId.ToString(),
                TargetId = value.TargetId.ToString(),
                StartTick = value.StartTick,
                EndTick = value.EndTick,
                ScopeId = value.ScopeId.ToString(),
            })],
            Evidence = [.. character.Evidence.Select(value => new CapabilityEvidenceDto
            {
                EvidenceId = value.EvidenceId.ToString(),
                SourceId = value.SourceId.ToString(),
                ActorId = value.ActorId.ToString(),
                TargetId = value.TargetId.ToString(),
                Tick = value.Tick,
                Succeeded = value.Succeeded,
            })],
        };
    }

    private static PersonalEncounterDto ToDto(PersonalEncounterState encounter) => new()
    {
        Id = encounter.Id.ToString(),
        BoardId = encounter.Board.Definition.PersonalBoardId.ToString(),
        Actors = [.. encounter.Actors.Values.OrderBy(value => value.Id).Select(value => new PersonalActorDto
        {
            Id = value.Id.ToString(),
            TeamId = value.TeamId.ToString(),
            CharacterId = value.CharacterId?.ToString(),
            CellId = value.CellId.ToString(),
            TurnMeter = value.TurnMeter,
            TurnRate = value.TurnRate,
            ActionPoints = value.ActionPoints,
            Health = value.Health,
            Defending = value.Defending,
            Surrendered = value.Surrendered,
            Prisoner = value.Prisoner,
            ReservedReactionPoints = value.ReservedReactionPoints,
            ReactionExpiresTick = value.ReactionExpiresTick,
            Equipment = [.. value.Loadout.Slots.Values.OrderBy(item => item.SlotId).Select(item => new EquipmentStateDto
            {
                SlotId = item.SlotId.ToString(),
                EquipmentId = item.Id.ToString(),
                Condition = (int)item.Condition,
                ResourceRemaining = item.ResourceRemaining,
            })],
            Injuries = [.. value.Injuries.Select(injury => new InjuryDto
            {
                Id = injury.Id.ToString(),
                Severity = (int)injury.Severity,
                Stabilized = injury.Stabilized,
            })],
        })],
        Objectives = [.. encounter.Objectives.OrderBy(value => value.Key).Select(value =>
            new ObjectiveDto { Id = value.Key.ToString(), State = (int)value.Value })],
        ExplorationChangeIds = [.. encounter.ExplorationChanges.Order().Select(value => value.ToString())],
        DamagedObjectIds = [.. encounter.DamagedObjects.Order().Select(value => value.ToString())],
        Retreated = encounter.Retreated,
        CleanedUp = encounter.CleanedUp,
        ActiveEffects = [.. encounter.ActiveEffects.Select(value => new EncounterEffectDto
        {
            Id = value.Id.ToString(),
            SourceId = value.SourceId.ToString(),
            TargetId = value.TargetId.ToString(),
            ExpiresTick = value.ExpiresTick,
            Stacks = value.Stacks,
        })],
    };

    private static CommandDto ToDto(VoyageCommand value) => new()
    {
        Id = value.Id.ToString(),
        Kind = (int)value.Kind,
        TargetTick = value.TargetTick,
        Priority = value.Priority,
        IssuerId = value.IssuerId.ToString(),
        TargetId = value.TargetId.ToString(),
        VectorX = value.Vector.X.Raw,
        VectorY = value.Vector.Y.Raw,
        Amount = value.Amount,
        OptionId = value.OptionId?.ToString(),
        Sequence = value.Sequence,
    };

    private static ValueDto[] Values(IEnumerable<(ContentId Id, int Value)> values) =>
        [.. values.OrderBy(value => value.Id).Select(value => new ValueDto { Id = value.Id.ToString(), Value = value.Value })];

    private static CampaignState FromDto(
        SavePreflightDto metadata,
        CampaignContentLock savedLock,
        CampaignPayloadDto payload,
        GameContentSnapshot content,
        bool addCompatibleDefinitions)
    {
        RequireCount(payload.Characters.Length, CampaignSaveLimits.MaximumCharacters);
        RequireCount(payload.World.Ships.Length, CampaignSaveLimits.MaximumShips);
        ImmutableArray<CharacterState> characters = [.. payload.Characters
            .Select(value => FromDto(value, content, addCompatibleDefinitions)).OrderBy(value => value.Id)];
        ImmutableDictionary<ShipId, ShipState> ships = payload.World.Ships.Select(value => FromDto(value, content))
            .ToImmutableDictionary(value => value.Id);
        PersonalEncounterState? encounter = payload.World.PersonalEncounter is null
            ? null
            : FromDto(payload.World.PersonalEncounter, content);

        RequireCount(payload.World.Commands.Length, VoyageWorld.MaximumCommands);
        RequireCount(payload.World.CommandHistory.Length, VoyageWorld.MaximumCommandHistory);
        RequireCount(payload.World.ScheduledActions.Length, VoyageWorld.MaximumSchedules);
        RequireCount(payload.World.ReadyActorIds.Length, VoyageWorld.MaximumReadyActors);
        RequireCount(payload.World.Events.Length, VoyageWorld.MaximumEvents);
        VoyageWorld world = new(
            payload.World.Seed,
            content.Fingerprint,
            payload.World.Tick,
            payload.World.RandomSequence,
            new TeamId(payload.World.PlayerTeamId),
            payload.World.ShipPaused,
            payload.World.PersonalPaused,
            ships,
            encounter,
            [.. payload.World.Commands.Select(FromDto)],
            [.. payload.World.CommandHistory.Select(value => new VoyageCommandLogEntry(
                value.SubmittedTick, FromDto(value.Command), value.CancelledTick))],
            [.. payload.World.ScheduledActions.Select(value => new ScheduledAction(
                FromDto(value.Command),
                ParseEnum<ScheduledActionPhase>(value.Phase),
                value.CommitTick,
                value.RecoverTick,
                value.ReservedResourceId is null ? null : new ResourceId(value.ReservedResourceId),
                value.ReservedAmount,
                [.. value.History.Select(ParseEnum<ScheduledActionPhase>)]))],
            [.. payload.World.ReadyActorIds.Select(value => new ActorId(value))],
            [.. payload.World.Events.Select(value => new VoyageEvent(
                new ContentId(value.Id), value.Tick, new ContentId(value.SourceId), new ContentId(value.TargetId),
                ParseEnum<VoyageCommandKind>(value.Kind), value.Succeeded, value.Amount, value.ResultCode))]);

        CampaignContentLock activeLock = savedLock.EffectiveFingerprint == content.Fingerprint
            ? savedLock
            : CampaignContentLock.Create(content, savedLock.AppliedMigrationIds);
        return new CampaignState(metadata.GameBuild, activeLock, new ContentId(payload.CurrentLocationId), world, characters);
    }

    private static ShipState FromDto(ShipDto value, GameContentSnapshot content)
    {
        RequireCount(value.Modules.Length, ShipLoadoutSystem.MaximumModules);
        RequireCount(value.Resources.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Contacts.Length, CampaignSaveLimits.MaximumCollectionEntries);
        if (!content.TryGetShipFrame(new ShipFrameId(value.FrameId), out ShipFrameDefinition? frame))
        {
            throw new InvalidOperationException("Ship frame is missing.");
        }

        ImmutableArray<InstalledModuleState> modules = [.. value.Modules.Select(module =>
        {
            if (!content.TryGetShipModule(new ModuleId(module.DefinitionId), out ShipModuleDefinition? definition))
            {
                throw new InvalidOperationException("Ship module is missing.");
            }

            ShipWeaponConfigurationDefinition? weapon = null;
            if (module.WeaponConfigurationId is not null &&
                !content.TryGetShipWeaponConfiguration(new ShipWeaponConfigurationId(module.WeaponConfigurationId), out weapon))
            {
                throw new InvalidOperationException("Ship weapon configuration is missing.");
            }

            return new InstalledModuleState(
                new ContentId(module.InstanceId), definition!, ParseEnum<ModuleCondition>(module.Condition), module.Integrity,
                module.IsOn, module.IsPowered, module.ShieldRaised, module.CurrentShield, weapon,
                ParseEnum<WeaponReadiness>(module.WeaponReadiness), module.ReadyTick);
        }).OrderBy(module => module.InstanceId)];
        ImmutableDictionary<ResourceId, int> resources = ValueDictionary(value.Resources)
            .ToImmutableDictionary(pair => new ResourceId(pair.Key), pair => pair.Value);
        ImmutableDictionary<ShipId, ShipContactState> contacts = value.Contacts.Select(contact =>
        {
            RequireCount(contact.WitnessIds.Length, CampaignSaveLimits.MaximumCharacters);
            ShipContactState state = new(
                new ShipId(contact.ShipId), new ContentId(contact.KnowledgeId), contact.LastObservedTick,
                contact.HasFiringSolution, ParseIds(contact.WitnessIds, CampaignSaveLimits.MaximumCharacters)
                    .Select(id => new ActorId(id)).ToImmutableHashSet());
            return state;
        }).ToImmutableDictionary(contact => contact.ShipId);
        return new ShipState(
            new ShipId(value.Id), new TeamId(value.TeamId), frame!, new ContentId(value.PathId), value.Hull, value.Armor,
            value.Cargo, new FixedVector2(new FixedScalar(value.PositionX), new FixedScalar(value.PositionY)),
            new FixedVector2(new FixedScalar(value.VelocityX), new FixedScalar(value.VelocityY)), value.HeadingMilliDegrees,
            value.CollisionRadius, modules, resources, contacts,
            ParseIds(value.PersistentEvidenceIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            value.Disengaged, value.Defending);
    }

    private static CharacterState FromDto(
        CharacterDto value,
        GameContentSnapshot content,
        bool addCompatibleDefinitions)
    {
        RequireCount(value.Capabilities.Attributes.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Capabilities.Skills.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Capabilities.GrantSources.Length, CharacterCapabilities.MaximumSetEntries);
        ImmutableArray<AttributeValueSnapshot> attributes =
            [.. value.Capabilities.Attributes.Select(item => new AttributeValueSnapshot(new AttributeId(item.Id), item.Value))];
        ImmutableArray<SkillValueSnapshot> skills =
            [.. value.Capabilities.Skills.Select(item => new SkillValueSnapshot(new SkillId(item.Id), item.Value, item.Practice))];
        if (addCompatibleDefinitions)
        {
            attributes = [.. content.Attributes.Select(definition => attributes
                .FirstOrDefault(item => item.Id == definition.AttributeId) ??
                new AttributeValueSnapshot(definition.AttributeId, (short)definition.DefaultValue))];
            skills = [.. content.Skills.Select(definition => skills
                .FirstOrDefault(item => item.Id == definition.SkillId) ??
                new SkillValueSnapshot(definition.SkillId, (byte)definition.Minimum, 0))];
        }

        CharacterCapabilitySnapshot snapshot = new(
            content.Fingerprint,
            attributes,
            skills,
            [.. ParseIds(value.Capabilities.FeatIds, CharacterCapabilities.MaximumSetEntries).Select(id => new FeatId(id))],
            [.. ParseIds(value.Capabilities.PerkIds, CharacterCapabilities.MaximumSetEntries).Select(id => new PerkId(id))],
            [.. value.Capabilities.GrantSources.Where(grant => grant.CapabilityId.StartsWith("access.", StringComparison.Ordinal))
                .Select(grant => new AccessId(grant.CapabilityId)).Distinct().Order()],
            [.. ParseIds(value.Capabilities.TechniqueIds, CharacterCapabilities.MaximumSetEntries).Select(id => new TechniqueId(id))],
            [.. value.Capabilities.GrantSources.Select(grant => new CapabilityGrant(
                new ContentId(grant.CapabilityId), new ContentId(grant.SourceId), ParseEnum<GrantSourceKind>(grant.SourceKind)))],
            ParseIds(value.Capabilities.PracticeKeys, CharacterCapabilities.MaximumPracticeKeys));
        CharacterCapabilities capabilities = CharacterCapabilities.Restore(snapshot, content);
        ImmutableDictionary<ContentId, int> resources = ValueDictionary(value.Resources);
        ImmutableDictionary<ContentId, int> training = ValueDictionary(value.TrainingProgress);
        CharacterState character = new(
            new CharacterId(value.Id), content.Fingerprint, new ScenarioId(value.ScenarioId), new RaceId(value.RaceId),
            new HeritageId(value.HeritageId), new BackgroundId(value.BackgroundId), new ContentId(value.PositionId), capabilities,
            ParseIds(value.LanguageIds, CampaignSaveLimits.MaximumCollectionEntries),
            ParseIds(value.ScriptIds, CampaignSaveLimits.MaximumCollectionEntries),
            ParseIds(value.EquipmentIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            resources.ToImmutableDictionary(pair => new ResourceId(pair.Key), pair => pair.Value),
            training.ToImmutableDictionary(pair => new TrainingProjectId(pair.Key), pair => pair.Value),
            value.CanAct)
        {
            ActiveEffects = [.. value.ActiveEffects.Select(effect => new ActiveCapabilityEffect(
                new ContentId(effect.EffectId), new ContentId(effect.SourceId), new CharacterId(effect.ActorId),
                new CharacterId(effect.TargetId), effect.StartTick, effect.EndTick, new ContentId(effect.ScopeId)))],
            Evidence = [.. value.Evidence.Select(evidence => new ObservableCapabilityEvidence(
                new ContentId(evidence.EvidenceId), new ContentId(evidence.SourceId), new CharacterId(evidence.ActorId),
                new CharacterId(evidence.TargetId), evidence.Tick, evidence.Succeeded))],
        };
        return character;
    }

    private static PersonalEncounterState FromDto(PersonalEncounterDto value, GameContentSnapshot content)
    {
        if (!content.TryGetEncounter(new EncounterId(value.Id), out EncounterDefinition? encounterDefinition) ||
            !content.TryGetPersonalBoard(new PersonalBoardId(value.BoardId), out PersonalBoardDefinition? boardDefinition) ||
            encounterDefinition!.PersonalBoardId != boardDefinition!.PersonalBoardId)
        {
            throw new InvalidOperationException("Encounter definition is missing or mismatched.");
        }

        BoardValidationResult boardResult = TacticalBoard.Create(
            boardDefinition,
            boardDefinition.CellIds.Select(id => content.TryGetBoardCell(id, out BoardCellDefinition? cell)
                ? cell! : throw new InvalidOperationException("Board cell is missing.")),
            boardDefinition.LinkIds.Select(id => content.TryGetZoneLink(id, out ZoneLinkDefinition? link)
                ? link! : throw new InvalidOperationException("Zone link is missing.")));
        if (!boardResult.Accepted)
        {
            throw new InvalidOperationException(boardResult.RejectionCode);
        }

        RequireCount(value.Actors.Length, boardDefinition.MaximumOccupants);
        TacticalBoard board = boardResult.Board!;
        ImmutableDictionary<ActorId, PersonalActorState>.Builder actors = ImmutableDictionary.CreateBuilder<ActorId, PersonalActorState>();
        foreach (PersonalActorDto actorDto in value.Actors)
        {
            CellId cellId = new(actorDto.CellId);
            ActorId actorId = new(actorDto.Id);
            ImmutableDictionary<ContentId, EquipmentState>.Builder slots = ImmutableDictionary.CreateBuilder<ContentId, EquipmentState>();
            RequireCount(actorDto.Equipment.Length, PersonalLoadout.MaximumSlots);
            foreach (EquipmentStateDto item in actorDto.Equipment)
            {
                EquipmentState equipment = new(new EquipmentId(item.EquipmentId), new ContentId(item.SlotId),
                    ParseEnum<EquipmentCondition>(item.Condition), item.ResourceRemaining);
                slots.Add(equipment.SlotId, equipment);
            }

            PersonalActorState actor = new(
                actorId, new TeamId(actorDto.TeamId), actorDto.CharacterId is null ? null : new CharacterId(actorDto.CharacterId),
                cellId, actorDto.TurnMeter, actorDto.TurnRate, actorDto.ActionPoints, actorDto.Health, actorDto.Defending,
                actorDto.Surrendered, actorDto.Prisoner, new PersonalLoadout(slots.ToImmutable()),
                [.. actorDto.Injuries.Select(injury => new InjuryState(
                    new ContentId(injury.Id), ParseEnum<InjurySeverity>(injury.Severity), injury.Stabilized))])
            {
                ReservedReactionPoints = actorDto.ReservedReactionPoints,
                ReactionExpiresTick = actorDto.ReactionExpiresTick,
            };
            board = board.Place(actorId, cellId);
            actors.Add(actorId, actor);
        }

        PersonalEncounterState encounter = new(
            encounterDefinition.EncounterId,
            board,
            actors.ToImmutable(),
            value.Objectives.ToImmutableDictionary(item => new ObjectiveId(item.Id), item => ParseEnum<ObjectiveState>(item.State)),
            ParseIds(value.ExplorationChangeIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            ParseIds(value.DamagedObjectIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            value.Retreated,
            value.CleanedUp)
        {
            ActiveEffects = [.. value.ActiveEffects.Select(effect => new ActiveEffectState(
                new EffectId(effect.Id), new ContentId(effect.SourceId), new ActorId(effect.TargetId), effect.ExpiresTick, effect.Stacks))],
        };
        return encounter;
    }

    private static VoyageCommand FromDto(CommandDto value) => new(
        new ContentId(value.Id), ParseEnum<VoyageCommandKind>(value.Kind), value.TargetTick, value.Priority,
        new ContentId(value.IssuerId), new ContentId(value.TargetId),
        new FixedVector2(new FixedScalar(value.VectorX), new FixedScalar(value.VectorY)), value.Amount,
        value.OptionId is null ? null : new ContentId(value.OptionId), value.Sequence);

    private static ImmutableDictionary<ContentId, int> ValueDictionary(ValueDto[] values)
    {
        RequireCount(values.Length, CampaignSaveLimits.MaximumCollectionEntries);
        if (values.Any(value => value.Value < 0))
        {
            throw new InvalidOperationException("Negative persistent resource value.");
        }

        return values.ToImmutableDictionary(value => new ContentId(value.Id), value => value.Value);
    }

    private static T ParseEnum<T>(int value)
        where T : struct, Enum
    {
        T parsed = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} value.");
    }

    private static void RequireCount(int count, int maximum)
    {
        if (count < 0 || count > maximum)
        {
            throw new InvalidOperationException("Persistent collection exceeds capacity.");
        }
    }
}
