using System.ComponentModel.DataAnnotations;
namespace PersonalSchedulingAssistant.Models
{
    public class EmailNotification
    {
            [Key]
            public int EmailNotificationId { get; set; }

            [Required]
            public string Title { get; set; } = string.Empty;

            [Required]
            public string Message { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; } = DateTime.Now;

            public bool IsEmailSent { get; set; } = false;

           
            public int? StudentId { get; set; }
            public Student? Student { get; set; }

            public string? RecipientEmail { get; set; }
    }
}
