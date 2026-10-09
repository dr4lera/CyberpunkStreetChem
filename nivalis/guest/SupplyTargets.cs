using System;
using System.Collections.Generic;
using System.Linq;
using Nivalis;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.InventorySystem;
using NivalisModKit;

namespace NivalisNightCity;

internal sealed record SupplyTarget(ItemType Item, VenueAreaGhost? Venue, int Current, int Demand)
{
    internal int Missing => Math.Max(0, Demand - Current);
}

internal static class SupplyTargets
{
    internal const int Servings = 10;
    internal static List<SupplyTarget> Build()
    {
        var manager = Singleton<ShoppingListManager>.Instance;
        manager.UpdateListExternal();
        var result = new List<SupplyTarget>();
        var nativeAllocated = new Dictionary<IntPtr, int>();
        foreach (var venue in Venues.PlayerOwned)
        {
            var demands = new Dictionary<IntPtr, (ItemType item, int quantity)>();
            foreach (var meal in Venues.MenuOf(venue)) foreach (var ingredient in meal.Ingredients)
            {
                var pointer = ingredient.Item.Pointer;
                int previous = demands.TryGetValue(pointer, out var entry) ? entry.quantity : 0;
                demands[pointer] = (ingredient.Item, checked(previous + ingredient.Amount * Servings));
            }
            // Preserve native demands beyond menus, including shopping-list tasks.
            if (manager.VenuesLowOnIngredientsMap.TryGetValue(venue.Venue, out var native))
                foreach (var item in native.LowIngredients)
                {
                    int prior = demands.TryGetValue(item.Key.Pointer, out var entry) ? entry.quantity : 0;
                    demands[item.Key.Pointer] = (item.Key, Math.Max(prior, item.Value.demand));
                    nativeAllocated[item.Key.Pointer] = (nativeAllocated.TryGetValue(item.Key.Pointer, out int sum) ? sum : 0) + Math.Max(0, item.Value.MissingItems);
                }
            foreach (var demand in demands.Values)
                result.Add(new(demand.item, venue, venue.JointInventory.GetItemCount(demand.item), demand.quantity));
        }
        foreach (var item in manager.ShoppingList)
        {
            int residual = Math.Max(0, item.Value.MissingItems - (nativeAllocated.TryGetValue(item.Key.Pointer, out int count) ? count : 0));
            if (residual > 0) result.Add(new(item.Key, null, 0, residual));
        }
        return result;
    }
}
