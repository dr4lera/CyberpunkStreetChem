# Troubleshooting

## Nothing appears or shortcuts do nothing

Load the paired business save in Schedule I, close its pause menu, and wait for loading to finish. Close Cyberpunk menus and focus the actual game window before pressing shortcuts. Both games must run on the same Windows user account. Check these logs:

- Schedule I: `MelonLoader\Latest.log` should say **StreetChem pipe started**. Missing generated IL2CPP references usually means MelonLoader has not completed its first launch.
- Cyberpunk: `r6\logs\redscript_rCURRENT.log` should list the three StreetChem scripts and **Compilation complete**.
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

## Compatibility limits

Solo only. No shared quests, combat or multiplayer. Equipment is a camera-matched transparent layer: it has no Night City collision/depth occlusion and can overlap HUD/menu content. Native plant rules, native property space and seed unlocks still apply. Crowd NPC bodies can stream out; reassign a body to the same dealer if needed. Performance depends on running both games. Other graphics injectors and mods touching the same shortcuts or NPC AI may conflict. Only the versions in INSTALL.md were tested.
