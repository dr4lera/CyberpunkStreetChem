# Street Chem — solo v0.1.0

Grow and sell Schedule I products in Cyberpunk's Night City. Both games run on your own PC: Schedule I owns the plants, product inventory and dealer earnings; you play and interact in Cyberpunk.

**First experimental solo release. Multiplayer is not included.** Equipment uses a camera-matched image layer, so Night City walls do not yet occlude it and it does not create Cyberpunk collision. This is not a complete total conversion.

## Start here

1. Download from [Releases](https://github.com/dr4lera/CyberpunkStreetChem/releases/latest), rather than GitHub's source-code ZIP. Mod managers can use the separate **StreetChem-Cyberpunk2077-v0.1.0.zip** and **StreetChem-ScheduleI-v0.1.0.zip** archives. The original **StreetChem-Solo-v0.1.0.zip** remains available with the automatic installer for both games.
2. Install the required loaders and follow [installation](docs/INSTALL.md).
3. Load your paired saves in both games, then play Cyberpunk. Schedule I must be loaded and unpaused.
4. Aim at a flat floor: **Ctrl+B** buys a grow tent for 1200 eddies. Aim at its pot: **Ctrl+E** plants, waters or harvests according to its state. Harvest bags automatically.
5. **Ctrl+N** selects product; aim at a civilian within four metres and **Ctrl+S** sells it.

Read [all controls and runner instructions](docs/CONTROLS.md), [troubleshooting](docs/TROUBLESHOOTING.md), [verification](docs/TESTING.md), and [changelog](CHANGELOG.md).

## Included

- Native weed growing, automatic harvest/bagging and the native morning clock rollover.
- Saved Night City tent anchors linked to actual Schedule I equipment.
- Real stock debits and eddy payments for nearby civilian sales.
- Cyberpunk civilian runner bodies linked to recruited Schedule I dealers.
- Runner cash collection and giving stock through the Ctrl+S menu.
- No function-key gameplay bindings and no automatic test-money grants.

## Requirements

Windows x64; owned copies of **Cyberpunk 2077 2.31** and **Schedule I 0.4.6f13 (IL2CPP)**; sufficient resources to run both. Tested loaders: RED4ext 1.30.0, redscript 0.5.31, MelonLoader 0.7.3, and ReShade 6.8.0 with full add-on support in Cyberpunk. Existing test setup also had ArchiveXL 1.27.3 and TweakXL 1.11.4. Those two are not directly called by Street Chem and are not bundled. Other versions/platforms have not been verified.

The download contains this mod only. Install dependencies from their official projects. No game assets, saves, credentials or multiplayer prototype are included.

## Repository layout

| Folder | Purpose |
|---|---|
| `host/` | Cyberpunk controls, placements, sales and runner behavior |
| `guest/` | Schedule I native production/inventory/save bridge |
| `native/` | RED4ext host plugin and ReShade image transport |
| `generated/` | Committed transport/input constants |
| `installer/` | Portable install and backup restoration |
| `tools/` | Source build, release packaging and validation |
| `docs/` | Installation, controls, testing and troubleshooting |

For building from source, see [BUILD.md](docs/BUILD.md). Code is MIT licensed; retail game content remains with its owners. See [credits](CREDITS.md).
