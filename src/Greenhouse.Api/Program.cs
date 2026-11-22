using Greenhouse.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Дані: SQLite або PostgreSQL залежно від Database:Provider.
builder.Services.AddGreenhouseDatabase(
    builder.Configuration["Database:Provider"],
    builder.Configuration.GetConnectionString("Greenhouse") ?? "Data Source=greenhouse.db");

var app = builder.Build();

// Міграції: БД створюється з нуля без ручних кроків.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>().Database.MigrateAsync();
}

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).WithTags("Health");

await app.RunAsync();

/// <summary>Точка входу; partial, щоб інтеграційні тести могли використати WebApplicationFactory.</summary>
public partial class Program;
