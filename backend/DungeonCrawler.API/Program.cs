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

var app = builder.Build();

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
