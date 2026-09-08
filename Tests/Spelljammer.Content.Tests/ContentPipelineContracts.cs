using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

internal static partial class ContentContracts
{
    private static void StableIdsAreValidatedAndOrdinal()
    {
        True(ContentId.TryParse("ability.strength", out ContentId valid), "A valid ID was rejected.");
        False(ContentId.TryParse("Ability.Strength", out _), "A culture-sensitive ID was accepted.");
        False(default(ContentId).IsValid, "The default ID became valid.");
        string maximum = "domain." + new string('a', 120);
        Equal(ContentId.MaximumLength, maximum.Length, "Maximum-length fixture is wrong.");
        True(ContentId.TryParse(maximum, out _), "A maximum-length ID was rejected.");
        False(ContentId.TryParse(maximum + "a", out _), "An oversized ID was accepted.");
        True(valid.CompareTo(new ContentId("ability.toughness")) < 0, "ID comparison was not ordinal.");
    }
}
