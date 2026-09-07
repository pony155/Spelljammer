# Character Level Progression

## Status

The content definition, compiler validation, typed registry, and base twenty-level
table are implemented. Character experience state, experience awards, level-up
commands, reward spending, and save migration remain planned.

## Purpose

Character level is a pacing and advancement budget. It does not define a class,
automatically improve every action, or replace Abilities, Skills, Feats,
equipment, injuries, and training. Each character owns an individual level and
cumulative experience total. Major voyage rewards may be shared across the
recruited roster so reserve characters remain viable.

Every scenario may select a `LevelProgressionTableDefinition`. A scenario with
no table does not enable character-level advancement. There is no implicit
fallback table and no maximum level hard-coded in simulation code.

## Data contract

A level progression table owns an ordered `levels` array. Each row declares:

- `level`, beginning at 1 and increasing without gaps;
- `requiredExperience`, the cumulative minimum XP for that level;
- `maximumHealthIncrease`, `maximumManaIncrease`, `maximumStaminaIncrease`,
  `maximumResolveIncrease`, and `maximumStrainIncrease`, applied to the
  character's maximum pools;
- `abilityPoints`, `skillPoints`, and `featPoints`, added to the corresponding
  unspent advancement budgets.

Level 1 must require zero experience. Later thresholds must increase strictly.
Reward values are non-negative. The final authored row determines the maximum
level. Adding or removing levels, changing XP pacing, or changing any reward is
a content edit rather than a C# change.

The base table is
`Content/Packs/base/Definitions/LevelProgressionTables/character-standard.json`.
Its current numbers are initial balancing values, not engine constants.

## Runtime contract

When experience is committed, the simulation resolves the highest table row
whose threshold has been reached. Crossing several thresholds in one award
grants every crossed row exactly once and records pending player choices.
Rewards are committed transactionally with the experience change; partial
granting is invalid.

Health, Mana, Stamina, Resolve, and Strain increases affect maximum values.
Whether current values are restored on level-up must be a separate
campaign/content rule; it
must not be inferred from the maximum increase. Ability, Skill, and Feat points
remain unspent until an explicit valid command allocates them.

Base maxima, regeneration, decay, and Resolve or Strain thresholds belong to
the scenario's character resource profile. The level table contains only the
increment granted by crossing each authored level.

Changing a table used by a saved campaign changes the content fingerprint and
requires the normal compatibility or migration path. Saves persist the table
ID, level, cumulative experience, granted-row history or equivalent validated
state, and unspent point balances; they do not recompute old rewards silently
from a changed table.

## Validation

Content compilation rejects missing or empty tables, non-contiguous levels, a
non-zero level-1 threshold, non-increasing XP thresholds, negative rewards,
invalid IDs, missing localization keys, and scenario references to unknown
tables. Collection and file-size limits continue to bound authored tables.
