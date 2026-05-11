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

    // TODO: DbSet property for Accounts
    
    // TODO: DbSet property for PlayerProgress

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO: Configure Account entity (table name, key, constraints)
        // Example: modelBuilder.Entity<Account>().ToTable("accounts");

        // TODO: Configure PlayerProgress entity (table name, key, foreign key, constraints)
        
        // TODO: Configure one-to-one relationship between Account and PlayerProgress
    }
}
