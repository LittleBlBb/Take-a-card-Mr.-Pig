using Microsoft.EntityFrameworkCore;
using CardGameAPI.Models;

namespace CardGameAPI.Data;

public class GameContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Move> Moves { get; set; }

    public GameContext(DbContextOptions<GameContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.RefreshToken)
            .IsUnique();

        modelBuilder.Entity<Session>()
            .Property(s => s.StartTime)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<Session>()
            .Property(s => s.PlayerHand)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Session>()
            .Property(s => s.BotHand)
            .HasColumnType("jsonb");
    }
}