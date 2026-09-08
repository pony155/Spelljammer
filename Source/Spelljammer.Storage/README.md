# Spelljammer.Storage

This headless project owns reusable local-file transaction mechanics shared by
game settings and campaign saves. It provides:

- a minimal mockable file-system boundary;
- durable same-directory staging;
- read-back validation supplied by the calling domain;
- atomic promotion with an exact recovery artifact;
- bounded recovery validation and canonical rewrite; and
- cleanup limited to the exact temporary or recovery path.

It does not understand settings JSON, campaign envelopes, content IDs, save
schemas, or domain diagnostics. Callers encode their data and provide a
validation callback before any staged value is published.
