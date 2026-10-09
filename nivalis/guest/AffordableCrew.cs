using System;
using System.Collections.Generic;
using System.Linq;
using Nivalis;
using Nivalis.GhostSystem.Ai;
using Nivalis.GhostSystem.CustomerLoop;
using NivalisModKit;
using UnityEngine;

namespace NivalisNightCity;

internal static class AffordableCrew
{
    private sealed record Applicant(Person Person, int Wage, VenueTasks Roles);
    private sealed record Team(Applicant[] People, long Wage);
    private static readonly VenueTasks Service = VenueTasks.Cooking | VenueTasks.Serving | VenueTasks.Cleaning;

    internal static object Apply(VenueAreaGhost venue)
    {
        var roster = Venues.StaffOf(venue).Select(s => s.Person).ToArray();
        var local = roster.Select(p => p.Pointer).ToHashSet();
        var claimed = Venues.PlayerOwned.SelectMany(Venues.StaffOf).Select(s => s.Person.Pointer).ToHashSet();
        var pool = new List<Applicant>();
        foreach (var person in Singleton<PersonDataManager>.Instance.guidToPersons.Values)
        {
            var data = person.RuntimeData;
            if (data == null || (!local.Contains(person.Pointer) &&
                (!data.Hireable || data.HasWork || claimed.Contains(person.Pointer)))) continue;
            VenueTasks roles = 0;
            foreach (var role in new[] { VenueTasks.Cooking, VenueTasks.Serving, VenueTasks.Cleaning, VenueTasks.Managing })
                if (person.HasSkills(role)) roles |= role;
            if (roles == 0) continue;
            int wage = data.Wage;
            if (!local.Contains(person.Pointer))
            {
                var originalTasks = data.Tasks;
                try { data.Tasks = roles; wage = Math.Max(100, Math.Max(data.Wage, data.CalculateBaseWage().y)); }
                finally { data.Tasks = originalTasks; }
            }
            pool.Add(new Applicant(person, wage, roles));
        }
        pool = pool.OrderBy(p => p.Wage).ThenBy(p => p.Person.Guid).ToList();
        var cooks = pool.Where(p => (p.Roles & VenueTasks.Cooking) != 0).Take(24).ToArray();
        var servers = pool.Where(p => (p.Roles & VenueTasks.Serving) != 0).Take(24).ToArray();
        var cleaners = pool.Where(p => (p.Roles & VenueTasks.Cleaning) != 0).Take(24).ToArray();
        List<Team> Teams(HashSet<IntPtr> excluded)
        {
            var result = new List<Team>();
            foreach (var cook in cooks) foreach (var server in servers)
            {
                if (cook.Person.Pointer == server.Person.Pointer || excluded.Contains(cook.Person.Pointer) ||
                    excluded.Contains(server.Person.Pointer)) continue;
                if (((cook.Roles | server.Roles) & Service) == Service)
                    result.Add(new Team(new[] { cook, server }, (long)cook.Wage + server.Wage));
                else foreach (var cleaner in cleaners.Where(p => !excluded.Contains(p.Person.Pointer) &&
                    p.Person.Pointer != cook.Person.Pointer && p.Person.Pointer != server.Person.Pointer).Take(3))
                    result.Add(new Team(new[] { cook, server, cleaner }, (long)cook.Wage + server.Wage + cleaner.Wage));
            }
            return result.OrderBy(t => t.Wage).ToList();
        }
        Applicant? manager = null;
        Team? first = null, second = null;
        long best = long.MaxValue;
        foreach (var candidate in pool.Where(p => (p.Roles & VenueTasks.Managing) != 0).Take(5))
        {
            var excluded = new HashSet<IntPtr> { candidate.Person.Pointer };
            foreach (var a in Teams(excluded).Take(32))
            {
                var nextExcluded = new HashSet<IntPtr>(excluded);
                foreach (var p in a.People) nextExcluded.Add(p.Person.Pointer);
                var b = Teams(nextExcluded).FirstOrDefault();
                if (b == null || a.People.Length + b.People.Length + 1 > venue.StaffLimit) continue;
                long cost = a.Wage + b.Wage + 2L * candidate.Wage;
                if (cost >= best) continue;
                best = cost; manager = candidate; first = a; second = b;
            }
        }
        if (manager == null || first == null || second == null)
            throw new InvalidOperationException("not enough independent qualified applicants; existing roster preserved");
        float start = venue.Venue.OpenTime.x, end = venue.Venue.OpenTime.y;
        if (start < 8) start += 24;
        if (end <= start) end += 24;
        end = Math.Min(end, start + 24);
        float middle = (start + end) / 2;
        double before = roster.Sum(p => p.RuntimeData.Wage * Math.Max(0, p.RuntimeData.WorkingHours.y - p.RuntimeData.WorkingHours.x));
        double after = best * (end - start) / 2;
        if (after >= before) return new { replaced = false, before, after = before, reason = "existing crew already costs less" };
        var selected = first.People.Concat(second.People).Append(manager).ToArray();
        var keep = selected.Select(p => p.Person.Pointer).ToHashSet();
        var native = Singleton<VenueManager>.Instance;
        int released = 0, hired = 0;
        foreach (var person in roster)
            if (!keep.Contains(person.Pointer)) { native.FireStaff(venue.Venue, person); released++; }
        foreach (var applicant in selected)
        {
            var person = applicant.Person;
            if (!local.Contains(person.Pointer))
            {
                native.HireStaff(venue.Venue, person);
                if (person.RuntimeData.WorksAt?.Pointer != venue.Venue.Pointer)
                    throw new InvalidOperationException("native hire refused; keep the lab held for recovery");
                hired++;
            }
            var data = person.RuntimeData;
            data.Wage = applicant.Wage;
            bool isManager = person.Pointer == manager.Person.Pointer;
            native.ChangeStaffTasks(venue.Venue, data, isManager ? VenueTasks.Managing : applicant.Roles & Service);
            data.WorkingHours = isManager ? new Vector2(start, end) :
                first.People.Any(p => p.Person.Pointer == person.Pointer) ? new Vector2(start, middle) : new Vector2(middle, end);
            foreach (var skill in new[] { StaffSkills.Cooking, StaffSkills.Serving, StaffSkills.Cleaning, StaffSkills.Managing })
                if (skill != null && StaffSkills.Has(person, skill))
                {
                    float xp = skill.GetExperienceForLevel(skill.LevelCount);
                    if (!float.IsFinite(xp) || xp < 0) throw new InvalidOperationException("invalid native training cap");
                    data.Skills[skill] = xp;
                }
            data.ReportWorkingConditionChange();
            if (Plugin.Active?.KeepStaffHappy == true) SandboxSetup.MakeHappy(data);
        }
        venue.ReportStaffWorkingHoursChanged();
        return new { replaced = true, hired, released, staff = selected.Length,
            beforeDailyWagesHundredths = before, afterDailyWagesHundredths = after };
    }
}
