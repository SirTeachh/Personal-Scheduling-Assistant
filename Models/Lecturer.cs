using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalSchedulingAssistant.Models
{
    public class Lecturer
    {
        [Key]
        public int LecturerId { get; set; }

   
        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required, StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string LastName { get; set; } = string.Empty;
        // A lecturer can teach many modules
        public ICollection<bridgeLecturer_Module>? LecturerModules { get; set; }
    }
}