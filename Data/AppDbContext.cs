using Microsoft.EntityFrameworkCore;
using MultiplayerMatchmaking.Models;

namespace MultiplayerMatchmaking.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PlayerProfile> Players => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerProfile>(e =>
        {
            e.HasKey(p => p.PlayerId);
            e.Property(p => p.PlayerId).IsRequired();
            e.Property(p => p.Mmr).IsRequired();
            e.Property(p => p.MatchesPlayed).IsRequired();
        });
    }
}