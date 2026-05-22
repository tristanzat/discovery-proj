using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

public interface IInventoryItemEffectService
{
    // Applies a combat-usable item effect directly to the in-memory session.
    CombatItemUseResult ApplyInCombat(string itemCode, DungeonRoomSession session);
}

public sealed record CombatItemUseResult(
    bool IsSuccess,
    string Outcome,
    string Message,
    int Amount);
