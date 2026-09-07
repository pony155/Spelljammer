# Spelljammer.Settings

This headless project owns the versioned local player-preference profile,
validation, bounded JSON codec, transactional publication, and durable atomic
replacement. Stable language and resolution choices were added in schema 2;
schema 1 profiles migrate in memory with safe `en-US` and `desktop` defaults.
It contains no WPF, localization, SpriteForge, native pointers, or authoritative
campaign state.

The profile contains language, resolution, bounded audio levels, subtitles,
reduced motion, screen shake, and UI scale. Campaign-affecting settings remain
planned and must enter the content-locked campaign save through an explicit
schema migration.
