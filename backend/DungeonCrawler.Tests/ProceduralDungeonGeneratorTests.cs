using DungeonCrawler.API.Services;

namespace DungeonCrawler.Tests;

public class ProceduralDungeonGeneratorTests
{
    [Fact]
    public void GenerateFloor_ReturnsDeterministicLayout_WhenSeedIsProvided()
    {
        var generator = new ProceduralDungeonGenerator();

        var a = generator.GenerateFloor(playerLevel: 3, seed: 424242);
        var b = generator.GenerateFloor(playerLevel: 3, seed: 424242);

        Assert.Equal(a.Rooms.Count, b.Rooms.Count);
        Assert.Equal(a.Rooms.Select(r => r.EnemyName), b.Rooms.Select(r => r.EnemyName));
        Assert.Equal(a.Rooms.Select(r => r.EnemyMaxHp), b.Rooms.Select(r => r.EnemyMaxHp));
    }

    [Fact]
    public void GenerateFloor_ScalesAverageHp_AtHigherLevel()
    {
        var generator = new ProceduralDungeonGenerator();

        var lowLevel = generator.GenerateFloor(playerLevel: 1, seed: 8080);
        var highLevel = generator.GenerateFloor(playerLevel: 8, seed: 8080);

        var lowAverageHp = lowLevel.Rooms.Average(r => r.EnemyMaxHp);
        var highAverageHp = highLevel.Rooms.Average(r => r.EnemyMaxHp);

        Assert.True(highAverageHp > lowAverageHp);
    }

    [Fact]
    public void GenerateFloor_ReturnsAtLeastThreeRooms()
    {
        var generator = new ProceduralDungeonGenerator();

        var result = generator.GenerateFloor(playerLevel: 1, seed: 101010);

        Assert.True(result.Rooms.Count >= 3);
        Assert.All(result.Rooms, room => Assert.True(room.EnemyMaxHp >= 20));
    }
}
