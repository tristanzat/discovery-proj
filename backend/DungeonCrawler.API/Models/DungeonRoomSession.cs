namespace DungeonCrawler.API.Models;

/// <summary>
/// Ephemeral combat state for a player's generated dungeon run.
/// Stored in-memory for now; can be moved to Cosmos DB later.
/// </summary>
public sealed class DungeonRoomSession
{
    public required string SessionId { get; init; }

    public int AccountId { get; init; }

    public required string Username { get; init; }

    public int PlayerHp { get; set; }

    public int PlayerMaxHp { get; init; }

    public int EnemyHp { get; set; }

    public int EnemyMaxHp { get; set; }

    public required string EnemyName { get; set; }

    public required string EnemyTypeTag { get; set; }

    public int FloorNumber { get; init; }

    // Zero-based index for the currently active room within this floor.
    public int CurrentRoomIndex { get; set; }

    public int TotalRooms { get; init; }

    public int RoomsCleared { get; set; }

    public required List<DungeonGeneratedRoom> Rooms { get; init; }

    public bool IsCompleted { get; set; }

    public required string Status { get; set; }

    public int TurnNumber { get; set; }
}

public sealed record DungeonGeneratedRoom(
    string EnemyName,
    string EnemyTypeTag,
    int EnemyMaxHp);
