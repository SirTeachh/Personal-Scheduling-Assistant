using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Summary stats
            ViewBag.LecturerCount = await _context.Lecturers.CountAsync();
            ViewBag.DemmieCount = await _context.Demmies.CountAsync();
            ViewBag.ModuleCount = await _context.Modules.CountAsync();
            ViewBag.SessionCount = await _context.Sessions.CountAsync();

            // Chart data
            ViewBag.ModuleNames = await _context.Modules.Select(m => m.ModuleCode).ToListAsync();
            ViewBag.SessionCounts = await _context.Modules
                .Select(m => _context.Sessions.Count(s => s.ModuleId == m.ModuleId))
                .ToListAsync();

            ViewBag.DemmieNames = await _context.Demmies
                .Select(d => d.FirstName + " " + d.LastName)
                .ToListAsync();

            ViewBag.DemmieWorkloads = await _context.Demmies
                .Select(d => _context.DemmieSessions.Count(ds => ds.DemmieId == d.DemmieId))
                .ToListAsync();

            ViewBag.Conflicts = await _context.Conflicts
                .OrderByDescending(c => c.ConflictId)
                .Take(10)
                .ToListAsync();

            return View();
        }
    }
}
