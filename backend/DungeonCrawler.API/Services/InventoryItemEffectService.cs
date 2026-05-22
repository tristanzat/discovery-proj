using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

/// <summary>
/// Resolves item effects that can be used during active combat sessions.
/// </summary>
public sealed class InventoryItemEffectService : IInventoryItemEffectService
{
    private const string MinorHealingPotion = "minor-healing-potion";
    private const int MinorHealingPotionAmount = 20;

    public CombatItemUseResult ApplyInCombat(string itemCode, DungeonRoomSession session)
    {
        if (itemCode.Equals(MinorHealingPotion, StringComparison.OrdinalIgnoreCase))
        {
            if (session.PlayerHp >= session.PlayerMaxHp)
            {
                return new CombatItemUseResult(
                    IsSuccess: false,
                    Outcome: "hp-already-full",
                    Message: "Player HP is already full.",
                    Amount: 0);
            }

            var healAmount = Math.Min(MinorHealingPotionAmount, session.PlayerMaxHp - session.PlayerHp);
            session.PlayerHp += healAmount;

            return new CombatItemUseResult(
                IsSuccess: true,
                Outcome: "healed",
                Message: "Minor Healing Potion restored HP.",
                Amount: healAmount);
        }

        return new CombatItemUseResult(
            IsSuccess: false,
            Outcome: "item-not-usable-in-combat",
            Message: "This item cannot be used during combat.",
            Amount: 0);
    }
}
