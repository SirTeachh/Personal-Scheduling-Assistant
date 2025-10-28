using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalSchedulingAssistant.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalSchedulingAssistant.Services
{
    public class WeeklyHourResetService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // checks hourly

        public WeeklyHourResetService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ResetHoursIfMondayAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WeeklyHourResetService] Error: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ResetHoursIfMondayAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var today = DateTime.UtcNow.DayOfWeek;

            // Only reset on Mondays between 00:00–01:00 UTC
            if (today == DayOfWeek.Monday)
            {
                var demmies = await context.Demmies.ToListAsync();

                foreach (var demmie in demmies)
                {
                    if (demmie.HoursWorkedThisWeek > 0)
                        demmie.HoursWorkedThisWeek = 0;
                }

                await context.SaveChangesAsync();
                Console.WriteLine($"✅ [WeeklyHourResetService] Reset all Demmie weekly hours on {DateTime.UtcNow}.");
            }
        }
    }
}
