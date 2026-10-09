# Install CyberTrap v0.3.0 — all three games

Windows x64; owned Cyberpunk 2077 2.31, Schedule I 0.4.6f13 IL2CPP, and Nivalis Nights 1.0 patch 3 hotfix 25738165. Run each normally once; back up saves. Close selected games before replacing mod files.

## Frameworks

Install from the official projects following their instructions:

| Game | Framework | Tested version |
|---|---|---|
| Cyberpunk | [RED4ext](https://github.com/WopsS/RED4ext/releases/tag/v1.30.0) | 1.30.0 |
| Cyberpunk | [redscript](https://github.com/jac3km4/redscript/releases/tag/v0.5.31) | 0.5.31 |
| Cyberpunk | [Codeware](https://github.com/psiberx/cp2077-codeware/releases/tag/v1.20.5) | 1.20.5 |
| Cyberpunk | [TweakXL](https://github.com/psiberx/cp2077-tweak-xl/releases/tag/v1.11.4) | 1.11.4 |
| Cyberpunk | [ReShade](https://reshade.me/) | 6.8.0, **full add-on support**, DirectX 12 |
| Schedule I | [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3) | 0.7.3 x64 IL2CPP |
| Nivalis | [BepInEx Unity IL2CPP](https://builds.bepinex.dev/projects/bepinex_be) | build 788, Windows x64 |

ArchiveXL 1.27.3 was present in the test setup; these mods do not directly call it. Schedule I Mono/beta is unsupported. ReShade without full add-on support cannot load the renderer. Do not overwrite an unrelated graphics proxy DLL.

Run the Unity games once with their loaders and let generated interfaces finish before closing them. Follow MelonLoader's .NET requirements if prompted.

Nivalis's package includes ModKit 0.6.1, built from [upstream commit 65ce1e01afdb1d56f171761ff1ff619a4ec148ec](https://github.com/BGASM/NivalisModKit/commit/65ce1e01afdb1d56f171761ff1ff619a4ec148ec), under MIT. This is a source build; upstream currently publishes v0.6.0. Keep only one ModKit DLL installed.

## Extract the game-root packages

- `CyberTrap-Cyberpunk2077-v0.3.0.zip` into the root containing `bin`, `r6`, `red4ext`.
- `CyberTrap-ScheduleI-v0.3.0.zip` into the root containing `Schedule I.exe`.
- `CyberTrap-NivalisNights-v0.3.0.zip` into the root containing `Nivalis Nights.exe`.

A mod manager can assign each ZIP to its matching game. SHA256 files verify downloads; they are not mods. Frameworks, games, saves and credentials are not in these ZIPs. The separate launcher/installer is unfinished.

For StreetChem's renderer, copy `ReShade.fxh` from your installed official Standard effects into `bin/x64/streetchem/shaders/`, beside `StreetChemLab.fx`. In ReShade's Home overlay, add `.\streetchem\shaders` to effect search paths, reload effects, and enable StreetChemLab alongside your preferred effects.

## Configure the Nivalis pair

Create a dedicated native save copy and retain the original `.sav` and matching `.modkit.json`. Native venue setup stays in that copy. Configuration values use the exact copied save filename **without `.sav`**.

In `BepInEx/config/dr4lera.nivalis.nightcity.cfg`:

```ini
[Lab]
SaveName = YOUR_DEDICATED_SAVE_NAME
[Bridge]
CyberpunkPath = D:\Games\Cyberpunk 2077
CentralSupplies = true
[Sandbox]
AutoSetup = false
KeepStaffHappy = true
```

In `BepInEx/config/bgasm.nivalis.modkit.cfg`:

```ini
[DevBridge]
Enabled = true
AllowCommands = true
Port = 5710
```

Create `red4ext/plugins/NivalisNightCity/NivalisNightCity.json` in Cyberpunk with your own paths and the same save name:

```json
{
  "nivalisGame": "D:\\Games\\Nivalis Nights",
  "save": "YOUR_DEDICATED_SAVE_NAME",
  "autoRestock": true
}
```

Restart after configuration changes. Keep the kit's generated token private. Never copy another player's token, saves or financial journals. An unconfigured business host stays inactive, so StreetChem can run without an enabled Nivalis link.

## Play

Load the dedicated Nivalis save, your paired Schedule I save, then the corresponding Cyberpunk save. No automatic save loading or hiding is provided yet. Nivalis starts held: at a manager choose **Start business operations**, or press **Ctrl+Alt+U**. Cyberpunk gameplay renews its lease; pause/disconnection holds it again.

Managers stream in near existing food/bar map points. **F to Talk**, **Q/E or Up/Down** to select a reply, **F** to confirm. Menus/hours/crew edits checkpoint the dedicated Nivalis save. Initial venue setup does not buy ingredients for free: restock with enough eddies. Save all three games after transfers.

See [Nivalis setup commands and finance](NIVALIS.md), [controls](CONTROLS.md), and [troubleshooting](TROUBLESHOOTING.md).

## Upgrade / remove

Back up replaced files. With games closed, remove only these mods' DLLs/scripts/shader and disable StreetChemLab. Keep frameworks used by other mods. Keep saves and local transaction history; deleting journals during a payment breaks recovery. Uninstalling does not undo native setup in the dedicated save. The older [two-game install guide](INSTALL.md) is only for legacy StreetChem releases.
