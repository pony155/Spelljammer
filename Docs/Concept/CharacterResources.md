# Character Resources

## Status

The common five-resource state, data-driven resource profile, character
creation, action costs, spell Mana spending, psionic Strain generation, and
save representation are implemented. Full combat regeneration scheduling,
Resolve attacks, threshold-applied statuses, HUD presentation, and encounter
Health synchronization remain planned.

## Resource model

Every character has five values stored separately from Abilities and Skills:

| Resource | Direction | Purpose |
| --- | --- | --- |
| Health | Depletes from maximum toward zero | Physical survivability |
| Stamina | Depletes from maximum toward zero | Physical exertion |
| Mana | Depletes from maximum toward zero | Magical ability cost |
| Resolve | Depletes from maximum toward zero | Mental resilience |
| Strain | Accumulates from zero toward maximum | Psionic exertion and overload risk |

Each value stores current value, base maximum, base recovery or decay rate,
permanent modifiers, and temporary modifiers. Effective maximum and recovery
are derived independently and current values are always clamped from zero to
the effective maximum.

`CharacterResourceSet` owns the common operations: checking and spending a
depleting resource, restoring it, applying resource damage, generating or
reducing Strain, calculating a percentage, and recovering one simulation tick.
Abilities and effects must use these operations rather than duplicate resource
arithmetic.

## Data ownership

The scenario selects a `CharacterResourceProfileDefinition`. The profile JSON
owns all starting maxima, natural recovery or decay rates, Resolve thresholds,
and Strain thresholds. Ordinary resources start full; accumulating resources
start at zero. A missing profile means that scenario does not enable the five
resource system—there is no hidden numeric fallback.

The base profile is
`Content/Packs/base/Definitions/CharacterResourceProfiles/standard.json`.
Balance changes to maxima, regeneration, decay, or thresholds require only a
content revision and JSON edit.

Inventory-like quantities such as training supplies and crafting materials are
not character resources. They remain in separate bounded quantity storage and
do not acquire Health/Mana-style maxima or regeneration semantics.

## Health

Health receives physical and explicitly authored Health damage. At zero, the
actor enters incapacitation and death handling; zero does not silently kill the
character. Health has no normal combat recovery in the base profile. Healing,
items, Feats, treatment, rest, and other effects may restore it explicitly.

Health is an immediate combat capacity while injuries are persistent authored
consequences. Damage can reduce Health and create an injury in one transaction;
healing Health does not erase an injury.

## Stamina and Mana

Strenuous movement, attacks, defenses, and physical active Feats spend Stamina.
Stamina normally recovers during combat according to the selected profile and
modifiers. An action is rejected before publication when its complete cost
cannot be paid.

Magical active Feats spend Mana. Psionic active Feats do not spend Mana unless
their authored cost explicitly combines both systems. Mana recovery is slower
than Stamina in the base profile but remains content-controlled.

## Resolve

Fear, intimidation, terror, mental attacks, hostile psionics, and psychological
effects may damage or suppress Resolve. Resolve is defensive and is not the
normal activation currency for psionics. Low Resolve increases vulnerability;
reaching zero enters an authored Mental Break flow rather than applying one
universal result.

Resolve threshold percentages are ordered from high to low in the profile. The
base semantic bands are Stable, Shaken, Vulnerable, and Mental Break, while the
actual status applied depends on the source effect.

## Strain and overload

Psionic active Feats generate Strain. Reaching maximum Strain does not act like
insufficient Mana and does not automatically kill or permanently disable the
character. Further use may be legal but exposes the character to data-driven
backlash, Resolve damage, failure, Stun, Confusion, or temporary loss of
psionic access.

Strain thresholds are ordered from low to high. The base bands are Normal,
Strained, Critical, and Overload. Strain decays toward zero using the profile's
recovery rate when the authoritative combat or rest system requests recovery.

## Presentation and persistence

The HUD presents Health, Stamina, Mana, and Resolve as remaining capacity and
Strain as accumulating danger. It displays the current named threshold without
requiring percentage arithmetic from the player. Mana or Strain may be visually
de-emphasized when a character lacks the matching access, but Resolve remains
visible and relevant to everyone.

Campaign saves preserve current values, base maxima, recovery rates,
accumulation direction, and permanent and temporary modifier provenance.
Loading clamps values and validates the five entries against the scenario's
active profile before publishing state.

