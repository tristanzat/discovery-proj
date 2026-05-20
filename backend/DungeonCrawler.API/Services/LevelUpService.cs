using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

/// <summary>
/// Handles XP thresholds and level advancement.
/// Each level-up increases MaxHp by 20.
/// </summary>
public sealed class LevelUpService : ILevelUpService
{
    // Cumulative XP to reach each level. Index = level number (index 0 and 1 unused).
    // Example: 25 XP earned from the starter quest immediately grants level 2.
    private static readonly int[] XpThresholds =
    [
        0,    // unused
        0,    // level 1 — start level, no XP required
        25,   // level 2
        75,   // level 3
        150,  // level 4
        250,  // level 5
        375,  // level 6
        525,  // level 7
        700,  // level 8
        900,  // level 9
        1125  // level 10 (cap)
    ];

    private const int MaxLevel = 10;
    private const int HpPerLevel = 20;

    public int XpRequiredForLevel(int level)
    {
        if (level <= 1) return 0;
        // Clamp to the last valid threshold for levels beyond the cap.
        if (level >= XpThresholds.Length) return XpThresholds[^1];
        return XpThresholds[level];
    }

    public int ApplyLevelUps(PlayerProgress progress)
    {
        int levelsGained = 0;

        // Keep leveling until XP doesn't reach the next threshold or the cap is hit.
        while (progress.Level < MaxLevel)
        {
            int xpNeeded = XpRequiredForLevel(progress.Level + 1);
            if (progress.Xp < xpNeeded) break;

            progress.Level += 1;
            progress.MaxHp += HpPerLevel;
            levelsGained++;
        }

        return levelsGained;
    }
}
