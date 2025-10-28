using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class ConflictController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ConflictService _conflictService;
        private readonly UserManager<User> _userManager;

        public ConflictController(AppDbContext context, ConflictService conflictService, UserManager<User> userManager)
        {
            _context = context;
            _conflictService = conflictService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            bool isLecturer = await _userManager.IsInRoleAsync(user, "Lecturer");

            IQueryable<Conflict> query = _context.Conflicts
                .Include(c => c.Session1).ThenInclude(s => s.Module)
                .Include(c => c.Session2).ThenInclude(s => s.Module)
                .Include(c => c.Student)
                .Include(c => c.Venue);

            if (isLecturer)
            {
                //  Find lecturer’s modules
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null)
                {
                    TempData["Error"] = "No lecturer profile found for this account.";
                    return RedirectToAction("Index", "Home");
                }

                var moduleIds = lecturer.LecturerModules.Select(m => m.ModuleId).ToList();

                //  Filter conflicts to lecturer-related sessions only
                query = query.Where(c =>
                    (c.Session1 != null && moduleIds.Contains(c.Session1.ModuleId)) ||
                    (c.Session2 != null && moduleIds.Contains(c.Session2.ModuleId))
                );
            }

            var conflicts = await query
            .Where(c => !c.IsResolved)
            .OrderBy(c => c.Type)
            .ToListAsync();


            var grouped = conflicts
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(grouped);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id)
        {
            var conflict = await _context.Conflicts.FindAsync(id);
            if (conflict == null) return NotFound();

            conflict.IsResolved = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Conflict marked as resolved.";
            return RedirectToAction(nameof(Index));
        }

        // Automatically apply the suggested fix
        [HttpPost]
        public async Task<IActionResult> ApplySuggestion(int id)
        {
            var conflict = await _context.Conflicts
                .Include(c => c.Session1)
                .Include(c => c.Session2)
                .Include(c => c.Student)
                .Include(c => c.Venue)
                .FirstOrDefaultAsync(c => c.ConflictId == id);

            if (conflict == null)
                return NotFound();

            string message = string.Empty;

            switch (conflict.Type)
            {
                case "Venue":
                    // Move session2 to another available venue
                    var requiredCapacity = conflict.Session1?.CapacityOverride ?? 0;
                    var altVenue = await _context.Venues
                        .FirstOrDefaultAsync(v => v.VenueId != conflict.VenueId && v.Capacity >= requiredCapacity);
                    if (altVenue != null && conflict.Session2 != null)
                    {
                        conflict.Session2.VenueId = altVenue.VenueId;
                        message = $"Session moved to {altVenue.Name}.";
                    }
                    break;

                case "Student":
                    // Drop one of the conflicting allocations
                    var allocation = await _context.Allocations
                        .FirstOrDefaultAsync(a => a.StudentId == conflict.StudentId && a.SessionId == conflict.SessionId2);
                    if (allocation != null)
                    {
                        _context.Allocations.Remove(allocation);
                        message = "Removed one of the conflicting student allocations.";
                    }
                    break;

                case "Demmie":
                    if (conflict.Session2 == null)
                    {
                        message = "Unable to process: Session2 not found.";
                        break;
                    }

                    // Find the conflicting Demmie
                    var conflictingDemmie = await _context.Demmies
                        .Include(d => d.DemmieSessions)
                        .FirstOrDefaultAsync(d =>
                            d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId1) &&
                            d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId2));

                    if (conflictingDemmie == null)
                    {
                        message = "Unable to find conflicting Demmie assignment.";
                        break;
                    }

                    // Remove the specific bridge for conflicting 
                    var existingBridge = await _context.DemmieSessions
                        .FirstOrDefaultAsync(ds => ds.DemmieId == conflictingDemmie.DemmieId &&
                                                   ds.SessionId == conflict.SessionId2);
                    if (existingBridge != null)
                    {
                        _context.DemmieSessions.Remove(existingBridge);
                    }

                    // Update conflicting Demmie's IsAssigned if no sessions left
                    var remainingSessions = await _context.DemmieSessions
                        .CountAsync(ds => ds.DemmieId == conflictingDemmie.DemmieId);
                    conflictingDemmie.IsAssigned = remainingSessions > 0;
                    if (!conflictingDemmie.IsAssigned)
                    {
                        conflictingDemmie.AssignedDate = null;
                    }

                    // Find an available Demmie 
                    var availableDemmie = await _context.Demmies
                        .OrderBy(d => d.FirstName)
                        .FirstOrDefaultAsync(d => !d.IsAssigned);

                    if (availableDemmie == null)
                    {
                        message = "No available Demmies found for reassignment.";
                        
                        await _context.SaveChangesAsync();
                        TempData["Warning"] = message + " (Original assignment removed, but no replacement assigned.)";
                        return RedirectToAction(nameof(Index));
                    }

                    // Add new bridge for available 
                    _context.DemmieSessions.Add(new bridgeDemmie_Session
                    {
                        DemmieId = availableDemmie.DemmieId,
                        SessionId = conflict.SessionId2.Value,
                        DemmieName = $"{availableDemmie.FirstName} {availableDemmie.LastName}",
                        ModuleCode = conflict.Session2.Module?.ModuleCode ?? "Unknown",
                        WeekDay = conflict.Session2.WeekDay ?? "Unknown",
                        VenueName = conflict.Session2.Venue?.Name ?? "Unassigned"
                    });

                    // Mark available Demmie as assigned
                    availableDemmie.IsAssigned = true;
                    availableDemmie.AssignedDate = DateTime.UtcNow;

                    message = $"Reassigned Session2 from {conflictingDemmie.FirstName} {conflictingDemmie.LastName} to {availableDemmie.FirstName} {availableDemmie.LastName}.";
                    conflict.IsResolved = true;
                    break;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Applied suggestion successfully! {message}";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManualOverride(int id)
        {
            var conflict = await _context.Conflicts
                .Include(c => c.Session1).ThenInclude(s => s.Module)
                .Include(c => c.Session2).ThenInclude(s => s.Module)
                .Include(c => c.Session2).ThenInclude(s => s.Venue)
                .Include(c => c.Student)
                .Include(c => c.Venue)
                .FirstOrDefaultAsync(c => c.ConflictId == id);

            if (conflict == null)
                return NotFound();

            // Type-specific ViewBag options
            switch (conflict.Type)
            {
                case "Venue":
                    // Alternative venues: similar capacity, exclude current
                    var requiredCapacity = conflict.Session2?.CapacityOverride ?? conflict.Session1?.CapacityOverride ?? 0;
                    ViewBag.AlternativeVenues = new SelectList(
                        await _context.Venues
                            .Where(v => v.VenueId != conflict.VenueId && v.Capacity >= requiredCapacity)
                            .OrderBy(v => v.Name)
                            .Select(v => new { v.VenueId, Display = $"{v.Name} (Capacity: {v.Capacity})" })
                            .ToListAsync(),
                        "VenueId",
                        "Display"
                    );
                    break;

                case "Student":
                    // All other sessions (manual will check overlaps)
                    ViewBag.AlternativeSessions = new SelectList(
                        await _context.Sessions
                            .Include(s => s.Module)
                            .Include(s => s.Venue)  
                            .Where(s => s.SessionId != conflict.SessionId1 && s.SessionId != conflict.SessionId2)
                            .OrderBy(s => s.Module.ModuleCode)
                            .ThenBy(s => s.WeekDay)
                            .Select(s => new
                            {
                                s.SessionId,
                                Display = $"{s.Module.ModuleCode} ({s.WeekDay} {s.StartTime}-{s.EndTime} in {s.Venue.Name})"
                            })
                            .ToListAsync(),
                        "SessionId",
                        "Display"
                    );
                    break;

                case "Demmie":
                    // Available Demmies 
                    ViewBag.AvailableDemmies = new SelectList(
                        await _context.Demmies
                            //.Where(d => !d.IsAssigned)
                            .Where(d => d.HoursWorkedThisWeek < d.WeeklyHourLimit)
                            .OrderBy(d => d.FirstName)
                            .ThenBy(d => d.LastName)
                            .Select(d => new
                            {
                                d.DemmieId,
                                Display = $"{d.FirstName} {d.LastName} (Hours this week: {d.HoursWorkedThisWeek}/{d.WeeklyHourLimit})"
                            })
                            .ToListAsync(),
                        "DemmieId",
                        "Display"
                    );
                    // Also load conflicting Demmie for display
                    var conflictingDemmie = await _context.Demmies
                        .Include(d => d.DemmieSessions)
                        .FirstOrDefaultAsync(d =>
                            d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId1) &&
                            d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId2));
                    ViewBag.ConflictingDemmie = conflictingDemmie;
                    break;
            }

            return PartialView("_ManualOverrideModal", conflict);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualOverride(int id, string type, int? newVenueId = null, int? newSessionId = null, int? newDemmieId = null)
        {
            try
            {
                
                var conflict = await _context.Conflicts
                    .Include(c => c.Session1)
                    .Include(c => c.Session2).ThenInclude(s => s.Module)
                    .Include(c => c.Session2).ThenInclude(s => s.Venue)
                    .Include(c => c.Student)
                    .Include(c => c.Venue)
                    .FirstOrDefaultAsync(c => c.ConflictId == id);

                if (conflict == null || conflict.Type != type)
                    return NotFound();

                string msg = "No changes applied.";

                switch (type)
                {
                    case "Venue":
                        if (newVenueId.HasValue && conflict.Session2 != null)
                        {
                            // Check if new venue is available
                            var newVenue = await _context.Venues.FindAsync(newVenueId.Value);
                            if (newVenue != null && newVenue.VenueId != conflict.VenueId)
                            {
                                conflict.Session2.VenueId = newVenueId.Value;
                                msg = $"Session reassigned to venue: {newVenue.Name}.";
                            }
                            else
                            {
                                TempData["Error"] = "Invalid venue selection.";
                                return RedirectToAction(nameof(Index));
                            }
                        }
                        break;

                    case "Student":
                        if (newSessionId.HasValue)
                        {
                            var allocation = await _context.Allocations
                                .FirstOrDefaultAsync(a => a.StudentId == conflict.StudentId && a.SessionId == conflict.SessionId2);
                            if (allocation != null)
                            {
                                // Remove from old session, add to new
                                allocation.SessionId = newSessionId.Value;
                                msg = "Student reassigned to a new session.";
                            }
                            else
                            {
                                TempData["Error"] = "Student allocation not found.";
                                return RedirectToAction(nameof(Index));
                            }
                        }
                        break;

                    case "Demmie":
                        if (newDemmieId.HasValue)
                        {
                            // Find conflicting Demmie
                            var conflictingDemmie = await _context.Demmies
                                .Include(d => d.DemmieSessions)
                                .FirstOrDefaultAsync(d =>
                                    d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId1) &&
                                    d.DemmieSessions.Any(ds => ds.SessionId == conflict.SessionId2));

                            if (conflictingDemmie == null)
                            {
                                TempData["Error"] = "Conflicting Demmie assignment not found.";
                                return RedirectToAction(nameof(Index));
                            }

                            var availableDemmie = await _context.Demmies.FindAsync(newDemmieId.Value);
                            if (availableDemmie == null || availableDemmie.IsAssigned)
                            {
                                TempData["Error"] = "Invalid Demmie selection.";
                                return RedirectToAction(nameof(Index));
                            }

                            // Remove old bridge
                            var existingBridge = await _context.DemmieSessions
                                .FirstOrDefaultAsync(ds => ds.DemmieId == conflictingDemmie.DemmieId &&
                                                           ds.SessionId == conflict.SessionId2);
                            if (existingBridge != null)
                            {
                                _context.DemmieSessions.Remove(existingBridge);
                            }

                            // Update conflicting Demmie's IsAssigned
                            var remainingSessions = await _context.DemmieSessions.CountAsync(ds => ds.DemmieId == conflictingDemmie.DemmieId);
                            conflictingDemmie.IsAssigned = remainingSessions > 0;
                            if (!conflictingDemmie.IsAssigned)
                            {
                                conflictingDemmie.AssignedDate = null;
                            }

                            // Add new bridge 
                            if (conflict.Session2 != null)
                            {
                                _context.DemmieSessions.Add(new bridgeDemmie_Session
                                {
                                    DemmieId = newDemmieId.Value,
                                    SessionId = conflict.SessionId2.Value,
                                    DemmieName = $"{availableDemmie.FirstName} {availableDemmie.LastName}",
                                    ModuleCode = conflict.Session2.Module?.ModuleCode ?? "Unknown",
                                    WeekDay = conflict.Session2.WeekDay ?? "Unknown",
                                    VenueName = conflict.Session2.Venue?.Name ?? "Unassigned"
                                });
                            }

                            // Mark new Demmie as assigned
                            availableDemmie.IsAssigned = true;
                            availableDemmie.AssignedDate = DateTime.UtcNow;

                            msg = $"Demmie reassigned from {conflictingDemmie.FirstName} {conflictingDemmie.LastName} to {availableDemmie.FirstName} {availableDemmie.LastName}.";
                        }
                        break;
                }

                conflict.IsResolved = true;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Manual override applied. {msg}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to apply manual override: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DetectConflicts()
        {
            try
            {
                // Run the conflict detection service again
                await _conflictService.DetectConflictsAsync();

                TempData["Success"] = "Conflict detection completed successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while re-running conflict detection: {ex.Message}";
            }

            // Redirect back to the main conflict dashboard
            return RedirectToAction(nameof(Index));
        }
    }
}
