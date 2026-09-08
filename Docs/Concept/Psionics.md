# DRAFT

### Telepathy

| Active Feat | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Psionic Blast | `feat.active.psionics.telepathy.psionic-blast` | 2 | Deal direct psionic damage to one target's mind; resistible by Willpower. |
| Mind Imprint | `feat.active.psionics.telepathy.mind-imprint` | 2 | Plant a suggestion in a target's mind that they feel compelled to follow; resisted by Willpower, and the compulsion fades if contradicted by harm or strong emotion. |
| Mind Reading | `feat.active.psionics.telepathy.mind-reading` | 2 | Peer into a target's immediate intentions to reveal their planned action and attack for the next turn; resisted by Willpower. |
| Mind Shaping | `feat.active.psionics.telepathy.mind-shaping` | 2 | Shape a target's mental state—inflict panic, confusion, or rage on enemies, or grant courage, clarity, or focus to allies; resisted by Willpower when used offensively. |
| Mind Restoration | `feat.active.psionics.telepathy.mind-restoration` | 2 | Calm and stabilize a target's mind, removing scare, confusion, and mental breakdown conditions. |
| Mind Domination | `feat.active.psionics.telepathy.mind-domination` | 3 | Seize control of a target's actions for a limited duration; resisted and broken by significant harm or counter-psionics. |
| Mass Mind Domination | `feat.active.psionics.telepathy.mass-mind-domination` | 4 | Seize control of multiple targets' actions simultaneously; number of targets and range determined by caster's Willpower; resisted and broken by significant harm or counter-psionics. |
| Psionic Storm | `feat.active.psionics.telepathy.psionic-storm` | 3 | Unleash a psionic storm in an area, dealing direct psionic damage to all minds within range; resistible by Willpower. |
| Psionic Scan | `feat.active.psionics.telepathy.psionic-scan` | 2 | Sweep a wide area to detect and locate all conscious minds within range, revealing their general position and relative mental strength. |

Telepathy never rewrites memory, forces permanent belief, or grants
unrestricted mind reading. A resisted attempt still creates Psionics Strain,
distorted impressions, or detectable feedback for the target.

Psionic active Feats generate `resource.strain`; they do not spend Mana or
Resolve by default. Maximum Strain is a risk threshold rather than a hard ban
on further use. Backlash and Resolve damage are authored effects, and Strain
decay and overload thresholds come from the active character resource profile.

### Telekinesis

| Active Feat | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Telekinetic Force | `feat.active.psionics.telekinesis.force` | 1 | Apply remote force to one visible target or object, choosing to push it away or pull it toward the caster. |
| Force Shield | `feat.active.psionics.telekinesis.force-shield` | 2 | Raise a short-duration telekinetic barrier that absorbs or deflects incoming physical force for the caster or a nearby ally. |
| Hold | `feat.active.psionics.telekinesis.hold` | 2 | Grip a target object or creature with telekinetic force, holding it in place for a duration; resisted by Strength. |
| Disintegration | `feat.active.psionics.telekinesis.disintegration` | 3 | Shatter an object's molecular bonds, completely disintegrating it. |
| Fly | `feat.active.psionics.telekinesis.fly` | 3 | Lift and propel the caster through the air for a duration via telekinetic force. |

## Limits, saves, and delivery

Psionics cannot freely create wealth, resurrect the dead, read any mind
without limit, time travel, or bypass the galaxy map. Saves store known active
Feat IDs and ongoing Status instances, including source, target, remaining
duration, stacks, and potency. One-shot Effect requests are resolved
transactionally and are not saved as a second ongoing-state model.

Content validation rejects duplicate IDs, unknown access or target tags,
negative costs, unbounded targets or durations, and effects that bypass these
limits. Implement the four first-playable active Feats first; add the
remaining ones only when they make combat or exploration more interesting.
