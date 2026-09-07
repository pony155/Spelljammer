# Milestone 2 content fixtures

The current production `Content/Packs/base` directory supplies the valid base
fixture; Milestone 2 assertions select its Attribute and Skill registries while
Milestone 3 and 4 assertions also exercise its character and supernatural
content. `invalid/cases.json`
applies focused virtual replacements and reduced limits to that base pack.
`additive/starwrights` proves that a third-party pack can add a namespaced Skill
without changing simulation source or presentation layout code.
`expected/fingerprints.json` pins the full base-only and additive semantic
fingerprints.
