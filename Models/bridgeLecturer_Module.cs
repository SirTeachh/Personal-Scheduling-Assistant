using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class bridgeLecturer_Module
    {
        [Key]
        public int LecturerModuleId { get; set; }

        // Foreign keys
        public int LecturerId { get; set; }
        public Lecturer? Lecturer { get; set; }

        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        // For readability
        public string? LecturerName { get; set; }
        public string? ModuleCode { get; set; }
        public string? ModuleTitle { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }
}
