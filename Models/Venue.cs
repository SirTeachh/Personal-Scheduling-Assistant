using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class Venue
    {
        public int VenueId { get; set; }
        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
        public int BuildingId { get; set; }

        [ForeignKey(nameof(BuildingId))]
        public Building? Building { get; set; }
    }
}
