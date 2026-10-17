# CyberTrap2077 — v0.3.1

Play as V in Cyberpunk's Night City while your own Schedule I runs real growing, inventory, products and dealers. Purchases and sales use V's eddies.

**Experimental Windows x64 solo mashup. Both games run locally.** Melty's Play prepares the supporting software, starts Schedule I, waits for its native bridge, uses native Continue when a save exists, then starts the actual Cyberpunk game and follows its full lifetime. No separate setup BAT or framework downloads are required from players.

Both games remain running, so allow enough RAM for Cyberpunk and Schedule I together. Removing Nivalis avoids running a third game; this release does not claim a measured RAM reduction.

## What you can do

- **Ctrl+B** buys a grow tent on a flat floor; **Ctrl+E** plants, waters or harvests its pot. Harvest is bagged automatically.
- **Ctrl+N** selects product; **Ctrl+S** near a civilian sells stock or manages a runner. NPC runners use real recruited Schedule I dealers.
- Consume native products through **Inventory → Backpack → Consumables**, with timed benefits, drawbacks and crash phases. Native saved stock is debited before effects apply.
- Visit the H10 hallway dealer and choose **Browse drugs** for Cyberpunk's native vendor screen. Purchases add real Schedule I packages and reserve capacity before charging V.
- Growing, stock, quality, packaging, native day rollover and transaction receipts come from your own running Schedule I game. Keep corresponding saves after transfers.

## Play and requirements

Get **CyberTrap-Melty-v0.3.1.zip** and its dependency-notices companion on the existing Melty listing. Melty installs all required files into their matching games. Automatic GitHub source ZIPs contain no playable binaries.

Cyberpunk **2.31** (file version 3.0.80.51928) is the host. Schedule I must use its **IL2CPP** branch. The original build used **0.4.6f13**; this repair also adapts the clock bridge to **0.4.7f12**, where sleeping and time-skip APIs moved. Mono/beta is unsupported.

Use Cyberpunk's normal Continue/load menu. If you have never played Schedule I, create your character and native save in its normal game menu. Both games keep their native windows and existing graphics settings. Close an already running session normally before pressing Play.

The launcher includes its own runtime, Codeware, MelonLoader, ReShade full-add-on support, the shader include and a private .NET 6 runtime for the Unity loader. Melty supplies RED4ext, redscript, TweakXL and ArchiveXL. Setup backs up changed settings, preserves unrelated ReShade techniques, refuses conflicting proxy files, and never force-closes games. First-time Unity interface generation may take several minutes. Startup errors are saved locally for diagnosis.

## Scope and limits

Nivalis businesses are **temporarily removed from this release at the creator's request**. Nivalis is not required, installed, configured or started. Upgrades preserve but disable this project's old Nivalis host scripts/plugin, so that half does not keep running in Cyberpunk.

Schedule equipment is an image layer: Night City walls do not occlude it and it adds no Cyberpunk collision. Multiplayer, apartments, shared Schedule I interiors and hidden guest windows are not included. The optional icon-converter source remains in the repository for developers; this release does not require it or distribute retail artwork.

Read the [controls](docs/CONTROLS.md), [consumables](docs/CONSUMABLES.md), [dealer guide](docs/DEALER.md), and [verification](docs/TESTING.md). Older manual-install and Nivalis guides describe earlier releases.

## Source and credits

Original code is MIT licensed. Concept and testing: zrock/dr4lera. AI-assisted implementation with OpenAI Codex. Third-party loaders and libraries retain their licenses and author credits; their notices accompany the package. No retail assemblies, game assets, saves, tokens or private transaction journals are distributed. See [credits](CREDITS.md), [notices](THIRD_PARTY_NOTICES.md), and [build instructions](docs/BUILD.md).

Current release packaging is in tools/PackageMelty.ps1. The inactive Nivalis source stays in the repository for future work.
