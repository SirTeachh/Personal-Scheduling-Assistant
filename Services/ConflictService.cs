using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

public class ConflictService
{
    private readonly AppDbContext _context;

    public ConflictService(AppDbContext context)
    {
        _context = context;
    }

    public async Task DetectConflictsAsync()
    {
        var existing = await _context.Conflicts.Where(c => !c.IsResolved).ToListAsync();
        _context.Conflicts.RemoveRange(existing);

        var sessions = await _context.Sessions
            .Include(s => s.Venue)
            .Include(s => s.Module)
            .ToListAsync();

        var venues = await _context.Venues.Include(v => v.Building).ToListAsync();

        // Venue Conflicts

        foreach (var s1 in sessions)
        {
            foreach (var s2 in sessions)
            {
                if (s1.SessionId >= s2.SessionId) continue;

                if (s1.VenueId == s2.VenueId &&
                    s1.WeekDay == s2.WeekDay &&
                    TimesOverlap(s1.StartTime, s1.EndTime, s2.StartTime, s2.EndTime))
                {
                    // Suggest alternative venues of similar capacity
                    var altVenues = venues
                        .Where(v => v.VenueId != s1.VenueId && v.Capacity >= (s1.CapacityOverride ?? v.Capacity))
                        .Select(v => $"{v.Name} ({v.Building?.BuildingName})")
                        .Take(3);

                    var suggestion = altVenues.Any()
                        ? $"Consider moving one session to: {string.Join(", ", altVenues)}."
                        : "No suitable alternative venues found.";

                        _context.Conflicts.Add(new Conflict
                        {
                            Type = "Venue",
                            Description = $"Venue conflict: {s1.Venue?.Name} double-booked on {s1.WeekDay} ({s1.StartTime}-{s1.EndTime}) for {s1.Module?.ModuleName} and {s2.Module?.ModuleName}.",
                            SessionId1 = s1.SessionId,
                            SessionId2 = s2.SessionId,
                            VenueId = s1.VenueId,
                            SuggestedResolution = suggestion
                        });
                }
            }
        }

        // Student Conflicts
        var allocations = await _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.Session).ThenInclude(s => s.Module)
            .ToListAsync();

        var grouped = allocations.GroupBy(a => a.StudentId);

        foreach (var group in grouped)
        {
            var list = group.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a1 = list[i];
                    var a2 = list[j];

                    if (a1.Session!.WeekDay == a2.Session!.WeekDay &&
                        TimesOverlap(a1.Session.StartTime, a1.Session.EndTime, a2.Session.StartTime, a2.Session.EndTime))
                    {
                        var suggestion = $"Move one session to a different time slot, suggestion: " +
                            $"{a1.Session.EndTime}–{AddHours(a1.Session.EndTime, 2)}.";

                        _context.Conflicts.Add(new Conflict
                        {
                            Type = "Student",
                            Description = $"Student {a1.Student?.FirstName} {a1.Student?.LastName} has overlapping sessions " +
                            $"({a1.Session.Module?.ModuleCode} and {a2.Session.Module?.ModuleCode}) on {a1.Session.WeekDay}.",
                            StudentId = a1.StudentId,
                            SessionId1 = a1.SessionId,
                            SessionId2 = a2.SessionId,
                            SuggestedResolution = suggestion
                        });
                    }
                }
            }
        }

        // Demmie Conflicts
        var demmieSessions = await _context.DemmieSessions
            .Include(d => d.Demmie).ThenInclude(u => u.User)
            .Include(d => d.Session).ThenInclude(s => s.Module)
            .ToListAsync();

        var groupedDems = demmieSessions.GroupBy(d => d.DemmieId);

        foreach (var group in groupedDems)
        {
            var list = group.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var d1 = list[i];
                    var d2 = list[j];

                    if (d1.Session!.WeekDay == d2.Session!.WeekDay &&
                        TimesOverlap(d1.Session.StartTime, d1.Session.EndTime, d2.Session.StartTime, d2.Session.EndTime))
                    {
                        // Suggest an unassigned demmie or reassignment
                        var availableDemmie = await _context.Demmies.FirstOrDefaultAsync(x => !x.IsAssigned);
                        string suggestion = availableDemmie != null
                            ? $"Consider reassigning one session to {availableDemmie.FirstName} {availableDemmie.LastName}."
                            : "No available demmies found for reassignment.";

                        _context.Conflicts.Add(new Conflict
                        {
                            Type = "Demmie",
                            Description = $"Demmie {d1.Demmie?.User?.FirstName} {d1.Demmie?.User?.SecondName} is assigned to overlapping " +
                            $"sessions ({d1.Session.Module?.ModuleCode} and {d2.Session.Module?.ModuleCode}) on {d1.Session.WeekDay}.",
                            SessionId1 = d1.SessionId,
                            SessionId2 = d2.SessionId,
                            SuggestedResolution = suggestion
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private bool TimesOverlap(string start1, string end1, string start2, string end2)
    {
        var t1Start = TimeSpan.Parse(start1);
        var t1End = TimeSpan.Parse(end1);
        var t2Start = TimeSpan.Parse(start2);
        var t2End = TimeSpan.Parse(end2);
        return t1Start < t2End && t2Start < t1End;
    }

    private string AddHours(string start, int hours)
    {
        var time = TimeSpan.Parse(start);
        var newTime = time.Add(TimeSpan.FromHours(hours));
        return newTime.ToString(@"hh\:mm");
    }
}