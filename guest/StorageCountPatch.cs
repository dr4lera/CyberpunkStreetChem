using HarmonyLib;
using Il2CppScheduleOne.ItemFramework;

namespace StreetChem;

// Schedule I uses a checked LINQ int sum while initializing storage visuals.
// Imported/large stocks can overflow that aggregate even though each slot is valid.
// Keep every item untouched; only saturate the aggregate exposed by its int API.
[HarmonyPatch(typeof(IItemSlotOwner), nameof(IItemSlotOwner.GetQuantitySum))]
internal static class StorageCountPatch
{
    static bool Prefix(IItemSlotOwner __instance, ref int __result)
    {
        var slots = __instance.ItemSlots;
        if (slots == null) return true;
        long total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var item = slots[i]?.ItemInstance;
            if (item != null) total += item.Quantity;
        }
        __result = (int)Math.Clamp(total, int.MinValue, int.MaxValue);
        return false;
    }
}
