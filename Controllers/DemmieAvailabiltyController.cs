using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

[Authorize(Roles = "Demmie, Admin")]
public class DemmieAvailabilityController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public DemmieAvailabilityController(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var demmie = await _context.Demmies.FirstOrDefaultAsync(d => d.UserId == user.Id);

        if (demmie == null)
        {
            TempData["Error"] = "No Demmie profile found.";
            return RedirectToAction("Index", "Home");
        }

        var availabilities = await _context.DemmieAvailabilities
            .Where(a => a.DemmieId == demmie.DemmieId)
            .OrderBy(a => a.WeekDay)
            .ToListAsync();

        return View(availabilities);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        var demmie = await _context.Demmies.FirstOrDefaultAsync(d => d.UserId == user.Id);

        if (demmie == null)
            return NotFound();

        var existing = await _context.DemmieAvailabilities
            .Where(a => a.DemmieId == demmie.DemmieId)
            .ToListAsync();

        // Prepopulate weekdays if none exist yet
        if (!existing.Any())
        {
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
            existing = days.Select(d => new DemmieAvailability
            {
                DemmieId = demmie.DemmieId,
                WeekDay = d,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                IsAvailable = false
            }).ToList();
            _context.DemmieAvailabilities.AddRange(existing);
            await _context.SaveChangesAsync();
        }

        return View(existing);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(List<DemmieAvailability> availabilities)
    {
        if (!ModelState.IsValid)
            return View(availabilities);

        foreach (var availability in availabilities)
        {
            var existing = await _context.DemmieAvailabilities
                .FirstOrDefaultAsync(a => a.AvailabilityId == availability.AvailabilityId);

            if (existing != null)
            {
                existing.StartTime = availability.StartTime;
                existing.EndTime = availability.EndTime;
                existing.IsAvailable = availability.IsAvailable;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Availability updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
