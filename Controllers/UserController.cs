using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;
using PersonalSchedulingAssistant.Models.ViewModels;

namespace PersonalSchedulingAssistant.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private UserManager<User> userManager;
        private RoleManager<IdentityRole> roleManager;
        private readonly AppDbContext _context;
        public UserController(UserManager<User> userMngr,RoleManager<IdentityRole> roleMngr, AppDbContext context)
        {
            userManager = userMngr;
            roleManager = roleMngr;
            _context = context;
        }
        public async Task<IActionResult> Index(string roleFilter, string searchString, string sortOrder)
        {
            // Base user query
            var users = userManager.Users.ToList();

            // Create a working list of view model users
            List<User> userList = new List<User>();
            foreach (var user in users)
            {
                user.RoleNames = await userManager.GetRolesAsync(user);
                userList.Add(user);
            }

            // Filter by role (e.g., "Admin", "Lecturer", "Student")
            if (!string.IsNullOrEmpty(roleFilter))
            {
                userList = userList
                    .Where(u => u.RoleNames.Contains(roleFilter, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            // Search by name or email
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                userList = userList.Where(u =>
                    (!string.IsNullOrEmpty(u.FirstName) && u.FirstName.ToLower().Contains(searchString)) ||
                    (!string.IsNullOrEmpty(u.SecondName) && u.SecondName.ToLower().Contains(searchString)) ||
                    (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(searchString))
                ).ToList();
            }

            // SORTING

            ViewBag.CurrentSort = sortOrder;
            ViewBag.FirstNameSortParm = String.IsNullOrEmpty(sortOrder) ? "fname_desc" : "";
            ViewBag.SecondNameSortParm = sortOrder == "sname" ? "sname_desc" : "sname";
            ViewBag.RoleSortParm = sortOrder == "role" ? "role_desc" : "role";

            userList = sortOrder switch
            {
                "fname_desc" => userList.OrderByDescending(u => u.FirstName).ToList(),
                "sname" => userList.OrderBy(u => u.SecondName).ToList(),
                "sname_desc" => userList.OrderByDescending(u => u.SecondName).ToList(),
                "role" => userList.OrderBy(u => u.RoleNames.FirstOrDefault()).ToList(),
                "role_desc" => userList.OrderByDescending(u => u.RoleNames.FirstOrDefault()).ToList(),
                _ => userList.OrderBy(u => u.FirstName).ToList(),
            };

            // Package into view model
            var model = new UserViewModel
            {
                allUsers = userList,
                Roles = roleManager.Roles
            };

            ViewBag.RoleFilter = roleFilter;
            ViewBag.SearchString = searchString;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleApproval(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.Approved = !user.Approved; // flip status
            await userManager.UpdateAsync(user);

            TempData["message"] = $"User {user.Email} approval status set to {user.Approved}.";
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.RoleNames = await userManager.GetRolesAsync(user);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            User user = await userManager.FindByIdAsync(id);
            if (user != null)
            {
                IdentityResult result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                { 
                    string errorMessage = "";
                    foreach (IdentityError error in result.Errors)
                    {
                        errorMessage += error.Description + " | ";
                    }
                    TempData["message"] = errorMessage;
                }
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(string? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var userRoles = await userManager.GetRolesAsync(user);
            ViewBag.AllRoles = roleManager.Roles.Select(r => new SelectListItem
            {
                Value = r.Name,
                Text = r.Name,
                Selected = userRoles.Contains(r.Name)
            }).ToList();

            return View(user);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,FirstName,Email,SecondName")] User user, string selectedRole)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.Users.FindAsync(id);
                    if (existingUser == null)
                    {
                        return NotFound();
                    }

                    // Update only the fields you want to allow
                    existingUser.FirstName = user.FirstName;
                    existingUser.SecondName = user.SecondName;
                    existingUser.Email = user.Email;
                    existingUser.UserName = user.Email;

                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();

                    // Update roles
                    var currentRoles = await userManager.GetRolesAsync(existingUser);

                    if (currentRoles.Any())
                    {
                        await userManager.RemoveFromRolesAsync(existingUser, currentRoles);
                    }

                    if (!string.IsNullOrEmpty(selectedRole))
                    {
                        await userManager.AddToRoleAsync(existingUser, selectedRole);
                    }
                    
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(e => e.Id == user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(user);
        }
    }
}
