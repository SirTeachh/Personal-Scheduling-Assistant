using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class Demmie
    {
        [Key]
        public int DemmieId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required, StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        // Availability and assignment info
        public bool IsAssigned { get; set; } = false;
        public DateTime? AssignedDate { get; set; }

        public int WeeklyHourLimit { get; set; } = 10;
        public int HoursWorkedThisWeek { get; set; } = 0;

        // Demmie can assist multiple modules or sessions
        public ICollection<bridgeDemmie_Module>? DemmieModules { get; set; }
        public ICollection<bridgeDemmie_Session>? DemmieSessions { get; set; }

        public ICollection<DemmieAvailability>? Availabilities { get; set; }
    }
}
