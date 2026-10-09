using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Nivalis;
using Nivalis.GhostSystem;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.InventorySystem;
using UnityEngine;

namespace NivalisNightCity;

internal static class FurnitureSetup
{
    private static GameObject? holder;
    private static BuildTimeSceneGhosts? emptyRegistry;
    private static IGhostRegistry Registry()
    {
        // GhostManager is NOT an IGhostRegistry. Use a real native registry with
        // no neighbors for new objects, then explicitly link the authored parent.
        if (emptyRegistry == null)
        {
            emptyRegistry = ScriptableObject.CreateInstance<BuildTimeSceneGhosts>();
            emptyRegistry.GhostsInfo = new Il2CppSystem.Collections.Generic.List<GhostInfo>();
        }
        return emptyRegistry.Cast<IGhostRegistry>();
    }
    private static bool Compatible(PlacementSlot slot, ItemType item) =>
        slot.Objects.Any(o => o.Pointer == item.Pointer) ||
        (slot.Objects.Length == 0 && (slot.type & item.Slot) != 0);

    private static TableGhost? AddTableInArea(VenueAreaGhost venue, FurnitureCategory category, ItemType? required)
    {
        var names = required == null ? new[] { "Restaurant_Table_1" } :
            new[] { "Base_Cabinet_W40cm_3Drawer_Set_03", "Base_Cabinet_W60cm_1Door_Set_03", "Kitchen_Counter_Cabinet_01", "Bar_Counter_1" };
        foreach (var name in names)
        {
            var item = NivalisModKit.Items.ByName(name);
            if (item?.EntityPrefab == null) continue;
            // Inspect the retail prefab's authored slots without invoking a fill on it.
            var tableView = item.EntityPrefab.GetComponentInChildren<TableView>(true);
            if (tableView == null) continue;
            if (required != null)
            {
                bool compatible = false;
                var surface = tableView.parent?.surface;
                if (surface != null)
                    foreach (var s in surface.Slots)
                        if (Compatible(s, required)) compatible = true;
                if (!compatible) continue;
            }
            foreach (var area in venue.FurnitureAreas)
            {
                bool dining = category == FurnitureCategory.Comfortables;
                float margin = dining ? 1.2f : 0.7f;
                if ((area.category & category) == 0 || area.Size.x < margin * 2 || area.Size.z < margin * 2) continue;
                float floor = area.Offset.y - area.Size.y / 2;
                // Stay inside the game's authored placement region, at its floor.
                float spacing = dining ? 2.4f : 1.0f;
                float clearance = dining ? 2.1f : 0.95f;
                for (float x = -area.Size.x / 2 + margin; x <= area.Size.x / 2 - margin; x += spacing)
                for (float z = -area.Size.z / 2 + margin; z <= area.Size.z / 2 - margin; z += spacing)
                {
                    var local = new Vector3(area.Offset.x + x, floor + 0.03f, area.Offset.z + z);
                    var world = venue.Position + venue.Rotation * local;
                    bool crowded = false;
                    for (int i = 0; i < venue.PlacedFurniture.Count; i++)
                    {
                        var furniture = venue.PlacedFurniture[i].Value;
                        if (furniture == null) continue;
                        var delta = furniture.Position - world;
                        if (Math.Abs(delta.y) < 2 && delta.x * delta.x + delta.z * delta.z < clearance * clearance)
                        { crowded = true; break; }
                    }
                    if (crowded) continue;
                    var slot = new PlacementSlot(local, Vector3.zero, 0.5f, Slot.RotationType.Local,
                        false, 0, ItemType.SlotType.None, new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ItemType>(new[] { item }));
                    return Create(item, venue, slot, venue).TryCast<TableGhost>();
                }
            }
        }
        return null;
    }

    private static Ghost? RestoreSlot(VenueAreaGhost venue, Ghost parent, PlacementSlot slot)
    {
        if (slot.isOccupied) return slot.current;
        var point = parent.Position + parent.Rotation * slot.Position;
        for (int i = 0; i < venue.PlacedFurniture.Count; i++)
        {
            var existing = venue.PlacedFurniture[i].Value;
            if (existing == null || existing.Pointer == parent.Pointer) continue;
            if (existing.TryCast<ChairGhost>() == null && existing.TryCast<FoodProcessorGhost>() == null &&
                existing.TryCast<IngredientProcessorGhost>() == null) continue;
            if (Vector3.Distance(existing.Position, point) < 0.12f)
            {
                slot.current = existing;
                return existing;
            }
        }
        return null;
    }

    private static Ghost Create(ItemType item, Ghost parent, PlacementSlot slot, VenueAreaGhost venue)
    {
        if (item.EntityPrefab == null) throw new InvalidOperationException("item has no native entity prefab");
        if (holder == null)
        {
            holder = new GameObject("NightCitySandboxPrefabLab");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
        }
        // Read native component defaults from an inactive copy; never change the retail prefab.
        var copy = UnityEngine.Object.Instantiate(item.EntityPrefab.gameObject, holder.transform);
        try
        {
            copy.transform.SetPositionAndRotation(parent.Position + parent.Rotation * slot.Position,
                parent.Rotation * Quaternion.Euler(slot.rotation));
            var component = copy.GetComponentInChildren<SerializableObject>(true);
            var view = component?.TryCast<IGhostView>() ?? throw new InvalidOperationException("prefab has no ghost view");
            var manager = GhostManager.Instance;
            // The inactive clone has never run Awake: its serialized view GUID can be empty.
            // Assign identity BEFORE the native factory/fill calls; assigning it afterwards
            // allowed WashingBasin to call UpdatePosition with a null ID during loading.
            var identity = GhostManager.GenerateGUID();
            view.ViewId = identity;
            var ghost = view.CreateGhostBaseFromView();
            if (ghost.TryCast<ChairGhost>() == null && ghost.TryCast<TableGhost>() == null &&
                ghost.TryCast<FoodProcessorGhost>() == null && ghost.TryCast<IngredientProcessorGhost>() == null)
                throw new NotSupportedException("Only chairs, tables and kitchen processors are permitted; fixtures use baked game objects.");
            ghost.Id = identity;
            ghost.ViewId = identity;
            ghost.SceneIndex = venue.SceneIndex;
            if (!view.FillGhostBaseFromViewState(ghost, Registry()))
                throw new InvalidOperationException("native view state initialization refused");
            ghost.SceneIndex = venue.SceneIndex;
            ghost.Position = copy.transform.position;
            ghost.Rotation = copy.transform.rotation;
            ghost.IsActive = true;
            ghost.IsValid = true;
            ghost.IsBakedInstance = false;
            ghost.LoadedView = null;
            ghost.Link(parent);
            manager.RegisterGhostImmediate(ghost);
            if (!venue.RegisterPlacedFurniture(ghost))
                throw new InvalidOperationException("venue refused native furniture registration");
            slot.current = ghost;
            return ghost;
        }
        finally { UnityEngine.Object.Destroy(copy); }
    }

    internal static object FillSeating(VenueAreaGhost venue, int targetTables = 0)
    {
        int added = 0;
        var errors = new List<string>();
        var tables = new List<TableGhost>();
        int DiningTables() => Enumerable.Range(0, venue.Tables.Count)
            .Count(i => venue.Tables[i].Value?.ChairSpots.Count > 0);
        int tablesAdded = 0;
        int target = Math.Min(Math.Max(0, targetTables), venue.TableLimit);
        for (int attempt = 0; attempt < target && DiningTables() < target; attempt++)
        {
            if (AddTableInArea(venue, FurnitureCategory.Comfortables, null) == null) break;
            tablesAdded++;
        }
        if (!venue.HasSeating && !Enumerable.Range(0, venue.Tables.Count).Any(i => venue.Tables[i].Value?.ChairSpots.Count > 0))
        {
            for (int attempt = 0; attempt < 2; attempt++)
                if (AddTableInArea(venue, FurnitureCategory.Comfortables, null) == null) break;
        }
        for (int i = 0; i < venue.Tables.Count; i++)
        {
            var table = venue.Tables[i].Value;
            if (table != null) tables.Add(table);
        }
        // This first slice only uses the venue's existing authored chair slots.
        foreach (var table in tables)
        {
            foreach (var slot in table.ChairSpots)
            {
                var existing = RestoreSlot(venue, table, slot);
                if (existing != null)
                {
                    var chairGhost = existing.TryCast<ChairGhost>();
                    if (chairGhost != null) table.Chairs.AddUnique(chairGhost);
                    continue;
                }
                try
                {
                    ItemType? chair = null;
                    foreach (var candidate in slot.Objects)
                    {
                        if (candidate.EntityPrefab != null) { chair = candidate; break; }
                    }
                    if (chair == null)
                        foreach (var candidate in venue.Venue.chairs)
                            if (candidate.EntityPrefab != null) { chair = candidate; break; }
                    if (chair == null) chair = NivalisModKit.Items.ByName("Restaurant_Chair_5");
                    if (chair?.EntityPrefab == null) throw new InvalidOperationException("no venue-compatible chair prefab");
                    var created = Create(chair, table, slot, venue).TryCast<ChairGhost>()
                        ?? throw new InvalidOperationException("chair item created a different ghost type");
                    table.Chairs.AddUnique(created);
                    added++;
                }
                catch (Exception e) { errors.Add(e.Message); }
            }
        }
        return new { name = NivalisModKit.Venues.NameOf(venue), added, tablesAdded,
            diningTables = DiningTables(), target, tableLimit = venue.TableLimit, seating = venue.HasSeating, errors };
    }

    internal static object FillFixtures(VenueAreaGhost venue)
    {
        int added = 0;
        var errors = new List<string>();
        var slots = new List<PlacementSlot>();
        foreach (var slot in venue.TableSlots) slots.Add(slot);
        foreach (var slot in venue.FurnitureSlots) slots.Add(slot);
        foreach (var slot in slots)
        {
            if (slot.isOccupied || slot.Objects.Length == 0) continue;
            try
            {
                var item = slot.GetItemType();
                if (item?.EntityPrefab == null) continue;
                // Existing sinks/fridges are authored scene ghosts. Never synthesize those.
                if (item.EntityPrefab.TryCast<TableView>() == null &&
                    item.EntityPrefab.GetComponentInChildren<TableView>(true) == null) continue;
                var position = venue.Position + venue.Rotation * slot.Position;
                Ghost? existing = null;
                for (int i = 0; i < venue.PlacedFurniture.Count; i++)
                {
                    var ghost = venue.PlacedFurniture[i].Value;
                    if (ghost?.TryCast<HoldableGhost>()?.Item?.Pointer == item.Pointer &&
                        Vector3.Distance(ghost.Position, position) < 0.25f) { existing = ghost; break; }
                }
                if (existing != null) slot.current = existing;
                else { Create(item, venue, slot, venue); added++; }
            }
            catch (Exception e) { errors.Add(e.Message); }
        }
        return new { added, errors };
    }

    internal static object FillKitchen(VenueAreaGhost venue)
    {
        var errors = new List<string>();
        var added = new List<string>();
        var slots = new List<(TableGhost Table, PlacementSlot Slot)>();
        for (int i = 0; i < venue.Tables.Count; i++)
        {
            var table = venue.Tables[i].Value;
            if (table == null) continue;
            foreach (var slot in table.SurfaceSpots)
                if (RestoreSlot(venue, table, slot) == null) slots.Add((table, slot));
        }
        foreach (var name in new[] { "Food_Processor", "Cooker", "Drinks_Machine", "Grill", "Deep_Fryer", "Blender", "Grinder" })
        {
            var item = NivalisModKit.Items.ByName(name);
            if (item == null) continue;
            bool existing = false;
            for (int i = 0; i < venue.PlacedFurniture.Count; i++)
                if (venue.PlacedFurniture[i].Value?.TryCast<HoldableGhost>()?.Item?.Pointer == item.Pointer)
                    existing = true;
            if (existing) continue;
            var target = slots.FirstOrDefault(s => !s.Slot.isOccupied && Compatible(s.Slot, item));
            if (target.Table == null)
            {
                var table = AddTableInArea(venue, FurnitureCategory.Kitchen, item);
                if (table == null) { errors.Add(name + ": no safe compatible kitchen surface remains"); continue; }
                foreach (var slot in table.SurfaceSpots)
                    if (RestoreSlot(venue, table, slot) == null) slots.Add((table, slot));
                target = slots.FirstOrDefault(s => !s.Slot.isOccupied && Compatible(s.Slot, item));
                if (target.Table == null) { errors.Add(name + ": new counter has no compatible surface"); continue; }
            }
            try
            {
                Create(item, target.Table, target.Slot, venue);
                added.Add(name);
            }
            catch (Exception e) { errors.Add(name + ": " + e.Message); }
        }
        return new { name = NivalisModKit.Venues.NameOf(venue), added, errors };
    }
}
