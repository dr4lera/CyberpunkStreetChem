using System;
using Nivalis;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.InventorySystem;
using NivalisModKit;

namespace NivalisNightCity;

internal static class StorageSetup
{
    // Dedicated sandbox only: provision enough native storage for the complete
    // menu's shopping demand. Reapply after loading/furniture capacity changes.
    internal static void EnsureMenuCapacity()
    {
        if (Plugin.Active?.SandboxEnabled != true) return;
        foreach (var targets in System.Linq.Enumerable.GroupBy(SupplyTargets.Build(), t => t.Venue?.Pointer ?? IntPtr.Zero))
        {
            var venue = System.Linq.Enumerable.First(targets).Venue;
            if (venue == null) continue;
            var inventory = venue.JointInventory;
            int normal = inventory.NormalCount, cold = inventory.RefridgeratedCount;
            foreach (var item in targets)
            {
                int needed = item.Missing;
                if (item.Item.RequiresRefridgeration) cold += needed;
                else normal += needed;
            }
            Raise(inventory.NormalInventory, normal + 100);
            Raise(inventory.RefridgeratedInventory, cold + 100);
        }
    }

    private static void Raise(ItemContainer container, int target)
    {
        var restriction = container._restriction;
        if (restriction == null || restriction.MaxItems == null || !restriction.MaxItems.HasValue) return;
        if (restriction.MaxItems.Value < target) restriction.MaxItems = new OptionalInt(target);
        if (restriction.MaxSlots != null && restriction.MaxSlots.HasValue && restriction.MaxSlots.Value < 128)
            restriction.MaxSlots = new OptionalInt(128);
    }

    internal static int Free(VenueAreaGhost venue, ItemType item)
    {
        var container = item.RequiresRefridgeration ? venue.JointInventory.RefridgeratedInventory : venue.JointInventory.NormalInventory;
        var max = container._restriction?.MaxItems;
        return max == null || !max.HasValue ? int.MaxValue : Math.Max(0, max.Value - container.ItemCount);
    }
}
