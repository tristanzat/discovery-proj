namespace DungeonCrawler.API.Services;

public interface IQuestRewardService
{
    LootDropResult CreateLootDrop(string enemyName);
}

public sealed record LootDropResult(
    string ItemCode,
    string ItemName,
    string Rarity,
    int Quantity);