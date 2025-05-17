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
        // Уникальность username
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Автоматическое время для сессии
        modelBuilder.Entity<Session>()
            .Property(s => s.StartTime)
            .HasDefaultValueSql("NOW()");

        // необходимо добавить поля в Session для хранения рук и очков от Prolog (FR1.7, FR2.7)
    }
}