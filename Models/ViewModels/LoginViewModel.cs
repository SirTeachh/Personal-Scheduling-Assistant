using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        [UIHint("email")]
        public string? Email { get; set; }

        [Required]
        [UIHint("password")]
        public string? Password { get; set; }

        public string ReturnUrl { get; set; } = "/";
        public bool RememberMe { get; set; }
    }
}
