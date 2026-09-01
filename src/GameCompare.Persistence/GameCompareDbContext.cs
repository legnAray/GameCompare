using GameCompare.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameCompare.Persistence;

public sealed class GameCompareDbContext(DbContextOptions<GameCompareDbContext> options)
    : DbContext(options)
{
    public DbSet<GameRecord> Games => Set<GameRecord>();

    public DbSet<ComparisonEvent> ComparisonEvents => Set<ComparisonEvent>();

    public DbSet<RatingState> RatingStates => Set<RatingState>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var games = modelBuilder.Entity<GameRecord>();
        games.ToTable("Games");
        games.HasKey(game => game.Id);
        games.HasIndex(game => game.PlayniteId).IsUnique();
        games.HasIndex(game => game.IsActiveCompleted);
        games.Property(game => game.Name).HasMaxLength(500);
        games.Property(game => game.NotePath).HasMaxLength(2048);
        games.Property(game => game.PlaytimeSeconds).HasConversion<long>();

        var events = modelBuilder.Entity<ComparisonEvent>();
        events.ToTable("ComparisonEvents");
        events.HasKey(item => item.Id);
        events.HasIndex(item => item.ComparedAt);
        events.HasIndex(item => new { item.LeftGameId, item.RightGameId });
        events.Property(item => item.Outcome).HasConversion<int>();
        events.HasOne<GameRecord>()
            .WithMany()
            .HasForeignKey(item => item.LeftGameId)
            .OnDelete(DeleteBehavior.Restrict);
        events.HasOne<GameRecord>()
            .WithMany()
            .HasForeignKey(item => item.RightGameId)
            .OnDelete(DeleteBehavior.Restrict);

        var ratings = modelBuilder.Entity<RatingState>();
        ratings.ToTable("RatingStates");
        ratings.HasKey(item => item.GameId);
        ratings.HasIndex(item => item.Rank);
        ratings.HasOne<GameRecord>()
            .WithOne()
            .HasForeignKey<RatingState>(item => item.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        var syncRuns = modelBuilder.Entity<SyncRun>();
        syncRuns.ToTable("SyncRuns");
        syncRuns.HasKey(item => item.Id);
        syncRuns.HasIndex(item => item.FinishedAt);
    }
}
