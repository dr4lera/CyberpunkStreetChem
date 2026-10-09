# v0.3.0 — CyberTrap / Nivalis businesses

- Added Nivalis guest and Cyberpunk business host source to the StreetChem mod repository.
- Native restaurant/bar managers with business labels, V-style replies, menus/prices, hours, crews, supply purchases, net settlements and operation controls.
- Independent qualified staff, safe authored dining expansion, compatible kitchen setup, continuous all-menu/all-list purchasing and preserved native wages/rent.
- Dedicated-save mutation guards, curfew/story suppression, receipt recovery and isolated paper tests.
- Three separate game-root packages and documentation for all three games.
- Existing StreetChem v0.2.0 gameplay retained. Launcher/framework installer stays separate and is not part of this release.

# Changelog

## 0.2.0 — V is Using now

- Added a persistent H10 apartment drug dealer: normal Browse drugs interaction and native vendor purchases with unlimited supply; Ctrl+Shift+Y teleports home outside combat. Requires Codeware.
- Restore per-package inventory eddy values to match actual civilian sales, instead of the inherited 7,000-eddy Drug price.
- Preserve over-capacity stacks on positive purchase credits and retain native removal for consumption.
- Replace the old indoor dealer spawn with the saved hallway version.
- Purchases reserve native space before charging, extend existing over-capacity stacks, and retain recovery records and idempotent receipts.

- Mirror owned native products into Cyberpunk's normal consumables inventory, including custom mixes, quality and package size.
- Use the native Consume action; saved Schedule I stock debits precede Cyberpunk effects.
- Add distinct timed meth, cocaine, cannabis and shroom buffs/debuffs, plus crash phases.
- Meth increases movement and firearm rate by 25% and makes colors more vivid; drawbacks and timed cleanup prevent permanent buffs.
- Add persistent item catalog metadata, paired-save checks and large-stack capacity without changing global Schedule I inventory limits.
- Require TweakXL for custom inventory records; retain the solo-only bridge.
- Convert actual native product sprites into a private Cyberpunk icon archive using the player's own game files.
- Fix Health Booster names, large-stack replenishment loops and repeated loot notices; migrate the first candidate's mirror records.
- Strengthen meth color saturation/contrast with a subtle tint and gentle pulse; restore normal color when the high ends.

## 0.1.0 — first solo release

- Grow tent placement, native weed growth, watering and inspection.
- Automatic harvesting and bagging; refill and product selection.
- Nearby civilian sales and real native stock receipts.
- Cyberpunk NPC runners tied to recruited Schedule I dealers.
- Contextual cash/stock menu; bounded payouts to prevent integer overflow.
- Durable placement anchors and native morning rollover.
- Portable installer with backups, rollback and file hashes.
- Separate Cyberpunk and Schedule I game-root ZIPs for mod manager imports, alongside the combined installer download.
- Public build excludes personal compensation, test-money grants and multiplayer prototypes.

This is an experimental solo release; see documented verification and limits.
