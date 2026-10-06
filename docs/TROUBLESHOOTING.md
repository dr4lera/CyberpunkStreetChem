# Troubleshooting

## Nothing appears or shortcuts do nothing

Load the paired business save in Schedule I, close its pause menu, and wait for loading to finish. Close Cyberpunk menus and focus the actual game window before pressing shortcuts. Both games must run on the same Windows user account. Check these logs:

- Schedule I: `MelonLoader\Latest.log` should say **StreetChem pipe started**. Missing generated IL2CPP references usually means MelonLoader has not completed its first launch.
- Cyberpunk: `r6\logs\redscript_rCURRENT.log` should list all four StreetChem scripts and **Compilation complete**. TweakXL must be installed for consumables.
- Cyberpunk: newest log in `red4ext\logs` should show `StreetChemHost.dll` loaded.
- Cyberpunk: `bin\x64\ReShade.log` should show the add-on loaded. Use the **full add-on support** build and enable `StreetChemLab` in the current preset.

The tent will reconnect automatically after the real guest save finishes loading. Do not keep buying replacements while the guest is at its menu.

## No growth

Schedule I must be loaded and unpaused, with soil, light and water present. Aim at the tent and **Ctrl+G** to inspect; **Ctrl+E** waters a growing plant. The bridge advances the native 4 AM cutoff to morning only while Cyberpunk is active; manually pausing Schedule I still pauses production.

## Pot missing or lost tent

Load the correct Schedule I save first. A stale missing link is removed when reported. Aim at the real remaining tent and retry. **Ctrl+Shift+B** reconnects an unlinked native tent at the floor you aim at without creating or replanting one. Save Cyberpunk afterward. Use it only for recovery.

## Sale pending

Native Schedule I saves must finish before payments are acknowledged. Keep both games loaded. Verify the target is a live, non-aggressive civilian within four metres and inventory has sellable product. Each buyer has a 60-second cooldown. Do not switch guest saves mid-trade. Retry messages do not authorize deleting stock or receipt journals.

## Runner cash and stock

**Ctrl+S** aimed at an assigned runner opens a menu; a second **Ctrl+S** collects. Giving stock uses **Ctrl+S, Ctrl+N, Ctrl+S**, after selecting the product outside the menu. Collection is capped at one million eddies and remaining wallet capacity. Very large native float balances have coarse increments. Stock handoff charges no eddies. Use current paired saves; arbitrary mismatched rollback can lose newer payments.

## Wrong inventory names, missing artwork or loot messages

Use matching v0.2.0 halves and remove obsolete development copies of the same scripts. The backpack uses new drug records; old Health Booster mirrors are migrated away automatically. Synchronization is silent. Actual artwork needs the [local icon converter](CONSUMABLES.md#actual-native-product-icons) and a Cyberpunk reload. Generated textures/atlases are local files from your own native product sprites and are not in the release ZIP. Rebuild the local archive after adding new mixed products or packaging variants.

Cyberpunk displays **9999+** for very large grid counts; this is a UI abbreviation. Stock receipts contain exact before/after counts, and the native saved stack is debited. The capacity for a mirrored record is 99,999,999 packages. One Consume action uses a whole native package; a five-unit jar consumes that jar.

## No consumption effect or muted visuals

Consumption waits for the native Schedule I save. Finish other sales first and keep the paired solo guest loaded. The tooltip documents each family's high and crash. A new dose replaces the previous profile. The normal inventory action works inside Cyberpunk menus; menu time pauses the high's timer.

Enable **StreetChemLab** in your active ReShade preset, with full add-on support. Meth increases saturation, contrast and brightness slightly, adds a subtle warm tint, and gently pulses contrast. It resets after the high; the crash temporarily mutes colors. Use **Home → Reload** if you updated only the shader while playing. Headlights, HDR and scene lighting affect the apparent strength. The optional Ctrl+Alt+L full-screen laboratory diagnostic is not the drug effect and should stay off during normal play.

## Compatibility limits

Solo only. No shared quests, combat or multiplayer. Equipment is a camera-matched transparent layer: it has no Night City collision/depth occlusion and can overlap HUD/menu content. Native plant rules, native property space and seed unlocks still apply. Crowd NPC bodies can stream out; reassign a body to the same dealer if needed. Performance depends on running both games. Other graphics injectors and mods touching the same shortcuts or NPC AI may conflict. Only the versions in INSTALL.md were tested.

## Apartment dealer missing or purchase blocked

Install Codeware 1.20.5 and load both paired saves. The dealer is in the H10 hallway. **Ctrl+Shift+Y** teleports home outside combat. Look at him within four metres and use the normal **Browse drugs** interaction. Existing matching stacks may exceed native capacity; a new variant needs a free Schedule I hotbar slot. Rejected reservations do not charge V.

For a pending-purchase recovery warning, keep both paired saves and `%LOCALAPPDATA%\StreetChem` together. A mismatched stock/save rollback requires matching backups; repeated clicks cannot repair it. See [DEALER.md](DEALER.md).
