using System;
using System.Collections.Generic;
using System.Linq;
using Nivalis;
using Nivalis.GhostSystem.Ai;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.CraftingSystem;
using Nivalis.Locale;
using NivalisModKit;
using UnityEngine;

namespace NivalisNightCity;

internal static class OperatingSetup
{
    internal sealed class Hours { public Hours() { } public float Start { get; set; } public float End { get; set; } }
    private static readonly Dictionary<IntPtr, (Venue Venue, Vector2 Hours)> originalHours = new();
    internal static object FocusMenu(VenueAreaGhost venue, int count)
    {
        if (count < 4 || count > Math.Min(12, venue.MenuLimit)) throw new ArgumentOutOfRangeException("menu size");
        var location = Singleton<DemographicsManager>.Instance.GetGroupsForScene(venue.SceneIndex);
        var groups = new List<(DemographicGroup Group, float Weight)>();
        if (location != null)
            foreach (var group in location.groups) groups.Add((group.group, group.representation));
        float Score(MealRecipeDefinition recipe)
        {
            int price = Math.Max(100, (int)Math.Ceiling(recipe.CurrentBasePrice * 1.75));
            if (groups.Count == 0) return 1f / Math.Max(1, recipe.CurrentBasePrice);
            return groups.Sum(g => g.Weight * g.Group.JudgeRecipeByTaste(recipe.Recipe, price, venue.SceneIndex));
        }
        var allowed = venue.Venue.RecipeType;
        var recipes = new List<MealRecipeDefinition>();
        foreach (var recipe in Singleton<MealDatabase>.Instance._guidMealRecipeMap.Values)
            if (!recipe.Disabled && !recipe.PlayerOnly && (allowed.Length == 0 || allowed.Contains(recipe.RecipeType)) &&
                venue.HasProcessors(recipe)) recipes.Add(recipe);
        var selected = recipes.OrderByDescending(Score).ThenBy(r => r.IngredientsPrice).Take(count).ToArray();
        if (selected.Length < count) throw new InvalidOperationException("not enough compatible recipes; existing menu preserved");
        venue.Menu.Clear();
        foreach (var recipe in selected)
            venue.Menu.Add(new MealMenuItem(recipe, Math.Max(100, (int)Math.Ceiling(recipe.CurrentBasePrice * 1.75))));
        venue.UpdateSupplyStates();
        venue.UpdatePopularity();
        return new { count = venue.Menu.Count, meals = selected.Select(r => Items.NameOf(r.Output.type)).ToArray() };
    }
    internal static void RestoreDefinitions()
    {
        foreach (var entry in originalHours.Values) entry.Venue.openTime = entry.Hours;
        originalHours.Clear();
    }
    internal static void LoadHours()
    {
        var profiles = SaveData.For(Plugin.Id).Get("business-hours", new Dictionary<string, Hours>());
        foreach (var venue in Venues.PlayerOwned)
            if (profiles.TryGetValue(venue.Venue.Guid, out var hours)) SetHours(venue, hours.Start, hours.End, false, false);
    }
    internal static object SetHours(VenueAreaGhost venue, float start, float end, bool store = true, bool align = true)
    {
        if (start < 8 || end > 32 || end <= start || end - start < 4 || end - start > 16)
            throw new ArgumentOutOfRangeException("opening hours");
        if (!originalHours.ContainsKey(venue.Venue.Pointer))
            originalHours[venue.Venue.Pointer] = (venue.Venue, venue.Venue.OpenTime);
        var old = venue.Venue.OpenTime;
        if (old.x < 8) old.x += 24;
        if (old.y <= old.x) old.y += 24;
        if (align) foreach (var member in Venues.StaffOf(venue))
        {
            var data = member.Person.RuntimeData;
            float from = (data.WorkingHours.x - old.x) / Math.Max(1, old.y - old.x);
            float to = (data.WorkingHours.y - old.x) / Math.Max(1, old.y - old.x);
            data.WorkingHours = new Vector2(start + Math.Clamp(from, 0, 1) * (end - start),
                start + Math.Clamp(to, 0, 1) * (end - start));
            data.ReportWorkingConditionChange();
        }
        venue.Venue.openTime = new Vector2(start, end);
        venue.ReportStaffWorkingHoursChanged();
        if (store)
        {
            var profiles = SaveData.For(Plugin.Id).Get("business-hours", new Dictionary<string, Hours>());
            profiles[venue.Venue.Guid] = new Hours { Start = start, End = end };
            SaveData.For(Plugin.Id).Set("business-hours", profiles);
        }
        return new { start, end };
    }
    internal static object SwapCrew(VenueAreaGhost a, VenueAreaGhost b)
    {
        var first = Venues.StaffOf(a).Select(s => (Person: s.Person, Roles: s.Person.RuntimeData.Tasks, Wage: s.Wage,
            Shift: s.Person.RuntimeData.WorkingHours)).ToArray();
        var second = Venues.StaffOf(b).Select(s => (Person: s.Person, Roles: s.Person.RuntimeData.Tasks, Wage: s.Wage,
            Shift: s.Person.RuntimeData.WorkingHours)).ToArray();
        if (first.Length > b.StaffLimit || second.Length > a.StaffLimit) throw new InvalidOperationException("crew exceeds venue limits");
        var native = Singleton<VenueManager>.Instance;
        foreach (var p in first) native.FireStaff(a.Venue, p.Person);
        foreach (var p in second) native.FireStaff(b.Venue, p.Person);
        foreach (var entry in new[] { (Venue: a, Staff: second), (Venue: b, Staff: first) })
        {
            foreach (var p in entry.Staff)
            {
                native.HireStaff(entry.Venue.Venue, p.Person);
                if (p.Person.RuntimeData.WorksAt?.Pointer != entry.Venue.Venue.Pointer)
                    throw new InvalidOperationException("native crew reassignment refused; keep the lab held");
                native.ChangeStaffTasks(entry.Venue.Venue, p.Person.RuntimeData, p.Roles);
                p.Person.RuntimeData.Wage = p.Wage;
                p.Person.RuntimeData.WorkingHours = p.Shift;
                p.Person.RuntimeData.ReportWorkingConditionChange();
            }
            entry.Venue.ReportStaffWorkingHoursChanged();
        }
        return new { swapped = true, first = a.Venue.Guid, second = b.Venue.Guid };
    }
    // Two complete service teams cover opening hours; one manager covers both.
    // Preserve real native wages and use qualified, already hired staff only.
    internal static object Align(VenueAreaGhost venue)
    {
        var staff = Venues.StaffOf(venue).Select(s => s.Person).ToArray();
        var native = Singleton<VenueManager>.Instance;
        var manager = staff.Where(p => p.HasSkills(VenueTasks.Managing))
            .OrderBy(p => p.RuntimeData.Wage).FirstOrDefault();
        if (manager == null) return new { aligned = false, reason = "manager required" };
        var workers = staff.Where(p => p.Pointer != manager.Pointer).ToArray();
        if (workers.Length > 12) return new { aligned = false, reason = "roster exceeds bounded planner limit" };
        var roles = VenueTasks.Cooking | VenueTasks.Serving | VenueTasks.Cleaning;
        var profiles = workers.Select(p => new[] { VenueTasks.Cooking, VenueTasks.Serving, VenueTasks.Cleaning }
            .Where(p.HasSkills).Aggregate((VenueTasks)0, (mask, role) => mask | role)).ToArray();
        VenueTasks Cover(int mask)
        {
            VenueTasks result = 0;
            for (int i = 0; i < workers.Length; i++)
                if ((mask & (1 << i)) != 0) result |= profiles[i];
            return result;
        }
        bool Complete(int mask)
        {
            if (Cover(mask) != roles) return false;
            for (int cook = 0; cook < workers.Length; cook++)
                if ((mask & (1 << cook)) != 0 && (profiles[cook] & VenueTasks.Cooking) != 0)
                    for (int server = 0; server < workers.Length; server++)
                        if (server != cook && (mask & (1 << server)) != 0 &&
                            (profiles[server] & VenueTasks.Serving) != 0) return true;
            return false;
        }
        var teams = new List<(int Mask, long Wage)>();
        for (int mask = 1; mask < (1 << workers.Length); mask++)
        {
            if (!Complete(mask)) continue;
            bool redundant = false;
            for (int i = 0; i < workers.Length; i++)
                if ((mask & (1 << i)) != 0 && Complete(mask & ~(1 << i))) { redundant = true; break; }
            if (redundant) continue;
            long wage = 0;
            for (int i = 0; i < workers.Length; i++)
                if ((mask & (1 << i)) != 0) wage += workers[i].RuntimeData.Wage;
            teams.Add((mask, wage));
        }
        int first = 0, second = 0;
        long best = long.MaxValue;
        foreach (var a in teams) foreach (var b in teams)
            if ((a.Mask & b.Mask) == 0 && a.Wage + b.Wage < best)
            { first = a.Mask; second = b.Mask; best = a.Wage + b.Wage; }
        if (first == 0) return new { aligned = false, reason = "two independent qualified service teams required" };
        float start = venue.Venue.OpenTime.x, end = venue.Venue.OpenTime.y;
        if (start < 8) start += 24;
        if (end <= start) end += 24;
        end = Math.Min(end, start + 24);
        float middle = (start + end) / 2;
        native.ChangeStaffTasks(venue.Venue, manager.RuntimeData, VenueTasks.Managing);
        manager.RuntimeData.WorkingHours = new Vector2(start, end);
        manager.RuntimeData.ReportWorkingConditionChange();
        int released = 0;
        for (int i = 0; i < workers.Length; i++)
        {
            var person = workers[i];
            if (((first | second) & (1 << i)) == 0)
            { native.FireStaff(venue.Venue, person); released++; continue; }
            native.ChangeStaffTasks(venue.Venue, person.RuntimeData, profiles[i]);
            person.RuntimeData.WorkingHours = (first & (1 << i)) != 0 ?
                new Vector2(start, middle) : new Vector2(middle, end);
            person.RuntimeData.ReportWorkingConditionChange();
        }
        venue.ReportStaffWorkingHoursChanged();
        return new { aligned = true, start, end, handover = middle, released,
            staff = Venues.StaffOf(venue).Count,
            expectedDailyWagesHundredths = (best / 2.0 + manager.RuntimeData.Wage) * (end - start) };
    }
}
