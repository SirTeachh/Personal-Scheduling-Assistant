using Microsoft.AspNetCore.Identity;

namespace PersonalSchedulingAssistant.Models.ViewModels
{
    public class UserViewModel
    {
        public IEnumerable<User> allUsers { get; set; }
        public IEnumerable<IdentityRole> Roles { get; set; } 
    }
}
