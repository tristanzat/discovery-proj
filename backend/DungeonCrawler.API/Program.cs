using DungeonCrawler.API.Data;
using DungeonCrawler.API.Models;
using DungeonCrawler.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register EF Core DbContext for Postgres.
// This is the main bridge between API code and relational storage.
var connectionString = builder.Configuration.GetConnectionString("DungeonCrawlerDb")
    ?? throw new InvalidOperationException(
        "Connection string 'DungeonCrawlerDb' was not found. Configure it in appsettings files.");

builder.Services.AddDbContext<DungeonCrawlerDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IDungeonSessionStore, InMemoryDungeonSessionStore>();
builder.Services.AddSingleton<IQuestRewardService, QuestRewardService>();

var app = builder.Build();

var starterQuestDefinitions = new[]
{
    new QuestDefinition
    {
        QuestId = "clear-goblin-room",
        Name = "Goblin Extermination I",
        Description = "Defeat a goblin in the beginner dungeon room.",
        RequiredEnemyDefeats = 1,
        RewardXp = 25,
        RewardGold = 15,
        RewardItemCode = "goblin-ear-trophy"
    }
};

// Seed baseline quest definitions so new accounts can immediately start Phase 2 gameplay.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DungeonCrawlerDbContext>();
    var existingQuestIds = await dbContext.QuestDefinitions
        .AsNoTracking()
        .Select(q => q.QuestId)
        .ToListAsync();

    var existingQuestIdSet = existingQuestIds.ToHashSet(StringComparer.Ordinal);
    var missingQuests = starterQuestDefinitions
        .Where(q => !existingQuestIdSet.Contains(q.QuestId))
        .ToList();

    if (missingQuests.Count > 0)
    {
        await dbContext.QuestDefinitions.AddRangeAsync(missingQuests);
        await dbContext.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

// Quick bootstrap endpoint to verify DbContext can be resolved from DI.
app.MapGet("/db-check", (DungeonCrawlerDbContext dbContext) =>
    Results.Ok(new { status = "db-context-available", provider = dbContext.Database.ProviderName }))
    .WithName("DatabaseCheck");

app.MapPost("/auth/register", async (
    RegisterRequest request,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Username and password are required." });
    }

    var username = request.Username.Trim();
    if (username.Length is < 3 or > 32)
    {
        return Results.BadRequest(new { error = "Username must be between 3 and 32 characters." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });
    }

    var usernameExists = await dbContext.Accounts
        .AsNoTracking()
        .AnyAsync(a => a.Username == username, cancellationToken);

    if (usernameExists)
    {
        return Results.Conflict(new { error = "Username is already taken." });
    }

    var account = new Account
    {
        Username = username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        CreatedAt = DateTime.UtcNow
    };

    dbContext.Accounts.Add(account);
    await dbContext.SaveChangesAsync(cancellationToken);

    // Initialize durable progression at account creation.
    dbContext.PlayerProgresses.Add(new PlayerProgress
    {
        AccountId = account.AccountId,
        Level = 1,
        Xp = 0,
        Gold = 0,
        MaxHp = 100,
        LastSavedAt = DateTime.UtcNow
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/accounts/{account.AccountId}", new
    {
        accountId = account.AccountId,
        username = account.Username,
        createdAt = account.CreatedAt
    });
})
.WithName("RegisterAccount");

app.MapPost("/auth/login", async (
    LoginRequest request,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Username and password are required." });
    }

    var username = request.Username.Trim();
    var account = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.Username == username, cancellationToken);

    if (account is null || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var progress = await dbContext.PlayerProgresses
        .AsNoTracking()
        .FirstOrDefaultAsync(pp => pp.AccountId == account.AccountId, cancellationToken);

    // NOTE: Stage 3 returns basic login payload only.
    // JWT/session token issuing is a later step.
    return Results.Ok(new
    {
        message = "login-success",
        accountId = account.AccountId,
        username = account.Username,
        level = progress?.Level ?? 1,
        xp = progress?.Xp ?? 0,
        gold = progress?.Gold ?? 0,
        maxHp = progress?.MaxHp ?? 100
    });
})
.WithName("LoginAccount");

app.MapGet("/quests/available/{accountId:int}", async (
    int accountId,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var accountExists = await dbContext.Accounts
        .AsNoTracking()
        .AnyAsync(a => a.AccountId == accountId, cancellationToken);

    if (!accountExists)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var questDefinitions = await dbContext.QuestDefinitions
        .AsNoTracking()
        .OrderBy(q => q.QuestId)
        .ToListAsync(cancellationToken);

    var playerQuests = await dbContext.PlayerQuests
        .AsNoTracking()
        .Where(pq => pq.AccountId == accountId)
        .ToDictionaryAsync(pq => pq.QuestId, cancellationToken);

    return Results.Ok(new
    {
        accountId,
        quests = questDefinitions.Select(q =>
        {
            playerQuests.TryGetValue(q.QuestId, out var playerQuest);
            return new
            {
                questId = q.QuestId,
                name = q.Name,
                description = q.Description,
                requiredEnemyDefeats = q.RequiredEnemyDefeats,
                reward = new
                {
                    xp = q.RewardXp,
                    gold = q.RewardGold,
                    itemCode = q.RewardItemCode
                },
                status = playerQuest?.Status ?? "not-started",
                progressCount = playerQuest?.ProgressCount ?? 0
            };
        })
    });
})
.WithName("GetAvailableQuests");

app.MapPost("/quests/accept", async (
    AcceptQuestRequest request,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var accountExists = await dbContext.Accounts
        .AsNoTracking()
        .AnyAsync(a => a.AccountId == request.AccountId, cancellationToken);

    if (!accountExists)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var questDefinition = await dbContext.QuestDefinitions
        .AsNoTracking()
        .FirstOrDefaultAsync(q => q.QuestId == request.QuestId, cancellationToken);

    if (questDefinition is null)
    {
        return Results.NotFound(new { error = "Quest definition not found." });
    }

    var existingPlayerQuest = await dbContext.PlayerQuests
        .FirstOrDefaultAsync(
            pq => pq.AccountId == request.AccountId && pq.QuestId == request.QuestId,
            cancellationToken);

    if (existingPlayerQuest is not null)
    {
        return Results.Conflict(new { error = "Quest is already accepted or completed." });
    }

    dbContext.PlayerQuests.Add(new PlayerQuest
    {
        AccountId = request.AccountId,
        QuestId = request.QuestId,
        Status = "accepted",
        ProgressCount = 0,
        AcceptedAt = DateTime.UtcNow,
        CompletedAt = null
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        message = "quest-accepted",
        accountId = request.AccountId,
        questId = request.QuestId,
        status = "accepted"
    });
})
.WithName("AcceptQuest");

app.MapPost("/quests/complete", async (
    CompleteQuestRequest request,
    DungeonCrawlerDbContext dbContext,
    IDungeonSessionStore sessionStore,
    IQuestRewardService questRewardService,
    CancellationToken cancellationToken) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (session.AccountId != request.AccountId)
    {
        return Results.BadRequest(new { error = "Session does not belong to this account." });
    }

    if (!session.IsCompleted || session.Status != "victory")
    {
        return Results.BadRequest(new { error = "Session is not a victorious run." });
    }

    var playerQuest = await dbContext.PlayerQuests
        .FirstOrDefaultAsync(
            pq => pq.AccountId == request.AccountId && pq.QuestId == request.QuestId,
            cancellationToken);

    if (playerQuest is null)
    {
        return Results.NotFound(new { error = "Accepted quest not found for this account." });
    }

    if (playerQuest.Status == "completed")
    {
        return Results.Conflict(new { error = "Quest is already completed." });
    }

    var questDefinition = await dbContext.QuestDefinitions
        .AsNoTracking()
        .FirstOrDefaultAsync(q => q.QuestId == request.QuestId, cancellationToken);

    if (questDefinition is null)
    {
        return Results.NotFound(new { error = "Quest definition not found." });
    }

    var progress = await dbContext.PlayerProgresses
        .FirstOrDefaultAsync(pp => pp.AccountId == request.AccountId, cancellationToken);

    if (progress is null)
    {
        return Results.NotFound(new { error = "Player progression record was not found." });
    }

    playerQuest.ProgressCount = Math.Max(playerQuest.ProgressCount, questDefinition.RequiredEnemyDefeats);
    playerQuest.Status = "completed";
    playerQuest.CompletedAt = DateTime.UtcNow;

    progress.Xp += questDefinition.RewardXp;
    progress.Gold += questDefinition.RewardGold;
    progress.LastSavedAt = DateTime.UtcNow;

    var loot = questRewardService.CreateLootDrop(session.EnemyName);
    var existingInventoryItem = await dbContext.InventoryItems
        .FirstOrDefaultAsync(
            ii => ii.AccountId == request.AccountId && ii.ItemCode == loot.ItemCode,
            cancellationToken);

    if (existingInventoryItem is null)
    {
        dbContext.InventoryItems.Add(new InventoryItem
        {
            AccountId = request.AccountId,
            ItemCode = loot.ItemCode,
            ItemName = loot.ItemName,
            Rarity = loot.Rarity,
            Quantity = loot.Quantity,
            AcquiredAt = DateTime.UtcNow
        });
    }
    else
    {
        existingInventoryItem.Quantity += loot.Quantity;
        existingInventoryItem.AcquiredAt = DateTime.UtcNow;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        message = "quest-completed",
        accountId = request.AccountId,
        questId = request.QuestId,
        rewards = new
        {
            xp = questDefinition.RewardXp,
            gold = questDefinition.RewardGold,
            loot = new
            {
                code = loot.ItemCode,
                name = loot.ItemName,
                rarity = loot.Rarity,
                quantity = loot.Quantity
            }
        },
        progression = new
        {
            level = progress.Level,
            xp = progress.Xp,
            gold = progress.Gold
        }
    });
})
.WithName("CompleteQuest");

app.MapGet("/inventory/{accountId:int}", async (
    int accountId,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var accountExists = await dbContext.Accounts
        .AsNoTracking()
        .AnyAsync(a => a.AccountId == accountId, cancellationToken);

    if (!accountExists)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var items = await dbContext.InventoryItems
        .AsNoTracking()
        .Where(ii => ii.AccountId == accountId)
        .OrderBy(ii => ii.ItemName)
        .Select(ii => new
        {
            itemCode = ii.ItemCode,
            itemName = ii.ItemName,
            rarity = ii.Rarity,
            quantity = ii.Quantity,
            acquiredAt = ii.AcquiredAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        itemCount = items.Count,
        items
    });
})
.WithName("GetInventory");

app.MapPost("/dungeon/enter", async (
    EnterDungeonRequest request,
    DungeonCrawlerDbContext dbContext,
    IDungeonSessionStore sessionStore,
    CancellationToken cancellationToken) =>
{
    var account = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.AccountId == request.AccountId, cancellationToken);

    if (account is null)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var progress = await dbContext.PlayerProgresses
        .AsNoTracking()
        .FirstOrDefaultAsync(pp => pp.AccountId == request.AccountId, cancellationToken);

    var playerMaxHp = progress?.MaxHp ?? 100;
    var session = new DungeonRoomSession
    {
        SessionId = Guid.NewGuid().ToString("N"),
        AccountId = account.AccountId,
        Username = account.Username,
        PlayerHp = playerMaxHp,
        PlayerMaxHp = playerMaxHp,
        EnemyHp = 35,
        EnemyMaxHp = 35,
        EnemyName = "Cave Goblin",
        IsCompleted = false,
        Status = "in-progress",
        TurnNumber = 1
    };

    sessionStore.CreateOrReplace(session);

    return Results.Ok(new
    {
        message = "entered-dungeon-room",
        sessionId = session.SessionId,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status
    });
})
.WithName("EnterDungeonRoom");

app.MapGet("/dungeon/session/{sessionId}", (
    string sessionId,
    IDungeonSessionStore sessionStore) =>
{
    if (!sessionStore.TryGet(sessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    return Results.Ok(new
    {
        sessionId = session.SessionId,
        accountId = session.AccountId,
        username = session.Username,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("GetDungeonSession");

app.MapPost("/combat/attack", (
    CombatActionRequest request,
    IDungeonSessionStore sessionStore) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (session.IsCompleted)
    {
        return Results.BadRequest(new { error = "Combat is already completed for this session." });
    }

    // Deterministic values keep the MVP predictable while learning endpoint flow.
    const int playerDamage = 12;
    const int enemyDamage = 6;

    session.EnemyHp = Math.Max(0, session.EnemyHp - playerDamage);
    var outcome = "player-attacked";

    if (session.EnemyHp == 0)
    {
        session.IsCompleted = true;
        session.Status = "victory";
        outcome = "enemy-defeated";
    }
    else
    {
        session.PlayerHp = Math.Max(0, session.PlayerHp - enemyDamage);
        if (session.PlayerHp == 0)
        {
            session.IsCompleted = true;
            session.Status = "defeat";
            outcome = "player-defeated";
        }
        else
        {
            session.TurnNumber += 1;
        }
    }

    sessionStore.CreateOrReplace(session);

    return Results.Ok(new
    {
        outcome,
        sessionId = session.SessionId,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("CombatAttack");

app.MapPost("/combat/retreat", (
    CombatActionRequest request,
    IDungeonSessionStore sessionStore) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    session.IsCompleted = true;
    session.Status = "retreated";
    sessionStore.CreateOrReplace(session);

    return Results.Ok(new
    {
        message = "retreated-from-combat",
        sessionId = session.SessionId,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("CombatRetreat");

app.Run();

internal sealed record RegisterRequest(string Username, string Password);
internal sealed record LoginRequest(string Username, string Password);
internal sealed record EnterDungeonRequest(int AccountId);
internal sealed record CombatActionRequest(string SessionId);
internal sealed record AcceptQuestRequest(int AccountId, string QuestId);
internal sealed record CompleteQuestRequest(int AccountId, string QuestId, string SessionId);
