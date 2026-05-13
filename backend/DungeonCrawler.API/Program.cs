using DungeonCrawler.API.Data;
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

app.Run();
