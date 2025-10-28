
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
namespace PersonalSchedulingAssistant.Models
{
    public class User : IdentityUser
    {
        [StringLength(20)]
        public string? Title { get; set; }

        [Required, StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string SecondName { get; set; } = string.Empty;
        public IList<string> RoleNames { get; set; } = new List<string>();

        [Required, StringLength(100)]
        public string Department { get; set; } = "Computer Science";

        public bool Approved { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}