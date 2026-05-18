namespace DungeonCrawler.API.Models;

/// <summary>
/// Immutable quest blueprint shared by all players.
/// </summary>
public class QuestDefinition
{
    public required string QuestId { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public int RequiredEnemyDefeats { get; set; }

    public int RewardXp { get; set; }

    public int RewardGold { get; set; }

    public string? RewardItemCode { get; set; }

    public ICollection<PlayerQuest> PlayerQuests { get; set; } = [];
}