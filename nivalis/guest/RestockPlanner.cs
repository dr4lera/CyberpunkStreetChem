using System;
using System.Collections.Generic;
using System.Linq;
using Nivalis;
using NivalisModKit;

namespace NivalisNightCity;

internal static class RestockPlanner
{
    internal static object ShoppingList()
    {
        if (!GameEvents.IsInGame) throw new InvalidOperationException("load a save first");
        var manager = Singleton<ShoppingListManager>.Instance;
        manager.UpdateListExternal();
        var lines = new List<object>();
        long quotedHundredths = 0;
        int missingSupply = 0;
        // Native low-stock thresholds are too late for continuous service. Keep
        // ten servings per menu dish, plus every additional native list demand.
        foreach (var entry in SupplyTargets.Build().Where(t => t.Missing > 0).GroupBy(t => t.Item.Pointer))
        {
            var item = entry.First().Item;
            int missing = entry.Sum(t => t.Missing);
            var allocations = new List<object>();
            foreach (var target in entry)
            {
                allocations.Add(new { venue = target.Venue?.Venue.Guid, name = target.Venue?.Venue.name ?? "player",
                    current = target.Current, target = target.Demand, missing = target.Missing });
            }
            var offers = Economy.VendorsFor(item).Where(Economy.IsUnlocked)
                .Select(v => new { vendor = v, price = Economy.Price(v, item), stock = Economy.Stock(v, item) })
                .Where(v => v.price.HasValue && v.price.Value >= 0 && v.stock > 0)
                .OrderBy(v => v.price).ThenBy(v => Economy.NameOf(v.vendor)).ToArray();
            var purchases = new List<object>();
            int remaining = missing;
            long cost = 0;
            foreach (var offer in offers)
            {
                int amount = Math.Min(remaining, offer.stock);
                if (amount <= 0) break;
                long subtotal = checked((long)amount * offer.price!.Value);
                purchases.Add(new { supplier = Economy.NameOf(offer.vendor), amount,
                    unitPriceHundredths = offer.price.Value, subtotalHundredths = subtotal });
                cost += subtotal; remaining -= amount;
            }
            quotedHundredths += cost;
            if (remaining > 0) missingSupply++;
            lines.Add(new { id = item.Guid, item = Items.NameOf(item), current = entry.Sum(t => t.Current),
                target = entry.Sum(t => t.Demand), missing, purchases, costHundredths = cost,
                unavailable = remaining, venues = allocations });
        }
        return new { day = GameTime.Day, lines, quotedHundredths,
            reserveEddies = (quotedHundredths + 99) / 100, incompleteLines = missingSupply,
            previewOnly = true, servingsPerDish = SupplyTargets.Servings,
            note = "Live supplier quote for ten servings per menu dish plus the complete native shopping list. No items bought or eddies charged." };
    }
}
