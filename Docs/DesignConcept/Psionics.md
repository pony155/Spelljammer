# DRAFT

### Telepathy

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Psychic Blast | `psi.telepathy.psychic-blast` | 2 | Deal direct psychic damage to one target's mind; resistible by Willpower. |
| Mind Imprint | `psi.telepathy.mind-imprint` | 2 | Plant a suggestion in a target's mind that they feel compelled to follow; resisted by Willpower, and the compulsion fades if contradicted by harm or strong emotion. |
| Mind Reading | `psi.telepathy.mind-reading` | 2 | Peer into a target's immediate intentions to reveal their planned action and attack for the next turn; resisted by Willpower. |
| Mental Shaping | `psi.telepathy.mental-shaping` | 2 | Shape a target's mental state—inflict panic, confusion, or rage on enemies, or grant courage, clarity, or focus to allies; resisted by Willpower when used offensively. |
| Mental Restoration | `psi.telepathy.mental-restoration` | 2 | Calm and stabilize a target's mind, removing scare, confusion, and mental breakdown conditions. |
| Mind Control | `psi.telepathy.mind-control` | 3 | Seize control of a target's actions for a limited duration; resisted and broken by significant harm or counter-psionics. |
| Mass Mind Control | `psi.telepathy.mass-mind-control` | 4 | Seize control of multiple targets' actions simultaneously; number of targets and range determined by caster's Willpower; resisted and broken by significant harm or counter-psionics. |
| Psychic Storm | `psi.telepathy.psychic-storm` | 3 | Unleash a psychic storm in an area, dealing direct psychic damage to all minds within range; resistible by Willpower. |
| Psychic Scan | `psi.telepathy.psychic-scan` | 2 | Sweep a wide area to detect and locate all conscious minds within range, revealing their general position and relative mental strength. |

Telepathy never rewrites memory, forces permanent belief, or grants
unrestricted mind reading. A resisted attempt still creates Psionics Strain,
distorted impressions, or detectable feedback for the target.

### Telekinesis

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Telekinetic Force | `psi.telekinesis.force` | 1 | Apply remote force to one visible target or object, choosing to push it away or pull it toward the caster. |
| Force Shield | `psi.telekinesis.force-shield` | 2 | Raise a short-duration telekinetic barrier that absorbs or deflects incoming physical force for the caster or a nearby ally. |
| Hold | `psi.telekinesis.hold` | 2 | Grip a target object or creature with telekinetic force, holding it in place for a duration; resisted by Strength. |
| Disintegration | `psi.telekinesis.disintegration` | 3 | Shatter an object's molecular bonds, completely disintegrating it. |
| Fly | `psi.telekinesis.fly` | 3 | Lift and propel the caster through the air for a duration via telekinetic force. |

## Limits, saves, and delivery

Psionics cannot freely create wealth, resurrect the dead, read any mind
without limit, time travel, or bypass the galaxy map. Saves store known
technique IDs and active effects that matter: source ID, target, remaining
duration, and caster when relevant.

Content validation rejects duplicate IDs, unknown access or target tags,
negative costs, unbounded targets or durations, and effects that bypass these
limits. Implement the four first-playable techniques first; add the
remaining ones only when they make combat or exploration more interesting.
