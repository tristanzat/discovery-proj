namespace DungeonCrawler.API.Models;

/// <summary>
/// Tracks a player's progress for a specific quest definition.
/// </summary>
public class PlayerQuest
{
    public long PlayerQuestId { get; set; }

    public int AccountId { get; set; }

    public required string QuestId { get; set; }

    public required string Status { get; set; }

    public int ProgressCount { get; set; }

    public DateTime AcceptedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Account? Account { get; set; }

    public QuestDefinition? QuestDefinition { get; set; }
}