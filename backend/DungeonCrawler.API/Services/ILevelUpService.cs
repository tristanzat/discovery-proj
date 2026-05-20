using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

public interface ILevelUpService
{
    // Returns total cumulative XP required to reach the given level.
    int XpRequiredForLevel(int level);

    // Checks current XP against thresholds, mutates progress in place for any levels gained.
    // Returns the number of levels gained (0 if none).
    int ApplyLevelUps(PlayerProgress progress);
}
