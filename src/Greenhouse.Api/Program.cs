using System.Text.Json;
using System.Threading.RateLimiting;
using Greenhouse.Api.Auth;
using Greenhouse.Api.Endpoints;
using Greenhouse.Api.Infrastructure;
using Greenhouse.Core;
using Greenhouse.Core.Data;
using Greenhouse.Core.Domain;
using Greenhouse.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Дані: SQLite або PostgreSQL залежно від Database:Provider.
builder.Services.AddGreenhouseDatabase(
    builder.Configuration["Database:Provider"],
    builder.Configuration.GetConnectionString("Greenhouse") ?? "Data Source=greenhouse.db");
builder.Services.AddGreenhouseCore();
builder.Services.AddSingleton<TokenService>();

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Безпека: датчики пишуть з API-ключем, люди входять з JWT; права визначає роль.
var authSection = builder.Configuration.GetSection(AuthOptions.Section);
builder.Services.Configure<AuthOptions>(authSection);
var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();
if (authOptions.JwtSigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Auth:JwtSigningKey має містити щонайменше 32 символи. Задайте його змінною середовища Auth__JwtSigningKey.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthNames.ApiKeyScheme, null)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authOptions.JwtIssuer,
            ValidAudience = authOptions.JwtIssuer,
            IssuerSigningKey = TokenService.SigningKey(authOptions),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthNames.SensorPolicy, p => p
        .AddAuthenticationSchemes(AuthNames.ApiKeyScheme)
        .RequireRole(AuthNames.SensorRole))
    .AddPolicy(AuthNames.OperatorPolicy, p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(Roles.Operator, Roles.Admin))
    .AddPolicy(AuthNames.AdminPolicy, p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(Roles.Admin));

// Захист від перебору паролів: обмеження спроб входу з однієї IP-адреси.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthNames.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, authOptions.LoginAttemptsPerMinute),
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

// Міграції і стартовий адміністратор: БД створюється з нуля без ручних кроків.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>().Database.MigrateAsync();
    var auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
    await scope.ServiceProvider.GetRequiredService<UserService>()
        .EnsureAdminAsync(auth.AdminUsername, auth.AdminPassword, CancellationToken.None);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).WithTags("Health");
api.MapAuthEndpoints()
    .MapUserEndpoints()
    .MapGreenhouseEndpoints()
    .MapTelemetryEndpoints();

await app.RunAsync();

/// <summary>Точка входу; partial, щоб інтеграційні тести могли використати WebApplicationFactory.</summary>
public partial class Program;
