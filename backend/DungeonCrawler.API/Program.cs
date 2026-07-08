using DungeonCrawler.API.Data;
using DungeonCrawler.API.Hubs;
using DungeonCrawler.API.Models;
using DungeonCrawler.API.Services;
using Microsoft.AspNetCore.SignalR;
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
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDungeonSessionStore, InMemoryDungeonSessionStore>();
builder.Services.AddSingleton<IProceduralDungeonGenerator, ProceduralDungeonGenerator>();
builder.Services.AddSingleton<IQuestRewardService, QuestRewardService>();
builder.Services.AddSingleton<ILevelUpService, LevelUpService>();
builder.Services.AddSingleton<IInventoryItemEffectService, InventoryItemEffectService>();

var app = builder.Build();

static async Task MarkHubPresenceAsync(
    DungeonCrawlerDbContext dbContext,
    int accountId,
    CancellationToken cancellationToken)
{
    var existing = await dbContext.HubPresences
        .FirstOrDefaultAsync(hp => hp.AccountId == accountId, cancellationToken);

    if (existing is null)
    {
        dbContext.HubPresences.Add(new HubPresence
        {
            AccountId = accountId,
            LastSeenAt = DateTime.UtcNow
        });
        return;
    }

    existing.LastSeenAt = DateTime.UtcNow;
}

static Task NotifyHubPresenceChangedAsync(
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.HubGroupName)
        .SendAsync("hubPresenceChanged", new { changedAt = DateTime.UtcNow }, cancellationToken);
}

static Task NotifyHubChatMessageReceivedAsync(
    IHubContext<GameHub> hubContext,
    object chatMessage,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.HubGroupName)
        .SendAsync("hubChatMessageReceived", chatMessage, cancellationToken);
}

static Task NotifyTradeOffersChangedAsync(
    IHubContext<GameHub> hubContext,
    int accountId,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.AccountGroup(accountId))
        .SendAsync("tradeOffersChanged", new { accountId, changedAt = DateTime.UtcNow }, cancellationToken);
}

static Task NotifyInventoryChangedAsync(
    IHubContext<GameHub> hubContext,
    int accountId,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.AccountGroup(accountId))
        .SendAsync("inventoryChanged", new { accountId, changedAt = DateTime.UtcNow }, cancellationToken);
}

static Task NotifyQuestsChangedAsync(
    IHubContext<GameHub> hubContext,
    int accountId,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.AccountGroup(accountId))
        .SendAsync("questsChanged", new { accountId, changedAt = DateTime.UtcNow }, cancellationToken);
}

static Task NotifyProgressChangedAsync(
    IHubContext<GameHub> hubContext,
    int accountId,
    CancellationToken cancellationToken)
{
    return hubContext.Clients
        .Group(GameHub.AccountGroup(accountId))
        .SendAsync("progressChanged", new { accountId, changedAt = DateTime.UtcNow }, cancellationToken);
}

static Task NotifyDungeonSessionChangedAsync(
    IHubContext<GameHub> hubContext,
    DungeonRoomSession session,
    CancellationToken cancellationToken)
{
    var payload = new
    {
        accountId = session.AccountId,
        sessionId = session.SessionId,
        status = session.Status,
        isCompleted = session.IsCompleted,
        changedAt = DateTime.UtcNow
    };

    return Task.WhenAll(
        hubContext.Clients.Group(GameHub.AccountGroup(session.AccountId))
            .SendAsync("dungeonSessionChanged", payload, cancellationToken),
        hubContext.Clients.Group(GameHub.SessionGroup(session.SessionId))
            .SendAsync("dungeonSessionChanged", payload, cancellationToken));
}

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
        RewardItemCode = "goblin-ear-trophy",
        EnemyTypeTag = "Goblin"
    },
    new QuestDefinition
    {
        QuestId = "goblin-culling-contract",
        Name = "Goblin Culling Contract",
        Description = "Defeat 3 goblins to thin out the cave population.",
        RequiredEnemyDefeats = 3,
        RewardXp = 60,
        RewardGold = 35,
        RewardItemCode = "unknown-relic-shard",
        EnemyTypeTag = "Goblin"
    }
};

// Seed baseline quest definitions so new accounts can immediately start Phase 2 gameplay.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DungeonCrawlerDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Ensure Phase 2 tables/columns exist even when the database was initialized
    // from older scripts that only created Phase 1 tables.
    await dbContext.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS quest_definitions (
            quest_id VARCHAR(64) PRIMARY KEY,
            name VARCHAR(80) NOT NULL,
            description VARCHAR(300) NOT NULL,
            required_enemy_defeats INT NOT NULL DEFAULT 1,
            reward_xp INT NOT NULL DEFAULT 0,
            reward_gold INT NOT NULL DEFAULT 0,
            reward_item_code VARCHAR(64),
            enemy_type_tag VARCHAR(64)
        );

        CREATE TABLE IF NOT EXISTS player_quests (
            player_quest_id BIGSERIAL PRIMARY KEY,
            account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
            quest_id VARCHAR(64) NOT NULL REFERENCES quest_definitions(quest_id) ON DELETE RESTRICT,
            status VARCHAR(24) NOT NULL,
            progress_count INT NOT NULL DEFAULT 0,
            accepted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            completed_at TIMESTAMPTZ NULL,
            CONSTRAINT uq_player_quest UNIQUE (account_id, quest_id)
        );

        CREATE TABLE IF NOT EXISTS inventory_items (
            inventory_item_id BIGSERIAL PRIMARY KEY,
            account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
            item_code VARCHAR(64) NOT NULL,
            item_name VARCHAR(100) NOT NULL,
            rarity VARCHAR(24) NOT NULL,
            quantity INT NOT NULL DEFAULT 1,
            acquired_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            CONSTRAINT uq_inventory_item UNIQUE (account_id, item_code)
        );

        CREATE TABLE IF NOT EXISTS hub_chat_messages (
            hub_chat_message_id BIGSERIAL PRIMARY KEY,
            account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
            message VARCHAR(280) NOT NULL,
            sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS ix_hub_chat_messages_sent_at
            ON hub_chat_messages(sent_at DESC);

        CREATE TABLE IF NOT EXISTS trade_offers (
            trade_offer_id BIGSERIAL PRIMARY KEY,
            from_account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
            to_account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
            item_code VARCHAR(64) NOT NULL,
            item_name VARCHAR(100) NOT NULL,
            rarity VARCHAR(24) NOT NULL,
            quantity INT NOT NULL,
            note VARCHAR(180),
            status VARCHAR(24) NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            responded_at TIMESTAMPTZ NULL
        );

        CREATE INDEX IF NOT EXISTS ix_trade_offers_to_status_created
            ON trade_offers(to_account_id, status, created_at DESC);

        CREATE INDEX IF NOT EXISTS ix_trade_offers_from_status_created
            ON trade_offers(from_account_id, status, created_at DESC);

        CREATE TABLE IF NOT EXISTS hub_presence (
            account_id INT PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
            last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS ix_hub_presence_last_seen
            ON hub_presence(last_seen_at DESC);

        ALTER TABLE quest_definitions
            ADD COLUMN IF NOT EXISTS enemy_type_tag VARCHAR(64) NULL;
        """);

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
app.MapHub<GameHub>("/game");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

// Quick bootstrap endpoint to verify DbContext can be resolved from DI.
app.MapGet("/db-check", (DungeonCrawlerDbContext dbContext) =>
    Results.Ok(new { status = "db-context-available", provider = dbContext.Database.ProviderName }))
    .WithName("DatabaseCheck");

app.MapPost("/auth/register", async (
    RegisterRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
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

    // Starter consumables make inventory usage testable immediately in Phase 2.
    dbContext.InventoryItems.Add(new InventoryItem
    {
        AccountId = account.AccountId,
        ItemCode = "minor-healing-potion",
        ItemName = "Minor Healing Potion",
        Rarity = "common",
        Quantity = 3,
        AcquiredAt = DateTime.UtcNow
    });

    await MarkHubPresenceAsync(dbContext, account.AccountId, cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);
    await NotifyHubPresenceChangedAsync(hubContext, cancellationToken);

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
    IHubContext<GameHub> hubContext,
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

    await MarkHubPresenceAsync(dbContext, account.AccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await NotifyHubPresenceChangedAsync(hubContext, cancellationToken);

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
    IHubContext<GameHub> hubContext,
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
    await NotifyQuestsChangedAsync(hubContext, request.AccountId, cancellationToken);

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
    IQuestRewardService questRewardService,
    ILevelUpService levelUpService,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
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

    // Quest must have reached "ready" status through the combat kill-tracking in /combat/attack.
    if (playerQuest.Status != "ready")
    {
        return Results.BadRequest(new
        {
            error = "Quest progress is not complete yet. Keep defeating enemies.",
            progressCount = playerQuest.ProgressCount
        });
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

    playerQuest.Status = "completed";
    playerQuest.CompletedAt = DateTime.UtcNow;

    progress.Xp += questDefinition.RewardXp;
    progress.Gold += questDefinition.RewardGold;
    progress.LastSavedAt = DateTime.UtcNow;

    // Check if the new XP total crosses one or more level thresholds.
    int levelsGained = levelUpService.ApplyLevelUps(progress);

    var loot = questRewardService.ResolveLoot(questDefinition.RewardItemCode);
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
    await Task.WhenAll(
        NotifyQuestsChangedAsync(hubContext, request.AccountId, cancellationToken),
        NotifyInventoryChangedAsync(hubContext, request.AccountId, cancellationToken),
        NotifyProgressChangedAsync(hubContext, request.AccountId, cancellationToken));

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
            gold = progress.Gold,
            maxHp = progress.MaxHp,
            levelsGained,
            leveledUp = levelsGained > 0
        }
    });
})
.WithName("CompleteQuest");

app.MapGet("/quests/log/{accountId:int}", async (
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

    var questLog = await dbContext.PlayerQuests
        .AsNoTracking()
        .Where(pq => pq.AccountId == accountId)
        .Include(pq => pq.QuestDefinition)
        .OrderByDescending(pq => pq.Status == "ready")
        .ThenByDescending(pq => pq.AcceptedAt)
        .Select(pq => new
        {
            questId = pq.QuestId,
            name = pq.QuestDefinition != null ? pq.QuestDefinition.Name : pq.QuestId,
            description = pq.QuestDefinition != null ? pq.QuestDefinition.Description : string.Empty,
            requiredEnemyDefeats = pq.QuestDefinition != null ? pq.QuestDefinition.RequiredEnemyDefeats : 0,
            progressCount = pq.ProgressCount,
            status = pq.Status,
            acceptedAt = pq.AcceptedAt,
            completedAt = pq.CompletedAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        questCount = questLog.Count,
        quests = questLog
    });
})
.WithName("GetQuestLog");

app.MapPost("/quests/abandon", async (
    AbandonQuestRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
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
        return Results.Conflict(new { error = "Completed quests cannot be abandoned." });
    }

    dbContext.PlayerQuests.Remove(playerQuest);
    await dbContext.SaveChangesAsync(cancellationToken);
    await NotifyQuestsChangedAsync(hubContext, request.AccountId, cancellationToken);

    return Results.Ok(new
    {
        message = "quest-abandoned",
        accountId = request.AccountId,
        questId = request.QuestId
    });
})
.WithName("AbandonQuest");

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

app.MapPost("/inventory/use-combat", async (
    UseInventoryItemRequest request,
    DungeonCrawlerDbContext dbContext,
    IDungeonSessionStore sessionStore,
    IInventoryItemEffectService itemEffectService,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ItemCode))
    {
        return Results.BadRequest(new { error = "Item code is required." });
    }

    var itemCode = request.ItemCode.Trim();

    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (session.AccountId != request.AccountId)
    {
        return Results.BadRequest(new { error = "Session does not belong to this account." });
    }

    if (session.IsCompleted)
    {
        return Results.BadRequest(new { error = "Combat is already completed for this session." });
    }

    if (!string.Equals(session.Status, "in-progress", StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Combat consumables can only be used during an active room fight." });
    }

    var inventoryItem = await dbContext.InventoryItems
        .FirstOrDefaultAsync(
            ii => ii.AccountId == request.AccountId && ii.ItemCode == itemCode,
            cancellationToken);

    if (inventoryItem is null || inventoryItem.Quantity <= 0)
    {
        return Results.NotFound(new { error = "Item not found in inventory." });
    }

    var effect = itemEffectService.ApplyInCombat(itemCode, session);
    if (!effect.IsSuccess)
    {
        return Results.BadRequest(new
        {
            error = effect.Message,
            outcome = effect.Outcome
        });
    }

    inventoryItem.Quantity -= 1;
    if (inventoryItem.Quantity <= 0)
    {
        dbContext.InventoryItems.Remove(inventoryItem);
    }

    sessionStore.CreateOrReplace(session);
    await dbContext.SaveChangesAsync(cancellationToken);
    await Task.WhenAll(
        NotifyInventoryChangedAsync(hubContext, request.AccountId, cancellationToken),
        NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken));

    return Results.Ok(new
    {
        message = "item-used",
        outcome = effect.Outcome,
        itemCode,
        amount = effect.Amount,
        remainingQuantity = Math.Max(0, inventoryItem.Quantity),
        session = new
        {
            sessionId = session.SessionId,
            playerHp = session.PlayerHp,
            playerMaxHp = session.PlayerMaxHp,
            enemyHp = session.EnemyHp,
            status = session.Status
        }
    });
})
.WithName("UseInventoryItemInCombat");

app.MapPost("/dungeon/enter", async (
    EnterDungeonRequest request,
    DungeonCrawlerDbContext dbContext,
    IProceduralDungeonGenerator dungeonGenerator,
    IDungeonSessionStore sessionStore,
    IHubContext<GameHub> hubContext,
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

    var playerLevel = progress?.Level ?? 1;
    var floorLayout = dungeonGenerator.GenerateFloor(playerLevel, request.Seed);
    var firstRoom = floorLayout.Rooms[0];
    var playerMaxHp = progress?.MaxHp ?? 100;
    var session = new DungeonRoomSession
    {
        SessionId = Guid.NewGuid().ToString("N"),
        AccountId = account.AccountId,
        Username = account.Username,
        PlayerHp = playerMaxHp,
        PlayerMaxHp = playerMaxHp,
        EnemyHp = firstRoom.EnemyMaxHp,
        EnemyMaxHp = firstRoom.EnemyMaxHp,
        EnemyName = firstRoom.EnemyName,
        EnemyTypeTag = firstRoom.EnemyTypeTag,
        FloorNumber = floorLayout.FloorNumber,
        CurrentRoomIndex = 0,
        TotalRooms = floorLayout.Rooms.Count,
        RoomsCleared = 0,
        Rooms = floorLayout.Rooms.ToList(),
        IsCompleted = false,
        Status = "in-progress",
        TurnNumber = 1
    };

    sessionStore.CreateOrReplace(session);
    await NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken);

    return Results.Ok(new
    {
        message = "entered-procedural-floor",
        sessionId = session.SessionId,
        seed = floorLayout.Seed,
        floor = session.FloorNumber,
        roomNumber = session.CurrentRoomIndex + 1,
        totalRooms = session.TotalRooms,
        roomsCleared = session.RoomsCleared,
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
        floor = session.FloorNumber,
        roomNumber = session.CurrentRoomIndex + 1,
        totalRooms = session.TotalRooms,
        roomsCleared = session.RoomsCleared,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("GetDungeonSession");

app.MapPost("/dungeon/advance", async (
    AdvanceDungeonRoomRequest request,
    IDungeonSessionStore sessionStore,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (session.IsCompleted)
    {
        return Results.BadRequest(new { error = "Dungeon run is already completed." });
    }

    if (!string.Equals(session.Status, "room-cleared", StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Current room is not cleared yet." });
    }

    var nextRoomIndex = session.CurrentRoomIndex + 1;
    if (nextRoomIndex >= session.TotalRooms)
    {
        session.IsCompleted = true;
        session.Status = "victory";
        sessionStore.CreateOrReplace(session);
        await NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken);

        return Results.Ok(new
        {
            message = "floor-cleared",
            sessionId = session.SessionId,
            floor = session.FloorNumber,
            roomNumber = session.CurrentRoomIndex + 1,
            totalRooms = session.TotalRooms,
            roomsCleared = session.RoomsCleared,
            status = session.Status,
            isCompleted = session.IsCompleted
        });
    }

    var nextRoom = session.Rooms[nextRoomIndex];

    session.CurrentRoomIndex = nextRoomIndex;
    session.EnemyName = nextRoom.EnemyName;
    session.EnemyTypeTag = nextRoom.EnemyTypeTag;
    session.EnemyMaxHp = nextRoom.EnemyMaxHp;
    session.EnemyHp = nextRoom.EnemyMaxHp;
    session.Status = "in-progress";
    session.TurnNumber += 1;

    sessionStore.CreateOrReplace(session);
    await NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken);

    return Results.Ok(new
    {
        message = "advanced-to-next-room",
        sessionId = session.SessionId,
        floor = session.FloorNumber,
        roomNumber = session.CurrentRoomIndex + 1,
        totalRooms = session.TotalRooms,
        roomsCleared = session.RoomsCleared,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("AdvanceDungeonRoom");

app.MapPost("/combat/attack", async (
    CombatActionRequest request,
    IDungeonSessionStore sessionStore,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (session.IsCompleted)
    {
        return Results.BadRequest(new { error = "Combat is already completed for this session." });
    }

    if (string.Equals(session.Status, "room-cleared", StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Current room already cleared. Advance to the next room." });
    }

    // Deterministic values keep the MVP predictable while learning endpoint flow.
    const int playerDamage = 12;
    const int enemyDamage = 6;

    session.EnemyHp = Math.Max(0, session.EnemyHp - playerDamage);
    var outcome = "player-attacked";

    // Populated only on a killing blow so the response can surface quest progress changes.
    List<object> questProgressUpdates = [];

    if (session.EnemyHp == 0)
    {
        outcome = "enemy-defeated";

        // Advance kill count on every accepted quest whose enemy type tag matches the defeated enemy.
        var acceptedQuests = await dbContext.PlayerQuests
            .Where(pq => pq.AccountId == session.AccountId && pq.Status == "accepted")
            .Include(pq => pq.QuestDefinition)
            .ToListAsync(cancellationToken);

        foreach (var pq in acceptedQuests)
        {
            var tag = pq.QuestDefinition?.EnemyTypeTag;
            // Null tag means any enemy satisfies the quest; otherwise require a name match.
            var matches = tag is null
                || session.EnemyTypeTag.Contains(tag, StringComparison.OrdinalIgnoreCase)
                || session.EnemyName.Contains(tag, StringComparison.OrdinalIgnoreCase);

            if (!matches) continue;

            pq.ProgressCount += 1;

            // Transition to "ready" once the required kill count is reached.
            if (pq.ProgressCount >= (pq.QuestDefinition?.RequiredEnemyDefeats ?? 1))
            {
                pq.Status = "ready";
            }

            questProgressUpdates.Add(new
            {
                questId = pq.QuestId,
                progressCount = pq.ProgressCount,
                status = pq.Status
            });
        }

        if (acceptedQuests.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        session.RoomsCleared += 1;
        if (session.RoomsCleared >= session.TotalRooms)
        {
            session.IsCompleted = true;
            session.Status = "victory";
        }
        else
        {
            session.Status = "room-cleared";
        }
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
    var notifications = new List<Task>
    {
        NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken)
    };

    if (questProgressUpdates.Count > 0)
    {
        notifications.Add(NotifyQuestsChangedAsync(hubContext, session.AccountId, cancellationToken));
    }

    await Task.WhenAll(notifications);

    return Results.Ok(new
    {
        outcome,
        sessionId = session.SessionId,
        player = new { hp = session.PlayerHp, maxHp = session.PlayerMaxHp },
        enemy = new { name = session.EnemyName, hp = session.EnemyHp, maxHp = session.EnemyMaxHp },
        turn = session.TurnNumber,
        status = session.Status,
        isCompleted = session.IsCompleted,
        questProgress = questProgressUpdates
    });
})
.WithName("CombatAttack");

app.MapPost("/combat/retreat", async (
    CombatActionRequest request,
    IDungeonSessionStore sessionStore,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (!sessionStore.TryGet(request.SessionId, out var session) || session is null)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    session.IsCompleted = true;
    session.Status = "retreated";
    sessionStore.CreateOrReplace(session);
    await NotifyDungeonSessionChangedAsync(hubContext, session, cancellationToken);

    return Results.Ok(new
    {
        message = "retreated-from-combat",
        sessionId = session.SessionId,
        status = session.Status,
        isCompleted = session.IsCompleted
    });
})
.WithName("CombatRetreat");

app.MapGet("/player/progress/{accountId:int}", async (
    int accountId,
    DungeonCrawlerDbContext dbContext,
    ILevelUpService levelUpService,
    CancellationToken cancellationToken) =>
{
    var progress = await dbContext.PlayerProgresses
        .AsNoTracking()
        .FirstOrDefaultAsync(pp => pp.AccountId == accountId, cancellationToken);

    if (progress is null)
    {
        return Results.NotFound(new { error = "Player progress not found." });
    }

    var isMaxLevel = progress.Level >= 10;
    var xpForCurrentLevel = levelUpService.XpRequiredForLevel(progress.Level);

    return Results.Ok(new
    {
        accountId,
        level = progress.Level,
        xp = progress.Xp,
        gold = progress.Gold,
        maxHp = progress.MaxHp,
        levelProgress = new
        {
            xpIntoCurrentLevel = progress.Xp - xpForCurrentLevel,
            xpToNextLevel = isMaxLevel
                ? 0
                : levelUpService.XpRequiredForLevel(progress.Level + 1) - progress.Xp,
            atMaxLevel = isMaxLevel
        }
    });
})
.WithName("GetPlayerProgress");

app.MapGet("/hub/overview/{accountId:int}", async (
    int accountId,
    DungeonCrawlerDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var account = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

    if (account is null)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    var cutoffUtc = DateTime.UtcNow.AddMinutes(-10);
    var activeAdventurerCount = await dbContext.HubPresences
        .AsNoTracking()
        .CountAsync(hp => hp.LastSeenAt >= cutoffUtc, cancellationToken);

    var latestMessages = await dbContext.HubChatMessages
        .AsNoTracking()
        .Include(cm => cm.Account)
        .OrderByDescending(cm => cm.SentAt)
        .Take(30)
        .Select(cm => new
        {
            messageId = cm.HubChatMessageId,
            accountId = cm.AccountId,
            username = cm.Account != null ? cm.Account.Username : "unknown",
            message = cm.Message,
            sentAt = cm.SentAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        username = account.Username,
        activeAdventurerCount,
        messages = latestMessages
            .OrderBy(cm => cm.sentAt)
            .ToList()
    });
})
.WithName("GetHubOverview");

app.MapGet("/hub/chat/{accountId:int}", async (
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

    var latestMessages = await dbContext.HubChatMessages
        .AsNoTracking()
        .Include(cm => cm.Account)
        .OrderByDescending(cm => cm.SentAt)
        .Take(30)
        .Select(cm => new
        {
            messageId = cm.HubChatMessageId,
            accountId = cm.AccountId,
            username = cm.Account != null ? cm.Account.Username : "unknown",
            message = cm.Message,
            sentAt = cm.SentAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        messageCount = latestMessages.Count,
        messages = latestMessages
            .OrderBy(cm => cm.sentAt)
            .ToList()
    });
})
.WithName("GetHubChat");

app.MapPost("/hub/chat/send", async (
    SendHubChatMessageRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var account = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.AccountId == request.AccountId, cancellationToken);

    if (account is null)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    var message = request.Message.Trim();
    if (message.Length > 280)
    {
        return Results.BadRequest(new { error = "Message cannot exceed 280 characters." });
    }

    var chatMessage = new HubChatMessage
    {
        AccountId = request.AccountId,
        Message = message,
        SentAt = DateTime.UtcNow
    };

    dbContext.HubChatMessages.Add(chatMessage);
    await MarkHubPresenceAsync(dbContext, request.AccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    var chatPayload = new
    {
        messageId = chatMessage.HubChatMessageId,
        accountId = chatMessage.AccountId,
        username = account.Username,
        message = chatMessage.Message,
        sentAt = chatMessage.SentAt
    };

    await Task.WhenAll(
        NotifyHubChatMessageReceivedAsync(hubContext, chatPayload, cancellationToken),
        NotifyHubPresenceChangedAsync(hubContext, cancellationToken));

    return Results.Ok(new
    {
        message = "hub-chat-sent",
        chat = chatPayload
    });
})
.WithName("SendHubChatMessage");

app.MapPost("/hub/presence/ping", async (
    PingHubPresenceRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var accountExists = await dbContext.Accounts
        .AsNoTracking()
        .AnyAsync(a => a.AccountId == request.AccountId, cancellationToken);

    if (!accountExists)
    {
        return Results.NotFound(new { error = "Account not found." });
    }

    await MarkHubPresenceAsync(dbContext, request.AccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await NotifyHubPresenceChangedAsync(hubContext, cancellationToken);

    return Results.Ok(new
    {
        message = "hub-presence-updated",
        accountId = request.AccountId,
        seenAt = DateTime.UtcNow
    });
})
.WithName("PingHubPresence");

app.MapGet("/hub/roster/{accountId:int}", async (
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

    var cutoffUtc = DateTime.UtcNow.AddMinutes(-10);

    var roster = await dbContext.HubPresences
        .AsNoTracking()
        .Where(hp => hp.AccountId != accountId && hp.LastSeenAt >= cutoffUtc)
        .Join(
            dbContext.Accounts.AsNoTracking(),
            hp => hp.AccountId,
            a => a.AccountId,
            (hp, a) => new { Presence = hp, Account = a })
        .GroupJoin(
            dbContext.PlayerProgresses.AsNoTracking(),
            row => row.Account.AccountId,
            pp => pp.AccountId,
            (row, pps) => new { row.Presence, row.Account, Progress = pps.FirstOrDefault() })
        .OrderBy(row => row.Account.Username)
        .Take(40)
        .Select(row => new
        {
            accountId = row.Account.AccountId,
            username = row.Account.Username,
            level = row.Progress != null ? row.Progress.Level : 1,
            gold = row.Progress != null ? row.Progress.Gold : 0,
            lastSeenAt = row.Presence.LastSeenAt,
            lastSavedAt = row.Progress != null ? row.Progress.LastSavedAt : (DateTime?)null
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        rosterCount = roster.Count,
        roster
    });
})
.WithName("GetHubRoster");

app.MapGet("/trade/offers/{accountId:int}", async (
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

    var offers = await dbContext.TradeOffers
        .AsNoTracking()
        .Include(to => to.FromAccount)
        .Include(to => to.ToAccount)
        .Where(to => to.FromAccountId == accountId || to.ToAccountId == accountId)
        .OrderByDescending(to => to.CreatedAt)
        .Take(60)
        .Select(to => new
        {
            tradeOfferId = to.TradeOfferId,
            fromAccountId = to.FromAccountId,
            fromUsername = to.FromAccount != null ? to.FromAccount.Username : "unknown",
            toAccountId = to.ToAccountId,
            toUsername = to.ToAccount != null ? to.ToAccount.Username : "unknown",
            itemCode = to.ItemCode,
            itemName = to.ItemName,
            rarity = to.Rarity,
            quantity = to.Quantity,
            note = to.Note,
            status = to.Status,
            createdAt = to.CreatedAt,
            respondedAt = to.RespondedAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        accountId,
        offerCount = offers.Count,
        offers
    });
})
.WithName("GetTradeOffers");

app.MapPost("/trade/offers/send", async (
    SendTradeOfferRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new { error = "Quantity must be at least 1." });
    }

    if (string.IsNullOrWhiteSpace(request.ToUsername) || string.IsNullOrWhiteSpace(request.ItemCode))
    {
        return Results.BadRequest(new { error = "Recipient username and item code are required." });
    }

    var toUsername = request.ToUsername.Trim();
    var itemCode = request.ItemCode.Trim();
    var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

    if (note is not null && note.Length > 180)
    {
        return Results.BadRequest(new { error = "Trade note cannot exceed 180 characters." });
    }

    var fromAccount = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.AccountId == request.FromAccountId, cancellationToken);

    if (fromAccount is null)
    {
        return Results.NotFound(new { error = "Sender account not found." });
    }

    var toAccount = await dbContext.Accounts
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.Username == toUsername, cancellationToken);

    if (toAccount is null)
    {
        return Results.NotFound(new { error = "Recipient account not found." });
    }

    if (toAccount.AccountId == request.FromAccountId)
    {
        return Results.BadRequest(new { error = "You cannot trade with yourself." });
    }

    var senderStack = await dbContext.InventoryItems
        .FirstOrDefaultAsync(
            ii => ii.AccountId == request.FromAccountId && ii.ItemCode == itemCode,
            cancellationToken);

    if (senderStack is null || senderStack.Quantity < request.Quantity)
    {
        return Results.BadRequest(new { error = "Not enough item quantity available for this trade." });
    }

    var offer = new TradeOffer
    {
        FromAccountId = request.FromAccountId,
        ToAccountId = toAccount.AccountId,
        ItemCode = senderStack.ItemCode,
        ItemName = senderStack.ItemName,
        Rarity = senderStack.Rarity,
        Quantity = request.Quantity,
        Note = note,
        Status = "pending",
        CreatedAt = DateTime.UtcNow,
        RespondedAt = null
    };

    // Move items into escrow by removing them from the sender stack immediately.
    senderStack.Quantity -= request.Quantity;
    if (senderStack.Quantity <= 0)
    {
        dbContext.InventoryItems.Remove(senderStack);
    }

    dbContext.TradeOffers.Add(offer);
    await MarkHubPresenceAsync(dbContext, request.FromAccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await Task.WhenAll(
        NotifyTradeOffersChangedAsync(hubContext, request.FromAccountId, cancellationToken),
        NotifyTradeOffersChangedAsync(hubContext, toAccount.AccountId, cancellationToken),
        NotifyInventoryChangedAsync(hubContext, request.FromAccountId, cancellationToken),
        NotifyHubPresenceChangedAsync(hubContext, cancellationToken));

    return Results.Ok(new
    {
        message = "trade-offer-sent",
        tradeOfferId = offer.TradeOfferId,
        fromAccountId = offer.FromAccountId,
        fromUsername = fromAccount.Username,
        toAccountId = offer.ToAccountId,
        toUsername = toAccount.Username,
        itemCode = offer.ItemCode,
        itemName = offer.ItemName,
        rarity = offer.Rarity,
        quantity = offer.Quantity,
        status = offer.Status,
        createdAt = offer.CreatedAt
    });
})
.WithName("SendTradeOffer");

app.MapPost("/trade/offers/respond", async (
    RespondTradeOfferRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Action))
    {
        return Results.BadRequest(new { error = "Action is required." });
    }

    var action = request.Action.Trim().ToLowerInvariant();
    if (action is not ("accept" or "reject"))
    {
        return Results.BadRequest(new { error = "Action must be 'accept' or 'reject'." });
    }

    var offer = await dbContext.TradeOffers
        .FirstOrDefaultAsync(to => to.TradeOfferId == request.TradeOfferId, cancellationToken);

    if (offer is null)
    {
        return Results.NotFound(new { error = "Trade offer not found." });
    }

    if (offer.ToAccountId != request.AccountId)
    {
        return Results.BadRequest(new { error = "This account is not the recipient for the trade offer." });
    }

    if (offer.Status != "pending")
    {
        return Results.Conflict(new { error = "Only pending trade offers can be resolved." });
    }

    if (action == "accept")
    {
        var recipientStack = await dbContext.InventoryItems
            .FirstOrDefaultAsync(
                ii => ii.AccountId == offer.ToAccountId && ii.ItemCode == offer.ItemCode,
                cancellationToken);

        if (recipientStack is null)
        {
            dbContext.InventoryItems.Add(new InventoryItem
            {
                AccountId = offer.ToAccountId,
                ItemCode = offer.ItemCode,
                ItemName = offer.ItemName,
                Rarity = offer.Rarity,
                Quantity = offer.Quantity,
                AcquiredAt = DateTime.UtcNow
            });
        }
        else
        {
            recipientStack.Quantity += offer.Quantity;
            recipientStack.AcquiredAt = DateTime.UtcNow;
        }

        offer.Status = "accepted";
        offer.RespondedAt = DateTime.UtcNow;
    }
    else
    {
        var senderStack = await dbContext.InventoryItems
            .FirstOrDefaultAsync(
                ii => ii.AccountId == offer.FromAccountId && ii.ItemCode == offer.ItemCode,
                cancellationToken);

        if (senderStack is null)
        {
            dbContext.InventoryItems.Add(new InventoryItem
            {
                AccountId = offer.FromAccountId,
                ItemCode = offer.ItemCode,
                ItemName = offer.ItemName,
                Rarity = offer.Rarity,
                Quantity = offer.Quantity,
                AcquiredAt = DateTime.UtcNow
            });
        }
        else
        {
            senderStack.Quantity += offer.Quantity;
            senderStack.AcquiredAt = DateTime.UtcNow;
        }

        offer.Status = "rejected";
        offer.RespondedAt = DateTime.UtcNow;
    }

    await MarkHubPresenceAsync(dbContext, request.AccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await Task.WhenAll(
        NotifyTradeOffersChangedAsync(hubContext, offer.FromAccountId, cancellationToken),
        NotifyTradeOffersChangedAsync(hubContext, offer.ToAccountId, cancellationToken),
        NotifyInventoryChangedAsync(hubContext, offer.FromAccountId, cancellationToken),
        NotifyInventoryChangedAsync(hubContext, offer.ToAccountId, cancellationToken),
        NotifyHubPresenceChangedAsync(hubContext, cancellationToken));

    return Results.Ok(new
    {
        message = "trade-offer-resolved",
        tradeOfferId = offer.TradeOfferId,
        status = offer.Status,
        respondedAt = offer.RespondedAt
    });
})
.WithName("RespondTradeOffer");

app.MapPost("/trade/offers/cancel", async (
    CancelTradeOfferRequest request,
    DungeonCrawlerDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var offer = await dbContext.TradeOffers
        .FirstOrDefaultAsync(to => to.TradeOfferId == request.TradeOfferId, cancellationToken);

    if (offer is null)
    {
        return Results.NotFound(new { error = "Trade offer not found." });
    }

    if (offer.FromAccountId != request.AccountId)
    {
        return Results.BadRequest(new { error = "This account did not create the trade offer." });
    }

    if (offer.Status != "pending")
    {
        return Results.Conflict(new { error = "Only pending trade offers can be cancelled." });
    }

    var senderStack = await dbContext.InventoryItems
        .FirstOrDefaultAsync(
            ii => ii.AccountId == offer.FromAccountId && ii.ItemCode == offer.ItemCode,
            cancellationToken);

    if (senderStack is null)
    {
        dbContext.InventoryItems.Add(new InventoryItem
        {
            AccountId = offer.FromAccountId,
            ItemCode = offer.ItemCode,
            ItemName = offer.ItemName,
            Rarity = offer.Rarity,
            Quantity = offer.Quantity,
            AcquiredAt = DateTime.UtcNow
        });
    }
    else
    {
        senderStack.Quantity += offer.Quantity;
        senderStack.AcquiredAt = DateTime.UtcNow;
    }

    offer.Status = "cancelled";
    offer.RespondedAt = DateTime.UtcNow;

    await MarkHubPresenceAsync(dbContext, request.AccountId, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await Task.WhenAll(
        NotifyTradeOffersChangedAsync(hubContext, offer.FromAccountId, cancellationToken),
        NotifyTradeOffersChangedAsync(hubContext, offer.ToAccountId, cancellationToken),
        NotifyInventoryChangedAsync(hubContext, offer.FromAccountId, cancellationToken),
        NotifyHubPresenceChangedAsync(hubContext, cancellationToken));

    return Results.Ok(new
    {
        message = "trade-offer-cancelled",
        tradeOfferId = offer.TradeOfferId,
        status = offer.Status,
        respondedAt = offer.RespondedAt
    });
})
.WithName("CancelTradeOffer");

app.Run();

internal sealed record RegisterRequest(string Username, string Password);
internal sealed record LoginRequest(string Username, string Password);
internal sealed record EnterDungeonRequest(int AccountId, int? Seed = null);
internal sealed record CombatActionRequest(string SessionId);
internal sealed record AdvanceDungeonRoomRequest(string SessionId);
internal sealed record AcceptQuestRequest(int AccountId, string QuestId);
internal sealed record CompleteQuestRequest(int AccountId, string QuestId);
internal sealed record AbandonQuestRequest(int AccountId, string QuestId);
internal sealed record UseInventoryItemRequest(int AccountId, string SessionId, string ItemCode);
internal sealed record SendHubChatMessageRequest(int AccountId, string Message);
internal sealed record PingHubPresenceRequest(int AccountId);
internal sealed record SendTradeOfferRequest(int FromAccountId, string ToUsername, string ItemCode, int Quantity, string? Note);
internal sealed record RespondTradeOfferRequest(int AccountId, long TradeOfferId, string Action);
internal sealed record CancelTradeOfferRequest(int AccountId, long TradeOfferId);
