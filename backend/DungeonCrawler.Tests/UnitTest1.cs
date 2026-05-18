using DungeonCrawler.API.Services;

namespace DungeonCrawler.Tests;

public class QuestRewardServiceTests
{
    [Fact]
    public void CreateLootDrop_ReturnsGoblinTrophy_ForGoblinEnemies()
    {
        var service = new QuestRewardService();

        var result = service.CreateLootDrop("Cave Goblin");

        Assert.Equal("goblin-ear-trophy", result.ItemCode);
        Assert.Equal("Goblin Ear Trophy", result.ItemName);
        Assert.Equal("common", result.Rarity);
        Assert.Equal(1, result.Quantity);
    }

    [Fact]
    public void CreateLootDrop_ReturnsFallbackLoot_ForUnknownEnemies()
    {
        var service = new QuestRewardService();

        var result = service.CreateLootDrop("Ancient Slime");

        Assert.Equal("unknown-relic-shard", result.ItemCode);
        Assert.Equal("Unknown Relic Shard", result.ItemName);
        Assert.Equal("uncommon", result.Rarity);
        Assert.Equal(1, result.Quantity);
    }
}
