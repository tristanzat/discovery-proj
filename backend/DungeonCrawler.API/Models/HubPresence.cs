namespace DungeonCrawler.API.Models;

/// <summary>
/// Tracks when an account was last active in the hub overworld.
/// Used to build an active roster for chat and trading interactions.
/// </summary>
public class HubPresence
{
    public int AccountId { get; set; }

    public DateTime LastSeenAt { get; set; }

    public Account? Account { get; set; }
}
