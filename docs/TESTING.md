# Verification

## Gameplay evidence inherited from solo development

- Tent anchors restored after a Cyberpunk restart without replanting.
- Actual native weed growth progressed from 0 through approximately 0.48 to 1 after the native 4 AM cutoff repair.
- Native harvest and automatic bagging confirmed by the tester.
- A civilian sale debited native stock 8 to 7 and increased Cyberpunk eddies by the quoted 650.
- Tester confirmed native Cyberpunk NPC runners work.

## Release-specific checks

The release source removes personal test grants/compensation and multiplayer prototypes. The following checks passed for v0.1.0:

- Both native C++ modules and the .NET 6 guest compiled from the portable source build.
- All three public redscript files compiled against Cyberpunk 2077 2.31.
- Source validation checked required documentation, PowerShell syntax, secrets, personal data and prototype exclusion.
- The extracted release ZIP installed all seven payloads with matching SHA-256 hashes into isolated test game folders.
- Installer `-WhatIf` made no changes; existing ReShade settings and techniques survived installation.
- Backup restoration recovered original files and removed added payloads.
- A deliberately corrupted release payload was rejected before installation.
- Publication lint checked the source and package for secrets, decompiler fingerprints, personal paths and copied retail files.

The sanitized release build has not received a separate full gameplay session; the gameplay observations above came from development on this same setup.

Runner collection previously overflowed the wallet. This version bounds payouts to one million and wallet capacity, and waits for actual native cash debit and saving. This particular repaired collection needs an in-game before/after check; do not infer it passed from compilation.

## Limits

Only the stated Windows/game/loader versions were used. No second PC/account was available; multiplayer is not shipped. Arbitrary mismatched retail-save rollback, all civilian AI variants, every graphics configuration and new installations on other computers have not been verified.
