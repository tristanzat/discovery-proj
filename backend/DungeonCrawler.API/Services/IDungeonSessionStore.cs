using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

public interface IDungeonSessionStore
{
    DungeonRoomSession CreateOrReplace(DungeonRoomSession session);

    bool TryGet(string sessionId, out DungeonRoomSession? session);

    void Remove(string sessionId);
}
