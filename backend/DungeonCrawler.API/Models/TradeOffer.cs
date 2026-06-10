namespace DungeonCrawler.API.Models;

/// <summary>
/// Represents a player-to-player trade offer in the hub overworld.
/// Items are moved into escrow at offer creation and released on resolution.
/// </summary>
public class TradeOffer
{
    public long TradeOfferId { get; set; }

    public int FromAccountId { get; set; }

    public int ToAccountId { get; set; }

    public required string ItemCode { get; set; }

    public required string ItemName { get; set; }

    public required string Rarity { get; set; }

    public int Quantity { get; set; }

    public string? Note { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public Account? FromAccount { get; set; }

    public Account? ToAccount { get; set; }
}
