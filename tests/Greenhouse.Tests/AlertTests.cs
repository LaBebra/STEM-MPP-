using System.Net;
using System.Net.Http.Json;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Domain;

namespace Greenhouse.Tests;

public class AlertTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();
    private readonly HttpClient _sensor;

    public AlertTests() => _sensor = _factory.CreateSensorClient();

    public void Dispose()
    {
        _sensor.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<HttpResponseMessage> Send(double value, string at, string sensor = "temp-1") =>
        _sensor.PostReadingAsync(new { sensor, value, recorded_at = at });

    [Fact]
    public async Task Alert_opens_once_and_closes_when_back_to_normal()
    {
        await Send(22, "2026-01-01T10:00:00Z");
        Assert.Empty(await _sensor.AlertsAsync());

        await Send(30, "2026-01-01T10:01:00Z");
        await Send(31, "2026-01-01T10:02:00Z"); // та сама тривога, без дубліката
        var open = await _sensor.AlertsAsync("?active=true");
        var alert = Assert.Single(open);
        Assert.Equal("high", alert.Kind);
        Assert.Equal(30, alert.TriggerValue);

        await Send(25, "2026-01-01T10:03:00Z");
        Assert.Empty(await _sensor.AlertsAsync("?active=true"));
        var closed = Assert.Single(await _sensor.AlertsAsync());
        Assert.Equal(new DateTime(2026, 1, 1, 10, 3, 0, DateTimeKind.Utc), closed.ClosedAt);
    }

    [Fact]
    public async Task Jump_from_high_to_low_replaces_alert()
    {
        await Send(30, "2026-01-01T10:00:00Z");
        await Send(10, "2026-01-01T10:01:00Z");

        var alerts = await _sensor.AlertsAsync();
        Assert.Equal(["low", "high"], alerts.Select(a => a.Kind));
        Assert.Null(alerts[0].ClosedAt);
        Assert.NotNull(alerts[1].ClosedAt);
    }

    [Fact]
    public async Task Batch_is_evaluated_in_time_order()
    {
        await _sensor.PostBatchAsync(new[]
        {
            new { sensor = "hum-1", value = 60.0, recorded_at = "2026-01-01T10:02:00Z" },
            new { sensor = "hum-1", value = 40.0, recorded_at = "2026-01-01T10:01:00Z" },
        });

        var alert = Assert.Single(await _sensor.AlertsAsync());
        Assert.Equal("low", alert.Kind);
        Assert.NotNull(alert.ClosedAt);
    }

    [Fact]
    public async Task Rejected_batch_does_not_create_alerts()
    {
        await _sensor.PostBatchAsync(new object[]
        {
            new { sensor = "temp-1", value = 35 },
            new { sensor = "temp-1", value = 500 },
        });

        Assert.Empty(await _sensor.AlertsAsync());
    }

    [Fact]
    public async Task Operator_acknowledges_alert_once()
    {
        await Send(35, "2026-01-01T10:00:00Z");
        var id = (await _sensor.AlertsAsync()).Single().Id;
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);

        var first = await op.PostAsync(new Uri($"/api/alerts/{id}/ack", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var acked = await first.Content.ReadFromJsonAsync<AlertOut>(GreenhouseFactory.Json);
        Assert.StartsWith("operator-", acked!.AcknowledgedBy, StringComparison.Ordinal);

        var second = await (await op.PostAsync(new Uri($"/api/alerts/{id}/ack", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<AlertOut>(GreenhouseFactory.Json);
        Assert.Equal(acked.AcknowledgedAt, second!.AcknowledgedAt);
    }

    [Fact]
    public async Task Acknowledge_unknown_alert_is_404()
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        var response = await op.PostAsync(new Uri("/api/alerts/999/ack", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=501")]
    public async Task Invalid_alert_limit_is_400(string query)
    {
        var response = await _sensor.GetAsync(new Uri("/api/alerts" + query, UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
