namespace DungeonCrawler.API.Models;

/// <summary>
/// Ephemeral combat state for a player in a single dungeon room.
/// Stored in-memory for Phase 1; can be moved to Cosmos DB later.
/// </summary>
public sealed class DungeonRoomSession
{
    public required string SessionId { get; init; }

    public int AccountId { get; init; }

    public required string Username { get; init; }

    public int PlayerHp { get; set; }

    public int PlayerMaxHp { get; init; }

    public int EnemyHp { get; set; }

    public int EnemyMaxHp { get; init; }

    public required string EnemyName { get; init; }

    public bool IsCompleted { get; set; }

    public required string Status { get; set; }

    public int TurnNumber { get; set; }
}
