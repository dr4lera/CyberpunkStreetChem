# H10 drug shop

The dealer is a real Cyberpunk NPC in the hallway outside **V’s original Megabuilding H10 apartment**, on the apartment floor. Other purchased apartments do not have a dealer in this release. **Codeware 1.20.5** is required to spawn and restore him.

Approach within four metres, look at him and select **Browse drugs** with Cyberpunk’s normal interaction button. It opens the **native vendor screen**. Click a product and use the game’s normal Buy action; close the shop with its normal Back action. No separate shopping shortcuts or function keys are required. Rebound controls and controllers use the game’s interaction mapping.

For testing, **Ctrl+Shift+Y** teleports V beside the hallway shop, outside combat, after the paired solo saves have loaded. The dealer stays at the shop instead of following V. He cannot be recruited as a runner or treated as a civilian sale customer.

## Unlimited supply and real stock

The dealer has **unlimited supply**, independent of your Schedule I inventory. The vendor screen offers one package at a time and replenishes it after each completed purchase. You can keep buying; no Schedule I seller or existing personal stock is required.

He offers cannabis, meth, cocaine and mushrooms when the installed Schedule I version supplies their native base definitions. For a family already in your hotbar, the shop uses the first existing product variant, including its real name, quality and packaging. Otherwise it offers the native base product in a bag.

V pays with Cyberpunk eddies. The purchase adds a **real native package** to Schedule I inventory and its Cyberpunk consumable mirror. That stock can also be sold to civilians or given to runners. Schedule I cash is not withdrawn.

## Package sizes and prices

Each purchase gives one package: a bag contains one product unit, a jar five, and a brick twenty. The name/tooltip shows the packaging. The price is the native package market value converted at **10 eddies per Schedule I dollar, plus 25% retail markup**, rounded up. Buying and immediately reselling does not turn a profit.

Product, quality and packaging determine the displayed price. Custom blends use the family profiles in [CONSUMABLES.md](CONSUMABLES.md); every native mixing-property effect is not reproduced.

## Capacity and saves

Purchasing first reserves a matching unlocked Schedule I hotbar stack or an empty slot. Existing matching stacks can grow above native capacity, up to the bridge’s 99,999,999-package mirror limit. A new variant needs an empty slot; no item is removed to make room. Rejected reservations and insufficient eddies do not charge V.

Native stock is saved before completion is acknowledged. Purchase recovery intent and idempotent receipts are in `%LOCALAPPDATA%\StreetChem\sales`. V’s saved transaction state and payment fact prevent repeated charges for the same transaction in that save. **Make a Cyberpunk manual save after buying.** Keep both matching game saves and the StreetChem folder together when backing up or restoring. Arbitrary one-sided save rollback is unsupported.

If interrupted, reload the same paired saves and let the bridge reconnect. Manually changed stock during recovery produces a mismatch warning instead of a guessed replacement. Restore matching backups if needed.

New variants export their icons locally. Run the [local icon converter](CONSUMABLES.md#actual-native-product-icons) and restart Cyberpunk to add artwork for them. Retail product icons and NPC assets are not included in the public release.

Your backpack’s displayed eddy value follows the civilian selling price. The shop’s Buy price includes its retail markup. Positive stock credits update the native item instance directly and notify its normal inventory listeners, avoiding the capacity clamp in native slot addition. Normal stock debits still use the native removal path.
