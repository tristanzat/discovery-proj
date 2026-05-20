namespace DungeonCrawler.API.Services;

public interface IQuestRewardService
{
    // Resolves loot from a quest's configured reward item code.
    // Returns a fallback drop when itemCode is null or unrecognized.
    LootDropResult ResolveLoot(string? itemCode);
}

public sealed record LootDropResult(
    string ItemCode,
    string ItemName,
    string Rarity,
    int Quantity);