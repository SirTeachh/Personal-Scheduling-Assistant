using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Lecturer,Admin")]
    public class SessionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public SessionsController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? moduleId)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            IQueryable<Session> query = _context.Sessions
                .Include(s => s.Module)
                .Include(s => s.Venue);

            // --- Admin sees all sessions ---
            if (roles.Contains("Admin"))
            {
                var sessions = await query.ToListAsync();
                ViewBag.LecturerName = "Administrator";
                return View(sessions);
            }

            // --- Lecturer sees only sessions for modules they teach ---
            if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null)
                {
                    ViewBag.Message = "No lecturer profile found for your account.";
                    return View("NoLecturer");
                }

                // Get the module IDs this lecturer teaches
                var lecturerModuleIds = lecturer.LecturerModules.Select(lm => lm.ModuleId).ToList();

                // filter if user selected a specific module
                if (moduleId.HasValue)
                    query = query.Where(s => s.ModuleId == moduleId.Value);
                else
                    query = query.Where(s => lecturerModuleIds.Contains(s.ModuleId));

                var sessions = await query.ToListAsync();

                ViewBag.LecturerName = $"{user.FirstName} {user.SecondName}";
                ViewBag.Modules = await _context.Modules
                    .Where(m => lecturerModuleIds.Contains(m.ModuleId))
                    .ToListAsync();
                ViewBag.SelectedModuleId = moduleId;

                return View(sessions);
            }

            
            return RedirectToAction("AccessDenied", "Account");
        }

        // create session
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            List<Module> modules = new List<Module>();

            if (roles.Contains("Admin"))
            {
                modules = await _context.Modules.ToListAsync();
            }
            else if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                        .ThenInclude(lm => lm.Module)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer != null)
                {
                    modules = lecturer.LecturerModules
                        .Select(lm => lm.Module!)
                        .ToList();
                }
            }

            ViewBag.Modules = new SelectList(modules, "ModuleId", "ModuleName");
            ViewBag.Venues = new SelectList(await _context.Venues.ToListAsync(), "VenueId", "Name");
            ViewBag.Buildings = _context.Buildings.Include(b => b.Venues).ToList();
            return View(new Session());
        }

        // Create Module
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModule(Module module)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).First());
                return Json(new { success = false, errors });
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null)
                {
                    return Json(new { success = false, errors = new { general = "No lecturer profile found." } });
                }

                // Check if module code already exists for this lecturer or globally
                if (_context.Modules.Any(m => m.ModuleCode == module.ModuleCode))
                {
                    return Json(new { success = false, errors = new { ModuleCode = "Module code already exists." } });
                }

                // Create module
                _context.Modules.Add(module);
                await _context.SaveChangesAsync();

                // assign to lecturer
                var lecturerModule = new bridgeLecturer_Module
                {
                    LecturerId = lecturer.LecturerId,
                    ModuleId = module.ModuleId,
                    LecturerName = $"{lecturer.FirstName} {lecturer.LastName}",
                    ModuleCode = module.ModuleCode,
                    ModuleTitle = module.ModuleName,
                    AssignedDate = DateTime.UtcNow
                };
                _context.LecturerModules.Add(lecturerModule);
                await _context.SaveChangesAsync();
            }
            else if (roles.Contains("Admin"))
            {
                
                if (_context.Modules.Any(m => m.ModuleCode == module.ModuleCode))
                {
                    return Json(new { success = false, errors = new { ModuleCode = "Module code already exists." } });
                }

                _context.Modules.Add(module);
                await _context.SaveChangesAsync();
            }
            else
            {
                return Json(new { success = false, errors = new { general = "Unauthorized to create modules." } });
            }

            return Json(new { success = true, moduleId = module.ModuleId, moduleCode = module.ModuleCode, moduleName = module.ModuleName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Session session)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains("Lecturer"))
                {
                    var lecturer = await _context.Lecturers
                        .Include(l => l.LecturerModules)
                        .FirstOrDefaultAsync(l => l.UserId == user.Id);

                    if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                    {
                        ModelState.AddModelError("", "You can only create sessions for modules you teach.");
                        await LoadViewBags();
                        return View(session);
                    }
                }

                _context.Add(session);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Session created successfully.";
                return RedirectToAction(nameof(Index));
            }

            await LoadViewBags();
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBuilding(Building building)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, errors = new { general = "Invalid input." } });

            if (await _context.Buildings.AnyAsync(b => b.BuildingName == building.BuildingName))
                return Json(new { success = false, errors = new { Name = "Building name already exists." } });

            _context.Buildings.Add(building);
            await _context.SaveChangesAsync();

            return Json(new { success = true, buildingId = building.BuildingId, buildingName = building.BuildingName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVenue(Venue venue)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, errors = new { general = "Invalid venue details." } });

            if (await _context.Venues.AnyAsync(v => v.Name == venue.Name && v.BuildingId == venue.BuildingId))
                return Json(new { success = false, errors = new { Name = "A venue with this name already exists in the selected building." } });

            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var buildingName = await _context.Buildings
                .Where(b => b.BuildingId == venue.BuildingId)
                .Select(b => b.BuildingName)
                .FirstOrDefaultAsync();

            return Json(new { success = true, venueId = venue.VenueId, venueName = venue.Name, buildingName });
        }

 
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.Module)
                .Include(s => s.Venue)
                .FirstOrDefaultAsync(m => m.SessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                {
                    return Forbid();
                }
            }

            return View(session);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                {
                    return Forbid();
                }
            }

            await LoadViewBags(session.ModuleId);
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Session session)
        {
            if (id != session.SessionId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    var roles = await _userManager.GetRolesAsync(user);

                    if (roles.Contains("Lecturer"))
                    {
                        var lecturer = await _context.Lecturers
                            .Include(l => l.LecturerModules)
                            .FirstOrDefaultAsync(l => l.UserId == user.Id);

                        if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                        {
                            ModelState.AddModelError("", "You can only edit sessions for modules you teach.");
                            await LoadViewBags();
                            return View(session);
                        }
                    }

                    _context.Update(session);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Session updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionExists(session.SessionId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadViewBags(session.ModuleId);
            return View(session);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.Module)
                .Include(s => s.Venue)
                .FirstOrDefaultAsync(m => m.SessionId == id);

            if (session == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                {
                    return Forbid();
                }
            }

            return View(session);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Sessions == null)
            {
                return Problem("Entity set 'AppDbContext.Sessions'  is null.");
            }

            var session = await _context.Sessions.FindAsync(id);
            if (session != null)
            {
                var user = await _userManager.GetUserAsync(User);
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains("Lecturer"))
                {
                    var lecturer = await _context.Lecturers
                        .Include(l => l.LecturerModules)
                        .FirstOrDefaultAsync(l => l.UserId == user.Id);

                    if (lecturer == null || !lecturer.LecturerModules.Any(lm => lm.ModuleId == session.ModuleId))
                    {
                        return Forbid();
                    }
                }

                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Session deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool SessionExists(int id)
        {
            return (_context.Sessions?.Any(e => e.SessionId == id)).GetValueOrDefault();
        }

        private async Task LoadViewBags(int? selectedModuleId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            List<Module> modules = new List<Module>();

            if (roles.Contains("Admin"))
            {
                modules = await _context.Modules.ToListAsync();
            }
            else if (roles.Contains("Lecturer"))
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerModules)
                        .ThenInclude(lm => lm.Module)
                    .FirstOrDefaultAsync(l => l.UserId == user.Id);

                if (lecturer != null)
                {
                    modules = lecturer.LecturerModules
                        .Select(lm => lm.Module!)
                        .ToList();
                }
            }

            ViewBag.Modules = new SelectList(modules, "ModuleId", "ModuleName", selectedModuleId);
            ViewBag.Venues = new SelectList(await _context.Venues.ToListAsync(), "VenueId", "Name");
        }
    }
}
