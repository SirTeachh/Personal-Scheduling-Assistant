using System.ComponentModel.DataAnnotations;

namespace PersonalSchedulingAssistant.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Role")]
        public string? Role { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Title")]
        public string? Title { get; set; }

        [Required(ErrorMessage = "Please enter a first name")]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "Please enter a last name")]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Required]
        [Display(Name = "E-mail")]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [DataType(DataType.Password)] 
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords must match")]
        [Display(Name = "Confirm Password")]
        [DataType(DataType.Password)]
        public string? ConfirmPassword { get; set; }

        

        [StringLength(100)]
        [Display(Name = "Department")]
        public string Department { get; set; } = "Computer Science";
    }
}
