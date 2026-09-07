using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public sealed record TrainingContext(
    ImmutableHashSet<ContentId> AvailableFacilityIds,
    ImmutableHashSet<ContentId> AvailableSafetyIds);

public sealed record TrainingCompletionEvent(
    CharacterId CharacterId,
    TrainingProjectId ProjectId,
    ImmutableArray<FeatId> GrantedFeatIds,
    ImmutableArray<AccessId> GrantedAccessIds,
    ImmutableArray<TechniqueId> GrantedTechniqueIds,
    ResourceId ResourceId,
    int ResourceCost);

public sealed record TrainingCommandResult(
    CharacterState State,
    bool Accepted,
    string RejectionCode,
    int Progress,
    TrainingCompletionEvent? Completion);

public static class CharacterTrainingSystem
{
    public static TrainingCommandResult Start(
        CharacterState character,
        TrainingProjectId projectId,
        TrainingContext context,
        ICharacterContentCatalog catalog)
    {
        if (!TryProject(character, projectId, catalog, out TrainingProjectDefinition? project, out string rejection))
        {
            return Rejected(character, rejection);
        }

        if (character.TrainingProgress.ContainsKey(projectId))
        {
            return Rejected(character, "command.training-already-started");
        }

        if (character.TrainingProgress.Count >= CharacterCapabilities.MaximumSetEntries)
        {
            return Rejected(character, "command.queue-capacity");
        }

        foreach (SkillId skillId in project!.RequiredSkillIds)
        {
            if (!character.Capabilities.TryGetSkill(skillId, catalog, out byte value, out _) || value == 0)
            {
                return Rejected(character, ActionRejectionCodes.SkillRequired);
            }
        }

        if (!context.AvailableFacilityIds.Contains(project.FacilityId))
        {
            return Rejected(character, "command.training-facility-required");
        }

        if (!context.AvailableSafetyIds.Contains(project.SafetyId))
        {
            return Rejected(character, "command.training-safety-required");
        }

        character.Resources.TryGetValue(project.ResourceId, out int available);
        if (available < project.ResourceCost)
        {
            return Rejected(character, ActionRejectionCodes.ResourceInsufficient);
        }

        CharacterState started = character with { TrainingProgress = character.TrainingProgress.Add(projectId, 0) };
        return Accepted(started, 0);
    }

    public static TrainingCommandResult Contribute(
        CharacterState character,
        TrainingProjectId projectId,
        int workUnits,
        ICharacterContentCatalog catalog)
    {
        if (!TryProject(character, projectId, catalog, out TrainingProjectDefinition? project, out string rejection))
        {
            return Rejected(character, rejection);
        }

        if (!character.TrainingProgress.TryGetValue(projectId, out int current))
        {
            return Rejected(character, "command.training-not-started");
        }

        if (workUnits <= 0 || current < 0 || current > project!.ProgressCap ||
            (long)current + workUnits > project.ProgressCap)
        {
            return Rejected(character, "command.training-work-invalid");
        }

        int progress = current + workUnits;
        CharacterState contributed = character with
        {
            TrainingProgress = character.TrainingProgress.SetItem(projectId, progress),
        };
        return Accepted(contributed, progress);
    }

    public static TrainingCommandResult Cancel(
        CharacterState character,
        TrainingProjectId projectId,
        ICharacterContentCatalog catalog)
    {
        if (!TryProject(character, projectId, catalog, out _, out string rejection))
        {
            return Rejected(character, rejection);
        }

        if (!character.TrainingProgress.TryGetValue(projectId, out int progress))
        {
            return Rejected(character, "command.training-not-started");
        }

        CharacterState cancelled = character with { TrainingProgress = character.TrainingProgress.Remove(projectId) };
        return Accepted(cancelled, progress);
    }

    public static TrainingCommandResult Complete(
        CharacterState character,
        TrainingProjectId projectId,
        ICharacterContentCatalog catalog)
    {
        if (!TryProject(character, projectId, catalog, out TrainingProjectDefinition? project, out string rejection))
        {
            return Rejected(character, rejection);
        }

        if (!character.TrainingProgress.TryGetValue(projectId, out int progress))
        {
            return Rejected(character, "command.training-not-started");
        }

        if (progress < project!.WorkUnits || progress > project.ProgressCap)
        {
            return Rejected(character, "command.training-incomplete");
        }

        character.Resources.TryGetValue(project.ResourceId, out int available);
        if (available < project.ResourceCost)
        {
            return Rejected(character, ActionRejectionCodes.ResourceInsufficient);
        }

        ImmutableArray<FeatDefinition>.Builder featBuilder = ImmutableArray.CreateBuilder<FeatDefinition>();
        foreach (FeatId featId in project.GrantedFeatIds)
        {
            if (!catalog.TryGetFeat(featId, out FeatDefinition? feat) || feat!.TrainingProjectId != projectId)
            {
                return Rejected(character, ActionRejectionCodes.ActionUnknown);
            }

            featBuilder.Add(feat);
        }

        foreach (TechniqueId techniqueId in project.GrantedTechniqueIds)
        {
            bool known = techniqueId.Value.ToString().StartsWith("spell.", StringComparison.Ordinal)
                ? catalog.TryGetSpell(new SpellId(techniqueId.Value), out _)
                : techniqueId.Value.ToString().StartsWith("psychic.", StringComparison.Ordinal)
                    ? catalog.TryGetPsychicTechnique(new PsychicTechniqueId(techniqueId.Value), out _)
                    : catalog.TryGetTechnique(techniqueId, out _);
            if (!known)
            {
                return Rejected(character, ActionRejectionCodes.ActionUnknown);
            }
        }

        ImmutableArray<FeatDefinition> feats = featBuilder.MoveToImmutable();
        CharacterCapabilities capabilities;
        try
        {
            capabilities = character.Capabilities.WithTrainingGrants(project, feats);
        }
        catch (InvalidOperationException)
        {
            return Rejected(character, "command.queue-capacity");
        }

        CharacterState completed = character with
        {
            Capabilities = capabilities,
            Resources = character.Resources.SetItem(project.ResourceId, available - project.ResourceCost),
            TrainingProgress = character.TrainingProgress.Remove(projectId),
        };
        ImmutableArray<AccessId> access =
            [.. feats.SelectMany(value => value.GrantedAccessIds).Distinct().Order()];
        TrainingCompletionEvent completion = new(
            character.Id,
            projectId,
            [.. project.GrantedFeatIds.Order()],
            access,
            project.GrantedTechniqueIds,
            project.ResourceId,
            project.ResourceCost);
        return new TrainingCommandResult(completed, true, ActionRejectionCodes.None, progress, completion);
    }

    private static bool TryProject(
        CharacterState character,
        TrainingProjectId projectId,
        ICharacterContentCatalog catalog,
        out TrainingProjectDefinition? project,
        out string rejection)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(catalog);
        if (character.ContentFingerprint != catalog.Fingerprint)
        {
            project = null;
            rejection = ActionRejectionCodes.ContentMismatch;
            return false;
        }

        if (!catalog.TryGetTrainingProject(projectId, out project))
        {
            rejection = ActionRejectionCodes.ActionUnknown;
            return false;
        }

        rejection = ActionRejectionCodes.None;
        return true;
    }

    private static TrainingCommandResult Accepted(CharacterState state, int progress) =>
        new(state, true, ActionRejectionCodes.None, progress, null);

    private static TrainingCommandResult Rejected(CharacterState character, string code) =>
        new(character, false, code, 0, null);
}
