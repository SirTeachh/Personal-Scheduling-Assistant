using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class DemmieAvailability
    {
        [Key]
        public int AvailabilityId { get; set; }

        [ForeignKey("Demmie")]
        public int DemmieId { get; set; }
        public Demmie? Demmie { get; set; }

        [Required]
        public string WeekDay { get; set; } = string.Empty;

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
