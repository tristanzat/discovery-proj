using DungeonCrawler.API.Models;
using DungeonCrawler.API.Services;

namespace DungeonCrawler.Tests;

public class LevelUpServiceTests
{
    [Fact]
    public void ApplyLevelUps_DoesNotLevelUp_WhenXpBelowThreshold()
    {
        var service = new LevelUpService();
        var progress = MakeProgress(level: 1, xp: 24);

        int gained = service.ApplyLevelUps(progress);

        Assert.Equal(0, gained);
        Assert.Equal(1, progress.Level);
        Assert.Equal(100, progress.MaxHp);
    }

    [Fact]
    public void ApplyLevelUps_LevelsUpOnce_WhenXpReachesThreshold()
    {
        var service = new LevelUpService();
        var progress = MakeProgress(level: 1, xp: 25);

        int gained = service.ApplyLevelUps(progress);

        Assert.Equal(1, gained);
        Assert.Equal(2, progress.Level);
        Assert.Equal(120, progress.MaxHp);
    }

    [Fact]
    public void ApplyLevelUps_LevelsUpTwice_WhenXpSpansMultipleThresholds()
    {
        var service = new LevelUpService();
        // 75 XP crosses both level 2 (25) and level 3 (75) thresholds.
        var progress = MakeProgress(level: 1, xp: 75);

        int gained = service.ApplyLevelUps(progress);

        Assert.Equal(2, gained);
        Assert.Equal(3, progress.Level);
        Assert.Equal(140, progress.MaxHp);
    }

    [Fact]
    public void ApplyLevelUps_DoesNotExceedMaxLevel()
    {
        var service = new LevelUpService();
        var progress = MakeProgress(level: 10, xp: 99999);

        int gained = service.ApplyLevelUps(progress);

        Assert.Equal(0, gained);
        Assert.Equal(10, progress.Level);
    }

    [Fact]
    public void XpRequiredForLevel_ReturnsZero_ForLevelOne()
    {
        var service = new LevelUpService();

        Assert.Equal(0, service.XpRequiredForLevel(1));
    }

    [Fact]
    public void XpRequiredForLevel_Returns25_ForLevelTwo()
    {
        var service = new LevelUpService();

        Assert.Equal(25, service.XpRequiredForLevel(2));
    }

    // Helper: create a minimal PlayerProgress for level-up testing.
    private static PlayerProgress MakeProgress(int level, int xp) =>
        new() { AccountId = 1, Level = level, Xp = xp, Gold = 0, MaxHp = 100 + (level - 1) * 20, LastSavedAt = DateTime.UtcNow };
}
