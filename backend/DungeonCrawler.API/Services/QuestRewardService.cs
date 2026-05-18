namespace DungeonCrawler.API.Services;

/// <summary>
/// Starter loot table with deterministic outcomes.
/// This keeps early testing predictable before adding weighted RNG.
/// </summary>
public sealed class QuestRewardService : IQuestRewardService
{
    public LootDropResult CreateLootDrop(string enemyName)
    {
        if (enemyName.Contains("Goblin", StringComparison.OrdinalIgnoreCase))
        {
            return new LootDropResult(
                ItemCode: "goblin-ear-trophy",
                ItemName: "Goblin Ear Trophy",
                Rarity: "common",
                Quantity: 1);
        }

        return new LootDropResult(
            ItemCode: "unknown-relic-shard",
            ItemName: "Unknown Relic Shard",
            Rarity: "uncommon",
            Quantity: 1);
    }
}