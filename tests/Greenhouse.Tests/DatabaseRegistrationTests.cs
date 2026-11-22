using Greenhouse.Core.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Greenhouse.Tests;

public class DatabaseRegistrationTests
{
    [Theory]
    [InlineData("Sqlite", typeof(SqliteGreenhouseDbContext))]
    [InlineData("postgres", typeof(PostgresGreenhouseDbContext))]
    [InlineData(null, typeof(SqliteGreenhouseDbContext))]
    public void Provider_selects_context(string? provider, Type expected)
    {
        using var sp = new ServiceCollection()
            .AddGreenhouseDatabase(provider, provider?.StartsWith('p') == true ? "Host=localhost" : "Data Source=:memory:")
            .BuildServiceProvider();
        using var scope = sp.CreateScope();
        Assert.IsType(expected, scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>());
    }

    [Fact]
    public void Unknown_provider_throws() =>
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddGreenhouseDatabase("Oracle", "x"));

    [Fact]
    public void Design_time_factories_create_contexts()
    {
        using var sqlite = new SqliteDesignTimeFactory().CreateDbContext([]);
        using var pg = new PostgresDesignTimeFactory().CreateDbContext([]);
        Assert.NotNull(sqlite.Model.FindEntityType(typeof(Reading)));
        Assert.NotNull(pg.Model.FindEntityType(typeof(Reading)));
    }
}
