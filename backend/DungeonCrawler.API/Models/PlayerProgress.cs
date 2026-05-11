namespace DungeonCrawler.API.Models;

/// <summary>
/// Represents a player's progression state (durable).
/// Maps to the 'player_progress' table in Postgres.
/// One-to-one relationship with Account.
/// </summary>
public class PlayerProgress
{
    // TODO: Property for account_id (int, primary key AND foreign key to accounts)
    
    // TODO: Property for level (int, default 1)
    
    // TODO: Property for xp (int, default 0)
    
    // TODO: Property for gold (int, default 0)
    
    // TODO: Property for max_hp (int, default 100)
    
    // TODO: Property for last_saved_at (DateTime, UTC timestamp)

    // Navigation: back-reference to Account
    // TODO: Navigation property to Account
}
