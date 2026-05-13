using System.Collections.Concurrent;
using DungeonCrawler.API.Models;

namespace DungeonCrawler.API.Services;

/// <summary>
/// Thread-safe in-memory session storage for dungeon combat.
/// This keeps Phase 1 simple while preserving a swappable abstraction.
/// </summary>
public sealed class InMemoryDungeonSessionStore : IDungeonSessionStore
{
    private readonly ConcurrentDictionary<string, DungeonRoomSession> _sessions = new();

    public DungeonRoomSession CreateOrReplace(DungeonRoomSession session)
    {
        _sessions[session.SessionId] = session;
        return session;
    }

    public bool TryGet(string sessionId, out DungeonRoomSession? session)
    {
        var found = _sessions.TryGetValue(sessionId, out var existing);
        session = existing;
        return found;
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
