using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Youth> Youths => Set<Youth>();
    public DbSet<CheckInRecord> CheckIns => Set<CheckInRecord>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every lookup filters on the group first, then sorts by name.
        modelBuilder.Entity<Youth>()
            .HasIndex(y => new { y.Group, y.Surname, y.Name });

        modelBuilder.Entity<CheckInRecord>()
            .HasIndex(c => c.Timestamp);

        modelBuilder.Entity<CheckInRecord>()
            .HasOne(c => c.Youth)
            .WithMany(y => y.CheckIns)
            .HasForeignKey(c => c.YouthId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
