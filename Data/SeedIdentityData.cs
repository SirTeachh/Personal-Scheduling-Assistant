using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Models;

namespace PersonalSchedulingAssistant.Data
{
    public static class SeedIdentityData
    {
        private const string AdminEmail = "admin@scheduling.com";
        private const string AdminPassword = "AlphaNumeric@1";
        private const string AdminRole = "Admin";

        private const string LecturerRole = "Lecturer";
        private const string LecturerPassword = "Lecturer@123";

        private const string DemmieRole = "Demmie";
        private const string DemmiePassword = "Demmie@123";

        public static async Task PopulateIdentityAsync(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (context.Database.GetPendingMigrations().Any())
                context.Database.Migrate();

            // --- Ensure Roles Exist ---
            string[] roles = { AdminRole, LecturerRole, DemmieRole };
            foreach (var role in roles)
            {
                if (await roleManager.FindByNameAsync(role) == null)
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // --- Admin ---
            await EnsureUserWithRoleAsync(userManager, AdminEmail, AdminPassword, "Oratile", "Molongwana", "Mr", "Computer Science", AdminRole);

            // --- Lecturers (10 total) ---
            if (!context.Lecturers.Any())
            {
                for (int i = 1; i <= 10; i++)
                {
                    var email = $"lecturer{i}@scheduling.com";
                    var title = i % 2 == 0 ? "Mr" : "Mrs";
                    var firstName = $"Lecturer{i}";
                    var lastName = "Demo";

                    var user = await EnsureUserWithRoleAsync(userManager, email, LecturerPassword, firstName, lastName, title, "Computer Science", LecturerRole);

                    if (!context.Lecturers.Any(l => l.UserId == user.Id))
                    {
                        context.Lecturers.Add(new Lecturer
                        {
                            UserId = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.SecondName
                        });
                    }
                }

                await context.SaveChangesAsync();

                // Assign ONE lecturer per module
                var modules = context.Modules.ToList();
                var lecturers = context.Lecturers.Include(l => l.User).ToList();
                var rnd = new Random();

                // Ensure we have enough lecturers
                if (!lecturers.Any())
                    throw new Exception("No lecturers available to assign modules.");

                int lecturerIndex = 0;

                foreach (var module in modules)
                {
                    //if there are more modules than lecturers
                    var lecturer = lecturers[lecturerIndex % lecturers.Count];
                    lecturerIndex++;

                    context.LecturerModules.Add(new bridgeLecturer_Module
                    {
                        LecturerId = lecturer.LecturerId,
                        ModuleId = module.ModuleId,
                        LecturerName = $"{lecturer.User?.Title} {lecturer.User?.FirstName} {lecturer.User?.SecondName}",
                        ModuleCode = module.ModuleCode,
                        ModuleTitle = module.ModuleName,
                        AssignedDate = DateTime.UtcNow
                    });
                }

                await context.SaveChangesAsync();
            }

            // ---  Demmies (10 total) ---
            if (!context.Demmies.Any())
            {
                var demmieInfos = new[]
                {
                new { First = "Karabo", Last = "Molaisa", Email = "karabo.demmie@scheduling.com", Title = "Mr" },
                new { First = "Bontle", Last = "Sakamoto", Email = "bontle.demmie@scheduling.com", Title = "Mrs" },
                new { First = "Gwen", Last = "Tennison", Email = "gwen.demmie@scheduling.com", Title = "Mrs" },
                new { First = "Sli", Last = "Torano", Email = "sli.demmie@scheduling.com", Title = "Mr" },
                new { First = "Mike", Last = "Talente", Email = "mike.demmie@scheduling.com", Title = "Mr" },
                new { First = "Precious", Last = "Whiz", Email = "precious.demmie@scheduling.com", Title = "Mrs" },
                new { First = "Lebo", Last = "Tsatsi", Email = "lebo.demmie@scheduling.com", Title = "Mrs" },
                new { First = "Boohle", Last = "Singwest", Email = "boohle.demmie@scheduling.com", Title = "Mrs" },
                new { First = "Santa", Last = "Claus", Email = "santa.demmie@scheduling.com", Title = "Mr" },
                new { First = "Thabo", Last = "Ndlovu", Email = "thabo.demmie@scheduling.com", Title = "Mr" }};

                var rnd = new Random();

                foreach (var info in demmieInfos)
                {
                    var user = await EnsureUserWithRoleAsync(userManager, info.Email, DemmiePassword,
                        info.First, info.Last, info.Title, "Computer Science", DemmieRole);

                    if (!context.Demmies.Any(d => d.UserId == user.Id))
                    {
                        var weeklyLimit = 10;
                        var worked = rnd.Next(0, weeklyLimit + 1); 

                        context.Demmies.Add(new Demmie
                        {
                            UserId = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.SecondName,
                            IsAssigned = worked > 0,
                            WeeklyHourLimit = weeklyLimit,
                            HoursWorkedThisWeek = worked,
                            AssignedDate = worked > 0 ? DateTime.UtcNow.AddDays(-rnd.Next(1, 5)) : null
                        });
                    }
                }

                await context.SaveChangesAsync();
                // Assign Modules & Sessions
                var demmies = context.Demmies.Include(d => d.User).ToList();
                var modules = context.Modules.ToList();
                var sessions = context.Sessions.Include(s => s.Module).Include(s => s.Venue).ToList();

                foreach (var demmie in demmies)
                {
                    var assignedModules = modules.OrderBy(_ => rnd.Next()).Take(2).ToList();
                    foreach (var mod in assignedModules)
                    {
                        context.DemmieModules.Add(new bridgeDemmie_Module
                        {
                            DemmieId = demmie.DemmieId,
                            ModuleId = mod.ModuleId,
                            DemmieName = $"{demmie.User?.Title} {demmie.User?.FirstName} {demmie.User?.SecondName}",
                            ModuleCode = mod.ModuleCode,
                            ModuleTitle = mod.ModuleName
                        });
                    }

                    var assignedSessions = sessions.OrderBy(_ => rnd.Next()).Take(2).ToList();
                    foreach (var session in assignedSessions)
                    {
                        context.DemmieSessions.Add(new bridgeDemmie_Session
                        {
                            DemmieId = demmie.DemmieId,
                            SessionId = session.SessionId,
                            DemmieName = $"{demmie.User?.Title} {demmie.User?.FirstName} {demmie.User?.SecondName}",
                            ModuleCode = session.Module?.ModuleCode,
                            WeekDay = session.WeekDay,
                            VenueName = session.Venue?.Name
                        });
                    }
                }

                await context.SaveChangesAsync();
            }
        }
        // Ensures a user exists and is assigned to a role.
        private static async Task<User> EnsureUserWithRoleAsync(
            UserManager<User> userManager,
            string email,
            string password,
            string firstName,
            string lastName,
            string title,
            string department,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    SecondName = lastName,
                    Title = title,
                    Department = department,
                    EmailConfirmed = true,
                    Approved = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                    throw new Exception($"Failed to create {role} user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                await userManager.AddToRoleAsync(user, role);
            }

            return user;
        }
    }
}
