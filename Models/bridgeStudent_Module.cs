using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
    
{
    public class bridgeStudent_Module
    {
        [Key]
        public int StudentModuleId { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        // Readability
        public string? StudentName { get; set; }
        public string? ModuleCode { get; set; }
        public string? ModuleName { get; set; }
        public DateTime EnrollmentDate { get; set; } = DateTime.Now;
    }
}
