# Verification

## v0.2.0 — V is Using now

Observed in the installed solo candidate on October 6, 2026:

- The corrected Cyberpunk backpack displayed actual locally converted product sprites and native names, including **Sexy Diesel (5) — Premium**, with descriptive buff/crash text.
- The normal inventory Consume action produced saved native receipts: Strawberry Diesel **20473 → 20472**, OG Kush **20 → 19**, and Sexy Diesel **6 → 5**, with no eddies charged.
- Oversized native meth stock was debited exactly: **16777212 → 16777211 → 16777210**. Cyberpunk abbreviates large grid counts as **9999+**; that is not its stored receipt count.
- The first candidate's Health Booster display and repeated refill/loot notices were corrected by new drug records, dedicated backpack hooks, larger stack capacity and silent synchronization.
- Meth's native movement value changed from approximately **3.5 to 4.375** during the high, matching +25%, and later returned to **3.5** after the high/crash expired. The timed state reached phase 0.
- Equipped-weapon cycle-time modifiers were observed during the high. Rate changes follow the active weapon and are removed by the cleanup path; a controlled shot-interval test across all weapon types was not performed.
- The renderer received the meth visual state. The tester reported the initial color effect was visible but subtle, then confirmed the strengthened saturation/contrast, tint and gentle pulse were “much better.” That strength is included in the release. Color intensity is not calibrated for every HDR/graphics setup.
- Six native product sprites (including the new mushroom bag) were exported at 256×256 with alpha, cooked to `.xbm` plus `.inkatlas`, packed locally, and observed in Cyberpunk. Those generated assets are not distributed.
- The C++ host/render modules, .NET 6 guest, standalone .NET 10 IconCooker, and all five redscript files compiled (including the dealer against Codeware 1.20.5). The scriptable TweakXL initialization completed in-game.

The new candidate preserves the solo gameplay/runner paths. This is not a new exhaustive runner payout test. Cocaine/shroom profiles, every mixed-product property, all weapons and arbitrary save rollbacks have not received exhaustive validation. See the inherited observations and limits below.

Additional live v0.2.0 checks:

- The normal Browse drugs prompt opened Cyberpunk’s native vendor screen. Native shop receipts recorded repeated mushroom purchases for **813 eddies per bag**, adding stock **0 → 1 → 2 → 3**; the offer replenished after purchase.
- After repair, the tester confirmed consumption worked again. Cyberpunk reported the shroom high with native movement **3.5 → 3.15**, matching its -10% drawback, and native stock debits were recorded.
- A capacity-clamping failure in the first shop candidate was found and repaired before release. The affected private stack was restored while preserving later legitimate sales. The released code uses the native instance quantity field plus its normal change notifications for positive credits, rather than the capacity-limited slot-add path.
- The corrected bridge passed a balanced real-game oversized-stack regression: meth **16777207 → 16777208 → 16777207**. Repeating the purchase commit with the same ID added no extra stock; consuming the test package saved an exact one-package debit.
- Native civilian sale receipts continued to debit stock and quote the native package value during shop testing. The original growing and runner observations below remain applicable; those full loops were not exhaustively re-run in this update.

- After the final price and spawn update, the tester reported that all features worked smoothly. The dealer was present at H10 with no pending purchase, and V was beside the corrected hallway location.
- The persistent price catalog matched native civilian package quotes: **meth 3500**, **shrooms 650**, **cocaine brick 30000**, **Strawberry Diesel jar 6300**, **OG Kush bag 380**, and **Sexy Diesel jar 7550** eddies on this save. Backpack price hooks now use this same keyed quote rather than the base Drug price; values vary with native products and quality.
- The final live meth high reported **4.375003** movement and an active timer, with no stuck consumption/purchase transaction.

The tester’s acceptance covers this installed solo build. It is not a guarantee across untested game versions, graphics configurations or mismatched save rollbacks.

## Gameplay evidence inherited from solo development

- Tent anchors restored after a Cyberpunk restart without replanting.
- Actual native weed growth progressed from 0 through approximately 0.48 to 1 after the native 4 AM cutoff repair.
- Native harvest and automatic bagging confirmed by the tester.
- A civilian sale debited native stock 8 to 7 and increased Cyberpunk eddies by the quoted 650.
- Tester confirmed native Cyberpunk NPC runners work.

## v0.2.0 package checks

- Both game-root ZIPs contained exactly the expected payloads: eight Cyberpunk files and one Schedule I DLL.
- All nine package payload hashes matched the installed build accepted by the tester.
- The extracted combined ZIP passed installer WhatIf, exact payload hash installation, existing preset/config preservation, backup restoration and corrupt-payload rejection.
- Publication lint found zero failures/warnings in the public source snapshot and combined package. A separate size/hash comparison found no retail or dependency files copied into the package; the nine exact author-owned installed mod paths were excluded from that retail comparison.
- Generated local icons, retail assemblies, saves, private test tools and multiplayer prototypes were excluded.

## Release-specific checks inherited from v0.1.0

The release source removes personal test grants/compensation and multiplayer prototypes. The following checks passed for v0.1.0:

- Both native C++ modules and the .NET 6 guest compiled from the portable source build.
- All three public redscript files compiled against Cyberpunk 2077 2.31.
- Source validation checked required documentation, PowerShell syntax, secrets, personal data and prototype exclusion.
- The extracted release ZIP installed all seven payloads with matching SHA-256 hashes into isolated test game folders.
- Installer `-WhatIf` made no changes; existing ReShade settings and techniques survived installation.
- Backup restoration recovered original files and removed added payloads.
- A deliberately corrupted release payload was rejected before installation.
- Both game-specific ZIPs contain exactly their expected game-root paths (six Cyberpunk files, one Schedule I file), with bytes identical to the original released payload. Their publication lint has no failures; the missing-README warning is intentional because these archives contain only installable mod files and documentation is provided in the repository/combined download.
- Publication lint checked the source and package for secrets, decompiler fingerprints, personal paths and copied retail files.

The v0.1.0 observations above were inherited from development. The v0.2.0 public-source candidate received the live checks and tester acceptance listed at the top of this document.

Runner collection previously overflowed the wallet. This version bounds payouts to one million and wallet capacity, and waits for actual native cash debit and saving. This particular repaired collection needs an in-game before/after check; do not infer it passed from compilation.

## Limits

Only the stated Windows/game/loader versions were used. No second PC/account was available; multiplayer is not shipped. Arbitrary mismatched retail-save rollback, all civilian AI variants, every graphics configuration and new installations on other computers have not been verified.

## Nivalis / CyberTrap v0.3.0

Verified on the documented retail builds: all 15 owned venues served real customers in bounded native business-day tests; native setup objectives empty after cold reload; 75 globally unique employees with correct workplaces, independent cook/server coverage and native skill caps; 64 added tables/256 chairs inside authored bounds; all venues have menus/seating/equipment. No fake stock or profit is used.

Actual stock reservations, full-list delivery, unused-fund refunds and repeated same-day replenishment passed, including cold reload. Positive income and negative operating settlements reached V. An isolated paper-test mode prevents unsaved simulations from spending or collecting real host money. Failed tests were recovered from verified private checkpoints; private receipts/saves are not shipped.

First manager placement/facing, business labels, reply highlighting and menu page opening passed in Cyberpunk. Wider testing of the other automatic restaurant/bar locations and menu/hour/crew edits remains necessary. Do not describe the mod as a finished total conversion or guarantee business profitability. Multiplayer, apartments, shared Schedule I interiors, hidden guests and the separate launcher remain outside this release.
