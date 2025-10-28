using Microsoft.EntityFrameworkCore;
using PersonalSchedulingAssistant.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace PersonalSchedulingAssistant.Data
{
    public class AppDbContext : IdentityDbContext<User>
    {

        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        { }
        public DbSet<Student> Students { get; set; } = default!;
        public DbSet<Module> Modules { get; set; } = default!;
        public DbSet<Session> Sessions { get; set; } = default!;
        public DbSet<Building> Buildings { get; set; } = default!;
        public DbSet<Venue> Venues { get; set; } = default!;
        public DbSet<Allocation> Allocations { get; set; } = default!;
        public DbSet<Lecturer> Lecturers { get; set; } = default!;
        public DbSet<Demmie> Demmies { get; set; } = default!;
        public DbSet<DemmieAvailability> DemmieAvailabilities { get; set; } = default!;
        public DbSet<Notification> Notifications { get; set; } = default!;
        public DbSet<EmailNotification> EmailNotifications { get; set; } = default!;
        public DbSet<Conflict> Conflicts { get; set; } = default!;
        public DbSet<bridgeDemmie_Module> DemmieModules { get; set; } = default!;
        public DbSet<bridgeDemmie_Session> DemmieSessions { get; set; } = default!;
        public DbSet<bridgeLecturer_Module> LecturerModules { get; set; } = default!;
        public DbSet<bridgeStudent_Module> StudentModules { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Conflict>()
        .HasOne(c => c.Session1)
        .WithMany()
        .HasForeignKey(c => c.SessionId1)
        .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Conflict>()
                .HasOne(c => c.Session2)
                .WithMany()
                .HasForeignKey(c => c.SessionId2)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Conflict>()
                .HasOne(c => c.Venue)
                .WithMany()
                .HasForeignKey(c => c.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}