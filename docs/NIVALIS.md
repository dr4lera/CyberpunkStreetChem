# Nivalis business guide

Nivalis runs real venues; Cyberpunk manager NPCs control them. Buildings and furniture are not ported into Night City. Managers reuse vanilla models, face nearby V, and identify their Nivalis business in the conversation/nameplate/scanner. The first tested site is Wagami Market; other automatic food/bar locations need broader checks.

## Replies

F to Talk, Q/E or Up/Down to highlight, F to confirm. Replies cover status, actual dish prices, compatible local menus, native hours/shifts, independent qualified crews, all-business restocking, automatic purchase toggling, net cash settlement, start/pause operations and goodbye. Small camera movements keep the conversation open; leaving closes it. During a guest reconnect the manager can explain the unavailable link; edits wait for the paired save.

## Ingredients and eddies

The buffer aggregates ten servings of **every menu dish at every owned venue**, unioned with **every** additional native shopping-list item. Checks occur about every three seconds; multiple refills per day are allowed. Native suppliers deliver real goods within storage capacity. V reserves payment; delivered goods determine the actual bill; unused funds return. Low funds, supplier shortages or recovery states can still leave deficits.

Central supplies suppress overlapping native manager shopping only in the dedicated sandbox. Real wages and rent remain. Net operating settlements include income and costs and can be negative. Filling all businesses initially buys inventory; it is not a daily expense. Profit is not guaranteed.

Cyberpunk pause/disconnection freezes both the Nivalis clock and simulation. Held setup mode and isolated paper tests prevent spending/settlements. Preserve paired saves and journals together. Never delete a ticket or repeatedly replay a timed-out purchase to force a transfer.

## Dedicated-save setup

Normal saves are observe-only. Only the exact configured sandbox permits mutations. In ModKit's developer console or `nivalis/tools/guest_query.py` with your `--game` path:

1. `nc-maintenance enabled=true` holds operations.
2. `nc-setup save=YOUR_DEDICATED_SAVE_NAME` discovers recipes, acquires/maxes supported venues, seeds menus and hires staff. Bulk/loading setup deliberately does not create furniture.
3. Read `nc-venues` for owned GUIDs. After loading, `nc-provision save=... venue=GUID` fills authored seating/kitchen slots. `nc-seating save=... venue=GUID tables=8` expands dining within bounds and the native table limit. Repeat per owned venue.
4. Run `nc-setup save=...` again after equipment exists to populate compatible menus. `nc-manager-action save=... venue=GUID action=staff` hires an independent qualified crew and trains existing work skills to their native caps. Repeat per venue.
5. `nc-save save=...` checkpoints the dedicated save. Cold-reload it to verify before enabling operations. Restock through Cyberpunk so V pays the actual bill.

Commands are native mutations; do not blindly replay a timed-out setup or purchase. The tested prepared sandbox has all 192 recipes, 15 ready venues, 20 dishes each, seating, all seven appliance types and 75 unique employees.

Story dialogue and curfew are suppressed only in the sandbox. Native day-close/rent events remain. This bridge does not complete story achievements, add apartments or share Schedule I business interiors.
