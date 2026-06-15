using DungeonCrawler.API.Models;
using DungeonCrawler.API.Services;

namespace DungeonCrawler.Tests;

public class InventoryItemEffectServiceTests
{
    [Fact]
    public void ApplyInCombat_HealsByTwenty_WhenEnoughMissingHp()
    {
        var service = new InventoryItemEffectService();
        var session = BuildSession(playerHp: 70, playerMaxHp: 100);

        var result = service.ApplyInCombat("minor-healing-potion", session);

        Assert.True(result.IsSuccess);
        Assert.Equal("healed", result.Outcome);
        Assert.Equal(20, result.Amount);
        Assert.Equal(90, session.PlayerHp);
    }

    [Fact]
    public void ApplyInCombat_CapsHealing_AtMaxHp()
    {
        var service = new InventoryItemEffectService();
        var session = BuildSession(playerHp: 95, playerMaxHp: 100);

        var result = service.ApplyInCombat("minor-healing-potion", session);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Amount);
        Assert.Equal(100, session.PlayerHp);
    }

    [Fact]
    public void ApplyInCombat_Fails_WhenHpAlreadyFull()
    {
        var service = new InventoryItemEffectService();
        var session = BuildSession(playerHp: 100, playerMaxHp: 100);

        var result = service.ApplyInCombat("minor-healing-potion", session);

        Assert.False(result.IsSuccess);
        Assert.Equal("hp-already-full", result.Outcome);
        Assert.Equal(0, result.Amount);
        Assert.Equal(100, session.PlayerHp);
    }

    [Fact]
    public void ApplyInCombat_Fails_ForUnsupportedItem()
    {
        var service = new InventoryItemEffectService();
        var session = BuildSession(playerHp: 60, playerMaxHp: 100);

        var result = service.ApplyInCombat("goblin-ear-trophy", session);

        Assert.False(result.IsSuccess);
        Assert.Equal("item-not-usable-in-combat", result.Outcome);
        Assert.Equal(0, result.Amount);
        Assert.Equal(60, session.PlayerHp);
    }

    private static DungeonRoomSession BuildSession(int playerHp, int playerMaxHp) =>
        new()
        {
            SessionId = "test-session",
            AccountId = 1,
            Username = "tester",
            PlayerHp = playerHp,
            PlayerMaxHp = playerMaxHp,
            EnemyHp = 35,
            EnemyMaxHp = 35,
            EnemyName = "Cave Goblin",
            EnemyTypeTag = "Goblin",
            FloorNumber = 1,
            CurrentRoomIndex = 0,
            TotalRooms = 3,
            RoomsCleared = 0,
            Rooms =
            [
                new DungeonGeneratedRoom("Cave Goblin", "Goblin", 35),
                new DungeonGeneratedRoom("Stone Kobold", "Kobold", 40),
                new DungeonGeneratedRoom("Ash Skeleton", "Skeleton", 45)
            ],
            IsCompleted = false,
            Status = "in-progress",
            TurnNumber = 1
        };
}
