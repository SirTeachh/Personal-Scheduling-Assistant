using System;
using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class Session
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }
        public string WeekDay { get; set; } = null!;
        public string StartTime { get; set; } = null!; //"HH:mm"
        public string EndTime { get; set; } = null!;
        public string? Type { get; set; } 
        public int VenueId { get; set; }
        public Venue? Venue { get; set; }
        public int? CapacityOverride { get; set; }

        public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
    }
}

