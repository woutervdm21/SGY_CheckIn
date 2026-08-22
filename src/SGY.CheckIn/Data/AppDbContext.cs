using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Youth> Youths => Set<Youth>();
    public DbSet<CheckInRecord> CheckIns => Set<CheckInRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Youth>()
            .HasIndex(y => new { y.Surname, y.Name });

        modelBuilder.Entity<CheckInRecord>()
            .HasIndex(c => c.Timestamp);

        modelBuilder.Entity<CheckInRecord>()
            .HasOne(c => c.Youth)
            .WithMany(y => y.CheckIns)
            .HasForeignKey(c => c.YouthId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
