# Crew and Recruitment

## Status

The headless simulation and campaign persistence implement an immutable active
`CrewRoster`, selection of one protagonist, NPC recruitment, duplicate and
compatibility rejection, and a content-authored roster limit. Recruitment
presentation, encounter offers, dismissal, death handling, and ship-support
effects remain planned.

## Active roster

The character-creation candidate list is not the active crew. The player first
chooses one candidate as the protagonist. Every other compatible character is
an NPC candidate who may be recruited through gameplay.

An active roster always contains its protagonist. The scenario definition sets
the maximum number of members, including the protagonist. For
`scenario.first-voyage`, `maximumRosterSize` is `8`, allowing the protagonist
and up to seven recruited NPCs.

The simulation never uses `8` as a roster-rule constant. Recruitment resolves
the active scenario through the content catalog and reads its authored limit.
The compiler validates that limit against the simulation's general bounded
collection safety ceiling.

## Recruitment contract

Recruitment is an explicit immutable state transition. It rejects mismatched
content, missing or incompatible characters, duplicate members, attempts to
recruit the protagonist, malformed rosters, and full rosters without mutating
the published roster.

Future recruitment conditions may include relationships, faction standing,
contracts, pay, available quarters, life support, and event outcomes. Those
conditions should be added as inspectable requirements rather than hidden UI
checks.
