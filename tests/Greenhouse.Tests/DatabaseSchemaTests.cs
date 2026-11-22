using Greenhouse.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenhouse.Tests;

public class DatabaseSchemaTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public DatabaseSchemaTests() => _factory.CreateClient().Dispose(); // запуск застосунку застосовує міграції

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_factory.DbPath};Pooling=False");
        conn.Open();
        using var fk = conn.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys = ON";
        fk.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public void Indexes_exist_on_search_and_join_columns()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%'";
        var names = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }
        }

        Assert.Contains("IX_readings_sensor_id_recorded_at", names);
        Assert.Contains("IX_sensors_name", names);
        Assert.Contains("IX_alerts_sensor_id_closed_at", names);
        Assert.Contains("IX_sensors_greenhouse_id", names);
        Assert.Contains("IX_users_username", names);
        Assert.Contains("IX_refresh_tokens_token_hash", names);
    }

    [Theory]
    [InlineData("INSERT INTO readings (sensor_id, value, recorded_at) VALUES (999, 1, '2026-01-01')")] // FK
    [InlineData("INSERT INTO sensors (name, kind, greenhouse_id) VALUES ('temp-1', 'temperature', 1)")] // UNIQUE
    [InlineData("INSERT INTO sensors (name, kind, greenhouse_id) VALUES ('co2-1', 'co2', 1)")] // FK на тип
    [InlineData("INSERT INTO sensors (name, kind, greenhouse_id) VALUES ('t-9', 'temperature', 99)")] // FK на теплицю
    [InlineData("INSERT INTO thresholds (greenhouse_id, kind, comfort_min, comfort_max) VALUES (1, 'light', 5, 1)")] // PK + CHECK
    [InlineData("INSERT INTO users (username, password_hash, role, created_at) VALUES ('x', 'h', 'root', '2026-01-01')")] // CHECK ролі
    [InlineData("INSERT INTO alerts (sensor_id, kind, trigger_value, opened_at) VALUES (1, 'medium', 1, '2026-01-01')")] // CHECK
    [InlineData("INSERT INTO sensor_types (kind, unit, valid_min, valid_max) VALUES ('x', 'u', 5, 1)")] // CHECK
    public void Integrity_constraints_reject_bad_rows(string sql)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public void No_pending_migrations()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenhouseDbContext>();
        Assert.Empty(db.Database.GetPendingMigrations());
    }
}
