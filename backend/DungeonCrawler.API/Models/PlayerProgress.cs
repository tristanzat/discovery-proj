namespace DungeonCrawler.API.Models;

/// <summary>
/// Represents a player's progression state (durable).
/// Maps to the 'player_progress' table in Postgres.
/// One-to-one relationship with Account.
/// </summary>
public class PlayerProgress
{
    /// <summary>
    /// Foreign key to the Account this progression belongs to.
    /// Also the primary key for this table (one-to-one with Account).
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Current character level.
    /// Starts at 1.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Total experience points earned.
    /// Used to track progress toward next level.
    /// </summary>
    public int Xp { get; set; }

    /// <summary>
    /// Currency earned from combat and quests.
    /// Used for trading and items in later phases.
    /// </summary>
    public int Gold { get; set; }

    /// <summary>
    /// Maximum hit points the character can have.
    /// Increased as level increases.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    /// When this progression was last saved to the database (UTC).
    /// Updated after each significant action.
    /// </summary>
    public DateTime LastSavedAt { get; set; }

    /// <summary>
    /// Navigation property: back-reference to the Account.
    /// Allows bidirectional traversal: Account -> PlayerProgress -> Account.
    /// </summary>
    public Account? Account { get; set; }
}
