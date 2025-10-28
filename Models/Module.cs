using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; }
        public string ModuleName { get; set; }

        // Relationships
        public ICollection<bridgeStudent_Module>? StudentModules { get; set; }
        public ICollection<Session>? Sessions { get; set; }
        public ICollection<bridgeLecturer_Module>? LecturerModules { get; set; }
        public ICollection<bridgeDemmie_Module>? DemmieModules { get; set; }
    }
}
