var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).WithTags("Health");

await app.RunAsync();

/// <summary>Точка входу; partial, щоб інтеграційні тести могли використати WebApplicationFactory.</summary>
public partial class Program;
