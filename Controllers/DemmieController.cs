using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Demmie,Admin")]
    public class DemmieController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public DemmieController(UserManager<User> userManager, AppDbContext context, NotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }


        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge(); 

            var demmie = await _context.Demmies
                .Include(d => d.DemmieModules)
                    .ThenInclude(dm => dm.Module)
                .Include(d => d.DemmieSessions)
                    .ThenInclude(ds => ds.Session)
                .ThenInclude(s => s.Module)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (demmie == null)
            {
                TempData["Error"] = "No Demmie profile found for your account.";
                return RedirectToAction("Index", "Home");
            }

            // Calculate stats
            int totalSessions = demmie.DemmieSessions?.Count ?? 0;
            int pendingNotifications = await _context.Notifications
            .CountAsync(n => n.UserId == user.Id && !n.IsRead);


            ViewBag.Name = $"{user.FirstName}";
            ViewBag.TotalSessions = totalSessions;
            ViewBag.TotalHours = demmie.HoursWorkedThisWeek;
            ViewBag.Notifications = pendingNotifications;
            ViewBag.AssignedModules = demmie.DemmieModules?.Select(m => m.Module?.ModuleCode).ToList();

            return View(demmie);
        }

        [Authorize(Roles = "Demmie")]
        public async Task<IActionResult> Availability() => View();

        [Authorize(Roles = "Demmie")]
        public async Task<IActionResult> Appointments()
        {
            var user = await _userManager.GetUserAsync(User);
            var demmie = await _context.Demmies
                .Include(d => d.DemmieSessions)
                .ThenInclude(ds => ds.Session)
                .ThenInclude(s => s.Module)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            return View(demmie?.DemmieSessions);
        }

        [Authorize(Roles = "Demmie")]
        public IActionResult HourTracking()
        {
            var user = _userManager.GetUserAsync(User).Result;
            var demmie = _context.Demmies.FirstOrDefault(d => d.UserId == user.Id);
            return View(demmie);
        }

        [HttpPost]
        [Authorize(Roles = "Demmie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHours(int additionalHours)
        {
            var user = await _userManager.GetUserAsync(User);

            var demmie = await _context.Demmies
                .Include(d => d.DemmieModules)
                .ThenInclude(dm => dm.Module)
                .ThenInclude(m => m.LecturerModules)
                .ThenInclude(lm => lm.Lecturer)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (demmie == null)
                return NotFound();

            if (additionalHours <= 0)
            {
                TempData["Error"] = "Please enter a valid number of hours.";
                return RedirectToAction("HourTracking");
            }

            // Always update hours
            demmie.HoursWorkedThisWeek += additionalHours;
            _context.Update(demmie);
            await _context.SaveChangesAsync();

            // Check if the limit is exceeded
            if (demmie.HoursWorkedThisWeek > demmie.WeeklyHourLimit)
            {
                TempData["Warning"] = "You’ve exceeded your weekly limit! Please contact your lecturer.";

                // Send notification to demmie
                await _notificationService.SendAsync(
                    demmie.UserId,
                    "Weekly Hour Limit Exceeded",
                    $"You’ve worked {demmie.HoursWorkedThisWeek} hours, exceeding your weekly limit of {demmie.WeeklyHourLimit}.",
                    "Workload"
                );

                // Notify related lecturers
                var lecturerUserIds = demmie.DemmieModules?
                    .SelectMany(dm => dm.Module!.LecturerModules!)
                    .Select(lm => lm.Lecturer!.UserId)
                    .Distinct()
                    .ToList();

                if (lecturerUserIds != null && lecturerUserIds.Any())
                {
                    foreach (var lecturerUserId in lecturerUserIds)
                    {
                        await _notificationService.SendAsync(
                            lecturerUserId,
                            "Demmie Exceeded Weekly Hours",
                            $"Your demmie {demmie.FirstName} {demmie.LastName} has exceeded their weekly hour limit of {demmie.WeeklyHourLimit} (current total: {demmie.HoursWorkedThisWeek}).",
                            "Demmie Workload",
                            relatedId: demmie.DemmieId,
                            relatedType: "Demmie"
                        );
                    }
                }
            }

            TempData["Success"] = $"{additionalHours} hour(s) added successfully!";
            return RedirectToAction("HourTracking");
        }



        [Authorize(Roles = "Demmie")]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == user.Id);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            // Redirect back to Demmie or Lecturer view if provided
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
