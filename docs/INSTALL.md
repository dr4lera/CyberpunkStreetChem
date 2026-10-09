> For the current three-game v0.3.0 release use [CYBERTRAP_INSTALL.md](CYBERTRAP_INSTALL.md). This page describes legacy v0.2.0.

# Install solo v0.2.0

Use Windows x64 and owned copies of Cyberpunk 2077 **2.31** and Schedule I **0.4.6f13 IL2CPP**. Run both games normally once before modding. Back up your own saves before trying a new mod.

## Dependencies

Install these separately from their official projects:

| Game | Dependency | Tested version / setup |
|---|---|---|
| Cyberpunk | [RED4ext](https://github.com/WopsS/RED4ext/releases) | 1.30.0; extract following the loader's installation instructions |
| Cyberpunk | [redscript](https://github.com/jac3km4/redscript/releases) | 0.5.31; install following its README |
| Cyberpunk | [TweakXL](https://github.com/psiberx/cp2077-tweak-xl/releases) | 1.11.4; required for the inventory consumable records and timed profiles |
| Cyberpunk | [Codeware](https://github.com/psiberx/cp2077-codeware/releases) | 1.20.5; required for the persistent apartment dealer |
| Cyberpunk | [Codeware](https://github.com/psiberx/cp2077-codeware/releases) | 1.20.5; required for the persistent apartment dealer |
| Cyberpunk | [ReShade](https://reshade.me/) | **6.8.0 with full add-on support**, select `bin\x64\Cyberpunk2077.exe` and DirectX 10/11/12; install **Standard effects** so `ReShade.fxh` is available |
| Schedule I | [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3) | 0.7.3, x64 IL2CPP; select `Schedule I.exe`; launch once to generate `MelonLoader\Il2CppAssemblies`, then close it |

Street Chem does not bundle or replace those runtimes. A normal ReShade build without full add-on support will not load the image transport. Follow MelonLoader's runtime requirements if it asks for .NET. Do not mix Mono/beta-game assemblies with this IL2CPP build.

## Separate game ZIPs (mod managers or manual installation)

Both halves are required. These ZIPs start directly at each game's installation root; they have no `payload/` wrapper, dependency binaries or installer scripts.

| Release asset | Assign to / extract into | Contents |
|---|---|---|
| **StreetChem-Cyberpunk2077-v0.2.0.zip** | Cyberpunk 2077 root (contains `bin`, `r6`, `red4ext`) | Eight host files under `bin/x64`, `r6/scripts/StreetChem`, `red4ext/plugins/StreetChem` |
| **StreetChem-ScheduleI-v0.2.0.zip** | Schedule I root (contains `Schedule I.exe`) | `Mods/StreetChem.Guest.dll` |

For an upload/import form that asks which game each ZIP belongs to, assign the Cyberpunk ZIP to Cyberpunk and the Schedule I ZIP to Schedule I. Checksums are verification text, not installable mods. The combined Solo ZIP is the alternative automatic installer, not a third required mod.

Install the dependencies above first, close both games, and back up any existing StreetChem files before replacing them. Extract each archive into its matching game root, or have your mod manager install it there.

**Cyberpunk ReShade setup is still required when using the split ZIPs:**

1. Copy `ReShade.fxh` from your own official Standard effects installation into `bin/x64/streetchem/shaders/` beside `StreetChemLab.fx`.
2. In Cyberpunk's ReShade overlay (**Home**), add `.\streetchem\shaders` to the effect search paths alongside your existing paths, then reload effects.
3. Enable **StreetChemLab** in your selected preset. Keep other desired effects enabled. Full add-on support is required for `StreetChemRender.addon64`.

The split archives do not edit ReShade settings or create the automatic installer's backup journal. To uninstall these halves, close both games and remove the nine StreetChem payload files listed below, then disable StreetChemLab/remove its added shader search path. Restore any previous mod files from your own backup. Retain game saves and `%LOCALAPPDATA%\StreetChem`.

## Combined download with automatic installer

1. Download the **StreetChem-Solo-v0.2.0.zip** release asset and extract it to a normal folder. GitHub's automatic source-code ZIP does not contain playable binaries.
2. Close both games. The installer refuses to stop games or write while the selected copies are running.
3. Open PowerShell in the extracted folder and run, substituting your paths:

```powershell
pwsh -NoProfile -File .\Install.ps1 `
  -CyberpunkPath 'D:\Games\Cyberpunk 2077' `
  -ScheduleIPath 'D:\SteamLibrary\steamapps\common\Schedule I'
```

Windows PowerShell 5.1 users can replace `pwsh` with `powershell`; if local policy blocks downloaded scripts, use `-ExecutionPolicy Bypass` for this single invocation after reviewing the script. No global policy change is required.

If your ReShade Standard effects are elsewhere, add:

```powershell
-ReShadeIncludePath 'D:\Games\Cyberpunk 2077\bin\x64\reshade-shaders\Shaders\ReShade.fxh'
```

Add `-WhatIf` to check requirements and see the proposed action without changing files. The installer verifies the release hashes, backs up files it changes under `%LOCALAPPDATA%\StreetChem\install-backups`, installs nine mod files, and enables `StreetChemLab` alongside the selected ReShade preset's existing techniques. It copies your own installed ReShade include locally; that dependency is not in our download.

The installed payload is:

```text
Cyberpunk root/
  red4ext/plugins/StreetChem/StreetChemHost.dll
  r6/scripts/StreetChem/StreetChemCity.reds
  r6/scripts/StreetChem/StreetChemRunners.reds
  r6/scripts/StreetChem/StreetChemVisual.reds
  r6/scripts/StreetChem/StreetChemConsumables.reds
  r6/scripts/StreetChem/StreetChemDealer.reds
  r6/scripts/StreetChem/StreetChemDealer.reds
  bin/x64/StreetChemRender.addon64
  bin/x64/streetchem/shaders/StreetChemLab.fx
Schedule I root/
  Mods/StreetChem.Guest.dll
```

No Schedule I ReShade installation is needed for solo equipment rendering. Do not install the old development guest capture add-on; it is not part of this release.

## First launch and play

For actual Schedule I artwork in the inventory, follow the [local icon conversion steps](CONSUMABLES.md). The generated archive comes from your own installed product sprites. It is not redistributed in either game ZIP. v0.2.0 replaces old booster-shaped mirrors with the corrected drug records and silences synchronization loot notices.

1. Start Schedule I, load a **single-player** business save, and leave it unpaused. Own a saved property with free build space; the tutorial RV is not the recommended production property. Unlock/buy access to OG Kush seeds in your native save first.
2. Start Cyberpunk and load a save. Leave both games running on the same Windows account. Schedule I main menu/loading does not count as a loaded business.
3. Ensure Cyberpunk's ReShade `StreetChemLab` technique is enabled and full add-on support is active. In-game **Home** opens ReShade; no function keys are used by this mod.
4. Close Cyberpunk menus. Aim at a flat floor within eight metres and **Ctrl+B** to buy a tent (1200 eddies). Have enough native Schedule I property space and inventory room.
5. Aim at its pot and **Ctrl+E** to plant; **Ctrl+G** inspects. While growing, **Ctrl+E** waters. When mature, **Ctrl+E** harvests and bags automatically.
6. **Ctrl+N** selects product; aim at a living civilian within four metres and **Ctrl+S** sells one unit. For runners and every shortcut, read [CONTROLS.md](CONTROLS.md).
7. Create a Cyberpunk manual save after placing a tent and after earning eddies. Keep the latest paired saves and `%LOCALAPPDATA%\StreetChem` together.

## Uninstall / roll back

Close both games. Use the backup path printed by the installer:

```powershell
pwsh -NoProfile -File .\Restore.ps1 -BackupPath 'C:\path\printed\by\installer'
```

This restores changed files and removes files added by this installation. It leaves subsequently edited files alone with a warning. It does not delete game saves or your StreetChem business journal. Loaders remain installed; remove them separately using their official instructions if desired. Never delete a shared `dxgi.dll` blindly.

## H10 apartment dealer

The dealer uses your own vanilla Cyberpunk NPC model through Codeware. No NPC archive is bundled. See [DEALER.md](DEALER.md) for location, shopping controls, pricing and save recovery. Install Codeware before upgrading to this release.

## H10 apartment dealer

Install Codeware before upgrading. The dealer uses your own vanilla NPC model; no NPC archive is bundled. Read [DEALER.md](DEALER.md) for location, controls and purchases.
