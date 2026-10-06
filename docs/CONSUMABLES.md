# Schedule I products in Cyberpunk's inventory

## Actual native product icons

The guest exports each owned product's own inventory sprite to `%LOCALAPPDATA%\StreetChem\icon-source` during an inventory observation. Your personal icon archive is built locally from these files and is never included in a public download. New mixed products or packaging variants need another local icon cook and Cyberpunk reload.

1. Install the mod and load the solo Schedule I save. Run both games once so the Cyberpunk bridge observes the guest inventory and exports its icons.
2. Close Cyberpunk. Schedule I can remain running.
3. Install [WolvenKit Console](https://github.com/WolvenKit/WolvenKit/releases) and the .NET 10 runtime. The release's combined ZIP contains this mod's atlas converter. Source users need the .NET 10 SDK to build it.
4. Run from the extracted combined ZIP:

```powershell
pwsh -NoProfile -File .\tools\BuildLocalIcons.ps1 `
  -WolvenKitDir 'D:\Tools\WolvenKit.Console' `
  -CyberpunkPath 'D:\Games\Cyberpunk 2077' -Install
```

5. Restart Cyberpunk. The locally generated `archive/pc/mod/streetchem_local_icons.archive` provides the original native product artwork in inventory. The converter backs up a previous archive in its local staging folder and never overwrites game assets. It uses Cyberpunk's own installed compression DLL through WolvenKit's local cache.

Do not upload the generated PNGs, textures or archive. Code/converter files may be shared. A missing local archive causes missing product art; item names and stock transactions remain part of the mod code.

Load paired **solo** saves in both games and leave Schedule I unpaused. Your owned native product stacks appear automatically in **Inventory → Backpack → Consumables** in Cyberpunk. Mixed products keep their native name, quality and packaging. No new gameplay key binding is required.

Select a product and use Cyberpunk's normal **Consume / Use** action. The bridge debits one package from the matching native Schedule I stack and waits for its save before starting the high in Cyberpunk. Selling or giving stock to a runner updates the same inventory count. The Cyberpunk items are mirrors of native stock; they are protected from ordinary dropping, disassembly and vendor resale. They cannot create an extra copy of stock.

One use consumes **one native package**, not one unit inside a package. For example, a `(5)` jar uses the entire five-unit jar. The tooltip states the package size. Products with different quality or packaging use different item records.

| Native drug family | High | Drawback during high | Aftereffect |
|---|---|---|---|
| Meth / meth-based mixes | 120s: movement +25%, firearm rate +25%, vivid saturation/contrast, subtle tint and gentle pulse | Maximum recoil kick +20% | 45s: movement -15%, firearm rate -10%, stamina regeneration -15%, muted colors |
| Cocaine / cocaine-based mixes | 90s: movement +15%, firearm rate +15%, stamina regeneration +10%, lighter vivid-color effect | Maximum recoil kick +15% | 35s: movement -10%, firearm rate -10%, stamina regeneration -10%, muted colors |
| Cannabis / cannabis-based mixes | 180s: stamina regeneration +20% | Movement -10%, firearm rate -5%, slight peripheral darkening | 30s: movement -5%, muted colors |
| Shrooms / shroom-based mixes | 150s: jump height +20%, stronger vivid-color effect | Movement -10%, peripheral darkening | 30s: movement -5%, muted colors |

These are Street Chem's Cyberpunk gameplay profiles, not a reproduction of every Schedule I mixing-property effect. Quality is preserved for item identity and native sales; it does not currently multiply the buff. A new dose replaces the active high/aftereffect and restarts its timer; buffs do not add up without limit.

Movement effects use Cyberpunk's native status effect system. Firearm effects change the equipped weapon's firing cycle and follow weapon switches. They do not change reload or charging time. Weapon-specific caps or mechanics can limit the final firing rate. All temporary weapon modifiers are removed at the end or when the player detaches. Color effects require the installed ReShade add-on and enabled **StreetChemLab** technique. Color saturation has a short watchdog and resets if the gameplay bridge stops.

The large-stack candidate uses a maximum capacity of 99,999,999 packages per mirrored record, matching Cyberpunk's native quantity-stat limit, with zero mirrored weight. It does not reduce or truncate Schedule I's saved stack, or rewrite global native inventory limits. Existing oversized stacks can be debited through native `SetQuantity` without adding them through Schedule I's normal capacity checks. The bridge needs no free native slot to consume an existing product.

Create a Cyberpunk manual save after consuming or selling. Preserve `%LOCALAPPDATA%\StreetChem` with paired save backups: it contains item catalog metadata and saved stock receipts. Loading arbitrary mismatched retail saves is still unsupported. Consumption requires the paired loaded single-player guest; an offline mirror cannot grant a free high. Native saves are not rolled back when an older Cyberpunk save is loaded.

## Buying consumables

Buy native packages from the [H10 apartment dealer](DEALER.md). Bought stock is also available for sales and runner supply. New variants export icons locally; re-run the local converter and restart Cyberpunk to show artwork for them.

## Inventory eddy value

The displayed inventory value is the exact per-package civilian selling price from native Schedule I stock, including quality and packaging, at 10 eddies per native dollar. It is not the base Cyberpunk Drug record’s 7,000-eddy value. Runner proceeds can be lower because of their native commission. The dealer charges a 25% retail markup; his Buy price is shown separately in the vendor screen.
