using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using HarmonyLib;
using Nivalis;
using Nivalis.CraftingSystem;
using Nivalis.Dialogue;
using Nivalis.GhostSystem.Ai;
using Nivalis.GhostSystem;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.Locale;
using NivalisModKit;
using UnityEngine;

namespace NivalisNightCity;

internal static class SandboxSetup
{
    private static CurfewManager? savedManager;
    private static bool savedCurfewEnabled, savedSecurityEnabled;
    private static int closedDay = -1;
    private static readonly List<object> nativeHooks = new();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PackedTimeHook(IntPtr self, long change, IntPtr methodInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DialogueRequestHook(IntPtr self, IntPtr args, IntPtr methodInfo);
    internal static void InstallCurfewHooks()
    {
        var harmony = new Harmony(Plugin.Id + ".curfew");
        foreach (var method in new[] { "GoToCurfew", "TurnOnCurfew", "CatchPlayer" })
        {
            var target = AccessTools.Method(typeof(CurfewManager), method);
            if (target == null) throw new MissingMethodException("CurfewManager", method);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SandboxSetup), nameof(AllowCurfew)));
        }
        harmony.Patch(AccessTools.Method(typeof(SerializationManager), "Load"),
            prefix: new HarmonyMethod(typeof(SandboxSetup), nameof(BeforeLoad)));
        // IL2CPP passes this 8-byte value type in one x64 register. Harmony's generated
        // struct trampoline is unsafe here; use the kit's ABI-preserving native hook.
        if (NivalisModKit.StructLayout.Size<Change<TimeOfDayManager.TimeStamp>>() != 8)
            throw new NotSupportedException("unrecognized native time change layout");
        PackedTimeHook originalTime = null!;
        nativeHooks.Add(NativeHook.Install<CurfewManager, PackedTimeHook>("TimeUpdateListener",
            (self, change, info) =>
            {
                if (Plugin.Active?.SandboxEnabled != true) originalTime(self, change, info);
            }, out originalTime));
        if (NivalisModKit.StructLayout.Size<DialogueEventArgs>() != 32)
            throw new NotSupportedException("unrecognized native dialogue request layout");
        foreach (var method in new[] { "OnDialogueRequest", "ProcessDialogueRequestNow" })
        {
            DialogueRequestHook original = null!;
            nativeHooks.Add(NativeHook.Install<DialogueTreeProgressManager, DialogueRequestHook>(method,
                (self, args, info) =>
                {
                    if (Plugin.Active?.SandboxEnabled != true) original(self, args, info);
                }, out original));
        }
        harmony.Patch(AccessTools.Method(typeof(AgentsDialogueManager), "Update"),
            prefix: new HarmonyMethod(typeof(SandboxSetup), nameof(AllowCurfew)));
        harmony.Patch(AccessTools.Method(typeof(Nivalis.UI.EndOfDayWindow), "Show", Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(SandboxSetup), nameof(AllowCurfew)));
        harmony.Patch(AccessTools.Method(typeof(TimeOfDayManager), "UpdateStaticVariables"),
            postfix: new HarmonyMethod(typeof(SandboxSetup), nameof(ClearClockCurfew)));
        harmony.Patch(AccessTools.Method(typeof(RuntimePersonData), "WorkUpdate"),
            postfix: new HarmonyMethod(typeof(SandboxSetup), nameof(AfterWorkUpdate)));
        foreach (var method in new[] { "BuyIngredients", "TryPurchaseIngredients" })
            harmony.Patch(AccessTools.Method(typeof(VenueAreaGhost), method),
                prefix: new HarmonyMethod(typeof(SandboxSetup), nameof(AllowManagerPurchase)));
        GameEvents.HourStarted += a =>
        {
            if (Plugin.Active?.SandboxEnabled != true || a.Hour != 2 || closedDay == a.Day) return;
            // The original curfew sequence raises the shared accounting event before moving the player.
            // Preserve its rent/day-close listeners while suppressing that narrative/teleport sequence.
            closedDay = a.Day;
            Singleton<CurfewManager>.Instance.dayEndEvent.Invoke(TimeOfDayManager.CurrentTime);
        };
    }

    private static bool AllowManagerPurchase(VenueAreaGhost __instance, ref bool __result)
    {
        if (Plugin.Active?.SandboxEnabled != true || Plugin.Active.CentralSupplies != true || !__instance.PlayerOwned) return true;
        // Supplies are purchased against a real V reservation by RestockPurchaser.
        // Letting NPC managers also purchase from the shadow wallet creates a
        // second, uncontrolled spending path and redundant supplier orders.
        __result = false;
        return false;
    }

    private static bool AllowCurfew() => Plugin.Active?.SandboxEnabled != true;
    private static void BeforeLoad() => Plugin.Active?.BeforeLoad();
    private static void ClearClockCurfew()
    {
        if (Plugin.Active?.SandboxEnabled == true) CurfewManager.IsCurfewInEffect = false;
    }
    private static void AfterWorkUpdate(RuntimePersonData __instance)
    {
        if (Plugin.Active?.SandboxEnabled == true && Plugin.Active.KeepStaffHappy &&
            (__instance.PlayerHired || (__instance.WorksAt != null && Venues.PlayerOwned.Any(v => v.Venue.Pointer == __instance.WorksAt.Pointer))))
            MakeHappy(__instance);
    }
    internal static void MakeHappy(RuntimePersonData person)
    {
        var satisfaction = person.WorkSatisfaction;
        satisfaction.Happiness = Happiness.Happy;
        satisfaction.Value = 1;
        person.WorkSatisfaction = satisfaction;
        person.WorkQuit = 0;
    }

    internal static void DisableCurfew()
    {
        if (!Singleton<CurfewManager>.InstanceExist(out var cm)) return;
        if (savedManager == null)
        {
            savedManager = cm;
            savedCurfewEnabled = cm._isCurfewEnabled;
            savedSecurityEnabled = cm._isCurfewSecurityEnabled;
        }
        if (CurfewManager.IsCurfewInEffect) cm.TurnOffCurfew();
        cm._isCurfewEnabled = false;
        cm._isCurfewSecurityEnabled = false;
        cm._awarness = 0;
        CurfewManager.IsCurfewInEffect = false;
    }

    internal static void RestoreCurfewFlags()
    {
        closedDay = -1;
        if (savedManager == null) return;
        try
        {
            savedManager._isCurfewEnabled = savedCurfewEnabled;
            savedManager._isCurfewSecurityEnabled = savedSecurityEnabled;
        }
        finally { savedManager = null; }
    }

    internal static void CancelStory()
    {
        if (!Singleton<DialogueTreeProgressManager>.InstanceExist(out var dm)) return;
        dm._phoneDialogueQueue.Clear();
        if (!dm.IsInDialogue) return;
        var args = dm._currentDialogueEventArgs;
        dm._currentDialogueNode = null;
        dm._currentDialogueTree = null;
        if (args.HasValue) dm.onDialogueEnd.Invoke(args.Value);
    }

    internal static object Apply()
    {
        var db = Singleton<MealDatabase>.Instance;
        var definitions = new List<MealRecipeDefinition>();
        foreach (var recipe in db._guidMealRecipeMap.Values)
        {
            definitions.Add(recipe);
            if (!db.knownRecipes.Contains(recipe)) db.DiscoverRecipe(recipe);
        }
        var people = new List<Person>();
        var claimed = Venues.PlayerOwned.SelectMany(Venues.StaffOf).Select(s => s.Person.Pointer).ToHashSet();
        var allPeople = Singleton<PersonDataManager>.Instance.guidToPersons.Values;
        foreach (var person in allPeople)
            if (person != null && person.RuntimeData != null) people.Add(person);
        var pm = Singleton<PlayerManager>.Instance;
        var owner = new IPropertyOwner(pm.LocalPlayer.Pointer);
        var propertyManager = Singleton<PropertyManager>.Instance;
        var results = new List<object>();
        foreach (var venue in Venues.All)
        {
            if (!venue.PlayerOwned && !venue.Venue.IsAcquireable) continue;
            try
            {
                Debug.Log("NightCity setup: " + venue.Venue.name);
                if (!venue.PlayerOwned) Singleton<VenueManager>.Instance.GiveVenue(venue.Venue, owner);
                // Use the highest level supported by this venue instead of assuming a shared cap.
                int max = venue.Venue.LevellingData.Length;
                if (max > 0) venue.SetLevel(max);
                // Prefab ghost creation can enter native scene updates before IDs exist.
                // Provisioning must never spawn furniture during loading or bulk setup.
                var allowed = venue.Venue.RecipeType;
                int added = 0;
                foreach (var recipe in definitions)
                {
                    if (venue.Menu.Count >= venue.MenuLimit) break;
                    if (recipe.Disabled || recipe.PlayerOnly || (allowed.Length > 0 && !allowed.Contains(recipe.RecipeType))) continue;
                    if (!venue.HasProcessors(recipe) || venue.Menu.ContainsMeal(recipe.Recipe)) continue;
                    int price = Math.Max(100, (int)Math.Ceiling(recipe.CurrentBasePrice * 1.75));
                    venue.Menu.Add(new MealMenuItem(recipe, price));
                    added++;
                }
                var need = new[] { VenueTasks.Cooking, VenueTasks.Serving, VenueTasks.Cleaning, VenueTasks.Managing };
                // First cover every role, then add relief staff. A refused native hire must
                // never spin the main thread: attempts and candidate selection are bounded.
                for (int pass = 1; pass <= 2; pass++) foreach (var role in need)
                {
                    if (Venues.StaffOf(venue).Count(s => (s.Person.RuntimeData.Tasks & role) != 0) < pass &&
                        Venues.StaffOf(venue).Count < venue.StaffLimit)
                    {
                        var candidate = people.FirstOrDefault(p => !claimed.Contains(p.Pointer) && !p.RuntimeData.HasWork && p.RuntimeData.Hireable && p.HasSkills(role));
                        if (candidate == null) continue;
                        people.Remove(candidate);
                        claimed.Add(candidate.Pointer);
                        int staffBefore = Venues.StaffOf(venue).Count;
                        Singleton<VenueManager>.Instance.HireStaff(venue.Venue, candidate);
                        if (Venues.StaffOf(venue).Count <= staffBefore) continue;
                        Singleton<VenueManager>.Instance.ChangeStaffTasks(venue.Venue, candidate.RuntimeData, role);
                        candidate.RuntimeData.ReportWorkingConditionChange();
                    }
                }
                float start = venue.Venue.OpenTime.x;
                float end = venue.Venue.OpenTime.y;
                if (start < 8) start += 24;
                if (end <= start) end += 24;
                end = Math.Min(end, start + 24);
                float middle = (start + end) / 2;
                var roleOffsets = new Dictionary<VenueTasks, int>();
                foreach (var member in Venues.StaffOf(venue))
                {
                    var data = member.Person.RuntimeData;
                    var primary = need.FirstOrDefault(r => (data.Tasks & r) != 0);
                    int index = roleOffsets.TryGetValue(primary, out var seen) ? seen : 0;
                    roleOffsets[primary] = index + 1;
                    int roleCount = Venues.StaffOf(venue).Count(s => (s.Person.RuntimeData.Tasks & primary) != 0);
                    data.WorkingHours = roleCount < 2 ? new Vector2(start, end) :
                        (index % 2 == 0 ? new Vector2(start, middle) : new Vector2(middle, end));
                    data.Wage = Math.Max(data.Wage, data.CalculateBaseWage().y);
                    data.WorkQuit = 0;
                    data.WorkSatisfaction = data.CalculateHappiness(1, 0, data.Wage);
                    data.ReportWorkingConditionChange();
                    if (Plugin.Active?.KeepStaffHappy == true) MakeHappy(data);
                }
                venue.ReportStaffWorkingHoursChanged();
                results.Add(new { id = venue.Venue.Guid, name = Venues.NameOf(venue), owned = venue.PlayerOwned,
                    level = venue.CurrentLevel, menu = venue.Menu.Count, added, staff = Venues.StaffOf(venue).Count,
                    seating = venue.HasSeating, ready = venue.IsReadyToOpen,
                    furnitureSetup = "disabled pending safe placement validation",
                    needsSeating = !venue.HasSeating, needsKitchen = venue.Menu.Count == 0,
                    missingRoles = need.Where(r => !Venues.StaffOf(venue).Any(s => (s.Person.RuntimeData.Tasks & r) != 0)).Select(r => r.ToString()).ToArray() });
            }
            catch (Exception e)
            {
                results.Add(new { id = venue.Venue.Guid, name = Venues.NameOf(venue), error = e.Message });
            }
        }
        DisableCurfew();
        return new { recipes = db.knownRecipes.Count, venues = results };
    }

    internal static object Layout()
    {
        var layouts = new List<object>();
        foreach (var venue in Venues.All)
        {
            if (!venue.PlayerOwned) continue;
            var slots = new List<object>();
            foreach (var slot in venue.TableSlots)
                slots.Add(new { kind = "table", position = new { slot.Position.x, slot.Position.y, slot.Position.z },
                    rotation = new { slot.rotation.x, slot.rotation.y, slot.rotation.z }, occupied = slot.isOccupied,
                    options = slot.Objects.Select(Items.NameOf).ToArray() });
            foreach (var slot in venue.FurnitureSlots)
                slots.Add(new { kind = "furniture", position = new { slot.Position.x, slot.Position.y, slot.Position.z },
                    rotation = new { slot.rotation.x, slot.rotation.y, slot.rotation.z }, occupied = slot.isOccupied,
                    options = slot.Objects.Select(Items.NameOf).ToArray() });
            var furniture = new List<object>();
            var tables = new List<object>();
            for (int i = 0; i < venue.PlacedFurniture.Count; i++)
            {
                var ghost = venue.PlacedFurniture[i].Value;
                if (ghost == null) continue;
                furniture.Add(new { ghost.Id, ghost.PrefabId, type = ghost.GetIl2CppType().Name,
                    position = new { ghost.Position.x, ghost.Position.y, ghost.Position.z },
                    item = Items.NameOf(ghost.TryCast<HoldableGhost>()?.Item),
                    recipes = ghost.TryCast<FoodProcessorGhost>()?.Recipes?.Select(r => r.ToString()).ToArray(),
                    ingredientProcessor = ghost.TryCast<IngredientProcessorGhost>()?.Type.ToString() });
                var table = ghost.TryCast<TableGhost>();
                if (table != null)
                {
                    var chairSlots = new List<object>();
                    foreach (var slot in table.ChairSpots)
                        chairSlots.Add(new { position = new { slot.Position.x, slot.Position.y, slot.Position.z },
                            rotation = new { slot.rotation.x, slot.rotation.y, slot.rotation.z }, occupied = slot.isOccupied,
                            options = slot.Objects.Select(Items.NameOf).ToArray() });
                    var surfaceSlots = new List<object>();
                    foreach (var slot in table.SurfaceSpots)
                        surfaceSlots.Add(new { position = new { slot.Position.x, slot.Position.y, slot.Position.z },
                            rotation = new { slot.rotation.x, slot.rotation.y, slot.rotation.z }, occupied = slot.isOccupied,
                            options = slot.Objects.Select(Items.NameOf).ToArray() });
                    tables.Add(new { table.Id, chairSlots, surfaceSlots });
                }
            }
            var areas = new List<object>();
            foreach (var a in venue.FurnitureAreas)
                areas.Add(new { category = a.category.ToString(),
                    offset = new { a.Offset.x, a.Offset.y, a.Offset.z }, size = new { a.Size.x, a.Size.y, a.Size.z },
                    center = new { a.Bounds.center.x, a.Bounds.center.y, a.Bounds.center.z } });
            layouts.Add(new { id = venue.Venue.Guid, name = Venues.NameOf(venue), scene = venue.SceneIndex,
                position = new { venue.Position.x, venue.Position.y, venue.Position.z }, slots, furniture, tables,
                recipeTypes = venue.Venue.RecipeType.Select(r => r.ToString()).ToArray(),
                registeredFoodProcessors = venue.FoodProcessors.Count,
                registeredIngredientProcessors = venue.IngredientProcessors.Count,
                areas });
        }
        return layouts;
    }
}
