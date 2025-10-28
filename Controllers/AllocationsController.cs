using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;
using PersonalSchedulingAssistant.Models.ViewModels;
using PersonalSchedulingAssistant.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Authorize(Roles = "Lecturer,Admin")]
public class AllocationsController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly AllocationService _allocService;
    private readonly IEmailSender _emailSender;
    private readonly NotificationService _notificationService;
    public AllocationsController(AppDbContext context, UserManager<User> userManager, AllocationService allocService, IEmailSender emailSender, NotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _allocService = allocService;
        _emailSender = emailSender;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        return View();
    }

    //  Configure
    [HttpGet]
    public async Task<IActionResult> Configure()
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user);

        List<Module> modules;

        if (roles.Contains("Admin"))
        {
            modules = await _context.Modules.ToListAsync();
        }
        else
        {
            var lecturer = await _context.Lecturers
                .Include(l => l.LecturerModules)
                    .ThenInclude(lm => lm.Module)
                .FirstOrDefaultAsync(l => l.UserId == user.Id);

            modules = lecturer?.LecturerModules.Select(lm => lm.Module!).ToList() ?? new();
        }

        var vm = new AllocationConfigViewModel
        {
            Modules = modules,
            Venues = await _context.Venues.ToListAsync(),
            TotalStudents = await _context.Students.CountAsync(),
            Preview = new Dictionary<string, List<AllocationPreviewStudent>>()
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(AllocationConfigViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user);

        model.Modules = roles.Contains("Admin")
            ? await _context.Modules.ToListAsync()
            : await _context.Lecturers
                .Include(l => l.LecturerModules)
                    .ThenInclude(lm => lm.Module)
                .Where(l => l.UserId == user.Id)
                .SelectMany(l => l.LecturerModules.Select(lm => lm.Module!))
                .ToListAsync();

        model.Venues = await _context.Venues.ToListAsync();
        model.Sessions = await _context.Sessions
            .Include(s => s.Venue)
            .Where(s => s.ModuleId == model.SelectedModuleId)
            .ToListAsync();

        // Selected session display
        if (model.SelectedSessionId > 0)
        {
            var s = model.Sessions.FirstOrDefault(x => x.SessionId == model.SelectedSessionId);
            model.SelectedSessionName = s != null
                ? $"{s.WeekDay} {s.StartTime}-{s.EndTime} ({s.Venue?.Name})"
                : "N/A";
        }

        var students = await _allocService.GetStudentsForModuleAsync(model.SelectedModuleId.Value);
        model.TotalStudents = students.Count;

        var venues = await _allocService.GetSelectedVenuesAsync(model.SelectedVenueIds);
        if (!venues.Any())
        {
            ModelState.AddModelError("", "Please select at least one venue.");
            return View(model);
        }

        model.Preview = _allocService.ComputeAllocationPreview(students, venues, model.AllocationType, model.GroupSizeLimit);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(AllocationConfigViewModel model)
    {
        if (model.SelectedModuleId <= 0)
        {
            TempData["Error"] = "Please select a module before confirming.";
            return RedirectToAction("Configure");
        }

        if (!model.SelectedSessionId.HasValue || model.SelectedSessionId <= 0)
        {
            TempData["Error"] = "Please select a session before confirming.";
            return RedirectToAction("Configure");
        }

        // Fetch the selected session (must exist for the module)
        var selectedSession = await _context.Sessions
            .Include(s => s.Venue)
            .Include(s => s.Module)
            .FirstOrDefaultAsync(s => s.SessionId == model.SelectedSessionId.Value && s.ModuleId == model.SelectedModuleId);

        if (selectedSession == null)
        {
            TempData["Error"] = "Selected session not found or does not belong to the module.";
            return RedirectToAction("Configure");
        }

        // Validate selected venues exist
        var venues = await _allocService.GetSelectedVenuesAsync(model.SelectedVenueIds);
        if (!venues.Any())
        {
            TempData["Error"] = "Please select at least one venue.";
            return RedirectToAction("Configure");
        }

        // Note: We no longer need sessions per venue. The preview is venue-based for display,
        // but all students allocate to the single selected session.
        var students = await _allocService.GetStudentsForModuleAsync(model.SelectedModuleId.Value);
        var preview = _allocService.ComputeAllocationPreview(students, venues, model.AllocationType, model.GroupSizeLimit);

        // Collect all allocated students (exclude unallocated)
        var allAllocatedStudents = preview
            .Where(kvp => kvp.Key != "Unallocated (no space/group limit)")
            .SelectMany(kvp => kvp.Value)
            .ToList();

        if (!allAllocatedStudents.Any())
        {
            TempData["Error"] = "No students to allocate based on preview.";
            return RedirectToAction("Configure");
        }

        // Save all allocated students to the single selected session
        int totalSaved = await _allocService.SaveAllocationsAsync(allAllocatedStudents, selectedSession.SessionId);
        await _context.SaveChangesAsync();

        int unallocatedCount = preview.ContainsKey("Unallocated (no space/group limit)")
            ? preview["Unallocated (no space/group limit)"].Count
            : 0;

        TempData["Success"] = $"✅ {totalSaved} students allocated successfully to '{selectedSession.WeekDay} {selectedSession.StartTime}-{selectedSession.EndTime} ({selectedSession.Venue?.Name})'. " +
                              $"{(unallocatedCount > 0 ? $"{unallocatedCount} remain unallocated." : "All students allocated.")}";

        return RedirectToAction("Finalised");
    }
    // add session for unallocated students
    [HttpPost]
    public async Task<IActionResult> AddNewSession(
    AllocationConfigViewModel model,
    string WeekDay,
    string StartTime,
    string EndTime,
    int VenueId,
    string Type)
    {
        if (model.SelectedModuleId <= 0)
        {
            TempData["Error"] = "Module information missing.";
            return RedirectToAction("Configure");
        }

        if (!model.SelectedSessionId.HasValue || model.SelectedSessionId <= 0)
        {
            TempData["Error"] = "No session selected for allocated students.";
            return RedirectToAction("Configure");
        }

        var module = await _context.Modules.FindAsync(model.SelectedModuleId);
        if (module == null)
        {
            TempData["Error"] = "Module not found.";
            return RedirectToAction("Configure");
        }

        var selectedSession = await _context.Sessions
            .Include(s => s.Venue)
            .FirstOrDefaultAsync(s => s.SessionId == model.SelectedSessionId.Value);
        if (selectedSession == null)
        {
            TempData["Error"] = "Selected session not found.";
            return RedirectToAction("Configure");
        }

        // Fetch venues and students to recompute preview and identify allocated/unallocated
        var venues = await _allocService.GetSelectedVenuesAsync(model.SelectedVenueIds);
        var allStudents = await _allocService.GetStudentsForModuleAsync(module.ModuleId);
        var preview = _allocService.ComputeAllocationPreview(allStudents, venues, model.AllocationType, model.GroupSizeLimit);

        // Extract allocated and unallocated students
        List<AllocationPreviewStudent> allocatedStudents = new List<AllocationPreviewStudent>();
        List<AllocationPreviewStudent> unallocatedStudents = new List<AllocationPreviewStudent>();

        foreach (var venueEntry in preview)
        {
            if (venueEntry.Key == "Unallocated (no space/group limit)")
            {
                unallocatedStudents = venueEntry.Value;
            }
            else
            {
                allocatedStudents.AddRange(venueEntry.Value);
            }
        }

        if (!allocatedStudents.Any() && !unallocatedStudents.Any())
        {
            TempData["Error"] = "No students to assign.";
            return RedirectToAction("Configure");
        }

        // ave allocated students to the selected session
        int allocatedAssigned = 0;
        if (allocatedStudents.Any())
        {
            allocatedAssigned = await _allocService.SaveAllocationsAsync(allocatedStudents, selectedSession.SessionId);
        }

        // Create the new session
        var newSession = new Session
        {
            ModuleId = module.ModuleId,
            WeekDay = WeekDay,
            StartTime = StartTime,
            EndTime = EndTime,
            VenueId = VenueId,
            Type = Type
        };

        _context.Sessions.Add(newSession);
        await _context.SaveChangesAsync();

        // Reload with Venue to avoid null reference
        newSession = await _context.Sessions
            .Include(s => s.Venue)
            .FirstOrDefaultAsync(s => s.SessionId == newSession.SessionId);

        if (newSession?.Venue == null)
        {
            TempData["Error"] = "Venue not found for the new session.";
            return RedirectToAction("Configure");
        }

        // Save unallocated students to the new session
        int unallocatedAssigned = 0;
        if (unallocatedStudents.Any())
        {
            unallocatedAssigned = await _allocService.SaveAllocationsAsync(unallocatedStudents, newSession.SessionId);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"✅ {allocatedAssigned} allocated students assigned to the selected session ({selectedSession.WeekDay} {selectedSession.StartTime}-{selectedSession.EndTime}). " +
                              $"{unallocatedAssigned} unallocated students assigned to the new session ({WeekDay} {StartTime}-{EndTime} at {newSession.Venue.Name}).";

        return RedirectToAction("Finalised");
    }

    // AJAX Endpoints
    [HttpGet]
    public async Task<IActionResult> GetSessionsByModule(int moduleId)
    {
        var sessions = await _context.Sessions
            .Include(s => s.Venue)
            .Where(s => s.ModuleId == moduleId)
            .Select(s => new
            {
                sessionId = s.SessionId,
                displayName = $"{s.WeekDay} {s.StartTime}-{s.EndTime} ({s.Venue.Name})"
            })
            .ToListAsync();

        return Json(sessions);
    }

    [HttpGet]
    public IActionResult GetStudentCountByModule(int moduleId)
    {
        var count = _context.StudentModules.Count(sm => sm.ModuleId == moduleId);
        return Json(new { count });
    }

    [HttpGet]
    [Authorize(Roles = "Lecturer,Admin")]
    public async Task<IActionResult> Finalised()
    {
        // Identify the logged-in user
        var userEmail = User.Identity?.Name;

        // Try to get the current lecturer (if applicable)
        var lecturer = await _context.Lecturers
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.User.Email == userEmail);

        // Get all allocations initially
        var query = _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.Session)
                .ThenInclude(s => s.Venue)
            .Include(a => a.Session)
                .ThenInclude(s => s.Module)
            .AsQueryable();

        // filter lecturer modules
        if (User.IsInRole("Lecturer") && lecturer != null)
        {
            var lecturerModuleIds = await _context.LecturerModules
                .Where(lm => lm.LecturerId == lecturer.LecturerId)
                .Select(lm => lm.ModuleId)
                .ToListAsync();

            query = query.Where(a => lecturerModuleIds.Contains(a.Session.ModuleId));
        }

        // Finalise query execution
        var finalisedAllocations = await query
            .OrderBy(a => a.Session.WeekDay)
            .ThenBy(a => a.Session.StartTime)
            .ToListAsync();

        // Handle empty results
        if (!finalisedAllocations.Any())
        {
            TempData["Info"] = User.IsInRole("Lecturer")
                ? "No finalised allocations found for your modules."
                : "No finalised allocations found.";
        }

        return View(finalisedAllocations);
    }

    //  Display confirmation modal
    [HttpGet]
    public IActionResult ConfirmSendTimetables()
    {
        return PartialView("_ConfirmSendTimetablesModal");
    }

    // Execute sending of timetable emails
    [HttpPost]
    public async Task<IActionResult> SendTimetables()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var lecturer = await _context.Lecturers
            .Include(l => l.LecturerModules)
            .ThenInclude(lm => lm.Module)
            .FirstOrDefaultAsync(l => l.UserId == user.Id);

        if (lecturer == null)
        {
            TempData["Error"] = "No lecturer profile found.";
            return RedirectToAction("Index");
        }

        // Get all students assigned to modules taught by this lecturer
        var moduleIds = lecturer.LecturerModules.Select(lm => lm.ModuleId).ToList();

        var studentAllocations = await _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.Session)
                .ThenInclude(s => s.Module)
            .Include(a => a.Session)
                .ThenInclude(s => s.Venue)
            .Where(a => moduleIds.Contains(a.Session.ModuleId))
            .ToListAsync();

        // Group allocations by student
        var studentGroups = studentAllocations
            .GroupBy(a => a.Student)
            .Where(g => g.Key != null && !string.IsNullOrEmpty(g.Key.Email))
            .ToList();

        int emailsSent = 0;

        foreach (var group in studentGroups)
        {
            var student = group.Key!;
            var sessions = group.Select(g => g.Session).ToList();

            string timetableHtml = GenerateTimetableHtml(student, sessions);

            try
            {
                // Send email via SendGrid
                await _emailSender.SendEmailAsync(
                    student.Email!,
                    "Your Updated Timetable",
                    timetableHtml
                );

                // Log to EmailNotifications
                _context.EmailNotifications.Add(new EmailNotification
                {
                    Title = "Updated Timetable",
                    Message = $"A new timetable has been sent to {student.Email}",
                    RecipientEmail = student.Email,
                    StudentId = student.StudentId,
                    IsEmailSent = true
                });

                emailsSent++;
            }
            catch
            {
                _context.EmailNotifications.Add(new EmailNotification
                {
                    Title = "Timetable Delivery Failed",
                    Message = $"Failed to send timetable to {student.Email}",
                    RecipientEmail = student.Email,
                    StudentId = student.StudentId,
                    IsEmailSent = false
                });
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"{emailsSent} timetable emails sent successfully!";
        return RedirectToAction("Index");
    }

    //  Helper method to generate timetable email HTML
    private string GenerateTimetableHtml(Student student, List<Session> sessions)
    {
        var builder = new StringBuilder();
        builder.Append($"<h3>Hello {student.FirstName},</h3>");
        builder.Append("<p>Here’s your updated timetable:</p>");
        builder.Append("<table border='1' cellspacing='0' cellpadding='6' style='border-collapse:collapse;'>");
        builder.Append("<tr><th>Module</th><th>Day</th><th>Time</th><th>Venue</th></tr>");

        foreach (var s in sessions)
        {
            builder.Append($"<tr><td>{s.Module?.ModuleCode}</td><td>{s.WeekDay}</td><td>{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}</td><td>{s.Venue?.Name}</td></tr>");
        }

        builder.Append("</table>");
        builder.Append("<p>Kind regards,<br/>Scheduling Assistant System</p>");

        return builder.ToString();
    }

}
