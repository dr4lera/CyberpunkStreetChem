# CyberTrap / Street Chem — v0.3.0

Play in Cyberpunk's Night City while Schedule I runs real growing, inventory, drug effects and dealers, and Nivalis Nights runs real restaurants and bars. Purchases and business settlements use V's eddies.

**Experimental Windows x64 solo mods. All three games run locally.** The all-in-one launcher/framework installer is a separate development project and is not included in this release.

## Downloads

Use [published releases](https://github.com/dr4lera/CyberpunkStreetChem/releases), selecting v0.3.0. Automatic GitHub source ZIPs contain no playable binaries.

- `CyberTrap-Cyberpunk2077-v0.3.0.zip`: StreetChem host/renderer/drug dealer and Nivalis business host/manager scripts.
- `CyberTrap-ScheduleI-v0.3.0.zip`: native Schedule I guest bridge.
- `CyberTrap-NivalisNights-v0.3.0.zip`: native business bridge and MIT-licensed ModKit 0.6.1, built from pinned upstream source.

Read [installation for all three games](docs/CYBERTRAP_INSTALL.md), [Nivalis business guide](docs/NIVALIS.md), [controls](docs/CONTROLS.md), [troubleshooting](docs/TROUBLESHOOTING.md) and [verification](docs/TESTING.md).

## Night City businesses

Managers reuse vanilla NPC models at existing restaurants and bars. Each is labelled with its Nivalis business. **F to Talk**, **Q/E or Up/Down** to select V's reply, then **F** to confirm. Replies cover status, menu prices, opening hours, native qualified crews, all-business supplies, income/cost settlements and starting/pausing operations.

Nivalis owns ingredients, staff, equipment, seating, customers, reviews, wages and rent. Continuous purchasing targets ten servings of every menu dish plus the entire native shopping list. V pays the actual delivered-stock cost and receives/pays net operating settlements. The business simulation freezes when Cyberpunk is paused or disconnected. Profit is not guaranteed.

The tested sandbox has all 192 recipes and 15 ready venues with 20 dishes each, seating, all seven appliance types and 75 independent qualified employees. Normal saves remain observe-only; mutations require the exact configured dedicated save. Story dialogue and curfew are suppressed only there.

## Street Chem

- **Ctrl+B** buys a grow tent on a flat floor; **Ctrl+E** plants, waters or harvests its pot. Harvest is bagged automatically.
- **Ctrl+N** selects product; **Ctrl+S** near a civilian sells stock or manages a runner.
- Consume native products through **Inventory → Backpack → Consumables** for timed highs, benefits and drawbacks.
- Visit the H10 hallway dealer and choose **Browse drugs** for Cyberpunk's native vendor screen.

Read [consumables](docs/CONSUMABLES.md), [dealer guide](docs/DEALER.md) and [runner controls](docs/CONTROLS.md). The optional local icon converter reads artwork from your own Schedule I installation; retail artwork is not distributed.

## Requirements and limits

Tested games: Cyberpunk **2.31**, Schedule I **0.4.6f13 IL2CPP**, Nivalis **1.0 patch 3 hotfix 25738165**. Exact framework versions and official sources: [installation](docs/CYBERTRAP_INSTALL.md).

Schedule equipment is an image layer: Night City walls do not occlude it and it adds no Cyberpunk collision. Multiplayer, apartments, shared Schedule I business premises, automatic three-game startup and hidden guests are not included in this mod release. Only the first restaurant manager has received live location testing; other automatic map-point placements need broader checks.

## Source

`host/`, `guest/`, `native/`: StreetChem. `nivalis/`: Nivalis guest and Cyberpunk business bridge. `docs/`: all three games. `tools/PackageCyberTrap.ps1`: current three-game packages. The older two-game installer is retained for legacy StreetChem builds.

Original code is MIT licensed. AI-assisted implementation by Codex for dr4lera. Framework and ModKit authors retain their licenses and credit. No retail assemblies, assets, saves, tokens or private transaction journals are distributed. See [credits](CREDITS.md), [notices](THIRD_PARTY_NOTICES.md) and [build instructions](docs/BUILD.md).
