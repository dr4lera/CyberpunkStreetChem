# Street Chem — solo v0.2.0

**V is Using now:** native inventory consumables, original locally converted product icons, timed highs with buffs, drawbacks and stronger color effects, and a drug dealer outside V’s H10 apartment.

Grow and sell Schedule I products in Cyberpunk's Night City. Both games run on your own PC: Schedule I owns the plants, product inventory and dealer earnings; you play and interact in Cyberpunk.

**Experimental solo release. Multiplayer is not included.** Equipment uses a camera-matched image layer, so Night City walls do not yet occlude it and it does not create Cyberpunk collision. This is not a complete total conversion.

## Start here

1. Use the matching v0.2.0 Cyberpunk and Schedule I packages, or the combined installer ZIP. Mod managers need both game-specific halves. Automatic GitHub source-code ZIPs contain no playable binaries. [Published releases](https://github.com/dr4lera/CyberpunkStreetChem/releases) may include older builds; select the version explicitly.
2. Install the required loaders and follow [installation](docs/INSTALL.md).
3. Load your paired saves in both games, then play Cyberpunk. Schedule I must be loaded and unpaused.
4. Aim at a flat floor: **Ctrl+B** buys a grow tent for 1200 eddies. Aim at its pot: **Ctrl+E** plants, waters or harvests according to its state. Harvest bags automatically.
5. **Ctrl+N** selects product; aim at a civilian within four metres and **Ctrl+S** sells it.
6. Open **Inventory → Backpack → Consumables** and use a Schedule I product through the normal **Consume / Use** action. Product names, quality and package counts follow your native stock. Read [consumables, buffs and crashes](docs/CONSUMABLES.md).

7. Visit the **H10 hallway drug dealer**, use the normal **Browse drugs** interaction, and buy packages in Cyberpunk’s vendor screen. Supply is unlimited. [Dealer guide](docs/DEALER.md).


For actual product artwork, run the [local icon converter](docs/CONSUMABLES.md#actual-native-product-icons). The public download contains the converter; your own Schedule I installation supplies the artwork.

Read [all controls and runner instructions](docs/CONTROLS.md), [troubleshooting](docs/TROUBLESHOOTING.md), [verification](docs/TESTING.md), and [changelog](CHANGELOG.md).

## Included

- Native weed growing, automatic harvest/bagging and the native morning clock rollover.
- Saved Night City tent anchors linked to actual Schedule I equipment.
- Real stock debits and eddy payments for nearby civilian sales.
- Cyberpunk civilian runner bodies linked to recruited Schedule I dealers.
- Runner cash collection and giving stock through the Ctrl+S menu.
- No function-key gameplay bindings and no automatic test-money grants.
- A persistent native Cyberpunk drug dealer outside V’s original H10 apartment, with real Schedule I stock purchases.
- A persistent native NPC drug dealer outside V’s original H10 apartment.
- Accurate inventory eddy values matching civilian package selling prices.
- Native Cyberpunk inventory consumables, shared stock debits, timed buffs/debuffs, firearm rate changes and drug color effects.

## Requirements

Windows x64; owned copies of **Cyberpunk 2077 2.31** and **Schedule I 0.4.6f13 (IL2CPP)**; sufficient resources to run both. Tested loaders: RED4ext 1.30.0, redscript 0.5.31, MelonLoader 0.7.3, **TweakXL 1.11.4**, **Codeware 1.20.5** and ReShade 6.8.0 with full add-on support in Cyberpunk. TweakXL is required for inventory consumables; Codeware spawns the apartment dealer. The existing test setup also had ArchiveXL 1.27.3, which Street Chem does not directly call. Loaders are not bundled. Other versions/platforms have not been verified.

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

For building from source, see [BUILD.md](docs/BUILD.md). Core mod code is MIT licensed. The separate optional IconCooker uses GPL-3.0-only; its source/license are included beside the converter. Retail game content remains with its owners. See [credits](CREDITS.md).
