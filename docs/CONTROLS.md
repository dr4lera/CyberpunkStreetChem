# Street Chem controls

**Inventory consumables (v0.2.0):** open Cyberpunk's Inventory → Backpack → Consumables, select your native product, and use the normal Consume / Use action. No extra hotkey is required. See [CONSUMABLES.md](CONSUMABLES.md) for effects, crashes and package sizes.

Run Cyberpunk 2077 and Schedule I together. Load your paired saves in both games. Play and use these shortcuts in Cyberpunk. No function keys are required.

| Shortcut | Action |
|---|---|
| **Ctrl+H** | Cycle growing, selling, and recovery help. Press again for the next page. |
| **Ctrl+B** | Buy a grow tent with soil, water, and one OG Kush seed for **1200 eddies**. Aim at a flat floor within eight metres. |
| **Ctrl+R** | Turn the next tent placement 90 degrees. Does not rotate an existing tent. |
| **Ctrl+E** | Use the tent you aim at within five metres: plant if empty, water if growing, harvest if mature. |
| **Ctrl+G** | Inspect the aimed tent's growth, water, and light. Values range from 0 to 1; 1 means 100%. |
| **Ctrl+U** | Refill an **empty** tent with soil, water, and one seed for **300 eddies**. Harvest first. Failed refills refund the charge. |
| **Ctrl+P** | Automatically bag loose product in Schedule I's inventory. Bagging is included and has no separate charge. |
| **Ctrl+N** | Cycle available product stacks. Shows product, available quantity, and eddies per unit. |
| **Ctrl+S** | Sell one bag/unit of the selected product to the living civilian you aim at within four metres. Police and aggressive NPCs are excluded. |
| **Ctrl+J** | Cycle your recruited Schedule I dealers. Shows selected runner, stock, and collectable earnings. |
| **Ctrl+K** | Assign the selected dealer to an aimed ambient Night City civilian within five metres. If that dealer has an active body, releases them instead; aim and press again to reassign. |
| **Ctrl+Shift+B** | Recover an unlinked existing Schedule I tent at the floor you aim at. Preserves its plant and costs nothing. Prefers a planted tent. |
| **Ctrl+Shift+Y** | Teleport to V’s original H10 apartment for testing, outside combat; requires both paired saves. |
| **Ctrl+Alt+L** | Toggle the separate full-screen transport diagnostic. Leave it off for ordinary Night City play. |

## Apartment dealer

Approach the dealer in the **H10 hallway outside V’s original apartment** and look at him within four metres. Select **Browse drugs** using Cyberpunk’s normal interaction button. Buy through the native vendor screen and close it using the game’s normal Back action. The shop has unlimited supply and replenishes after purchases. No separate shopping hotkeys are required. Read [DEALER.md](DEALER.md).


## First crop

1. Aim at a clear, flat floor and press **Ctrl+B**. Leave space between tents so aiming selects the correct one.
2. Face the pot and press **Ctrl+E** to plant the included seed.
3. Press **Ctrl+G** to check it. While growing, **Ctrl+E** fills its water. Growth uses Schedule I's real clock and plant rules. Both games must stay running, and Schedule I must be loaded and unpaused. While Cyberpunk is active, the mod advances Schedule I's 4 AM sleep cutoff to its native morning wake time, so production can keep progressing. Manually pausing Schedule I still pauses production.
4. When growth reaches 1, press **Ctrl+E** to harvest. Harvested product is automatically bagged into real Schedule I inventory. Keep inventory space available.
5. Press **Ctrl+N** until the product you want to sell is displayed. Face a civilian and press **Ctrl+S**. The game saves the real stock debit before paying eddies.
6. Each customer has a 60-second cooldown; approach another civilian to continue selling. A customer who walks away before committing the deal does not consume stock.
7. For another crop, face the empty tent, press **Ctrl+U**, then **Ctrl+E** to plant.

## Runners

Select a dealer with **Ctrl+J**, aim at a living ambient civilian, then **Ctrl+K**. The runner uses a real Cyberpunk NPC body and Cyberpunk walking commands. Their stock and cash belong to the selected native Schedule I dealer. The runner looks for reachable civilians nearby; sales require the body to actually reach the customer. The native dealer commission is deducted and proceeds stay with the dealer until collected.

Aim at your assigned runner within four metres and press **Ctrl+S** to open the menu. **Ctrl+N** cycles **Collect eddies / Give up to five selected products / Close**; **Ctrl+S** confirms. Select the product with Ctrl+N before opening the runner menu. Normal Ctrl+S selling still works when aiming at other civilians.

**Collecting cash:** aim at the runner, press **Ctrl+S**, then **Ctrl+S** again. Each collection pays at most **1,000,000 eddies**, with the rest left on the native dealer. The bridge waits for the native debit and save before crediting Cyberpunk. It checks wallet capacity to avoid overflow. Very large native balances use coarse floating-point amounts, so a transfer can be slightly below the maximum. Save Cyberpunk after payment.

**Giving stock:** use **Ctrl+N** outside the menu to select your product. Aim at the runner and press **Ctrl+S**, then **Ctrl+N** once to select Give, then **Ctrl+S** to confirm. This moves up to five units from your real Schedule I inventory to that dealer; it does not charge eddies. All runners assigned to a dealer share that dealer's existing inventory and earnings.

Ambient NPCs can stream out when you leave the area. Return to that NPC or assign a new body to the same dealer; the native stock and earnings remain. Native NPC runners were confirmed working during development. Crowd streaming and every possible NPC's walking behavior have not all been tested.

This release is solo only. Multiplayer is not included.

## Saving and recovery

Create a Cyberpunk **manual save after your first placement, a recovery, sales, and dealer purchases**. The mod requests autosaves, but the game can delay or refuse them. Load the latest paired saves when returning. Schedule I saves production changes and sales using its native save system.

The mod's anchor and receipt files live in `%LOCALAPPDATA%\StreetChem`. Keep this folder alongside game save backups. Loading an older Cyberpunk save can roll back eddies while Schedule I retains a newer stock debit; arbitrary mismatched save rollback is not supported.

If a tent is absent, first confirm Schedule I has loaded the correct save. Use **Ctrl+Shift+B** only for recovery of an existing unlinked tent; it does not create a new one. A “pot missing” message removes a stale link—retry aiming at the real tent afterward.

After restarting, load both saves and allow Schedule I to finish loading before using mod controls. A running Schedule I main menu is not a loaded business. Its pause menu also stops native growing. In Cyberpunk, close menus before using shortcuts.

This is the experimental v0.2.0 solo release, “V is Using now.” It adds inventory consumables to the growing and selling loop; verification is recorded in docs/TESTING.md. Native Night City depth occlusion, collision, additional equipment, and broader economy/progression features remain unfinished.

## Nivalis businesses (v0.3.0)

F to Talk to a labelled restaurant/bar manager; Q/E or Up/Down selects V's reply; F confirms; Goodbye/leaving closes it. Menu edits, opening hours, qualified native crews, all-business supply purchases, automatic-restock toggling and net settlements are in the replies. Start/pause business operations there.

Ctrl+Alt+U starts the paired businesses; Ctrl+Alt+R requests every business's missing supplies; Ctrl+Alt+C settles net income/costs; Ctrl+Alt+A toggles continuous purchases. Nivalis starts held and requires the exact configured dedicated save. Keep all three paired saves and transaction journals. See [business guide](NIVALIS.md).
