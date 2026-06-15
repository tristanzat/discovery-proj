using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

public sealed class ProceduralDungeonGenerator : IProceduralDungeonGenerator
{
    private static readonly DungeonEnemyArchetype[] EnemyPool =
    [
        new("Cave Goblin", "Goblin", 30, 40),
        new("Tunnel Goblin Scout", "Goblin", 28, 38),
        new("Stone Kobold", "Kobold", 32, 44),
        new("Ash Skeleton", "Skeleton", 34, 46),
        new("Cursed Rat Swarm", "Beast", 26, 36),
    ];

    public ProceduralDungeonLayout GenerateFloor(int playerLevel, int? seed = null)
    {
        var resolvedSeed = seed ?? Random.Shared.Next(10_000, 999_999);
        var random = new Random(resolvedSeed);
        var normalizedLevel = Math.Max(1, playerLevel);

        var roomCount = Math.Min(6, 3 + normalizedLevel / 3 + random.Next(0, 2));
        var rooms = new List<DungeonGeneratedRoom>(roomCount);

        for (var roomIndex = 0; roomIndex < roomCount; roomIndex += 1)
        {
            var archetype = EnemyPool[random.Next(EnemyPool.Length)];
            var hpScaling = (normalizedLevel - 1) * 3 + roomIndex * 2;
            var hpRoll = random.Next(archetype.MinHp, archetype.MaxHp + 1);
            var enemyMaxHp = Math.Max(20, hpRoll + hpScaling);

            rooms.Add(new DungeonGeneratedRoom(
                EnemyName: archetype.Name,
                EnemyTypeTag: archetype.TypeTag,
                EnemyMaxHp: enemyMaxHp));
        }

        return new ProceduralDungeonLayout(
            Seed: resolvedSeed,
            FloorNumber: 1,
            Rooms: rooms);
    }

    private sealed record DungeonEnemyArchetype(
        string Name,
        string TypeTag,
        int MinHp,
        int MaxHp);
}
