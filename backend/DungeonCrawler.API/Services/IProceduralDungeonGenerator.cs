using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

public interface IProceduralDungeonGenerator
{
    ProceduralDungeonLayout GenerateFloor(int playerLevel, int? seed = null);
}

public sealed record ProceduralDungeonLayout(
    int Seed,
    int FloorNumber,
    IReadOnlyList<DungeonGeneratedRoom> Rooms);
