using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class Student
    {
        public int StudentId { get; set; }
        [Required, StringLength(10)]
        public string StudentNumber { get; set; } = null!;
        [Required, StringLength(50)]
        public required string FirstName { get; set; }

        [Required, StringLength(50)]
        public required string LastName { get; set; }

        [Required, EmailAddress]
        public required string Email { get; set; }

        public string? DegreeProgram { get; set; }


        // Navigation
        public ICollection<bridgeStudent_Module>? StudentModules { get; set; }
        public ICollection<Allocation>? Allocations { get; set; }
    }
}
