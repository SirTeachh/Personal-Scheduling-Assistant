using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;
using PersonalSchedulingAssistant.Models.ViewModels;
using PersonalSchedulingAssistant.Services;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class DemmieDistributionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly NotificationService _notificationService;

        public DemmieDistributionController(AppDbContext context, UserManager<User> userManager, NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lecturer = await _context.Lecturers
                .Include(l => l.LecturerModules)
                .FirstOrDefaultAsync(l => l.UserId == user.Id);

            if (lecturer == null)
            {
                TempData["Error"] = "No lecturer profile found for this account.";
                return RedirectToAction("Index", "Home");
            }

            var moduleIds = lecturer.LecturerModules.Select(m => m.ModuleId).ToList();

            // Get lecturer sessions
            var sessions = await _context.Sessions
                .Include(s => s.Module)
                .Include(s => s.Venue)
                .Where(s => moduleIds.Contains(s.ModuleId))
                .OrderBy(s => s.WeekDay)
                .ToListAsync();

            // Get all demmies
            var allDemmies = await _context.Demmies
                .Include(d => d.User)
                .Include(d => d.Availabilities)
                .OrderBy(d => d.User.FirstName)
                .ToListAsync();

            // Map session to available demmies
            var sessionDemmieAvailability = new Dictionary<int, List<Demmie>>();

            foreach (var session in sessions)
            {
                var sessionStart = TimeSpan.Parse(session.StartTime);
                var sessionEnd = TimeSpan.Parse(session.EndTime);

                var available = allDemmies
                    .Where(d =>
                        d.Availabilities != null &&
                        d.Availabilities.Any(a =>
                            a.WeekDay == session.WeekDay &&
                            a.IsAvailable &&
                            a.StartTime <= sessionStart &&
                            a.EndTime >= sessionEnd))
                    .ToList();

                sessionDemmieAvailability[session.SessionId] = available;
            }

            // Get assigned demmies for lecturer’s sessions
            var assignedDemmies = await _context.DemmieSessions
                .Include(b => b.Demmie)
                    .ThenInclude(d => d.User)
                .Where(b => sessions.Select(s => s.SessionId).Contains(b.SessionId))
                .ToListAsync();

            // Build mapping for lookup
            var assignedMap = assignedDemmies
                .GroupBy(b => b.SessionId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Demmie!).ToList());

            // Build the view model
            var vm = new DemmieDistributionViewModel
            {
                LecturerName = $"{user.FirstName} {user.SecondName}",
                Sessions = sessions,
                AvailableDemmies = allDemmies,
                AssignedDemmies = assignedMap,
                SessionAvailableDemmies = sessionDemmieAvailability
            };

            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> Assign(int sessionId, int demmieId)
        {
            var session = await _context.Sessions
                .Include(s => s.Module)
                .Include(s => s.Venue)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            var demmie = await _context.Demmies
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DemmieId == demmieId);

            if (session == null || demmie == null)
                return NotFound();

            bool alreadyAssigned = await _context.DemmieSessions
                .AnyAsync(b => b.SessionId == sessionId && b.DemmieId == demmieId);

            if (alreadyAssigned)
            {
                TempData["Error"] = "This Demmie is already assigned to this session.";
                return RedirectToAction(nameof(Index));
            }

            // Create Session Bridge
            var sessionBridge = new bridgeDemmie_Session
            {
                SessionId = session.SessionId,
                DemmieId = demmie.DemmieId,
                DemmieName = $"{demmie.FirstName} {demmie.LastName}",
                ModuleCode = session.Module?.ModuleCode,
                WeekDay = session.WeekDay,
                VenueName = session.Venue?.Name
            };

            _context.DemmieSessions.Add(sessionBridge);

            // Ensure a corresponding Module Bridge exists
            bool moduleAlreadyLinked = await _context.DemmieModules
                .AnyAsync(m => m.DemmieId == demmie.DemmieId && m.ModuleId == session.ModuleId);

            if (!moduleAlreadyLinked && session.Module != null)
            {
                _context.DemmieModules.Add(new bridgeDemmie_Module
                {
                    DemmieId = demmie.DemmieId,
                    ModuleId = session.Module.ModuleId,
                    DemmieName = $"{demmie.FirstName} {demmie.LastName}",
                    ModuleCode = session.Module.ModuleCode,
                    ModuleTitle = session.Module.ModuleName
                });
            }

            // Update Demmie assignment info
            demmie.IsAssigned = true;
            demmie.AssignedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            //send notification to demmie
            await _notificationService.SendAsync(
            demmie.UserId,
            "New Session Assignment",
            $"You have been assigned to a new session for {session.Module?.ModuleCode} on {session.WeekDay} at {session.StartTime}.",
            "Assignment",
            session.SessionId,
            "Session"
);


            TempData["Success"] = $"{demmie.User?.FirstName} assigned successfully.";
            return RedirectToAction(nameof(Index));
        }
 
        [HttpPost]
        public async Task<IActionResult> Unassign(int sessionId, int demmieId)
        {
            var bridge = await _context.DemmieSessions
                .Include(b => b.Session)
                .FirstOrDefaultAsync(b => b.SessionId == sessionId && b.DemmieId == demmieId);

            if (bridge == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction(nameof(Index));
            }

            var moduleId = bridge.Session?.ModuleId;
            _context.DemmieSessions.Remove(bridge);

            //  Remove module link if no sessions remain for it
            if (moduleId.HasValue)
            {
                bool stillAssignedToModule = await _context.DemmieSessions
                    .AnyAsync(ds => ds.DemmieId == demmieId && ds.Session!.ModuleId == moduleId.Value);

                if (!stillAssignedToModule)
                {
                    var moduleLink = await _context.DemmieModules
                        .FirstOrDefaultAsync(dm => dm.DemmieId == demmieId && dm.ModuleId == moduleId.Value);

                    if (moduleLink != null)
                        _context.DemmieModules.Remove(moduleLink);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Demmie unassigned successfully.";
            return RedirectToAction(nameof(Index));
        }

    }
}
