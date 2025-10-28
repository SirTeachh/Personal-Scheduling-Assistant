using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models
{
    public class Building
    {
        [Key]
        public int BuildingId { get; set; }

        [Required, StringLength(100)]
        public string BuildingName { get; set; } = string.Empty;


        public ICollection<Venue>? Venues { get; set; }
    }
}
