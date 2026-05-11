namespace DungeonCrawler.API.Models;

/// <summary>
/// Represents a player account (durable identity).
/// Maps to the 'accounts' table in Postgres.
/// </summary>
public class Account
{
    // TODO: Property for account_id (int, primary key)
    
    // TODO: Property for username (string, unique, max 32 chars)
    
    // TODO: Property for password_hash (string, not null)
    
    // TODO: Property for created_at (DateTime, UTC timestamp)

    // Navigation: one account has one player progress record
    // TODO: Navigation property to PlayerProgress
}
