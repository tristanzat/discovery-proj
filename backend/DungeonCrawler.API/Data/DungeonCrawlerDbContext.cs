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

    /// <summary>
    /// DbSet for QuestDefinition entities.
    /// Contains static quest templates shared by all players.
    /// </summary>
    public DbSet<QuestDefinition> QuestDefinitions { get; set; }

    /// <summary>
    /// DbSet for PlayerQuest entities.
    /// Tracks acceptance/progress/completion for each account and quest.
    /// </summary>
    public DbSet<PlayerQuest> PlayerQuests { get; set; }

    /// <summary>
    /// DbSet for InventoryItem entities.
    /// Tracks stackable loot earned by each account.
    /// </summary>
    public DbSet<InventoryItem> InventoryItems { get; set; }

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

        // Configure QuestDefinition entity
        modelBuilder.Entity<QuestDefinition>()
            .ToTable("quest_definitions")
            .HasKey(q => q.QuestId);

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.QuestId)
            .HasColumnName("quest_id")
            .HasMaxLength(64);

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.Name)
            .HasColumnName("name")
            .HasMaxLength(80)
            .IsRequired();

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.Description)
            .HasColumnName("description")
            .HasMaxLength(300)
            .IsRequired();

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.RequiredEnemyDefeats)
            .HasColumnName("required_enemy_defeats")
            .HasDefaultValue(1);

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.RewardXp)
            .HasColumnName("reward_xp")
            .HasDefaultValue(0);

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.RewardGold)
            .HasColumnName("reward_gold")
            .HasDefaultValue(0);

        modelBuilder.Entity<QuestDefinition>()
            .Property(q => q.RewardItemCode)
            .HasColumnName("reward_item_code")
            .HasMaxLength(64);

        // Configure PlayerQuest entity
        modelBuilder.Entity<PlayerQuest>()
            .ToTable("player_quests")
            .HasKey(pq => pq.PlayerQuestId);

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.PlayerQuestId)
            .HasColumnName("player_quest_id");

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.AccountId)
            .HasColumnName("account_id");

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.QuestId)
            .HasColumnName("quest_id")
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.Status)
            .HasColumnName("status")
            .HasMaxLength(24)
            .IsRequired();

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.ProgressCount)
            .HasColumnName("progress_count")
            .HasDefaultValue(0);

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.AcceptedAt)
            .HasColumnName("accepted_at")
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<PlayerQuest>()
            .Property(pq => pq.CompletedAt)
            .HasColumnName("completed_at");

        modelBuilder.Entity<PlayerQuest>()
            .HasIndex(pq => new { pq.AccountId, pq.QuestId })
            .IsUnique();

        modelBuilder.Entity<PlayerQuest>()
            .HasOne(pq => pq.Account)
            .WithMany(a => a.PlayerQuests)
            .HasForeignKey(pq => pq.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerQuest>()
            .HasOne(pq => pq.QuestDefinition)
            .WithMany(q => q.PlayerQuests)
            .HasForeignKey(pq => pq.QuestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure InventoryItem entity
        modelBuilder.Entity<InventoryItem>()
            .ToTable("inventory_items")
            .HasKey(ii => ii.InventoryItemId);

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.InventoryItemId)
            .HasColumnName("inventory_item_id");

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.AccountId)
            .HasColumnName("account_id");

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.ItemName)
            .HasColumnName("item_name")
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.Rarity)
            .HasColumnName("rarity")
            .HasMaxLength(24)
            .IsRequired();

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.AcquiredAt)
            .HasColumnName("acquired_at")
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(ii => new { ii.AccountId, ii.ItemCode })
            .IsUnique();

        modelBuilder.Entity<InventoryItem>()
            .HasOne(ii => ii.Account)
            .WithMany(a => a.InventoryItems)
            .HasForeignKey(ii => ii.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
