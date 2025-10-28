using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class Allocation
    {
        [Key]
        public int AllocationId { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int SessionId { get; set; }
        public Session? Session { get; set; }

        // for easier display
        public string? StudentName { get; set; }   
        public string? ModuleCode { get; set; }
        public string? SessionDay { get; set; }
        public string? VenueName { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    }
}
