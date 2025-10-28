using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;
using PersonalSchedulingAssistant.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalSchedulingAssistant.Services
{
    public class AllocationService
    {
        private readonly AppDbContext _context;

        public AllocationService(AppDbContext context)
        {
            _context = context;
        }

        // Loaders
        public async Task<List<Venue>> GetSelectedVenuesAsync(List<int>? venueIds)
        {
            venueIds ??= new List<int>();
            return await _context.Venues
                .Where(v => venueIds.Contains(v.VenueId))
                .ToListAsync();
        }

        public async Task<List<Student>> GetStudentsForModuleAsync(int moduleId)
        {
            return await _context.StudentModules
                .Where(sm => sm.ModuleId == moduleId)
                .Select(sm => sm.Student!)
                .ToListAsync();
        }

        // Allocation Computation
        public Dictionary<string, List<AllocationPreviewStudent>> ComputeAllocationPreview(
            List<Student> students,
            List<Venue> selectedVenues,
            string allocationType,
            int groupSizeLimit)
        {
            var allocation = new Dictionary<string, List<AllocationPreviewStudent>>();
            foreach (var v in selectedVenues)
                allocation[v.Name] = new List<AllocationPreviewStudent>();

            if (students.Count == 0 || selectedVenues.Count == 0)
                return allocation;

            int totalCapacity = selectedVenues.Sum(v => v.Capacity);
            int studentIndex = 0;

            int GetVenueLimit(Venue v) =>
                groupSizeLimit > 0
                    ? Math.Min(groupSizeLimit, v.Capacity)
                    : v.Capacity;

            switch (allocationType)
            {
                case "First Come, First Serve":
                    foreach (var venue in selectedVenues)
                    {
                        int limit = GetVenueLimit(venue);
                        while (allocation[venue.Name].Count < limit && studentIndex < students.Count)
                        {
                            var s = students[studentIndex++];
                            allocation[venue.Name].Add(CreatePreviewStudent(s, venue.Name));
                        }
                        if (studentIndex >= students.Count) break;
                    }
                    break;

                case "Balanced":
                    int perVenue = (int)Math.Ceiling((double)students.Count / selectedVenues.Count);
                    foreach (var venue in selectedVenues)
                    {
                        int limit = Math.Min(GetVenueLimit(venue), perVenue);
                        for (int i = 0; i < limit && studentIndex < students.Count; i++)
                        {
                            var s = students[studentIndex++];
                            allocation[venue.Name].Add(CreatePreviewStudent(s, venue.Name));
                        }
                    }
                    break;

                case "Round Robin":
                    int venueIndex = 0;
                    while (studentIndex < students.Count)
                    {
                        var venue = selectedVenues[venueIndex];
                        int limit = GetVenueLimit(venue);

                        if (allocation[venue.Name].Count < limit)
                        {
                            var s = students[studentIndex++];
                            allocation[venue.Name].Add(CreatePreviewStudent(s, venue.Name));
                        }

                        venueIndex = (venueIndex + 1) % selectedVenues.Count;
                        if (selectedVenues.All(v => allocation[v.Name].Count >= GetVenueLimit(v)))
                            break;
                    }
                    break;

                case "Venue Capacity":
                    foreach (var venue in selectedVenues)
                    {
                        int proportionalShare = (int)Math.Round((double)venue.Capacity / totalCapacity * students.Count);
                        int limit = Math.Min(GetVenueLimit(venue), proportionalShare);

                        for (int i = 0; i < limit && studentIndex < students.Count; i++)
                        {
                            var s = students[studentIndex++];
                            allocation[venue.Name].Add(CreatePreviewStudent(s, venue.Name));
                        }
                    }
                    break;

                case "Random":
                    var random = new Random();
                    var shuffledStudents = students.OrderBy(s => random.Next()).ToList();
                    studentIndex = 0;
                    while (studentIndex < shuffledStudents.Count)
                    {
                        var availableVenues = selectedVenues
                            .Where(v => allocation[v.Name].Count < GetVenueLimit(v))
                            .ToList();
                        if (!availableVenues.Any()) break;

                        var venue = availableVenues[random.Next(availableVenues.Count)];
                        var s = shuffledStudents[studentIndex++];
                        allocation[venue.Name].Add(CreatePreviewStudent(s, venue.Name));
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown allocation type: " + allocationType);
            }

            // Handle unallocated
            var unallocated = students.Skip(studentIndex).ToList();
            if (unallocated.Any())
            {
                allocation["Unallocated (no space/group limit)"] = unallocated
                    .Select(s => CreatePreviewStudent(s, "Unallocated"))
                    .ToList();
            }

            return allocation.Where(kvp => kvp.Value.Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static AllocationPreviewStudent CreatePreviewStudent(Student s, string venueName)
        {
            return new AllocationPreviewStudent
            {
                StudentId = s.StudentId,
                FullName = $"{s.FirstName} {s.LastName}",
                VenueName = venueName
            };
        }

        // Saving Allocations
        public async Task<int> SaveAllocationsAsync(IEnumerable<AllocationPreviewStudent> students, int sessionId)
        {
            int count = 0;

            foreach (var student in students)
            {
                bool exists = await _context.Allocations
                    .AnyAsync(a => a.StudentId == student.StudentId && a.SessionId == sessionId);

                if (!exists)
                {
                    _context.Allocations.Add(new Allocation
                    {
                        StudentId = student.StudentId,
                        SessionId = sessionId,
                        AssignedAt = DateTime.UtcNow
                    });
                    count++;
                }
            }

            return count;
        }
    }
}
