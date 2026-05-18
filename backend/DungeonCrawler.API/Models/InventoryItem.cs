namespace DungeonCrawler.API.Models;

/// <summary>
/// Represents one stackable inventory item for an account.
/// </summary>
public class InventoryItem
{
    public long InventoryItemId { get; set; }

    public int AccountId { get; set; }

    public required string ItemCode { get; set; }

    public required string ItemName { get; set; }

    public required string Rarity { get; set; }

    public int Quantity { get; set; }

    public DateTime AcquiredAt { get; set; }

    public Account? Account { get; set; }
}