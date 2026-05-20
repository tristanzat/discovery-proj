namespace DungeonCrawler.API.Services;

/// <summary>
/// Resolves a loot drop from a quest's reward item code.
/// The catalog maps known item codes to display name and rarity.
/// Add new items here as the loot table expands.
/// </summary>
public sealed class QuestRewardService : IQuestRewardService
{
    private static readonly Dictionary<string, (string ItemName, string Rarity)> ItemCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["goblin-ear-trophy"] = ("Goblin Ear Trophy", "common"),
            ["unknown-relic-shard"] = ("Unknown Relic Shard", "uncommon")
        };

    public LootDropResult ResolveLoot(string? itemCode)
    {
        if (itemCode is not null && ItemCatalog.TryGetValue(itemCode, out var entry))
        {
            return new LootDropResult(
                ItemCode: itemCode,
                ItemName: entry.ItemName,
                Rarity: entry.Rarity,
                Quantity: 1);
        }

        // Unknown or null item codes fall back to the generic relic shard.
        return new LootDropResult(
            ItemCode: "unknown-relic-shard",
            ItemName: "Unknown Relic Shard",
            Rarity: "uncommon",
            Quantity: 1);
    }
}