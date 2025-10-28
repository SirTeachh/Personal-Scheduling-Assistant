using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class bridgeDemmie_Module
    {
        [Key]
        public int Id { get; set; }

        public int DemmieId { get; set; }
        [ForeignKey("DemmieId")]
        public Demmie? Demmie { get; set; }

        public int ModuleId { get; set; }
        [ForeignKey("ModuleId")]
        public Module? Module { get; set; }

        // Readability
        public string? DemmieName { get; set; }
        public string? ModuleCode { get; set; } 
        public string? ModuleTitle { get; set; }
    }

    public class bridgeDemmie_Session
    {
        [Key]
        public int Id { get; set; }

        public int DemmieId { get; set; }
        [ForeignKey("DemmieId")]
        public Demmie? Demmie { get; set; }

        public int SessionId { get; set; }
        [ForeignKey("SessionId")]
        public Session? Session { get; set; }

        // Readability
        public string? DemmieName { get; set; }
        public string? ModuleCode { get; set; }
        public string? WeekDay { get; set; }
        public string? VenueName { get; set; }
    }
}
