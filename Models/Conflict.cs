using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class Conflict
    {
        [Key]
        public int ConflictId { get; set; }

        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int? SessionId1 { get; set; }
        public Session? Session1 { get; set; }

        public int? SessionId2 { get; set; }
        public Session? Session2 { get; set; }

        public int? StudentId { get; set; }
        public Student? Student { get; set; }

        public int? VenueId { get; set; }
        public Venue? Venue { get; set; }

        public bool IsResolved { get; set; } = false;

        public string? SuggestedResolution { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
