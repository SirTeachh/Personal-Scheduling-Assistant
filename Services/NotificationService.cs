using Microsoft.AspNetCore.Identity.UI.Services;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;

public class NotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task SendAsync(string userId, string title, string message, string category = "General", int? relatedId = null, string? relatedType = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Category = category,
            RelatedId = relatedId,
            RelatedType = relatedType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}
