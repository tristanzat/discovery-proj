using Microsoft.AspNetCore.SignalR;

namespace DungeonCrawler.API.Hubs;

public sealed class GameHub : Hub
{
    public const string HubGroupName = "hub-overworld";

    public static string AccountGroup(int accountId) => $"account:{accountId}";

    public static string SessionGroup(string sessionId) => $"session:{sessionId}";

    public Task JoinHub()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, HubGroupName);
    }

    public Task JoinAccount(int accountId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));
    }

    public Task LeaveAccount(int accountId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, AccountGroup(accountId));
    }

    public Task JoinSession(string sessionId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public Task LeaveSession(string sessionId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }
}