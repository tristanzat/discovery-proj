using Microsoft.EntityFrameworkCore;
using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Data;

/// <summary>
/// EF Core DbContext for the DungeonCrawler API.
/// Translates between C# models and Postgres tables.
/// </summary>
public class DungeonCrawlerDbContext : DbContext
{
    public DungeonCrawlerDbContext(DbContextOptions<DungeonCrawlerDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet for Account entities.
    /// Each Account maps to a row in the 'accounts' Postgres table.
    /// </summary>
    public DbSet<Account> Accounts { get; set; }

    /// <summary>
    /// DbSet for PlayerProgress entities.
    /// Each PlayerProgress maps to a row in the 'player_progress' Postgres table.
    /// </summary>
    public DbSet<PlayerProgress> PlayerProgresses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        // Maps C# class to 'accounts' table in Postgres
        // Specifies the primary key and constraints
        modelBuilder.Entity<Account>()
            .ToTable("accounts")
            .HasKey(a => a.AccountId);

        modelBuilder.Entity<Account>()
            .Property(a => a.AccountId)
            .HasColumnName("account_id");

        modelBuilder.Entity<Account>()
            .Property(a => a.Username)
            .HasColumnName("username")
            .HasMaxLength(32)
            .IsRequired();

        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Username)
            .IsUnique();

        modelBuilder.Entity<Account>()
            .Property(a => a.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        modelBuilder.Entity<Account>()
            .Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Configure PlayerProgress entity
        // Maps C# class to 'player_progress' table in Postgres
        // Specifies the foreign key relationship and constraints
        modelBuilder.Entity<PlayerProgress>()
            .ToTable("player_progress")
            .HasKey(pp => pp.AccountId);

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.AccountId)
            .HasColumnName("account_id");

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.Level)
            .HasColumnName("level")
            .HasDefaultValue(1);

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.Xp)
            .HasColumnName("xp")
            .HasDefaultValue(0);

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.Gold)
            .HasColumnName("gold")
            .HasDefaultValue(0);

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.MaxHp)
            .HasColumnName("max_hp")
            .HasDefaultValue(100);

        modelBuilder.Entity<PlayerProgress>()
            .Property(pp => pp.LastSavedAt)
            .HasColumnName("last_saved_at")
            .HasDefaultValueSql("NOW()");

        // Configure one-to-one relationship:
        // Account (1) ---- (1) PlayerProgress
        // When an Account is deleted, its PlayerProgress is cascade deleted
        modelBuilder.Entity<Account>()
            .HasOne(a => a.PlayerProgress)
            .WithOne(pp => pp.Account)
            .HasForeignKey<PlayerProgress>(pp => pp.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
