using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Models;

namespace PersonalSchedulingAssistant.Data
{
    public static class SeedData
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();
            // --- MODULES ---
            if (!context.Modules.Any())
            {
                var modules = new[]
                {
                new Module { ModuleCode = "CSIS1614", ModuleName = "Intro to Programming" },
                new Module { ModuleCode = "CSIL1522", ModuleName = "Computer Literacy" },
                new Module { ModuleCode = "BCIS2624", ModuleName = "Business Information Systems" },
                new Module { ModuleCode = "CSIS1553", ModuleName = "Computer Hardware" },
                new Module { ModuleCode = "STSA1624", ModuleName = "Statistics for CS" },
                new Module { ModuleCode = "EBUS1514", ModuleName = "Business Management" },
                new Module { ModuleCode = "UFSS1522", ModuleName = "First Year Seminar" },
                new Module { ModuleCode = "CSIS2624", ModuleName = "Data Structures" },
                new Module { ModuleCode = "ESBM2624", ModuleName = "Small Business Management" },
                new Module { ModuleCode = "CSIS3714", ModuleName = "Software Engineering" },
                new Module { ModuleCode = "CSIS3724", ModuleName = "Operating Systems" },
                new Module { ModuleCode = "CSIS3734", ModuleName = "Artificial Intelligence" }
            };
                context.Modules.AddRange(modules);
                context.SaveChanges();
            }

            // --- BUILDINGS ---
            if (!context.Buildings.Any())
            {
                context.Buildings.AddRange(
                    new Building { BuildingName = "WWG" },
                    new Building { BuildingName = "Modlec" },
                    new Building { BuildingName = "Genmin Lectorium" },
                    new Building { BuildingName = "Stabilis" }
                );
                context.SaveChanges();
            }

            // --- VENUES ---
            if (!context.Venues.Any())
            {
                var wwg = context.Buildings.First(b => b.BuildingName == "WWG");
                var modlec = context.Buildings.First(b => b.BuildingName == "Modlec");
                var genmin = context.Buildings.First(b => b.BuildingName == "Genmin Lectorium");
                var stabilis = context.Buildings.First(b => b.BuildingName == "Stabilis");

                var venues = new[]
                {
                new Venue { Name = "Lab 222", Capacity = 15, BuildingId = wwg.BuildingId },
                new Venue { Name = "Lab 223", Capacity = 15, BuildingId = wwg.BuildingId },
                new Venue { Name = "Lab A", Capacity = 30, BuildingId = modlec.BuildingId },
                new Venue { Name = "Genmin D", Capacity = 20, BuildingId = genmin.BuildingId },
                new Venue { Name = "Stabilis 1", Capacity = 30, BuildingId = stabilis.BuildingId },
                new Venue { Name = "Stabilis 3", Capacity = 30, BuildingId = stabilis.BuildingId }
            };
                context.Venues.AddRange(venues);
                context.SaveChanges();
            }

            // --- SESSIONS ---
            if (!context.Sessions.Any())
            {
                var sessionTypes = new[] { "Lecture", "Tutorial", "Practical" };
                var rnd = new Random();

                var sessions = new List<Session>
            {
                new Session { ModuleId = 1, WeekDay = "Monday", StartTime = "14:00", EndTime = "16:00", VenueId = 1, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 2, WeekDay = "Tuesday", StartTime = "10:00", EndTime = "12:00", VenueId = 2, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 3, WeekDay = "Wednesday", StartTime = "08:00", EndTime = "10:00", VenueId = 3, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 4, WeekDay = "Thursday", StartTime = "12:00", EndTime = "14:00", VenueId = 4, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 5, WeekDay = "Friday", StartTime = "08:00", EndTime = "10:00", VenueId = 2, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 6, WeekDay = "Monday", StartTime = "10:00", EndTime = "12:00", VenueId = 3, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 7, WeekDay = "Monday", StartTime = "14:00", EndTime = "16:00", VenueId = 2, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 8, WeekDay = "Monday", StartTime = "14:00", EndTime = "16:00", VenueId = 4, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 9, WeekDay = "Wednesday", StartTime = "08:00", EndTime = "10:00", VenueId = 1, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 10, WeekDay = "Wednesday", StartTime = "08:00", EndTime = "10:00", VenueId = 5, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 11, WeekDay = "Wednesday", StartTime = "08:00", EndTime = "10:00", VenueId = 6, Type = sessionTypes[rnd.Next(sessionTypes.Length)] },
                new Session { ModuleId = 12, WeekDay = "Friday", StartTime = "14:00", EndTime = "16:00", VenueId = 1, Type = sessionTypes[rnd.Next(sessionTypes.Length)] }
            };
                context.Sessions.AddRange(sessions);
                context.SaveChanges();
            }

            // --- STUDENTS ---
            if (!context.Students.Any())
            {
                var rnd = new Random();
                var degreePrograms = new[] { "Computer Science and Informatics", "Business Information Systems", "Data Science" };

                var students = Enumerable.Range(1, 100).Select(i => new Student
                {
                    StudentNumber = $"2025{i:000}",
                    FirstName = $"Student{i}",
                    LastName = "Demo",
                    Email = $"student{i}@ufs.ac.za",
                    DegreeProgram = degreePrograms[rnd.Next(degreePrograms.Length)]
                }).ToList();

                context.Students.AddRange(students);
                context.SaveChanges();
            }

            // --- ENROLLMENTS ---
            if (!context.StudentModules.Any())
            {
                var rnd = new Random();
                var students = context.Students.ToList();
                var modules = context.Modules.ToList();

                foreach (var student in students)
                {
                    var selected = modules.OrderBy(x => rnd.Next()).Take(rnd.Next(3, 6)).ToList();
                    foreach (var mod in selected)
                    {
                        context.StudentModules.Add(new bridgeStudent_Module
                        {
                            StudentId = student.StudentId,
                            ModuleId = mod.ModuleId,
                            StudentName = $"{student.FirstName} {student.LastName}",
                            ModuleCode = mod.ModuleCode,
                            ModuleName = mod.ModuleName
                        });
                    }
                }
                context.SaveChanges();
            }

            // --- ALLOCATIONS ---
            if (!context.Allocations.Any())
            {
                var rnd = new Random();
                var students = context.Students.Take(10).ToList(); // create allocations for 10 students
                var sessions = context.Sessions.Include(s => s.Module).Include(s => s.Venue).ToList();

                var allocations = new List<Allocation>();

                foreach (var student in students)
                {
                    // Pick 3 random sessions
                    var chosenSessions = sessions.OrderBy(x => rnd.Next()).Take(3).ToList();

                    foreach (var session in chosenSessions)
                    {
                        //  Check if already exists
                        bool alreadyExists = allocations.Any(a => a.StudentId == student.StudentId && a.SessionId == session.SessionId);

                        if (!alreadyExists)
                        {
                            allocations.Add(new Allocation
                            {
                                StudentId = student.StudentId,
                                StudentName = $"{student.FirstName} {student.LastName}",
                                SessionId = session.SessionId,
                                ModuleCode = session.Module?.ModuleCode,
                                SessionDay = session.WeekDay,
                                VenueName = session.Venue?.Name,
                                AssignedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                // --- Add intentional conflicts
                // Student 1 attends two overlapping Monday sessions
                var conflictSessions = context.Sessions
                    .Where(s => s.WeekDay == "Monday")
                    .Take(2)
                    .ToList();

                if (conflictSessions.Count >= 2)
                {
                    var student1 = students.First();
                    foreach (var s in conflictSessions)
                    {
                        if (!allocations.Any(a => a.StudentId == student1.StudentId && a.SessionId == s.SessionId))
                        {
                            allocations.Add(new Allocation
                            {
                                StudentId = student1.StudentId,
                                StudentName = $"{student1.FirstName} {student1.LastName}",
                                SessionId = s.SessionId,
                                ModuleCode = s.Module?.ModuleCode,
                                SessionDay = s.WeekDay,
                                VenueName = s.Venue?.Name,
                                AssignedAt = DateTime.UtcNow.AddMinutes(5)
                            });
                        }
                    }
                }

                // Student 2 has two Wednesday sessions overlapping
                var wedSessions = context.Sessions
                    .Where(s => s.WeekDay == "Wednesday")
                    .Take(2)
                    .ToList();

                if (wedSessions.Count >= 2)
                {
                    var student2 = students.Skip(1).First();
                    foreach (var s in wedSessions)
                    {
                        if (!allocations.Any(a => a.StudentId == student2.StudentId && a.SessionId == s.SessionId))
                        {
                            allocations.Add(new Allocation
                            {
                                StudentId = student2.StudentId,
                                StudentName = $"{student2.FirstName} {student2.LastName}",
                                SessionId = s.SessionId,
                                ModuleCode = s.Module?.ModuleCode,
                                SessionDay = s.WeekDay,
                                VenueName = s.Venue?.Name,
                                AssignedAt = DateTime.UtcNow.AddMinutes(10)
                            });
                        }
                    }
                }

                context.Allocations.AddRange(allocations);
                context.SaveChanges();
            }

        }
    }
}
